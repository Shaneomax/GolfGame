using UnityEngine;

namespace GolfGame.Controllers
{
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

        [Tooltip("Maximum sideways deviation (in degrees) applied to a shot when the accuracy arrow is fully off-centre. " +
                 "0 = no deviation (perfect shot every time). 20-30 = realistic Golf Rival-style miss.")]
        public float MaxDeviationAngle = 22f;

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

        [Header("Shot Accuracy")]
        [Tooltip("Reference to the ShotAccuracyController that owns the arrow indicator. " +
                 "Assign in the Inspector.")]
        public ShotAccuracyController AccuracyController;

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
        public bool IsDraggingTarget => isDraggingTarget;
        public GameObject ActiveTargetMarker => activeTargetMarker;

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

                // Pass the current club to the accuracy controller so it knows
                // how fast to oscillate the needle (lower Accuracy = faster).
                if (AccuracyController != null)
                {
                    AccuracyController.SetClub(CurrentClub);
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
                    trajectoryPredictor.ShowTrajectory(transform.position, launchVelocity, activeTargetMarker.transform.position.y);
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
            // Only allow aiming/shooting if in the correct state
            if (GameStateManager.Instance.CurrentState != GameStateManager.GameState.Aiming)
                return;

            isDragging = true;
            dragStartPosition = Input.mousePosition; // Use screen space for UI consistency

            // Lock the accuracy arrow the moment the player touches the ball.
            // This freezes the needle position and stores LockedAccuracyValue.
            AccuracyController?.LockAccuracy();
        }

        private void OnMouseDrag()
        {
            if (!isDragging) return;

            // Calculate drag vector
            Vector3 currentMousePos = Input.mousePosition;
            Vector3 dragVector = dragStartPosition - currentMousePos;
            
            // Convert drag to world space or local aim direction
            // Here we use the drag vector to influence the trajectory
            Vector3 launchVelocity = CalculateDragVelocity(dragVector);

            // Update trajectory predictor in real-time
            if (trajectoryPredictor != null)
            {
                trajectoryPredictor.ShowTrajectory(transform.position, launchVelocity);
            }
        }

        private void OnMouseUp()
        {
            if (!isDragging) return;
            isDragging = false;

            if (trajectoryPredictor != null)
                trajectoryPredictor.HideTrajectory();

            Vector3 launchVelocity = CalculateDragVelocity(dragStartPosition - Input.mousePosition);
            
            if (launchVelocity.sqrMagnitude > 0.1f)
            {
                rb.AddForce(launchVelocity, ForceMode.VelocityChange);
                GameStateManager.Instance.ChangeState(GameStateManager.GameState.Flight);
            }
        }

        private Vector3 CalculateDragVelocity(Vector3 dragVector)
        {
            // Normalize drag to a 0-1 range based on MaxDragDistance
            float dragMagnitude = Mathf.Clamp(dragVector.magnitude, 0f, MaxDragDistance);
            float powerRatio = dragMagnitude / MaxDragDistance;

            // Use FixedAimDirection (set during Setup) as the base direction
            Vector3 flatDirection = fixedAimDirection;

            // ── GOLF RIVAL ACCURACY DEVIATION ────────────────────────────────
            // LockedAccuracyValue: -1 (max left miss) … 0 (perfect) … +1 (max right miss)
            // Rotate the flat direction sideways around the world-up axis.
            if (AccuracyController != null && AccuracyController.IsLocked)
            {
                float deviationAngle = AccuracyController.LockedAccuracyValue * MaxDeviationAngle;
                flatDirection = Quaternion.AngleAxis(deviationAngle, Vector3.up) * flatDirection;
            }
            // ─────────────────────────────────────────────────────────────────

            // Add lift (Parabolic arc)
            Vector3 loftAxis = Vector3.Cross(flatDirection, Vector3.up);
            Vector3 launchDir = Quaternion.AngleAxis(DefaultLoftAngle, loftAxis) * flatDirection;

            // Calculate final velocity
            float clubPower = CurrentClub != null ? CurrentClub.Power : 10f;
            return launchDir * (powerRatio * clubPower * PowerScale);
        }

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