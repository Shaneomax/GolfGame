using UnityEngine;

namespace GolfGame.Controllers
{
    [RequireComponent(typeof(Rigidbody))]
    public class BallPhysicsController : MonoBehaviour
    {
        [Header("Physics Modifiers")]
        public float GroundLinearDamping = 0.15f;
        public float GroundAngularDamping = 0.1f;
        public float MudAngularDrag = 8.0f;
        public float MudLinearDrag = 4.0f;

        [Header("GolfClash-Style Bounce System")]
        [Tooltip("How much of the incoming downward impact speed is converted into upward bounce on the 1st bounce. Higher = more dramatic first bounce.")]
        [Range(0f, 2f)]
        public float FirstBounceImpactScale = 1.1f;

        [Tooltip("How much of the horizontal (forward) speed is also converted into upward energy on the first bounce. Simulates the ball digging in.")]
        [Range(0f, 0.5f)]
        public float ForwardToBounceConversion = 0.25f;

        [Tooltip("Fraction of the first bounce height used for the second bounce (e.g. 0.5 = half height). GolfClash uses ~0.5.")]
        [Range(0f, 1f)]
        public float BounceDecayRatio = 0.5f;

        [Tooltip("How much forward speed is retained on each subsequent bounce (1 = no loss, 0 = full stop).")]
        [Range(0f, 1f)]
        public float ForwardRetentionPerBounce = 0.75f;

        [Tooltip("Extra forward kill on the very first bounce (ball 'sticks' into the ground a bit). 0.6 = keeps 60% of forward speed.")]
        [Range(0f, 1f)]
        public float FirstBounceForwardKill = 0.6f;

        [Tooltip("Maximum number of bounces before the ball is forced to roll.")]
        public int MaxBounces = 3;

        [Header("Green Settings")]
        [Tooltip("Scales how high the first bounce is on the Green. Lower than fairway = softer landing.")]
        [Range(0f, 1f)]
        public float GreenBounceImpactScale = 0.45f;

        [Tooltip("Max number of bounces allowed on the Green before forcing roll.")]
        public int GreenMaxBounces = 2;

        [Tooltip("How much roll resistance to apply on the Green (lower = stops sooner). 0.972 is a good starting point.")]
        [Range(0.90f, 0.999f)]
        public float GreenRollFactor = 0.972f;

        [Header("Bunker / Sand Settings")]
        [Tooltip("Linear drag applied while the ball is in the bunker. High value = stops very quickly.")]
        public float BunkerLinearDrag = 8.0f;

        [Tooltip("Angular drag applied while the ball is in the bunker.")]
        public float BunkerAngularDrag = 12.0f;

        [Tooltip("Roll deceleration factor for the bunker. Very low = ball barely rolls. 0.91 = stops in 1-2m.")]
        [Range(0.90f, 0.999f)]
        public float BunkerRollFactor = 0.91f;

        [Header("Arcade Roll Settings")]
        [Range(0.90f, 0.999f)]
        public float RollPreservationFactor = 0.994f;

        private Rigidbody rb;
        private Collider ballCollider;
        private BallData currentBall;

        private int collisionCount = 0;
        private bool isGrounded = false;
        private bool isInMud = false;
        private bool isInBunker = false;
        private string currentGroundTag = "Untagged";
        private int bounceCount = 0;

        // Tracks the upward velocity we set on the last bounce so we can halve it next time
        private float lastBounceUpVelocity = 0f;
        private float flightStartTime = 0f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ballCollider = GetComponent<Collider>();
        }

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

        private void FixedUpdate()
        {
            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Flight)
            {
                // 1. DYNAMIC MOMENTUM KILL
                if (isInMud)
                {
                    rb.linearDamping = MudLinearDrag;
                    rb.angularDamping = MudAngularDrag;
                }
                else if (isInBunker)
                {
                    rb.linearDamping = BunkerLinearDrag;
                    rb.angularDamping = BunkerAngularDrag;
                }
                else
                {
                    rb.linearDamping = 0f;
                    rb.angularDamping = 0.05f;
                }

                // 2. ROLL FRICTION LOGIC (only applied when rolling, not bouncing)
                bool isPureRolling = isGrounded && Mathf.Abs(rb.linearVelocity.y) < 0.2f;
                if (isPureRolling)
                {
                    Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                    float currentSpeed = flatVel.magnitude;

                    if (currentSpeed > 0.01f)
                    {
                        float dynamicRollFactor = RollPreservationFactor;
                        if (currentGroundTag == "Fairway" || currentGroundTag == "Untagged")
                            dynamicRollFactor = Mathf.Min(RollPreservationFactor, 0.965f);
                        else if (currentGroundTag == "Rough")
                            dynamicRollFactor = Mathf.Min(RollPreservationFactor, 0.4f);
                        else if (currentGroundTag == "Green")
                            dynamicRollFactor = GreenRollFactor;
                        else if (currentGroundTag == "Bunker")
                            dynamicRollFactor = BunkerRollFactor;

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

        private void OnCollisionEnter(Collision collision)
        {
            currentGroundTag = collision.gameObject.tag;
            if (currentGroundTag == "Mud") isInMud = true;
            if (currentGroundTag == "Bunker") isInBunker = true;

            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Flight)
            {
                bool isFairway = currentGroundTag == "Fairway" || currentGroundTag == "Untagged";
                bool isGreen = currentGroundTag == "Green";

                if (isFairway)
                {
                    bounceCount++;

                    // Capture the incoming velocity BEFORE the physics engine zeroes it out
                    Vector3 impactVelocity = rb.linearVelocity;

                    if (bounceCount == 1)
                        StartCoroutine(ApplyFirstBounce(impactVelocity));
                    else if (bounceCount <= MaxBounces) 
                        StartCoroutine(ApplySubsequentBounce());
                    else
                        StartCoroutine(KillBounce());
                }
                else if (isGreen)
                {
                    bounceCount++;

                    Vector3 impactVelocity = rb.linearVelocity;

                    if (bounceCount == 1)
                        StartCoroutine(ApplyGreenFirstBounce(impactVelocity));
                    else if (bounceCount <= GreenMaxBounces)
                        StartCoroutine(ApplySubsequentBounce());
                    else
                        StartCoroutine(KillBounce());
                }
                else if (currentGroundTag == "Bunker")
                {
                    // Sand — no bounce at all, ball digs in immediately
                    StartCoroutine(KillBounce());
                }
            }

            collisionCount++;
            isGrounded = collisionCount > 0;
        }

        /// <summary>
        /// First bounce: high, kills forward momentum significantly.
        /// Upward velocity is computed from incoming downward impact + forward speed contribution.
        /// </summary>
        private System.Collections.IEnumerator ApplyFirstBounce(Vector3 impactVelocity)
        {
            yield return new WaitForFixedUpdate();
            if (rb == null) yield break;

            Vector3 horizontalVel = new Vector3(impactVelocity.x, 0f, impactVelocity.z);
            float forwardSpeed = horizontalVel.magnitude;
            float impactDownSpeed = Mathf.Abs(Mathf.Min(impactVelocity.y, 0f)); // positive downward speed

            // Upward velocity = impact down speed scaled + portion of forward speed (ball digs in)
            float bounceUpVelocity = (impactDownSpeed * FirstBounceImpactScale)
                                   + (forwardSpeed * ForwardToBounceConversion);

            // Kill forward momentum more aggressively on first hit (ball "digs in")
            float newForwardSpeed = forwardSpeed * FirstBounceForwardKill;
            Vector3 newHorizontal = horizontalVel.sqrMagnitude > 0.001f
                ? horizontalVel.normalized * newForwardSpeed
                : Vector3.zero;

            lastBounceUpVelocity = bounceUpVelocity;
            rb.linearVelocity = new Vector3(newHorizontal.x, bounceUpVelocity, newHorizontal.z);
        }

        /// <summary>
        /// Green first bounce: much lower than fairway, soft landing feel.
        /// Uses GreenBounceImpactScale instead of FirstBounceImpactScale.
        /// </summary>
        private System.Collections.IEnumerator ApplyGreenFirstBounce(Vector3 impactVelocity)
        {
            yield return new WaitForFixedUpdate();
            if (rb == null) yield break;

            Vector3 horizontalVel = new Vector3(impactVelocity.x, 0f, impactVelocity.z);
            float forwardSpeed = horizontalVel.magnitude;
            float impactDownSpeed = Mathf.Abs(Mathf.Min(impactVelocity.y, 0f));

            // Soft bounce — use the reduced Green impact scale
            float bounceUpVelocity = (impactDownSpeed * GreenBounceImpactScale)
                                   + (forwardSpeed * ForwardToBounceConversion * 0.4f);

            // Kill forward speed on landing (green is soft/slow)
            float newForwardSpeed = forwardSpeed * FirstBounceForwardKill * 0.7f;
            Vector3 newHorizontal = horizontalVel.sqrMagnitude > 0.001f
                ? horizontalVel.normalized * newForwardSpeed
                : Vector3.zero;

            lastBounceUpVelocity = bounceUpVelocity;
            rb.linearVelocity = new Vector3(newHorizontal.x, bounceUpVelocity, newHorizontal.z);
        }

        /// <summary>
        /// Subsequent bounces: exactly BounceDecayRatio of the previous bounce height.
        /// Each one also gently damps forward momentum.
        /// </summary>
        private System.Collections.IEnumerator ApplySubsequentBounce()
        {
            yield return new WaitForFixedUpdate();
            if (rb == null) yield break;

            // Each bounce is exactly BounceDecayRatio (default 0.5 = half) of the previous
            lastBounceUpVelocity *= BounceDecayRatio;

            Vector3 vel = rb.linearVelocity;
            vel.x *= ForwardRetentionPerBounce;
            vel.z *= ForwardRetentionPerBounce;
            vel.y = lastBounceUpVelocity;

            rb.linearVelocity = vel;
        }

        /// <summary>
        /// After MaxBounces, force the ball into a roll by zeroing upward velocity.
        /// </summary>
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
            if (collision.gameObject.CompareTag("Mud")) isInMud = false;
            if (collision.gameObject.CompareTag("Bunker")) isInBunker = false;
            collisionCount = Mathf.Max(0, collisionCount - 1);
            isGrounded = collisionCount > 0;
        }

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

        private void StopBall()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep(); 
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.ChangeState(GameStateManager.GameState.Setup);
        }
    }
}
