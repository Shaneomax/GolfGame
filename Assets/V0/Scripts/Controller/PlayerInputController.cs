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

        [Tooltip("Maximum sideways deviation (in degrees) applied to a shot when the accuracy arrow is fully off-centre. 0 = no deviation (perfect shot every time). 20-30 = realistic Golf Rival-style miss.")]
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
        [Tooltip("Reference to the ShotAccuracyController that owns the arrow indicator. Assign in the Inspector.")]
        public ShotAccuracyController AccuracyController;

        [Header("Target Marker Settings")]
        [Tooltip("Prefab for the 3D target marker spawned on the ground.")]
        public GameObject TargetMarkerPrefab;

        [Tooltip("Maximum angle (degrees) the player can swing the target marker left or right from the direction it spawned in. 0 = locked, 180 = fully free.")]
        public float MaxAimAngle = 45f;

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

        /// <summary>The aim direction frozen at marker-spawn time. Used as the centre of the angle cone.</summary>
        private Vector3 initialAimDirection = Vector3.forward;
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
                    LineRenderer lr = trajectoryPredictor.GetComponent<LineRenderer>();
                    if (lr != null) 
                    {
                        lr.enabled = false;
                    }
                }

                if (activeTargetMarker != null)
                {
                    activeTargetMarker.SetActive(false);
                }

                if (AccuracyController != null)
                {
                    AccuracyController.SetClub(CurrentClub);
                }
            }
            else if (newState == GameStateManager.GameState.Flight)
            {
                flightStartTime = Time.time;

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
            return Mathf.Max(range, 1f);
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
                initialAimDirection = fixedAimDirection;
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
                        float testDist = Mathf.Min(horizontalDiff.magnitude, maxRange);
                        Vector3 testHitPoint = transform.position + testDir * testDist;
                        testHitPoint.y = 0f;

                        // Only allow movement if the marker will stay inside the camera's view
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
                    // The line renderer calculates EXACTLY to the marker's position. No halving.
                    Vector3 launchVelocity = CalculateVelocityToHitTarget(activeTargetMarker.transform.position);
                    trajectoryPredictor.ShowTrajectory(transform.position, launchVelocity, activeTargetMarker.transform.position.y);
                }
            }
            else if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Aiming)
            {
                if (!isDragging && trajectoryPredictor != null)
                {
                    trajectoryPredictor.HideTrajectory();
                }
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
                bounceCombine      = PhysicsMaterialCombine.Maximum,
                frictionCombine    = PhysicsMaterialCombine.Average
            };

            ballCollider.material = bounceMat;
            Debug.Log($"[PlayerInputController] Applied bounciness: {CurrentBall.Bounciness}");
        }

        #endregion

        #region Physics Management

        private void UpdatePhysicsDrag()
        {
            if (isInMud)
            {
                rb.linearDamping  = MudLinearDrag;
                rb.angularDamping = MudAngularDrag;
            }
            else if (isGrounded)
            {
                rb.linearDamping  = CurrentBall != null ? CurrentBall.LinearDrag : GroundLinearDamping;
                rb.angularDamping = CurrentBall != null ? CurrentBall.AngularDrag : GroundAngularDamping;
            }
            else
            {
                rb.linearDamping  = CurrentBall != null ? (CurrentBall.WindResistance * 0.02f) : 0.02f;
                rb.angularDamping = 0.01f;
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
            if (GameStateManager.Instance.CurrentState != GameStateManager.GameState.Aiming)
                return;

            isDragging = true;
            dragStartPosition = Input.mousePosition; 
        }

        private void OnMouseDrag()
        {
            if (!isDragging) return;
            Vector3 currentMousePos = Input.mousePosition;
            Vector3 dragVector = dragStartPosition - currentMousePos;
        }

        private void OnMouseUp()
        {
            if (!isDragging) return;
            isDragging = false;

            if (trajectoryPredictor != null)
                trajectoryPredictor.HideTrajectory();

            if (AccuracyController != null)
                AccuracyController.LockAccuracy();

            if (activeTargetMarker == null)
            {
                Debug.LogWarning("[PlayerInput] No target marker — cannot fire.");
                return;
            }

            Vector3 dragVector = dragStartPosition - Input.mousePosition;
            Vector3 launchVelocity = CalculateDeviatedShotVelocity(dragVector);

            if (launchVelocity.sqrMagnitude > 0.1f)
            {
                rb.WakeUp();
                rb.AddForce(launchVelocity, ForceMode.VelocityChange);
                GameStateManager.Instance.ChangeState(GameStateManager.GameState.Flight);
            }
            else
            {
                if (AccuracyController != null)
                    AccuracyController.ResetLock();
                Debug.Log("[PlayerInput] Shot cancelled — drag too short. Accuracy lock reset.");
            }
        }

        private Vector3 CalculateDeviatedShotVelocity(Vector3 dragVector)
        {
            float dragMagnitude = Mathf.Clamp(dragVector.magnitude, 0f, MaxDragDistance);
            float powerRatio    = dragMagnitude / MaxDragDistance;

            Vector3 toTarget      = activeTargetMarker.transform.position - transform.position;
            Vector3 flatDirection = new Vector3(toTarget.x, 0f, toTarget.z).normalized;

            float distanceMultiplier = 1f;

            if (AccuracyController != null && AccuracyController.IsLocked)
            {
                float deviationAngle = AccuracyController.LockedAccuracyValue * AccuracyController.DeviationMultiplier;
                Debug.Log($"[PlayerInput] Accuracy locked: value={AccuracyController.LockedAccuracyValue:F3} | deviation={deviationAngle:F1}°");
                flatDirection = Quaternion.AngleAxis(deviationAngle, Vector3.up) * flatDirection;

                float accuracyAbs = Mathf.Abs(AccuracyController.LockedAccuracyValue);
                if (accuracyAbs < 0.05f)
                {
                    distanceMultiplier = 1.05f; // Perfect shot bonus
                }
                else
                {
                    distanceMultiplier = 1f - (accuracyAbs * 0.2f); // Miss penalty
                }
            }
            else
            {
                Debug.LogWarning("[PlayerInput] AccuracyController not locked at fire time! Using fallback.");
                float fallback = Random.Range(-MaxDeviationAngle, MaxDeviationAngle);
                flatDirection  = Quaternion.AngleAxis(fallback, Vector3.up) * flatDirection;
            }

            Vector3 loftAxis  = Vector3.Cross(flatDirection, Vector3.up);
            Vector3 launchDir = Quaternion.AngleAxis(DefaultLoftAngle, loftAxis) * flatDirection;

            Vector3 preciseVelocity = CalculateVelocityToHitTarget(activeTargetMarker.transform.position);
            float speed = preciseVelocity.magnitude * powerRatio * distanceMultiplier;

            return launchDir * speed;
        }

        private Vector3 CalculateDragVelocity(Vector3 dragVector)
        {
            float dragMagnitude = Mathf.Clamp(dragVector.magnitude, 0f, MaxDragDistance);
            float powerRatio    = dragMagnitude / MaxDragDistance;
            float clubPower     = CurrentClub != null ? CurrentClub.Power : 10f;
            Vector3 loftAxis    = Vector3.Cross(fixedAimDirection, Vector3.up);
            Vector3 launchDir   = Quaternion.AngleAxis(DefaultLoftAngle, loftAxis) * fixedAimDirection;
            return launchDir * (powerRatio * clubPower * PowerScale);
        }

        #endregion
    }
}