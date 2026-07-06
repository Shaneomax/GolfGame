using UnityEngine;

namespace GolfGame.Controllers
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerInputController : MonoBehaviour
    {
        #region Settings

        [Header("Input Settings (Scaled 0-10)")]
        [Tooltip("Maximum screen drag amount. Dragging beyond this does nothing. (8 = 80% of screen height)")]
        public float MaxDragDistance = 8f;

        [Tooltip("The drag amount needed to hit the target marker perfectly. (5 = 50% of screen height)")]
        public float NormalDragDistance = 5f;

        [Tooltip("Minimum drag required to fire a shot. Drags below this are cancelled. (3 = 30% of screen height)")]
        public float MinDragToShoot = 3f;

        [Tooltip("Fixed loft angle (in degrees) to ensure a realistic golf shot arc (Parabola).")]
        public float DefaultLoftAngle = 30f;

        [Tooltip("Scale factor to reduce the overall launch force. Lower = softer shot.")]
        public float PowerScale = 0.3f;

        [Tooltip("Maximum sideways deviation (in degrees) applied to a shot when missed.")]
        public float MaxDeviationAngle = 22f;

        [Header("Visuals")]
        [Tooltip("Line Renderer used to show the elastic pull-back connection.")]
        public LineRenderer DragLineRenderer;

        [Tooltip("Multiplier for how far the visual line stretches in world space per drag unit.")]
        public float DragVisualMultiplier = 0.5f;

        [Header("Physics Modifiers")]
        public float GroundLinearDamping = 0.15f;
        public float GroundAngularDamping = 0.1f;
        public float MudAngularDrag = 8.0f;
        public float MudLinearDrag = 4.0f;

        #endregion

        #region Data References

        [Header("Data References")]
        public BallData CurrentBall;
        public ClubData CurrentClub;
        public ShotAccuracyController AccuracyController;
        public GameObject TargetMarkerPrefab;
        public float MaxAimAngle = 45f;
        
        [Tooltip("The minimum allowed distance from the ball to the target marker.")]
        public float MinTargetDistance = 3f; // <-- NEW MIN DISTANCE VARIABLE

        [Header("Level References")]
        [Tooltip("Drag the hole/flag Transform here in the Inspector.")]
        public Transform FlagTransform;

        #endregion

        #region Private Fields

        private Rigidbody rb;
        private Camera mainCamera;
        private Collider ballCollider;
        private TrajectoryPredictor trajectoryPredictor;
        
        private Vector3 dragStartPosition;
        private bool isDragging = false;
        private float flightStartTime = 0f;

        private int collisionCount = 0;
        private bool isGrounded = false;
        private bool isInMud = false;

        private Vector3 fixedAimDirection = Vector3.forward;
        public Vector3 FixedAimDirection => fixedAimDirection;

        private Vector3 initialAimDirection = Vector3.forward;
        private GameObject activeTargetMarker;
        private bool isDraggingTarget = false;
        public bool IsDraggingTarget => isDraggingTarget;

        public GameObject ActiveTargetMarker => activeTargetMarker;

        #endregion

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ballCollider = GetComponent<Collider>();
            mainCamera = Camera.main;
            trajectoryPredictor = GetComponent<TrajectoryPredictor>();
            AccuracyController = FindFirstObjectByType<ShotAccuracyController>();
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
            if (activeTargetMarker != null) Destroy(activeTargetMarker);
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
                if (trajectoryPredictor != null) trajectoryPredictor.HideTrajectory();
                if (activeTargetMarker != null) activeTargetMarker.SetActive(false);
                if (AccuracyController != null) AccuracyController.SetClub(CurrentClub);
            }
            else if (newState == GameStateManager.GameState.Flight)
            {
                flightStartTime = Time.time;
                if (activeTargetMarker != null) activeTargetMarker.SetActive(false);
            }
        }

        private void OnStateExit(GameStateManager.GameState oldState)
        {
            if (oldState == GameStateManager.GameState.Setup)
                isDraggingTarget = false;
        }

        // Helper method to convert pixel drag into our 0-10 scale based on screen height
        private float GetScaledDragMagnitude(Vector3 dragVector)
        {
            return (dragVector.magnitude / Screen.height) * 10f;
        }

        private float CalculateMaxRange()
        {
            float clubPower = CurrentClub != null ? CurrentClub.Power : 15f;
            float v = clubPower * PowerScale; 
            float g = Mathf.Abs(Physics.gravity.y);
            float theta = DefaultLoftAngle * Mathf.Deg2Rad;
            float range = (v * v * Mathf.Sin(2f * theta)) / g;
            return Mathf.Max(range, 1f);
        }

        private void SpawnTargetMarker()
        {
            if (TargetMarkerPrefab != null && activeTargetMarker == null)
            {
                float maxRange = CalculateMaxRange();
                float targetDistance = maxRange * 0.7f; // Default to 70% of max range
                
                // Safely use the Inspector-assigned Flag
                if (FlagTransform != null)
                {
                    Vector3 toFlag = FlagTransform.position - transform.position;
                    toFlag.y = 0f; // Keep it on a flat plane
                    
                    // Aim directly at the flag
                    fixedAimDirection = toFlag.normalized;
                    
                    // If the flag is closer than our 70% distance, snap the marker to the flag!
                    if (toFlag.magnitude < targetDistance)
                    {
                        targetDistance = toFlag.magnitude;
                    }
                }

                // Clamp the spawn distance between min and max
                targetDistance = Mathf.Clamp(targetDistance, MinTargetDistance, maxRange);

                Vector3 spawnPos = transform.position + fixedAimDirection * targetDistance;
                spawnPos.y = 0f;
                activeTargetMarker = Instantiate(TargetMarkerPrefab, spawnPos, Quaternion.identity);
            }
            
            if (activeTargetMarker != null)
            {
                activeTargetMarker.SetActive(true);
                Vector3 diff = activeTargetMarker.transform.position - transform.position;
                fixedAimDirection = new Vector3(diff.x, 0f, diff.z).normalized;
                initialAimDirection = fixedAimDirection;
            }
        }

        private void RepositionTargetMarker()
        {
            if (activeTargetMarker != null)
            {
                float maxRange = CalculateMaxRange();
                float targetDistance = maxRange * 0.7f; // Default to 70% of max range
                
                // Safely use the Inspector-assigned Flag
                if (FlagTransform != null)
                {
                    Vector3 toFlag = FlagTransform.position - transform.position;
                    toFlag.y = 0f;
                    
                    fixedAimDirection = toFlag.normalized;
                    
                    if (toFlag.magnitude < targetDistance)
                    {
                        targetDistance = toFlag.magnitude;
                    }
                }

                // Clamp the reposition distance between min and max
                targetDistance = Mathf.Clamp(targetDistance, MinTargetDistance, maxRange);

                Vector3 newPos = transform.position + fixedAimDirection * targetDistance;
                newPos.y = 0f;
                activeTargetMarker.transform.position = newPos;
                activeTargetMarker.SetActive(true);
                
                // Update the initial aim so dragging limits still work correctly
                initialAimDirection = fixedAimDirection; 
            }
        }

        private void HandleSetupInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

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
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    Vector3 diff = hitPoint - transform.position;
                    Vector3 horizontalDiff = new Vector3(diff.x, 0f, diff.z);
                    float maxRange = CalculateMaxRange();

                    Vector3 desiredDir = horizontalDiff.normalized;
                    if (desiredDir.sqrMagnitude > 0.001f)
                    {
                        float signedAngle = Vector3.SignedAngle(initialAimDirection, desiredDir, Vector3.up);
                        float clampedAngle = Mathf.Clamp(signedAngle, -MaxAimAngle, MaxAimAngle);

                        Vector3 testDir = Quaternion.AngleAxis(clampedAngle, Vector3.up) * initialAimDirection;
                        
                        // CLAMP DISTANCE BETWEEN MIN AND MAX HERE
                        float testDist = Mathf.Clamp(horizontalDiff.magnitude, MinTargetDistance, maxRange);
                        
                        Vector3 testHitPoint = transform.position + testDir * testDist;
                        testHitPoint.y = 0f;

                        Vector3 viewportPos = mainCamera.WorldToViewportPoint(testHitPoint);
                        if (viewportPos.x >= 0f && viewportPos.x <= 1f && viewportPos.y >= 0f && viewportPos.y <= 1f && viewportPos.z > 0f)
                        {
                            fixedAimDirection = testDir;
                            hitPoint = testHitPoint;
                            activeTargetMarker.transform.position = hitPoint;
                        }
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
            if (denominator <= 0.001f || d <= 0.001f) return horizontalDiff.normalized * 5f + Vector3.up * 2f;

            float speed = (d / Mathf.Cos(theta)) * Mathf.Sqrt(g / denominator);
            float maxClubPower = CurrentClub != null ? CurrentClub.Power : 15f;
            float maxSpeed = maxClubPower * PowerScale;
            speed = Mathf.Min(speed, maxSpeed);

            Vector3 flatDirection = horizontalDiff.normalized;
            Vector3 loftAxis = Vector3.Cross(flatDirection, Vector3.up);
            return Quaternion.AngleAxis(DefaultLoftAngle, loftAxis) * flatDirection * speed;
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
            else if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Aiming)
            {
                if (!isDragging && trajectoryPredictor != null) trajectoryPredictor.HideTrajectory();
            }
        }

        private void FixedUpdate()
        {
            if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Flight)
            {
                if (Time.time > flightStartTime + 0.1f && isGrounded)
                {
                    if (CurrentBall != null && rb.linearVelocity.sqrMagnitude < (CurrentBall.StopThreshold * CurrentBall.StopThreshold))
                    {
                        StopBall();
                    }
                }
            }
        }

        public void ApplyBallData()
        {
            if (CurrentBall != null && rb != null)
            {
                rb.mass = CurrentBall.Mass;
                ApplyBounciness();
                UpdatePhysicsDrag();
            }
        }

        private void ApplyBounciness()
        {
            if (ballCollider == null || CurrentBall == null) return;

            PhysicsMaterial bounceMat = new PhysicsMaterial("BallPhysics")
            {
                bounciness = CurrentBall.Bounciness,
                dynamicFriction = 0.6f,
                staticFriction = 0.6f,
                bounceCombine = PhysicsMaterialCombine.Average,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            ballCollider.material = bounceMat;
        }

        private void UpdatePhysicsDrag()
        {
            if (isInMud)
            {
                rb.linearDamping = MudLinearDrag;
                rb.angularDamping = MudAngularDrag;
            }
            else if (isGrounded)
            {
                rb.linearDamping = CurrentBall != null ? CurrentBall.LinearDrag : GroundLinearDamping;
                rb.angularDamping = CurrentBall != null ? CurrentBall.AngularDrag : GroundAngularDamping;
            }
            else
            {
                rb.linearDamping = CurrentBall != null ? (CurrentBall.WindResistance * 0.02f) : 0.02f;
                rb.angularDamping = 0.01f;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Mud")) isInMud = true;
            collisionCount++;
            isGrounded = collisionCount > 0;
            UpdatePhysicsDrag();
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision.gameObject.CompareTag("Mud")) isInMud = false;
            collisionCount = Mathf.Max(0, collisionCount - 1);
            isGrounded = collisionCount > 0;
            UpdatePhysicsDrag();
        }

        private void StopBall()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep(); 
            
            GameStateManager.Instance.ChangeState(GameStateManager.GameState.Setup);
        }
        private void OnTriggerEnter(Collider other)
        {
            // Check if the ball hit the flag/hole
            if (other.CompareTag("Flag"))
            {
                Debug.Log("[PlayerInput] Reached the flag! Ending the loop.");
                
                // Instantly stop the ball from rolling further
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
                
                // Break the loop and transition to the Resolution state
                GameStateManager.Instance.ChangeState(GameStateManager.GameState.Resolution);
            }
        }

        private void OnMouseDown()
        {
            if (GameStateManager.Instance.CurrentState != GameStateManager.GameState.Aiming) return;
            isDragging = true;
            dragStartPosition = Input.mousePosition; 
            
            // NEW: Enable and initialize the pull-back line
            if (DragLineRenderer != null)
            {
                DragLineRenderer.enabled = true;
                DragLineRenderer.SetPosition(0, transform.position);
                DragLineRenderer.SetPosition(1, transform.position);
            }
        }

        private void OnMouseDrag()
        {
            if (!isDragging) return;
            Vector3 dragVector = dragStartPosition - Input.mousePosition;
            
            float dragMagnitude = Mathf.Clamp(GetScaledDragMagnitude(dragVector), 0f, MaxDragDistance);
            float overpowerRatio = Mathf.InverseLerp(NormalDragDistance, MaxDragDistance, dragMagnitude);

            if (AccuracyController != null)
            {
                AccuracyController.SetDragPowerMultiplier(overpowerRatio);
            }

            // NEW: Draw the elastic pull-back line
            if (DragLineRenderer != null)
            {
                // Draw the line in the exact opposite direction of where we are aiming
                Vector3 visualPullBackDir = -FixedAimDirection;
                
                // Calculate the end point in the 3D world based on drag magnitude
                Vector3 endPoint = transform.position + (visualPullBackDir * (dragMagnitude * DragVisualMultiplier));
                
                // Keep the line flat on the ground level of the ball
                endPoint.y = transform.position.y;
                
                DragLineRenderer.SetPosition(0, transform.position);
                DragLineRenderer.SetPosition(1, endPoint);
            }
        }

        private void OnMouseUp()
        {
            if (!isDragging) return;
            isDragging = false;

            // NEW: Hide the pull-back line when the ball is released
            if (DragLineRenderer != null)
            {
                DragLineRenderer.enabled = false;
            }

            Vector3 dragVector = dragStartPosition - Input.mousePosition;
            float dragMagnitude = GetScaledDragMagnitude(dragVector);

            // Cancel the shot if the drag is below the minimum threshold (e.g. less than 3)
            if (dragMagnitude < MinDragToShoot)
            {
                if (AccuracyController != null)
                {
                    AccuracyController.SetDragPowerMultiplier(0f);
                    AccuracyController.ResetLock();
                }
                Debug.Log($"[PlayerInput] Shot cancelled — drag {dragMagnitude:F1} below minimum {MinDragToShoot}.");
                return;
            }

            if (trajectoryPredictor != null) trajectoryPredictor.HideTrajectory();
            if (AccuracyController != null) AccuracyController.LockAccuracy();

            if (activeTargetMarker == null) return;

            Vector3 launchVelocity = CalculateDeviatedShotVelocity(dragVector);

            if (launchVelocity.sqrMagnitude > 0.1f)
            {
                rb.WakeUp();
                rb.AddForce(launchVelocity, ForceMode.VelocityChange);
                GameStateManager.Instance.ChangeState(GameStateManager.GameState.Flight);
            }
            else
            {
                if (AccuracyController != null) AccuracyController.ResetLock();
            }
        }

        private Vector3 CalculateDeviatedShotVelocity(Vector3 dragVector)
        {
            float dragMagnitude = Mathf.Clamp(GetScaledDragMagnitude(dragVector), 0f, MaxDragDistance);
            
            // Dragging to 5 (NormalDrag) gives a powerRatio of 1.0 (Hits marker perfectly).
            // Dragging to 8 gives 1.6x power. Dragging to 3 gives 0.6x power.
            float powerRatio = dragMagnitude / NormalDragDistance;

            Vector3 toTarget = activeTargetMarker.transform.position - transform.position;
            Vector3 flatDirection = new Vector3(toTarget.x, 0f, toTarget.z).normalized;

            float distanceMultiplier = 1f;

            if (AccuracyController != null && AccuracyController.IsLocked)
            {
                float deviationAngle = AccuracyController.LockedAccuracyValue * AccuracyController.DeviationMultiplier;
                flatDirection = Quaternion.AngleAxis(deviationAngle, Vector3.up) * flatDirection;

                float accuracyAbs = Mathf.Abs(AccuracyController.LockedAccuracyValue);
                if (accuracyAbs < 0.05f) distanceMultiplier = 1.05f; // Perfect shot bonus
                else distanceMultiplier = 1f - (accuracyAbs * 0.2f); // Miss penalty
            }

            Vector3 loftAxis  = Vector3.Cross(flatDirection, Vector3.up);
            Vector3 launchDir = Quaternion.AngleAxis(DefaultLoftAngle, loftAxis) * flatDirection;

            Vector3 preciseVelocity = CalculateVelocityToHitTarget(activeTargetMarker.transform.position);
            
            // Apply the drag power ratio directly to the precise speed needed
            float speed = preciseVelocity.magnitude * powerRatio * distanceMultiplier;

            return launchDir * speed;
        }
    }
}