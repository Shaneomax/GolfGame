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

        #endregion

        #region Dependencies

        [Header("Dependencies")]
        public BallData CurrentBall;
        public ClubData CurrentClub;
        public ShotAccuracyController AccuracyController;
        public AimVisualsController AimVisuals;
        public BallPhysicsController PhysicsController;

        #endregion

        private Rigidbody rb;
        private Camera mainCamera;
        
        private Vector3 dragStartPosition;
        private bool isDragging = false;
        private bool isDraggingTarget = false;

        // Facade properties for external scripts (like CinemachineAimController)
        public bool IsDraggingTarget => isDraggingTarget;
        public GameObject ActiveTargetMarker => AimVisuals != null ? AimVisuals.ActiveTargetMarker : null;
        public Vector3 FixedAimDirection => AimVisuals != null ? AimVisuals.FixedAimDirection : Vector3.forward;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            mainCamera = Camera.main;
            
            if (AccuracyController == null)
                AccuracyController = FindFirstObjectByType<ShotAccuracyController>();
            
            if (AimVisuals == null)
                AimVisuals = GetComponent<AimVisualsController>();
                
            if (PhysicsController == null)
                PhysicsController = GetComponent<BallPhysicsController>();
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
        }

        public void ApplyBallData()
        {
            if (PhysicsController != null)
            {
                PhysicsController.Initialize(CurrentBall);
            }
        }

        private void OnStateEnter(GameStateManager.GameState newState)
        {
            if (newState == GameStateManager.GameState.Setup)
            {
                if (AimVisuals != null)
                {
                    AimVisuals.SpawnTargetMarker(CalculateMaxRange());
                    AimVisuals.RepositionTargetMarker(CalculateMaxRange());
                }
            }
            else if (newState == GameStateManager.GameState.Aiming)
            {
                if (AccuracyController != null) AccuracyController.SetClub(CurrentClub);
            }
            else if (newState == GameStateManager.GameState.Flight)
            {
                if (PhysicsController != null) PhysicsController.NotifyFlightStarted();
            }
        }

        private void OnStateExit(GameStateManager.GameState oldState)
        {
            if (oldState == GameStateManager.GameState.Setup)
                isDraggingTarget = false;
        }

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

        private void Update()
        {
            if (GameStateManager.Instance == null) return;

            if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Setup)
            {
                HandleSetupInput();
                
                if (AimVisuals != null && AimVisuals.ActiveTargetMarker != null)
                {
                    Vector3 launchVelocity = CalculateVelocityToHitTarget(AimVisuals.ActiveTargetMarker.transform.position);
                    AimVisuals.ShowTrajectory(launchVelocity);
                }
            }
        }

        private void HandleSetupInput()
        {
            if (AimVisuals == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    if (AimVisuals.ActiveTargetMarker != null && 
                       (hit.collider.gameObject == AimVisuals.ActiveTargetMarker || 
                        hit.collider.transform.IsChildOf(AimVisuals.ActiveTargetMarker.transform)))
                    {
                        isDraggingTarget = true;
                    }
                }
            }
            else if (Input.GetMouseButton(0) && isDraggingTarget && AimVisuals.ActiveTargetMarker != null)
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
                        float signedAngle = Vector3.SignedAngle(AimVisuals.InitialAimDirection, desiredDir, Vector3.up);
                        float clampedAngle = Mathf.Clamp(signedAngle, -AimVisuals.MaxAimAngle, AimVisuals.MaxAimAngle);

                        Vector3 testDir = Quaternion.AngleAxis(clampedAngle, Vector3.up) * AimVisuals.InitialAimDirection;
                        float testDist = Mathf.Clamp(horizontalDiff.magnitude, AimVisuals.MinTargetDistance, maxRange);
                        
                        Vector3 testHitPoint = transform.position + testDir * testDist;
                        testHitPoint.y = 0f;

                        Vector3 viewportPos = mainCamera.WorldToViewportPoint(testHitPoint);
                        if (viewportPos.x >= 0f && viewportPos.x <= 1f && viewportPos.y >= 0f && viewportPos.y <= 1f && viewportPos.z > 0f)
                        {
                            AimVisuals.UpdateAimDirection(testDir);
                            hitPoint = testHitPoint;
                            AimVisuals.ActiveTargetMarker.transform.position = hitPoint;
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

        private void OnMouseDown()
        {
            if (GameStateManager.Instance.CurrentState != GameStateManager.GameState.Aiming) return;
            isDragging = true;
            dragStartPosition = Input.mousePosition; 
            
            if (AimVisuals != null)
            {
                AimVisuals.UpdateDragLine(0, 0, transform.position);
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

            if (AimVisuals != null)
            {
                AimVisuals.UpdateDragLine(dragMagnitude, overpowerRatio, transform.position);
            }
        }

        private void OnMouseUp()
        {
            if (!isDragging) return;
            isDragging = false;

            if (AimVisuals != null)
            {
                AimVisuals.HideDragLine();
                AimVisuals.HideTrajectory();
            }

            Vector3 dragVector = dragStartPosition - Input.mousePosition;
            float dragMagnitude = GetScaledDragMagnitude(dragVector);

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

            if (AccuracyController != null) AccuracyController.LockAccuracy();

            if (AimVisuals == null || AimVisuals.ActiveTargetMarker == null) return;

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
            float powerRatio = dragMagnitude / NormalDragDistance;

            Vector3 toTarget = AimVisuals.ActiveTargetMarker.transform.position - transform.position;
            Vector3 flatDirection = new Vector3(toTarget.x, 0f, toTarget.z).normalized;

            float distanceMultiplier = 1f;

            if (AccuracyController != null && AccuracyController.IsLocked)
            {
                float deviationAngle = AccuracyController.LockedAccuracyValue * AccuracyController.DeviationMultiplier;
                flatDirection = Quaternion.AngleAxis(deviationAngle, Vector3.up) * flatDirection;

                float accuracyAbs = Mathf.Abs(AccuracyController.LockedAccuracyValue);
                if (accuracyAbs < 0.05f) distanceMultiplier = 1.05f; 
                else distanceMultiplier = 1f - (accuracyAbs * 0.2f); 
            }

            Vector3 loftAxis  = Vector3.Cross(flatDirection, Vector3.up);
            Vector3 launchDir = Quaternion.AngleAxis(DefaultLoftAngle, loftAxis) * flatDirection;

            Vector3 preciseVelocity = CalculateVelocityToHitTarget(AimVisuals.ActiveTargetMarker.transform.position);
            
            float speed = preciseVelocity.magnitude * powerRatio * distanceMultiplier;

            return launchDir * speed;
        }
    }
}