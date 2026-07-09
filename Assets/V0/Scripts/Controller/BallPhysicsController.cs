using UnityEngine;
using GolfGame.Data;
using GolfGame.Environment;

namespace GolfGame.Controllers
{
    [RequireComponent(typeof(Rigidbody))]
    public class BallPhysicsController : MonoBehaviour
    {
        [Header("Global Modifiers")]
        [Range(0.90f, 0.999f)]
        public float RollPreservationFactor = 0.994f;
        public GroundData DefaultGround; // Fallback just in case!

        private Rigidbody rb;
        private Collider ballCollider;
        private BallData currentBall;

        // Current state tracking
        private int collisionCount = 0;
        private bool isGrounded = false;
        private int bounceCount = 0;
        private float lastBounceUpVelocity = 0f;
        private float flightStartTime = 0f;
        
        private GroundData currentGround;

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
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.ChangeState(GameStateManager.GameState.Setup);
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
                        // Use the RollFactor directly from the ScriptableObject
                        float dynamicRollFactor = Mathf.Min(RollPreservationFactor, currentGround.RollFactor);
                        
                        float targetSpeed = currentSpeed * dynamicRollFactor;
                        Vector3 newVelocity = flatVel.normalized * targetSpeed;
                        newVelocity.y = rb.linearVelocity.y; 
                        rb.linearVelocity = newVelocity;
                    }
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
            // Fetch the terrain data directly from the object we hit
            GroundSurface surface = collision.gameObject.GetComponent<GroundSurface>();
            if (surface != null && surface.SurfaceData != null)
            {
                currentGround = surface.SurfaceData;
            }
            else
            {
                currentGround = DefaultGround; 
            }

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
                    // First bounce logic is now universal, just driven by the current GroundData
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
    }
}