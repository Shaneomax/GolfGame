using UnityEngine;
using GolfGame.Data;
using GolfGame.Environment;
using System.Collections.Generic;

namespace GolfGame.Controllers
{
    [System.Serializable]
    public struct LayerGroundMapping
    {
        [Tooltip("Select the Unity Layer (e.g., Green, Bunker).")]
        public LayerMask TerrainLayer;
        
        [Tooltip("The GroundData to apply when the ball hits this layer.")]
        public GroundData SurfaceData;
    }
    [System.Serializable]
    public struct TerrainTextureMapping
    {
        [Tooltip("The index in the Terrain's Layer Palette (0 = top texture, 1 = second, etc.)")]
        public int TextureIndex;
        
        [Tooltip("The GroundData to apply for this texture.")]
        public GroundData SurfaceData;
    }
    [RequireComponent(typeof(Rigidbody))]
    public class BallPhysicsController : MonoBehaviour
    {
        [Header("Global Modifiers")]
        [Range(0f, 1f)]
        public float RollPreservationFactor = 0.9f;
        public GroundData DefaultGround; // Fallback just in case!

        [Header("Terrain Layer Setup")]
        [Tooltip("Map your terrain layers (Green, Bunker, etc.) to their specific GroundData.")]
        public List<LayerGroundMapping> TerrainLayerMappings;

        [Header("Terrain Painted Texture Setup")]
        public List<TerrainTextureMapping> TerrainTextureMappings;

        private Rigidbody rb;
        private Collider ballCollider;
        private BallData currentBall;

        // Current state tracking
        private int collisionCount = 0;
        private bool isGrounded = false;
        private int bounceCount = 0;
        public int BounceCount => bounceCount; // Add this line
        private float lastBounceUpVelocity = 0f;
        private float lastRollingSpeed = 0f;
        private float flightStartTime = 0f;
        
        private GroundData currentGround;
        public GroundData CurrentGround => currentGround;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ballCollider = GetComponent<Collider>();
            currentGround = DefaultGround;
        }

        // --- RESTORED LOGIC START ---

        public void Initialize(BallData ballData)
        {
            currentBall = ballData;
            ApplyBallData();
        }

        public void NotifyFlightStarted()
        {
            flightStartTime = Time.time;
            bounceCount = 0;
            lastBounceUpVelocity = 0f;
            rb.WakeUp();
        }

        private void ApplyBallData()
        {
            if (currentBall != null && rb != null)
            {
                rb.mass = currentBall.Mass;
                rb.linearDamping = 0f; 
                rb.angularDamping = 0.05f; 
                ApplyBounciness();
            }
        }

        private void ApplyBounciness()
        {
            if (ballCollider == null || currentBall == null) return;
            // Set bounciness to 0 — we handle all bouncing ourselves for full control
            PhysicsMaterial bounceMat = new PhysicsMaterial("BallPhysics")
            {
                bounciness = 0f,
                dynamicFriction = 0.6f,
                staticFriction = 0.6f,
                bounceCombine = PhysicsMaterialCombine.Minimum,
                frictionCombine = PhysicsMaterialCombine.Multiply 
            };
            ballCollider.material = bounceMat;
            if (rb != null) rb.maxAngularVelocity = 150f; 
        }

        // Detect hitting the flag to end the hole
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Flag"))
            {
                Debug.Log("[PlayerInput] Reached the flag! Ending the loop.");
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
                if (GameStateManager.Instance != null)
                    GameStateManager.Instance.ChangeState(GameStateManager.GameState.Resolution);
            }
        }

        // Stops the ball and loops back to the Setup state for the next shot
        private void StopBall()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep(); 
            
            StartCoroutine(DelayStateChange());
        }

        private System.Collections.IEnumerator DelayStateChange()
        {
            yield return new WaitForSeconds(2f);
            
            if (currentGround != null && currentGround.IsWaterHazard)
            {
                PlayerInputController input = GetComponent<PlayerInputController>();
                if (input != null)
                {
                    transform.position = input.LastLaunchPosition;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                
                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.ChangeState(GameStateManager.GameState.Setup);
                }
                yield break;
            }

            if (GameStateManager.Instance != null)
            {
                if (currentGround != null && currentGround.IsNiceOn)
                {
                    GameStateManager.Instance.ChangeState(GameStateManager.GameState.Aiming);
                }
                else
                {
                    GameStateManager.Instance.ChangeState(GameStateManager.GameState.Setup);
                }
            }
        }

        // --- RESTORED LOGIC END ---

        private void FixedUpdate()
        {
            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Flight)
            {
                // 1. DYNAMIC MOMENTUM KILL
                rb.linearDamping = currentGround.LinearDrag;
                rb.angularDamping = currentGround.AngularDrag;

                // 2. ROLL FRICTION LOGIC
                bool isPureRolling = isGrounded && Mathf.Abs(rb.linearVelocity.y) < 0.2f;
                if (isPureRolling)
                {
                    Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                    float currentSpeed = flatVel.magnitude;

                    if (currentSpeed > 0.01f)
                    {
                        if (currentGround.UseExtraRoll && currentGround.ExtraRollFactor > 0f)
                        {
                            if (lastRollingSpeed > 0f && currentSpeed < lastRollingSpeed)
                            {
                                // Calculate how much speed was lost to natural friction
                                float speedLost = lastRollingSpeed - currentSpeed;
                                
                                // ExtraRollFactor dictates how much of that lost speed we restore
                                // 0 = restore nothing (natural friction). 1 = restore 100% (unstoppable).
                                float speedToRestore = speedLost * currentGround.ExtraRollFactor;
                                currentSpeed += speedToRestore;
                                
                                rb.linearVelocity = new Vector3(flatVel.normalized.x * currentSpeed, rb.linearVelocity.y, flatVel.normalized.z * currentSpeed);
                            }
                        }
                        
                        // Always track the speed so we can compare next frame
                        lastRollingSpeed = currentSpeed;
                    }
                }
                else
                {
                    // Reset speed tracking when airborne or bouncing
                    lastRollingSpeed = 0f;
                }

                // 3. STOP LOGIC
                if (Time.time > flightStartTime + 0.1f && isGrounded)
                {
                    if (currentBall != null && rb.linearVelocity.sqrMagnitude < (currentBall.StopThreshold * currentBall.StopThreshold))
                    {
                        StopBall();
                    }
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            currentGround = DefaultGround; // Reset to default initially
            
            // 1. Check if we hit a Unity Terrain
            Terrain hitTerrain = collision.gameObject.GetComponent<Terrain>();
            if (hitTerrain != null && collision.contacts.Length > 0)
            {
                // Read the painted texture at the exact point of impact
                int textureIndex = GetMainTerrainTexture(collision.contacts[0].point, hitTerrain);
                
                if (TerrainTextureMappings != null)
                {
                    foreach (var mapping in TerrainTextureMappings)
                    {
                        if (mapping.TextureIndex == textureIndex)
                        {
                            currentGround = mapping.SurfaceData;
                            break;
                        }
                    }
                }
            }
            // 2. Fallback to standard GameObject Layers (for non-terrain objects)
            else if (TerrainLayerMappings != null)
            {
                int hitLayer = collision.gameObject.layer;
                foreach (var mapping in TerrainLayerMappings)
                {
                    if ((mapping.TerrainLayer.value & (1 << hitLayer)) > 0)
                    {
                        currentGround = mapping.SurfaceData;
                        break; 
                    }
                }
            }

            // ... The rest of your EXISTING flight and bounce logic remains exactly the same ...

            // --- EXISTING FLIGHT & BOUNCE LOGIC ---
            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Flight)
            {
                bounceCount++;
                Vector3 impactVelocity = rb.linearVelocity;

                if (currentGround.MaxBounces <= 0)
                {
                    // Sand/Mud logic - no bounces allowed
                    StartCoroutine(KillBounce());
                }
                else if (bounceCount == 1)
                {
                    AimVisualsController aimVisuals = GetComponent<AimVisualsController>();
                    if (aimVisuals != null)
                    {
                        aimVisuals.ShrinkTrailOnBounce();
                    }

                    // First bounce logic
                    StartCoroutine(ApplyFirstBounce(impactVelocity));
                }
                else if (bounceCount <= currentGround.MaxBounces)
                {
                    StartCoroutine(ApplySubsequentBounce());
                }
                else
                {
                    StartCoroutine(KillBounce());
                }
            }

            collisionCount++;
            isGrounded = collisionCount > 0;
        }

        private System.Collections.IEnumerator ApplyFirstBounce(Vector3 impactVelocity)
        {
            yield return new WaitForFixedUpdate();
            if (rb == null) yield break;

            Vector3 horizontalVel = new Vector3(impactVelocity.x, 0f, impactVelocity.z);
            float forwardSpeed = horizontalVel.magnitude;
            float impactDownSpeed = Mathf.Abs(Mathf.Min(impactVelocity.y, 0f)); 

            // Driven entirely by the Scriptable Object
            float bounceUpVelocity = (impactDownSpeed * currentGround.FirstBounceImpactScale)
                                   + (forwardSpeed * currentGround.ForwardToBounceConversion);

            float newForwardSpeed = forwardSpeed * currentGround.FirstBounceForwardKill;
            
            Vector3 newHorizontal = horizontalVel.sqrMagnitude > 0.001f
                ? horizontalVel.normalized * newForwardSpeed
                : Vector3.zero;

            lastBounceUpVelocity = bounceUpVelocity;
            rb.linearVelocity = new Vector3(newHorizontal.x, bounceUpVelocity, newHorizontal.z);
        }

        private System.Collections.IEnumerator ApplySubsequentBounce()
        {
            yield return new WaitForFixedUpdate();
            if (rb == null) yield break;

            lastBounceUpVelocity *= currentGround.BounceDecayRatio;

            Vector3 vel = rb.linearVelocity;
            vel.x *= currentGround.ForwardRetentionPerBounce;
            vel.z *= currentGround.ForwardRetentionPerBounce;
            vel.y = lastBounceUpVelocity;

            rb.linearVelocity = vel;

        }

        private System.Collections.IEnumerator KillBounce()
        {
            yield return new WaitForFixedUpdate();
            if (rb == null) yield break;

            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;
        }

        private void OnCollisionExit(Collision collision)
        {
            collisionCount = Mathf.Max(0, collisionCount - 1);
            isGrounded = collisionCount > 0;
            
            if (!isGrounded) 
            {
                currentGround = DefaultGround; // Reset to default when airborne
            }
        }

        private int GetMainTerrainTexture(Vector3 worldPos, Terrain terrain)
        {
            TerrainData terrainData = terrain.terrainData;
            Vector3 terrainPos = terrain.transform.position;

            // Calculate normalized position on the terrain
            float mapX = (worldPos.x - terrainPos.x) / terrainData.size.x;
            float mapZ = (worldPos.z - terrainPos.z) / terrainData.size.z;

            // Convert to alphamap coordinates
            int alphaX = Mathf.FloorToInt(mapX * terrainData.alphamapWidth);
            int alphaZ = Mathf.FloorToInt(mapZ * terrainData.alphamapHeight);

            // Safety bounds check
            if (alphaX < 0 || alphaX >= terrainData.alphamapWidth || alphaZ < 0 || alphaZ >= terrainData.alphamapHeight)
                return 0;

            // Get the splatmap data at this exact pixel (returns a 3D array)
            float[,,] splatmapData = terrainData.GetAlphamaps(alphaX, alphaZ, 1, 1);

            // Find the texture index with the highest weight
            int maxIndex = 0;
            float maxWeight = 0f;
            for (int i = 0; i < terrainData.alphamapLayers; i++)
            {
                if (splatmapData[0, 0, i] > maxWeight)
                {
                    maxWeight = splatmapData[0, 0, i];
                    maxIndex = i;
                }
            }
            return maxIndex;
        }
    }
}