using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfGame.Controllers
{
    public class PhysicsSimulator : MonoBehaviour
    {
        public static PhysicsSimulator Instance { get; private set; }

        private Scene ghostScene;
        private PhysicsScene ghostPhysicsScene;
        private List<GameObject> ghostEnvironment = new List<GameObject>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                CreateGhostScene();
                DuplicateEnvironment();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void CreateGhostScene()
        {
            CreateSceneParameters parameters = new CreateSceneParameters(LocalPhysicsMode.Physics3D);
            ghostScene = SceneManager.CreateScene("GhostPhysicsScene", parameters);
            ghostPhysicsScene = ghostScene.GetPhysicsScene();
        }

        private void DuplicateEnvironment()
        {
            Collider[] allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
            foreach (var col in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                // CRITICAL FIX: Only copy Terrain colliders so the ghost ball doesn't hit rocks!
                // Walk the parent tree so if a custom green has NiceOn on its parent, we still copy it.
                bool isTerrain = col is TerrainCollider || col.gameObject.CompareTag("Terrain");
                Transform checkTerrainParent = col.transform;
                while (checkTerrainParent != null && !isTerrain)
                {
                    if (checkTerrainParent.CompareTag("NiceOn") || LayerMask.LayerToName(checkTerrainParent.gameObject.layer).Equals("NiceOn", System.StringComparison.OrdinalIgnoreCase))
                    {
                        isTerrain = true;
                        break;
                    }
                    checkTerrainParent = checkTerrainParent.parent;
                }
                if (!isTerrain) continue;
                
                // Do not copy triggers
                if (col.isTrigger) continue;

                // Do not copy aiming helpers like the Target Marker or the Ball Pivot, 
                // otherwise the ghost ball will hit them and bounce backwards!
                if (col.gameObject.name.Contains("Marker") || col.gameObject.name.Contains("Pivot") || col.gameObject.name.Contains("GolfBall"))
                    continue;

                // Create an empty GameObject for the physics representation
                GameObject ghostObj = new GameObject(col.name + "_Ghost");
                
                // Fix: Walk up the parent hierarchy to see if any parent has the NiceOn tag or layer, 
                // since the user's custom green might have the tag on the parent object.
                bool isNiceOn = false;
                Transform checkParent = col.transform;
                while (checkParent != null)
                {
                    if (checkParent.CompareTag("NiceOn") || LayerMask.LayerToName(checkParent.gameObject.layer).Equals("NiceOn", System.StringComparison.OrdinalIgnoreCase))
                    {
                        isNiceOn = true;
                        break;
                    }
                    checkParent = checkParent.parent;
                }

                if (isNiceOn)
                {
                    int niceOnLayer = LayerMask.NameToLayer("NiceOn");
                    ghostObj.layer = niceOnLayer != -1 ? niceOnLayer : col.gameObject.layer;
                    try { ghostObj.tag = "NiceOn"; } catch { }
                }
                else
                {
                    ghostObj.layer = col.gameObject.layer;
                    try { ghostObj.tag = col.gameObject.tag; } catch { }
                }
                // Match position and rotation exactly
                ghostObj.transform.position = col.transform.position;
                ghostObj.transform.rotation = col.transform.rotation;
                ghostObj.transform.localScale = col.transform.lossyScale;

                // Add the appropriate collider type
                if (col is BoxCollider bc)
                {
                    var gbc = ghostObj.AddComponent<BoxCollider>();
                    gbc.center = bc.center;
                    gbc.size = bc.size;
                    gbc.material = bc.material;
                }
                else if (col is SphereCollider sc)
                {
                    var gsc = ghostObj.AddComponent<SphereCollider>();
                    gsc.center = sc.center;
                    gsc.radius = sc.radius;
                    gsc.material = sc.material;
                }
                else if (col is CapsuleCollider cc)
                {
                    var gcc = ghostObj.AddComponent<CapsuleCollider>();
                    gcc.center = cc.center;
                    gcc.radius = cc.radius;
                    gcc.height = cc.height;
                    gcc.direction = cc.direction;
                    gcc.material = cc.material;
                }
                else if (col is MeshCollider mc)
                {
                    var gmc = ghostObj.AddComponent<MeshCollider>();
                    gmc.sharedMesh = mc.sharedMesh;
                    gmc.convex = mc.convex;
                    gmc.material = mc.material;
                }
                else if (col is TerrainCollider tc)
                {
                    var gtc = ghostObj.AddComponent<TerrainCollider>();
                    gtc.terrainData = tc.terrainData;
                    gtc.material = tc.material;
                }

                // Move the ghost object to the hidden physics scene
                SceneManager.MoveGameObjectToScene(ghostObj, ghostScene);
                ghostEnvironment.Add(ghostObj);
            }
        }

        private GameObject reusableGhostBall;

        public void SimulateTrajectory(
            GameObject ballPrefab, 
            Vector3 startPos, 
            Vector3 launchVelocity, 
            Vector2 appliedSpin,
            Vector3 flightRightDir,
            int maxSteps, 
            float timeStep,
            out Vector3[] pointsArray,
            out Vector3[] velocitiesArray,
            out bool landedOnGreen)
        {
            // 1. Manage reusable ghost ball
            if (reusableGhostBall == null)
            {
                reusableGhostBall = Instantiate(ballPrefab);
                reusableGhostBall.transform.SetParent(null);
                
                // CRITICAL FIX: The clone is an exact copy of the player ball! 
                // We must strip away all input/visual scripts so they don't run in the background (like spawning a second marker).
                MonoBehaviour[] allScripts = reusableGhostBall.GetComponentsInChildren<MonoBehaviour>();
                foreach (var script in allScripts)
                {
                    if (script is BallPhysicsController) continue;
                    script.enabled = false;
                    Destroy(script);
                }

                // Strip renderers and audio to save performance
                foreach (var renderer in reusableGhostBall.GetComponentsInChildren<Renderer>())
                    renderer.enabled = false;
                foreach (var audio in reusableGhostBall.GetComponentsInChildren<AudioSource>())
                    audio.enabled = false;
                
                SceneManager.MoveGameObjectToScene(reusableGhostBall, ghostScene);
            }
            
            // 2. Setup Physics State
            Rigidbody rb = reusableGhostBall.GetComponent<Rigidbody>();
            rb.position = startPos;
            rb.rotation = Quaternion.identity;
            rb.linearVelocity = launchVelocity;
            rb.angularVelocity = Vector3.zero;

            BallPhysicsController physicsController = reusableGhostBall.GetComponent<BallPhysicsController>();
            if (physicsController != null)
            {
                physicsController.isGhostBall = true;
                physicsController.SetAppliedSpin(appliedSpin);
                physicsController.SetFlightRightDir(flightRightDir);
                physicsController.NotifyFlightStarted(); // Calls rb.WakeUp() and resets bounces
            }

            List<Vector3> points = new List<Vector3>();
            List<Vector3> velocities = new List<Vector3>();
            points.Add(startPos);
            velocities.Add(launchVelocity);

            // 3. Step the simulation
            bool hasLandedOnGreen = false;
            for (int i = 0; i < maxSteps; i++)
            {
                // Ball stopped naturally
                if (rb.linearVelocity.sqrMagnitude <= 0.001f && i > 10)
                {
                    break;
                }
                
                // Explicitly call FixedUpdate equivalent
                if (physicsController != null)
                {
                    physicsController.ManualFixedUpdate();
                }

                ghostPhysicsScene.Simulate(timeStep);
                
                Vector3 ballPos = rb.position;
                points.Add(ballPos);
                velocities.Add(rb.linearVelocity);

                // Use the ghost ball's actual collision logic to detect if it landed on the green.
                // Since ghost scene duplication accurately mirrors tags/layers now, this is 100% reliable!
                if (physicsController != null && physicsController.HasHitGreen)
                {
                    hasLandedOnGreen = true;
                }
            }

            pointsArray = points.ToArray();
            velocitiesArray = velocities.ToArray();
            landedOnGreen = hasLandedOnGreen;
        }

        private void OnDestroy()
        {
            if (ghostScene.IsValid())
            {
                // Note: Scene cannot be destroyed directly in Unity runtime, 
                // but objects inside it will be cleaned up on app exit.
            }
        }
    }
}
