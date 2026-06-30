using UnityEngine;

namespace GolfGame.Controllers
{
    /// <summary>
    /// Handles user input for aiming and hitting the golf ball, as well as managing physics 
    /// drag based on terrain context (Air, Ground, Mud).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerInputController : MonoBehaviour
    {
        #region Settings

        [Header("Input Settings")]
        [Tooltip("The maximum distance on the screen the user can drag to build up force.")]
        public float MaxDragDistance = 3f;

        [Tooltip("Fixed loft angle (in degrees) to ensure a realistic golf shot arc (Parabola).")]
        public float DefaultLoftAngle = 30f;

        [Tooltip("Scale factor to reduce the overall launch force. Lower = softer shot. Adjust this first if the ball goes too far.")]
        public float PowerScale = 0.3f;

        [Header("Physics Modifiers")]
        [Tooltip("Linear damping when the ball is rolling on the ground. Overrides BallData.LinearDrag on landing.")]
        public float GroundLinearDamping = 0.15f;

        [Tooltip("Angular damping when rolling. If BallData is assigned, BallData.AngularDrag is used instead.")]
        public float GroundAngularDamping = 0.1f;

        [Header("Terrain Modifiers")]
        [Tooltip("How fast the ball stops rolling in the mud.")]
        public float MudAngularDrag = 8.0f; 

        [Tooltip("Extra air/sliding resistance when in mud.")]
        public float MudLinearDrag = 4.0f;

        #endregion

        #region Data References

        [Header("Data References")]
        [Tooltip("Data containing physics values and ball details.")]
        public BallData CurrentBall;
        
        [Tooltip("Data containing club power stats.")]
        public ClubData CurrentClub;

        #endregion

        #region Private Fields

        private Rigidbody rb;
        private Camera mainCamera;
        private Collider ballCollider;
        
        private Vector3 dragStartPosition;
        private bool isDragging = false;
        private float flightStartTime = 0f;

        // Ground detection
        private int collisionCount = 0;
        private bool isGrounded = false;
        private bool isInMud = false;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ballCollider = GetComponent<Collider>();
            mainCamera = Camera.main; 
        }

        private void Start()
        {
            ApplyBallData();
        }

        private void FixedUpdate()
        {
            if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Flight)
            {
                // Wait a brief moment after launch to allow physics to apply the force
                if (Time.time > flightStartTime + 0.1f)
                {
                    if (CurrentBall != null && rb.linearVelocity.sqrMagnitude < (CurrentBall.StopThreshold * CurrentBall.StopThreshold))
                    {
                        StopBall();
                    }
                }
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Applies the ball configuration data (Mass, default drags) to the Rigidbody.
        /// Can be called externally when instantiating the ball.
        /// </summary>
        public void ApplyBallData()
        {
            if (CurrentBall != null && rb != null)
            {
                rb.mass = CurrentBall.Mass;
                ApplyBounciness();
                UpdatePhysicsDrag();
            }
            else if (CurrentBall == null)
            {
                Debug.LogWarning("No BallData assigned to PlayerInputController!");
            }
        }

        /// <summary>
        /// Creates and applies a PhysicsMaterial to the ball's collider using BallData.Bounciness.
        /// </summary>
        private void ApplyBounciness()
        {
            if (ballCollider == null || CurrentBall == null) return;

            PhysicsMaterial bounceMat = new PhysicsMaterial("BallPhysics")
            {
                bounciness         = CurrentBall.Bounciness,
                dynamicFriction    = 0.4f,
                staticFriction     = 0.4f,
                // CombineMax ensures the bounciness value is always respected on contact
                bounceCombine      = PhysicsMaterialCombine.Maximum,
                frictionCombine    = PhysicsMaterialCombine.Average
            };

            ballCollider.material = bounceMat;
            Debug.Log($"[PlayerInputController] Applied bounciness: {CurrentBall.Bounciness}");
        }

        #endregion

        #region Physics Management

        /// <summary>
        /// Updates the Rigidbody's damping based on its current terrain/air context.
        /// </summary>
        private void UpdatePhysicsDrag()
        {
            if (isInMud)
            {
                rb.linearDamping  = MudLinearDrag;
                rb.angularDamping = MudAngularDrag;
            }
            else if (isGrounded)
            {
                // Use BallData.AngularDrag if available, otherwise fall back to Inspector value
                rb.linearDamping  = GroundLinearDamping;
                rb.angularDamping = CurrentBall != null ? CurrentBall.AngularDrag : GroundAngularDamping;
            }
            else
            {
                // Air: use BallData.LinearDrag for realistic in-flight resistance
                rb.linearDamping  = CurrentBall != null ? CurrentBall.LinearDrag : 0.02f;
                rb.angularDamping = 0.01f; // Minimal spin drag in the air
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Mud"))
            {
                isInMud = true;
            }
            
            collisionCount++;
            isGrounded = collisionCount > 0;
            
            UpdatePhysicsDrag();
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision.gameObject.CompareTag("Mud"))
            {
                isInMud = false;
            }
            
            collisionCount = Mathf.Max(0, collisionCount - 1);
            isGrounded = collisionCount > 0;
            
            UpdatePhysicsDrag();
        }

        /// <summary>
        /// Stops the ball completely and transitions the state back to Aiming.
        /// </summary>
        private void StopBall()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep(); 

            GameStateManager.Instance.ChangeState(GameStateManager.GameState.Aiming);
        }

        #endregion

        #region Input Handling

        private void OnMouseDown()
        {
            if (GameStateManager.Instance.CurrentState != GameStateManager.GameState.Aiming)
                return;

            isDragging = true;
            dragStartPosition = GetMouseWorldPos();
        }

        private void OnMouseUp()
        {
            if (!isDragging) return;
            isDragging = false;

            Vector3 dragVector = dragStartPosition - GetMouseWorldPos();
            float dragMagnitude = Mathf.Clamp(dragVector.magnitude, 0, MaxDragDistance);
            
            // 1. Flatten the drag vector to the X/Z plane to get the purely horizontal direction of the shot
            Vector3 flatDirection = new Vector3(dragVector.x, 0, dragVector.z).normalized;
            if (flatDirection.sqrMagnitude < 0.01f) 
            {
                flatDirection = Vector3.forward; // Fallback if dragged straight down perfectly
            }

            // 2. Rotate the flat direction UPWARD by the Loft angle to get a parabolic arc.
            //    Axis = Cross(flatDir, up) gives the "left" axis relative to the shot.
            //    A positive angle around that left axis pitches the shot upward.
            Vector3 loftAxis = Vector3.Cross(flatDirection, Vector3.up);
            Vector3 launchDirection = Quaternion.AngleAxis(DefaultLoftAngle, loftAxis) * flatDirection;

            // 3. Scale the force by the ClubData's Power, the drag ratio, and the PowerScale tuner
            float clubPower = CurrentClub != null ? CurrentClub.Power : 5f;
            float powerRatio = dragMagnitude / MaxDragDistance;
            Vector3 launchForce = launchDirection * (powerRatio * clubPower * PowerScale);

            rb.AddForce(launchForce, ForceMode.Impulse);
            flightStartTime = Time.time;
            
            // Assume we immediately leave the ground when launched, allowing air physics to take over
            UpdatePhysicsDrag();

            GameStateManager.Instance.ChangeState(GameStateManager.GameState.Flight);
        }

        /// <summary>
        /// Converts the current mouse screen position into world space based on the camera.
        /// </summary>
        private Vector3 GetMouseWorldPos()
        {
            Vector3 mousePoint = Input.mousePosition;
            mousePoint.z = mainCamera.WorldToScreenPoint(transform.position).z;
            return mainCamera.ScreenToWorldPoint(mousePoint);
        }

        #endregion
    }
}