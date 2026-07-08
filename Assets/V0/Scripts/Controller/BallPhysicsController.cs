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

        [Tooltip("How much forward momentum is kept on subsequent Fairway bounces (0 = none, 1 = all).")]
        [Range(0f, 1f)]
        public float FairwayForwardDamping = 0.9f;

        [Tooltip("Boost multiplier for the VERY FIRST bounce. Increase this if the first bounce isn't high enough.")]
        [Range(1f, 3f)]
        public float FirstBounceBoost = 1.2f;

        [Tooltip("How much forward speed is converted into upward bounce on the first hit (0 to 1).")]
        [Range(0f, 1f)]
        public float ForwardToBounceConversion = 0.35f;

        [Tooltip("How much bounce height is kept on subsequent Fairway bounces (0.5 = half of the previous bounce).")]
        [Range(0f, 1.5f)]
        public float FairwayBounceDamping = 0.5f;

        [Header("Arcade Roll Settings")]
        [Range(0.90f, 0.999f)]
        public float RollPreservationFactor = 0.994f;

        private Rigidbody rb;
        private Collider ballCollider;
        private BallData currentBall;

        private int collisionCount = 0;
        private bool isGrounded = false;
        private bool isInMud = false;
        private string currentGroundTag = "Untagged";
        private int bounceCount = 0;
        private float lastBounceVelocityY = 0f;
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
                else
                {
                    rb.linearDamping = 0f; 
                    rb.angularDamping = 0.05f; 
                }

                // 2. EXISTING ROLL LOGIC
                bool isPureRolling = isGrounded && Mathf.Abs(rb.linearVelocity.y) < 0.2f;
                if (isPureRolling)
                {
                    Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                    float currentSpeed = flatVel.magnitude;

                    if (currentSpeed > 0.01f)
                    {
                        float dynamicRollFactor = RollPreservationFactor;
                        if (currentGroundTag == "Fairway" || currentGroundTag == "Untagged")
                            dynamicRollFactor = Mathf.Min(RollPreservationFactor, 0.985f);
                        else if (currentGroundTag == "Rough")
                            dynamicRollFactor = Mathf.Min(RollPreservationFactor, 0.95f);
                        else if (currentGroundTag == "Green")
                            dynamicRollFactor = Mathf.Max(RollPreservationFactor, 0.995f);

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
            PhysicsMaterial bounceMat = new PhysicsMaterial("BallPhysics")
            {
                bounciness = currentBall.Bounciness,
                dynamicFriction = 0.6f,
                staticFriction = 0.6f,
                bounceCombine = PhysicsMaterialCombine.Average,
                frictionCombine = PhysicsMaterialCombine.Multiply
            };
            ballCollider.material = bounceMat;
            if (rb != null) rb.maxAngularVelocity = 150f; 
        }

        private void OnCollisionEnter(Collision collision)
        {
            currentGroundTag = collision.gameObject.tag;
            if (currentGroundTag == "Mud") isInMud = true;

            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Flight)
            {
                bool isFairway = currentGroundTag == "Fairway" || currentGroundTag == "Untagged";
                if (isFairway)
                {
                    bounceCount++;
                    if (bounceCount == 1)
                        StartCoroutine(ApplyFirstBouncePhysics());
                    else if (bounceCount == 2 || bounceCount == 3) 
                        StartCoroutine(ApplyExactBounceDamping());
                    else if (bounceCount > 3)
                        StartCoroutine(KillBounce());
                }
            }

            collisionCount++;
            isGrounded = collisionCount > 0;
        }

        private System.Collections.IEnumerator ApplyFirstBouncePhysics()
        {
            yield return new WaitForFixedUpdate();
            if (rb != null)
            {
                Vector3 vel = rb.linearVelocity;
                Vector3 horizontalVel = new Vector3(vel.x, 0, vel.z);
                float forwardSpeed = horizontalVel.magnitude;
                float transferredSpeed = forwardSpeed * ForwardToBounceConversion;

                float newForwardSpeed = Mathf.Max(0, forwardSpeed - transferredSpeed);
                horizontalVel = horizontalVel.normalized * newForwardSpeed;

                float newUpwardVelocity = (vel.y * FirstBounceBoost) + transferredSpeed;
                lastBounceVelocityY = newUpwardVelocity;
                rb.linearVelocity = new Vector3(horizontalVel.x, newUpwardVelocity, horizontalVel.z);
            }
        }

        private System.Collections.IEnumerator ApplyExactBounceDamping()
        {
            yield return new WaitForFixedUpdate();
            if (rb != null)
            {
                Vector3 vel = rb.linearVelocity;
                lastBounceVelocityY *= FairwayBounceDamping; 
                vel.x *= FairwayForwardDamping;
                vel.z *= FairwayForwardDamping;
                vel.y = lastBounceVelocityY;
                rb.linearVelocity = vel;
            }
        }

        private System.Collections.IEnumerator KillBounce()
        {
            yield return new WaitForFixedUpdate();
            if (rb != null)
            {
                Vector3 vel = rb.linearVelocity;
                vel.y = 0f;
                vel.x *= FairwayForwardDamping;
                vel.z *= FairwayForwardDamping;
                rb.linearVelocity = vel;
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision.gameObject.CompareTag("Mud")) isInMud = false;
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
