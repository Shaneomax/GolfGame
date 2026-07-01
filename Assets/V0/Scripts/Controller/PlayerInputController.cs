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

        [Header("Target Marker Settings")]
        [Tooltip("Prefab for the 3D target marker spawned on the ground.")]
        public GameObject TargetMarkerPrefab;

        #endregion

        #region Private Fields

        private Rigidbody rb;
        private Camera mainCamera;
        private Collider ballCollider;
        private TrajectoryPredictor trajectoryPredictor;
        
        private Vector3 dragStartPosition;
        private bool isDragging = false;
        private float flightStartTime = 0f;

        // Ground detection
        private int collisionCount = 0;
        private bool isGrounded = false;
        private bool isInMud = false;

        private Vector3 fixedAimDirection = Vector3.forward;
        public Vector3 FixedAimDirection => fixedAimDirection;

        private GameObject activeTargetMarker;
        private bool isDraggingTarget = false;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            rb            = GetComponent<Rigidbody>();
            ballCollider  = GetComponent<Collider>();
            mainCamera    = Camera.main;
            trajectoryPredictor = GetComponent<TrajectoryPredictor>();
        }

        private void Start()
        {
            ApplyBallData();
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnStateEnter += OnStateEnter;
                GameStateManager.Instance.OnStateExit += OnStateExit;

                if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Setup)
                {
                    OnStateEnter(GameStateManager.GameState.Setup);
                }
            }
        }

        private void OnDestroy()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnStateEnter -= OnStateEnter;
                GameStateManager.Instance.OnStateExit -= OnStateExit;
            }
            if (activeTargetMarker != null)
            {
                Destroy(activeTargetMarker);
            }
        }

        private void OnStateEnter(GameStateManager.GameState newState)
        {
            if (newState == GameStateManager.GameState.Setup)
            {
                SpawnTargetMarker();
                RepositionTargetMarker();
            }
            else if (newState == GameStateManager.GameState.Aiming)
            {
                if (trajectoryPredictor != null)
                {
                    trajectoryPredictor.HideTrajectory();
                }
                if (activeTargetMarker != null)
                {
                    activeTargetMarker.SetActive(false);
                }
            }
            else if (newState == GameStateManager.GameState.Flight)
            {
                if (activeTargetMarker != null)
                {
                    activeTargetMarker.SetActive(false);
                }
            }
        }

        private void OnStateExit(GameStateManager.GameState oldState)
        {
            if (oldState == GameStateManager.GameState.Setup)
            {
                isDraggingTarget = false;
            }
        }

        /// <summary>
        /// Calculates the maximum horizontal range the ball can travel using projectile kinematics.
        /// R = (v² × sin(2θ)) / g
        /// </summary>
        private float CalculateMaxRange()
        {
            float clubPower = CurrentClub != null ? CurrentClub.Power : 15f;
            float v = clubPower * PowerScale;
            float g = Mathf.Abs(Physics.gravity.y);
            float theta = DefaultLoftAngle * Mathf.Deg2Rad;
            float range = (v * v * Mathf.Sin(2f * theta)) / g;
            return Mathf.Max(range, 1f); // Minimum 1 unit to avoid zero
        }

        private void SpawnTargetMarker()
        {
            if (TargetMarkerPrefab != null && activeTargetMarker == null)
            {
                float maxRange = CalculateMaxRange();
                Vector3 spawnPos = transform.position + fixedAimDirection * maxRange;
                spawnPos.y = 0f;
                activeTargetMarker = Instantiate(TargetMarkerPrefab, spawnPos, Quaternion.identity);
            }
            
            if (activeTargetMarker != null)
            {
                activeTargetMarker.SetActive(true);
                Vector3 diff = activeTargetMarker.transform.position - transform.position;
                fixedAimDirection = new Vector3(diff.x, 0f, diff.z).normalized;
            }
        }

        private void RepositionTargetMarker()
        {
            if (activeTargetMarker != null)
            {
                float maxRange = CalculateMaxRange();
                Vector3 newPos = transform.position + fixedAimDirection * maxRange;
                newPos.y = 0f;
                activeTargetMarker.transform.position = newPos;
                activeTargetMarker.SetActive(true);
            }
        }

        private void HandleSetupInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    if (activeTargetMarker != null && (hit.collider.gameObject == activeTargetMarker || hit.collider.transform.IsChildOf(activeTargetMarker.transform)))
                    {
                        isDraggingTarget = true;
                    }
                }
            }
            else if (Input.GetMouseButton(0) && isDraggingTarget && activeTargetMarker != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                // Project onto Y=0 ground plane
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    
                    Vector3 diff = hitPoint - transform.position;
                    float maxRange = CalculateMaxRange();
                    if (new Vector3(diff.x, 0f, diff.z).magnitude > maxRange)
                    {
                        hitPoint = transform.position + new Vector3(diff.x, 0f, diff.z).normalized * maxRange;
                    }

                    // Lock Y to 0
                    hitPoint.y = 0f;
                    activeTargetMarker.transform.position = hitPoint;

                    Vector3 horizontalDirection = new Vector3(diff.x, 0f, diff.z).normalized;
                    if (horizontalDirection.sqrMagnitude > 0.001f)
                    {
                        fixedAimDirection = horizontalDirection;
                    }
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDraggingTarget = false;
            }
        }

        private Vector3 CalculateVelocityToHitTarget(Vector3 targetPos)
        {
            Vector3 diff = targetPos - transform.position;
            Vector3 horizontalDiff = new Vector3(diff.x, 0f, diff.z);
            float d = horizontalDiff.magnitude;
            float h = diff.y;
            float g = Mathf.Abs(Physics.gravity.y);
            float theta = DefaultLoftAngle * Mathf.Deg2Rad;

            float denominator = 2f * (d * Mathf.Tan(theta) - h);
            if (denominator <= 0.001f || d <= 0.001f)
            {
                return horizontalDiff.normalized * 5f + Vector3.up * 2f;
            }

            float speed = (d / Mathf.Cos(theta)) * Mathf.Sqrt(g / denominator);
            
            float maxClubPower = CurrentClub != null ? CurrentClub.Power : 15f;
            float maxSpeed = maxClubPower * PowerScale;
            speed = Mathf.Min(speed, maxSpeed);

            Vector3 flatDirection = horizontalDiff.normalized;
            Vector3 loftAxis = Vector3.Cross(flatDirection, Vector3.up);
            Vector3 launchDir = Quaternion.AngleAxis(DefaultLoftAngle, loftAxis) * flatDirection;

            return launchDir * speed;
        }

        private void Update()
        {
            if (GameStateManager.Instance == null) return;

            if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Setup)
            {
                HandleSetupInput();

                if (trajectoryPredictor != null && activeTargetMarker != null)
                {
                    Vector3 launchVelocity = CalculateVelocityToHitTarget(activeTargetMarker.transform.position);
                    trajectoryPredictor.ShowTrajectory(transform.position, launchVelocity);
                }
            }
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

        private void OnMouseDrag()
        {
            if (!isDragging) return;
            // No trajectory line update when dragging per user request.
        }

        private void OnMouseUp()
        {
            if (!isDragging) return;
            isDragging = false;

            // Hide the trajectory line immediately on release
            if (trajectoryPredictor != null)
                trajectoryPredictor.HideTrajectory();

            Vector3 launchVelocity = CalculateLaunchVelocity();
            if (launchVelocity.sqrMagnitude < 0.001f) return; // Ignore accidental micro-taps

            // Force air-physics state immediately — don't wait for OnCollisionExit next frame
            collisionCount = 0;
            isGrounded     = false;
            isInMud        = false;

            rb.AddForce(launchVelocity, ForceMode.VelocityChange);
            flightStartTime = Time.time;

            UpdatePhysicsDrag();
            GameStateManager.Instance.ChangeState(GameStateManager.GameState.Flight);
        }

        /// <summary>
        /// Calculates the launch velocity vector from the current mouse drag.
        /// Shared between OnMouseDrag (preview) and OnMouseUp (actual shot).
        /// </summary>
        private Vector3 CalculateLaunchVelocity()
        {
            Vector3 dragVector    = dragStartPosition - GetMouseWorldPos();
            
            // Calculate power by projecting the drag vector onto the fixed aim direction
            // (Dragging backward relative to fixedAimDirection gives positive force forward)
            float dragProj        = Vector3.Dot(dragVector, fixedAimDirection);
            float dragMagnitude   = Mathf.Clamp(dragProj, 0f, MaxDragDistance);

            // 1. Lock shot direction to the fixed aim direction set in Setup phase
            Vector3 flatDirection = fixedAimDirection;

            // 2. Pitch the direction upward by the loft angle for a parabolic arc
            Vector3 loftAxis      = Vector3.Cross(flatDirection, Vector3.up);
            Vector3 launchDir     = Quaternion.AngleAxis(DefaultLoftAngle, loftAxis) * flatDirection;

            // 3. Scale by club power, drag ratio, and the PowerScale tuner.
            //    We use VelocityChange so velocity = force directly (no mass division needed)
            float clubPower  = CurrentClub != null ? CurrentClub.Power : 5f;
            float powerRatio = dragMagnitude / MaxDragDistance;

            return launchDir * (powerRatio * clubPower * PowerScale);
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