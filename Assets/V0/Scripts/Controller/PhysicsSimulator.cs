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

            foreach (var col in allColliders)
            {
                // CRITICAL FIX: Only copy Terrain colliders so the ghost ball doesn't hit rocks!
                // This allows the primary trajectory line to show the full arc to the hole, while PASS 2 handles rock bounces.
                bool isTerrain = col is TerrainCollider || col.gameObject.CompareTag("Terrain") || col.gameObject.CompareTag("NiceOn");
                if (!isTerrain) continue;
                
                // Do not copy triggers
                if (col.isTrigger) continue;

                // Create an empty GameObject for the physics representation
                GameObject ghostObj = new GameObject(col.name + "_Ghost");
                ghostObj.layer = col.gameObject.layer;
                try {
                    ghostObj.tag = col.gameObject.tag;
                } catch {
                    // Ignore if tag doesn't exist
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
            out Vector3[] velocitiesArray)
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

            // ── Cache all NiceOn objects from the REAL scene for bounds checking ──
            // This bypasses ALL physics-based detection (raycasts, collisions) which
            // have proven unreliable in the ghost physics scene.
            GameObject[] niceOnObjects = GameObject.FindGameObjectsWithTag("NiceOn");
            Debug.Log($"[PhysicsSimulator] Found {niceOnObjects.Length} NiceOn-tagged objects for green detection.");
            
            Bounds[] niceOnBounds = new Bounds[niceOnObjects.Length];
            for (int n = 0; n < niceOnObjects.Length; n++)
            {
                Collider col = niceOnObjects[n].GetComponent<Collider>();
                if (col != null)
                {
                    niceOnBounds[n] = col.bounds;
                    Debug.Log($"[PhysicsSimulator] NiceOn[{n}] '{niceOnObjects[n].name}' bounds: center={col.bounds.center}, size={col.bounds.size}");
                }
                else
                {
                    Renderer rend = niceOnObjects[n].GetComponent<Renderer>();
                    if (rend != null)
                    {
                        niceOnBounds[n] = rend.bounds;
                        Debug.Log($"[PhysicsSimulator] NiceOn[{n}] '{niceOnObjects[n].name}' renderer bounds: center={rend.bounds.center}, size={rend.bounds.size}");
                    }
                    else
                    {
                        niceOnBounds[n] = new Bounds(niceOnObjects[n].transform.position, Vector3.zero);
                        Debug.Log($"[PhysicsSimulator] NiceOn[{n}] '{niceOnObjects[n].name}' has NO collider or renderer!");
                    }
                }
            }

            // 3. Step the simulation
            bool hasLandedOnGreen = false;
            for (int i = 0; i < maxSteps; i++)
            {
                // Ball stopped
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

                // ── NiceOn detection via BOUNDS CHECK (no physics!) ─────────────
                // Check if the ball's XZ position is within the bounding box of any
                // NiceOn-tagged object AND the ball is near the surface height.
                if (i > 5 && rb.linearVelocity.y <= 0f)
                {
                    for (int n = 0; n < niceOnBounds.Length; n++)
                    {
                        Bounds b = niceOnBounds[n];
                        if (b.size == Vector3.zero) continue;
                        
                        // Check XZ containment and Y proximity (ball should be near the top of the green)
                        bool inXZ = ballPos.x >= b.min.x && ballPos.x <= b.max.x &&
                                    ballPos.z >= b.min.z && ballPos.z <= b.max.z;
                        bool nearY = ballPos.y <= b.max.y + 3f; // Ball is within 3m above the green surface
                        
                        if (inXZ && nearY)
                        {
                            hasLandedOnGreen = true;
                            Debug.Log($"[PhysicsSimulator] Ball at {ballPos} is over NiceOn '{niceOnObjects[n].name}' at step {i}. STOPPING trajectory.");
                            break;
                        }
                    }
                    if (hasLandedOnGreen) break;
                }
            }

            if (!hasLandedOnGreen && niceOnObjects.Length > 0)
            {
                Debug.Log($"[PhysicsSimulator] WARNING: Ball never detected over any NiceOn object! Final ball pos: {rb.position}, total points: {points.Count}");
            }

            pointsArray = points.ToArray();
            velocitiesArray = velocities.ToArray();
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
