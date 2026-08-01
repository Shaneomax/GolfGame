using UnityEngine;
using GolfGame.Data;

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

        [Header("Spin Adjustments")]
        [Tooltip("How many degrees the loft is adjusted at max top/back spin.")]
        public float SpinLoftEffect = 10f;
        
        [Tooltip("Lateral acceleration applied in-flight for max curl/sidespin.")]
        public float CurlAcceleration = 5f;

        #endregion

        #region Dependencies

        [Header("Dependencies")]
        public BallData CurrentBall;
        public ClubData CurrentClub;
        public CinemachineAimController AimController;
        public BallPhysicsController PhysicsController;
        public AimVisualsController AimVisuals;
        public ShotAccuracyController AccuracyController;
        public GolfGame.UI.SpinInputUI SpinInput;

        #endregion

        private Rigidbody rb;
        private Camera mainCamera;
        
        private Vector3 dragStartPosition;
        private bool isDragging = false;
        private bool isDraggingTarget = false;

        public bool IsDraggingTarget => isDraggingTarget;
        public GameObject ActiveTargetMarker => AimVisuals != null ? AimVisuals.ActiveTargetMarker : null;
        public Vector3 FixedAimDirection => AimVisuals != null ? AimVisuals.FixedAimDirection : Vector3.forward;
        public Vector3 LastLaunchPosition { get; private set; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            mainCamera = Camera.main;
            
            if (AccuracyController == null || !AccuracyController.gameObject.scene.IsValid())
                AccuracyController = FindFirstObjectByType<ShotAccuracyController>();
            
            if (AimVisuals == null)
                AimVisuals = GetComponent<AimVisualsController>();
                
            if (PhysicsController == null)
                PhysicsController = GetComponent<BallPhysicsController>();

            // Subscribe to hide/disable input when spin dashboard is open
            GolfGame.UI.GameplayUIController.OnSpinDashboardToggled += HandleSpinDashboardToggled;
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
            GolfGame.UI.GameplayUIController.OnSpinDashboardToggled -= HandleSpinDashboardToggled;
        }

        private void HandleSpinDashboardToggled(bool isOpen)
        {
            // Do not disable the component; allow live trajectory updating.
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
                    StartCoroutine(DelayedMarkerSetup());
                }
            }
            else if (newState == GameStateManager.GameState.Aiming)
            {
                if (AccuracyController != null) AccuracyController.SetClub(CurrentClub);

                // When ball is on NiceOn (putting green), Setup state is skipped entirely.
                // Putting uses the flag direction directly — the target marker is not needed,
                // so hide it to keep the screen clean.
                if (IsPuttingMode() && AimVisuals != null)
                {
                    AimVisuals.HideTrajectory(); // Hide flight trajectory line!
                    if (AimVisuals.ActiveTargetMarker != null)
                        AimVisuals.ActiveTargetMarker.SetActive(false);
                }
            }
            else if (newState == GameStateManager.GameState.Flight)
            {
                if (PhysicsController != null) PhysicsController.NotifyFlightStarted();
                // Always hide the trajectory the moment the ball is launched
                if (AimVisuals != null) AimVisuals.HideTrajectory();
            }
        }
        
        private System.Collections.IEnumerator DelayedMarkerSetup()
        {
            yield return new WaitForEndOfFrame();
            if (AimVisuals != null)
            {
                AimVisuals.SpawnTargetMarker(CalculateMaxRange());
                AimVisuals.RepositionTargetMarker(CalculateMaxRange());
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
            float debuff = PhysicsController != null && PhysicsController.CurrentGround != null ? PhysicsController.CurrentGround.PowerDebuff : 0f;
            float v = clubPower * PowerScale * (1f - debuff); 
            float g = Mathf.Abs(Physics.gravity.y);
            float theta = DefaultLoftAngle * Mathf.Deg2Rad;
            float range = (v * v * Mathf.Sin(2f * theta)) / g;
            return Mathf.Max(range, 1f);
        }

        private void Update()
        {
            if (GameStateManager.Instance == null) return;
            if (global::GameManager.IsPaused)
            {
                isDragging = false;
                isDraggingTarget = false;
                if (AimVisuals != null)
                {
                    AimVisuals.HideDragLine();
                    AimVisuals.HideTrajectory();
                }
                if (AccuracyController != null)
                {
                    AccuracyController.SetDragPowerMultiplier(0f);
                    AccuracyController.ResetLock();
                }
                return;
            }

            // Block all main game input while the Spin Dashboard is open.

            var state = GameStateManager.Instance.CurrentState;

            if (state == GameStateManager.GameState.Setup)
            {
                HandleSetupInput();
            }
            else if (state == GameStateManager.GameState.Aiming)
            {
                HandleAimingInput();
            }
        }

        // LateUpdate runs AFTER all Update() calls - this guarantees SpinInputUI has
        // already written the latest GlobalCurrentSpin before we redraw the trajectory.
        private void LateUpdate()
        {
            if (GameStateManager.Instance == null) return;
            if (global::GameManager.IsPaused) return;

            var state = GameStateManager.Instance.CurrentState;
            if (state == GameStateManager.GameState.Setup)
            {
                if (IsPuttingMode())
                {
                    if (AimVisuals != null) AimVisuals.HideTrajectory();
                    return; // Don't draw flight trajectory on the green!
                }
                
                if (AimVisuals != null && AimVisuals.ActiveTargetMarker != null)
                {
                    Vector3 launchVelocity = CalculateVelocityToHitTarget(AimVisuals.ActiveTargetMarker.transform.position);
                    AimVisuals.ShowTrajectory(launchVelocity);
                }
            }
            else if (state == GameStateManager.GameState.Aiming)
            {
                // CRITICAL FIX: When putting, forcibly kill the flight trajectory every single frame.
                // The trajectory can be drawn during the brief Setup state before currentGround is set
                // to NiceOn, and it persists. This ensures it is always hidden while putting.
                if (IsPuttingMode() && AimVisuals != null)
                {
                    AimVisuals.HideTrajectory();
                }
            }
        }

        private void HandleSetupInput()
        {
            if (GolfGame.UI.GameplayUIController.IsSpinDashboardOpen) return;

            // 1. Check for initial click to start dragging
            if (Input.GetMouseButtonDown(0))
            {
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    // Verify if the player clicked on or near the target marker
                    if (AimVisuals.ActiveTargetMarker != null && 
                       (hit.collider.gameObject == AimVisuals.ActiveTargetMarker || 
                        hit.collider.transform.IsChildOf(AimVisuals.ActiveTargetMarker.transform)))
                    {
                        isDraggingTarget = true;
                    }
                }
            }
            // 2. Stop dragging when mouse is released
            else if (Input.GetMouseButtonUp(0))
            {
                isDraggingTarget = false;
            }
            // 3. Handle the dragging and repositioning
            else if (Input.GetMouseButton(0) && isDraggingTarget && AimVisuals.ActiveTargetMarker != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                
                // Get the current height of the marker to define the drag plane
                float dragPlaneHeight = transform.position.y;
                if (AimVisuals.ActiveTargetMarker != null)
                {
                    dragPlaneHeight = AimVisuals.ActiveTargetMarker.transform.position.y - AimVisuals.MarkerYOffset;
                }
                
                Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, dragPlaneHeight, 0f)); 
                
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
                        // Snap Y to terrain surface + offset (avoid Physics.Raycast here
                        // because it can hit the marker's own collider and cause it to jump)
                        if (Terrain.activeTerrain != null)
                        {
                            testHitPoint.y = Terrain.activeTerrain.SampleHeight(testHitPoint) 
                                           + Terrain.activeTerrain.transform.position.y 
                                           + AimVisuals.MarkerYOffset;
                        }
                        else
                        {
                            testHitPoint.y = transform.position.y + AimVisuals.MarkerYOffset;
                        }

                        Vector3 viewportPos = mainCamera.WorldToViewportPoint(testHitPoint);
                        
                        if (viewportPos.x >= 0.02f && viewportPos.x <= 0.98f && 
                            viewportPos.y >= 0.02f && viewportPos.y <= 0.98f && 
                            viewportPos.z > 0f)
                        {
                            AimVisuals.UpdateAimDirection(testDir);
                            AimVisuals.ActiveTargetMarker.transform.position = testHitPoint;
                        }
                    }
                }
            }
        }

        private Vector3 CalculateVelocityToHitTarget(Vector3 targetPos)
        {
            Vector2 spin = GolfGame.UI.SpinInputUI.GlobalCurrentSpin;

            Vector3 diff = targetPos - transform.position;
            Vector3 horizontalDiff = new Vector3(diff.x, 0f, diff.z);
            float d = horizontalDiff.magnitude;
            float h = diff.y;
            float g = Mathf.Abs(Physics.gravity.y);
            
            // 1. Adjust loft based on vertical spin (Topspin = flatter, Backspin = higher)
            float adjustedLoft = DefaultLoftAngle - (spin.y * SpinLoftEffect);
            float theta = adjustedLoft * Mathf.Deg2Rad;

            float denominator = 2f * (d * Mathf.Tan(theta) - h);
            if (denominator <= 0.001f || d <= 0.001f) return horizontalDiff.normalized * 5f + Vector3.up * 2f;

            float speed = (d / Mathf.Cos(theta)) * Mathf.Sqrt(g / denominator);
            
            // CRITICAL FIX: DO NOT CLAMP SPEED HERE! 
            // The target marker's distance is already clamped during the setup phase. 
            // If we lower the loft with topspin, we MUST allow the speed to naturally increase to reach the exact same target.

            Vector3 flatDirection = horizontalDiff.normalized;
            Vector3 loftAxis = Vector3.Cross(flatDirection, Vector3.up);
            Vector3 baseVelocity = Quaternion.AngleAxis(adjustedLoft, loftAxis) * flatDirection * speed;

            // 2. Adjust initial velocity for sidespin (curl)
            if (Mathf.Abs(spin.x) > 0.01f)
            {
                // CRITICAL FIX: Calculate EXACT time of flight using quadratic formula for gravity
                float Vy = baseVelocity.y;
                float discriminant = Vy * Vy - 2f * g * h;
                float timeOfFlight = 0f;
                
                if (discriminant >= 0)
                    timeOfFlight = (Vy + Mathf.Sqrt(discriminant)) / g;
                else
                    timeOfFlight = d / (speed * Mathf.Cos(theta)); // Fallback

                Vector3 rightDir = Vector3.Cross(Vector3.up, flatDirection).normalized;
                
                float curlAccel = PhysicsController != null ? PhysicsController.CurlAcceleration : CurlAcceleration;
                // Offset initial launch to compensate for the continuous curl acceleration in the air
                Vector3 lateralOffset = rightDir * (-0.5f * curlAccel * spin.x * timeOfFlight);
                baseVelocity += lateralOffset;
            }

            return baseVelocity;
        }

        private bool IsPuttingMode()
        {
            return PhysicsController != null && PhysicsController.CurrentGround != null && PhysicsController.CurrentGround.IsNiceOn;
        }

        private Vector3 CalculatePuttingVelocity(Vector3 dragVector)
        {
            float dragMagnitude = Mathf.Clamp(GetScaledDragMagnitude(dragVector), 0f, MaxDragDistance);
            float distanceRatio = dragMagnitude / NormalDragDistance;
            float speedMultiplier = Mathf.Sqrt(distanceRatio);
            
            Vector3 baseForwardDir = Vector3.forward;
            if (AimVisuals != null && AimVisuals.FlagTransform != null)
            {
                Vector3 toFlag = AimVisuals.FlagTransform.position - transform.position;
                baseForwardDir = new Vector3(toFlag.x, 0f, toFlag.z).normalized;
            }

            // Calculate the angle based on horizontal vs vertical drag
            float dragAngle = Mathf.Atan2(dragVector.x, dragVector.y) * Mathf.Rad2Deg;
            
            // Apply the slingshot rotation to the final launch velocity
            Vector3 finalAimDir = Quaternion.Euler(0f, dragAngle, 0f) * baseForwardDir;

            float debuff = PhysicsController != null && PhysicsController.CurrentGround != null ? PhysicsController.CurrentGround.PowerDebuff : 0f;
            float maxClubPower = CurrentClub != null ? CurrentClub.Power : 15f;
            float maxSpeed = maxClubPower * PowerScale * 0.35f * (1f - debuff); 
            
            return finalAimDir * (maxSpeed * speedMultiplier);
        }

        /// <summary>
        /// Handles all drag input while in the Aiming state.
        /// Uses Update()-based polling (GetMouseButtonDown/Up) instead of OnMouse* callbacks
        /// so it works reliably on real Android devices without requiring a collider hit.
        /// </summary>
        private void HandleAimingInput()
        {
            // ── Finger/mouse pressed this frame ──────────────────────────────────
            if (Input.GetMouseButtonDown(0))
            {
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

                isDragging = true;
                dragStartPosition = Input.mousePosition;

                if (AimVisuals != null)
                {
                    if (IsPuttingMode())
                        AimVisuals.UpdatePuttingLine(0, 0, transform.position, 0f);
                    else
                        AimVisuals.UpdateDragLine(0, 0, transform.position);
                }
            }
            // ── Finger/mouse held ─────────────────────────────────────────────────
            else if (Input.GetMouseButton(0) && isDragging)
            {
                Vector3 dragVector = dragStartPosition - Input.mousePosition;

                float dragMagnitude = Mathf.Clamp(GetScaledDragMagnitude(dragVector), 0f, MaxDragDistance);
                float overpowerRatio = Mathf.InverseLerp(NormalDragDistance, MaxDragDistance, dragMagnitude);
                float dragAngle = Mathf.Atan2(dragVector.x, dragVector.y) * Mathf.Rad2Deg;

                if (AccuracyController != null)
                {
                    float? threshold = AimVisuals != null ? AimVisuals.ExtremeForceThreshold : (float?)null;
                    AccuracyController.SetDragPowerMultiplier(overpowerRatio, threshold);
                }

                if (AimVisuals != null)
                {
                    if (IsPuttingMode())
                        AimVisuals.UpdatePuttingLine(dragMagnitude, overpowerRatio, transform.position, dragAngle);
                    else
                        AimVisuals.UpdateDragLine(dragMagnitude, overpowerRatio, transform.position);
                }
            }
            // ── Finger/mouse released this frame ──────────────────────────────────
            else if (Input.GetMouseButtonUp(0) && isDragging)
            {
                isDragging = false;

                if (AimVisuals != null)
                {
                    AimVisuals.HideDragLine();
                    AimVisuals.HideTrajectory();
                }

                Vector3 dragVector = dragStartPosition - Input.mousePosition;
                ExecuteShot(dragVector);
            }
        }



        public void ForcePerfectShot()
        {
            if (GameStateManager.Instance.CurrentState != GameStateManager.GameState.Aiming) return;
            if (AimVisuals == null || AimVisuals.ActiveTargetMarker == null) return;

            if (AccuracyController != null) AccuracyController.ForcePerfectAccuracy();

            isDragging = false;
            AimVisuals.HideDragLine();
            AimVisuals.HideTrajectory();

            Vector3 launchVelocity;
            if (IsPuttingMode())
            {
                // For putting debug, just aim straight at the flag
                Vector3 toFlag = AimVisuals.FlagTransform.position - transform.position;
                Vector3 baseForwardDir = new Vector3(toFlag.x, 0f, toFlag.z).normalized;
                float debuff = PhysicsController != null && PhysicsController.CurrentGround != null ? PhysicsController.CurrentGround.PowerDebuff : 0f;
                float maxSpeed = (CurrentClub != null ? CurrentClub.Power : 15f) * PowerScale * 0.35f * (1f - debuff); 
                launchVelocity = baseForwardDir * maxSpeed;
            }
            else
            {
                // Normal shot: get the exact mathematical velocity needed to hit the marker right now
                launchVelocity = CalculateVelocityToHitTarget(AimVisuals.ActiveTargetMarker.transform.position);
            }

            LastLaunchPosition = transform.position;
            rb.WakeUp();
            rb.AddForce(launchVelocity, ForceMode.VelocityChange);
            
            if (PhysicsController != null)
            {
                PhysicsController.SetAppliedSpin(GolfGame.UI.SpinInputUI.GlobalCurrentSpin);
                
                Vector3 toTarget = AimVisuals.ActiveTargetMarker.transform.position - transform.position;
                Vector3 straightFlatDir = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
                PhysicsController.SetFlightRightDir(Vector3.Cross(Vector3.up, straightFlatDir).normalized);
            }

            GameStateManager.Instance.ChangeState(GameStateManager.GameState.Flight);
        }

        private void ExecuteShot(Vector3 dragVector)
        {
            float dragMagnitude = GetScaledDragMagnitude(dragVector);
            float effectiveMinDrag = IsPuttingMode() ? 0.1f : MinDragToShoot;

            if (dragMagnitude < effectiveMinDrag)
            {
                if (AccuracyController != null)
                {
                    AccuracyController.SetDragPowerMultiplier(0f);
                    AccuracyController.ResetLock();
                }
                Debug.Log($"[PlayerInput] Shot cancelled — drag {dragMagnitude:F1} below minimum {effectiveMinDrag}.");
                return;
            }

            if (AccuracyController != null) AccuracyController.LockAccuracy();

            Vector3 launchVelocity;
            if (IsPuttingMode())
            {
                launchVelocity = CalculatePuttingVelocity(dragVector);
            }
            else
            {
                if (AimVisuals == null || AimVisuals.ActiveTargetMarker == null) return;
                launchVelocity = CalculateDeviatedShotVelocity(dragVector);
            }

            // Allow tiny launch velocities when putting, otherwise require at least 0.1f sqrMagnitude
            float minLaunchVelocitySqr = IsPuttingMode() ? 0.001f : 0.1f;
            if (launchVelocity.sqrMagnitude > minLaunchVelocitySqr)
            {
                LastLaunchPosition = transform.position;
                rb.WakeUp();
                rb.AddForce(launchVelocity, ForceMode.VelocityChange);
                
                if (PhysicsController != null)
                {
                    PhysicsController.SetAppliedSpin(GolfGame.UI.SpinInputUI.GlobalCurrentSpin);
                    
                    // CRITICAL FIX: The curve direction MUST be perpendicular to the straight line to the target, 
                    // NOT the offset launch velocity!
                    Vector3 toTarget = AimVisuals.ActiveTargetMarker.transform.position - transform.position;
                    Vector3 straightFlatDir = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
                    PhysicsController.SetFlightRightDir(Vector3.Cross(Vector3.up, straightFlatDir).normalized);
                }

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
            
            float distanceRatio = 1f;
            if (dragMagnitude <= NormalDragDistance)
            {
                distanceRatio = dragMagnitude / NormalDragDistance;
            }
            else
            {
                float overpower = Mathf.InverseLerp(NormalDragDistance, MaxDragDistance, dragMagnitude);
                // Max 25% extra distance for full red drag instead of 2.5x distance
                distanceRatio = Mathf.Lerp(1.0f, 1.25f, overpower);
            }
            // Sqrt is used because distance scales with velocity squared
            float speedMultiplier = Mathf.Sqrt(distanceRatio);

            Vector3 toTarget = AimVisuals.ActiveTargetMarker.transform.position - transform.position;
            Vector3 flatDirection = new Vector3(toTarget.x, 0f, toTarget.z).normalized;

            float distanceMultiplier = 1f;

            Vector3 preciseVelocity = CalculateVelocityToHitTarget(AimVisuals.ActiveTargetMarker.transform.position);

            if (AccuracyController != null && AccuracyController.IsLocked)
            {
                float deviationAngle = AccuracyController.LockedAccuracyValue * AccuracyController.DeviationMultiplier;
                preciseVelocity = Quaternion.AngleAxis(deviationAngle, Vector3.up) * preciseVelocity;

                float accuracyAbs = Mathf.Abs(AccuracyController.LockedAccuracyValue);
                if (accuracyAbs < 0.05f) distanceMultiplier = 1.05f; 
                else distanceMultiplier = 1f - (accuracyAbs * 0.2f); 
            }
            
            return preciseVelocity * speedMultiplier * distanceMultiplier;
        }
    }
}