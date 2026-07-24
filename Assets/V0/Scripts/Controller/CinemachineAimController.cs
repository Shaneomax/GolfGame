using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;

namespace GolfGame.Controllers
{
    /// <summary>
    /// Manages all Cinemachine virtual cameras for the golf game.
    /// Listens to <see cref="GameStateManager"/> state changes and drives camera
    /// priority, position and look-at targets for Setup, Aiming and Flight phases.
    /// </summary>
    public class CinemachineAimController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────
        // Flight sub-state constants  (replaces magic integer literals)
        // ─────────────────────────────────────────────────────────────
        private const int FlightState_Launch  = 0;
        private const int FlightState_Descent = 1;
        private const int FlightState_Bounce  = 2;
        private const int FlightState_Roll    = 3;

        // ─────────────────────────────────────────────────────────────
        // Inspector – Camera References
        // ─────────────────────────────────────────────────────────────
        [Header("Cinemachine References")]
        public CinemachineCamera SetupCamera;
        public CinemachineCamera AimCamera;

        [Tooltip("Positioned low behind the tee, looks up at the rising ball.")]
        public CinemachineCamera FlightCamera;

        [Tooltip("Positioned to the side of the fairway, tracks the ball mid-flight.")]
        public CinemachineCamera ApexCamera;

        [Tooltip("Positioned near the target marker, looks back at the incoming ball.")]
        public CinemachineCamera LandingCamera;

        [Tooltip("Tightly follows behind the ball after it hits the ground and rolls.")]
        public CinemachineCamera RollCamera;

        [Tooltip("Anchor used to orient the camera's alignment behind the ball.")]
        public Transform AimTargetAnchor;

        // ─────────────────────────────────────────────────────────────
        // Inspector – Putting / NiceOn Settings
        // ─────────────────────────────────────────────────────────────
        [Header("Putting / NiceOn")]
        [Tooltip("How far behind the ball the camera sits when putting.")]
        public float PuttingCameraDistance = 4f;

        [Tooltip("How high above the ball the camera sits when putting.")]
        public float PuttingCameraHeight = 1.5f;

        [Tooltip("How smoothly the putting camera follows the rolling ball.")]
        public float PuttingCameraFollowSpeed = 3f;

        // ─────────────────────────────────────────────────────────────
        // Inspector – Normal Aim Camera Settings
        // ─────────────────────────────────────────────────────────────
        [Header("Normal Aim Camera")]
        [Tooltip("How far behind the ball the aim camera sits for normal shots.")]
        public float AimCameraDistance = 5f;

        [Tooltip("How high above the ball the aim camera sits for normal shots.")]
        public float AimCameraHeight = 2f;

        // ─────────────────────────────────────────────────────────────
        // Inspector – Roll Camera Smooth Follow
        // ─────────────────────────────────────────────────────────────
        [Header("Roll Camera Smooth Follow")]
        [Tooltip("Distance behind the ball the roll camera sits.")]
        public float RollCameraDistance = 4f;

        [Tooltip("Height above the ball the roll camera sits.")]
        public float RollCameraHeight = 2f;

        [Tooltip("How quickly the camera catches up to the ball. Lower = more cinematic lag.")]
        public float RollCameraFollowSpeed = 4f;

        [Tooltip("How quickly the camera rotates to face behind the ball's direction. Lower = softer turns.")]
        public float RollCameraLookSpeed = 5f;

        [Tooltip("How much the ball's live velocity direction steers the camera. 0 = always behind shot dir, 1 = always behind velocity.")]
        [Range(0f, 1f)]
        public float RollCameraVelocityInfluence = 0.85f;

        [Header("Roll Camera Zoom-In After Bounce")]
        [Tooltip("How fast the roll camera zooms in toward the ball.")]
        public float RollZoomSpeed = 0.5f;

        [Tooltip("The final close-up distance behind the ball.")]
        public float RollZoomTargetDistance = 2f;

        [Tooltip("The final close-up height above the ball.")]
        public float RollZoomTargetHeight = 1f;

        // ─────────────────────────────────────────────────────────────
        // Inspector – Landing Camera (Planted) Settings
        // ─────────────────────────────────────────────────────────────
        [Header("Landing Camera (Planted) Settings")]
        [Tooltip("Offset relative to the landing spot. X=Sideways, Y=Up, Z=Forward")]
        public Vector3 LandingCameraPlantedOffset = new Vector3(30f, 5f, 20f);

        [Header("Landing Camera Bounce Zoom")]
        [Tooltip("How fast the landing camera zooms toward the ball after the first bounce.")]
        public float LandingZoomSpeed = 0.15f;

        [Tooltip("The closest the landing camera will get to the ball during zoom.")]
        public float LandingZoomMinDistance = 10f;

        [Tooltip("How high above the ball the camera stays while zooming in.")]
        public float LandingZoomHeight = 5f;

        // ─────────────────────────────────────────────────────────────
        // Inspector – Setup Camera Touch Controls (Mobile Only)
        // ─────────────────────────────────────────────────────────────
        [Header("Setup Camera Controls (Mobile Touch Only)")]
        public float TouchPanSpeed  = 0.05f;
        public float TouchZoomSpeed = 0.05f;

        [Header("Setup Camera Boundaries")]
        [Tooltip("Enable to restrict how far the camera can pan.")]
        public bool UsePanBoundaries = true;
        public float MinPanX = -200f;
        public float MaxPanX = 200f;
        public float MinPanZ = -200f;
        public float MaxPanZ = 200f;

        // ─────────────────────────────────────────────────────────────
        // Private – Ball references
        // ─────────────────────────────────────────────────────────────
        private Transform             ballTransform;
        private Rigidbody             ballRigidbody;
        private PlayerInputController ballInput;

        // ─────────────────────────────────────────────────────────────
        // Private – Setup camera state
        // ─────────────────────────────────────────────────────────────
        private bool       _setupCameraPositioned = false;
        private Vector3    _localCameraOffset;
        private Quaternion _localCameraRotation;
        private bool       _hasSavedManualSetup   = false;

        // ─────────────────────────────────────────────────────────────
        // Private – Flight phase tracking
        // ─────────────────────────────────────────────────────────────
        private Vector3 _shotStartPosition;
        private Vector3 _shotTargetPosition;
        private float   _totalShotDistance;
        private int     _flightSubState          = FlightState_Launch;
        private bool    _hasCalculatedRealTarget = false;

        // ─────────────────────────────────────────────────────────────
        // Private – Roll camera smooth-follow state
        // ─────────────────────────────────────────────────────────────
        private Vector3 _rollCamVelocityRef     = Vector3.zero; // SmoothDamp velocity
        private Vector3 _smoothedRollDir        = Vector3.forward; // smoothed travel direction
        private float   _currentRollDistance;   // zoom state: current distance behind ball
        private float   _currentRollHeight;     // zoom state: current height above ball

        // ─────────────────────────────────────────────────────────────
        // Private – Landing Bounce Zoom state
        // ─────────────────────────────────────────────────────────────
        private Vector3 _bounceStartCamPos;
        private float   _bounceZoomProgress;
        private Tweener _bounceZoomTween;

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns <c>true</c> when the ball is currently on a NiceOn (putting green) surface.
        /// </summary>
        private bool IsPutting =>
            ballInput?.PhysicsController?.CurrentGround != null &&
            ballInput.PhysicsController.CurrentGround.IsNiceOn;

        // ─────────────────────────────────────────────────────────────
        // Unity lifecycle
        // ─────────────────────────────────────────────────────────────

        private void Start()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnStateEnter += OnGameStateChanged;
        }

        private void OnDestroy()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnStateEnter -= OnGameStateChanged;
        }

        private void Update()
        {
            if (GameStateManager.Instance == null) return;

            switch (GameStateManager.Instance.CurrentState)
            {
                case GameStateManager.GameState.Setup:
                    HandleSetupTouchInput();
                    break;

                case GameStateManager.GameState.Aiming:
                    HandleAimAnchorTracking();
                    break;

                case GameStateManager.GameState.Flight:
                    HandleFlightSubStateTransitions();
                    if (_flightSubState == FlightState_Bounce)
                        HandleLandingBounceZoom();
                    else if (_flightSubState == FlightState_Roll)
                        HandleFlightRollUpdate();
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // State-change entry point
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Called whenever the <see cref="GameStateManager"/> enters a new state.
        /// Resets all camera priorities and delegates to the appropriate Enter handler.
        /// </summary>
        private void OnGameStateChanged(GameStateManager.GameState newState)
        {
            FindBall();
            ResetAllCameraPriorities();

            switch (newState)
            {
                case GameStateManager.GameState.Setup:
                    EnterSetupState();
                    break;
                case GameStateManager.GameState.Aiming:
                    EnterAimingState();
                    break;
                case GameStateManager.GameState.Flight:
                    EnterFlightState();
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // State enter handlers
        // ─────────────────────────────────────────────────────────────

        /// <summary>Activates the Setup camera and resets positioning state.</summary>
        private void EnterSetupState()
        {
            _setupCameraPositioned = false;
            if (SetupCamera != null) SetupCamera.Priority = 10;
        }

        /// <summary>
        /// Positions and activates the Aim camera.
        /// Handles both putting (NiceOn) and normal shot modes.
        /// </summary>
        private void EnterAimingState()
        {
            if (AimCamera == null || ballTransform == null) return;

            AimCamera.Priority = 10;

            // Disable orbital follow so we drive the transform manually
            var orbitalFollow = AimCamera.GetComponent<CinemachineOrbitalFollow>();
            if (orbitalFollow != null) orbitalFollow.enabled = false;

            // Always use the normal shot snap regardless of ground type (NiceOn/putting).
            // This ensures the aim camera always appears in the same consistent position.
            SnapAimCameraForNormalShot();
        }

        /// <summary>
        /// Records shot metadata, configures flight cameras, and chooses
        /// the correct starting sub-state (putting bypasses launch/apex/landing).
        /// </summary>
        private void EnterFlightState()
        {
            if (ballTransform == null || ballInput == null || ballInput.ActiveTargetMarker == null) return;

            _shotStartPosition  = ballTransform.position;
            _shotTargetPosition = ballInput.ActiveTargetMarker.transform.position;
            _totalShotDistance  = HorizontalDistance(_shotStartPosition, _shotTargetPosition);

            ConfigureFlightCameras();

            if (IsPutting)
            {
                // Putting: jump straight to roll phase.
                // Keep the AimCamera active — it will slowly drift to follow the ball.
                // RollCamera is NOT used for putts.
                _flightSubState = FlightState_Roll;

                if (FlightCamera != null) FlightCamera.Priority = 0;
                if (RollCamera  != null) RollCamera.Priority   = 0;
                if (AimCamera   != null) AimCamera.Priority    = 10; // stays on from aiming phase
            }
            else
            {
                // Normal shot: start at launch phase
                _flightSubState          = FlightState_Launch;
                _hasCalculatedRealTarget = false;
            }
        }

        private void SnapAimCameraForNormalShot()
        {
            Transform flag = ballInput?.AimVisuals?.FlagTransform;
            
            // 1. Calculate direction to the FLAG, not the marker
            Vector3 aimDir = Vector3.forward;
            if (flag != null)
            {
                aimDir = GetHorizontalDirection(ballTransform.position, flag.position);
                aimDir = aimDir.sqrMagnitude > 0.001f ? aimDir.normalized : Vector3.forward;
            }
            else
            {
                aimDir = GetAimDirectionToMarker(fallback: AimTargetAnchor?.forward ?? Vector3.forward);
            }

            // 2. Position the camera straight behind the ball
            AimCamera.transform.position = ballTransform.position
                - (aimDir * AimCameraDistance)
                + (Vector3.up * AimCameraHeight);

            // 3. Stare directly at the flag
            AimCamera.transform.LookAt(flag != null ? flag : ballTransform);
        }

        // ─────────────────────────────────────────────────────────────
        // Per-frame update handlers
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Handles single-finger pan and two-finger pinch-to-zoom on the Setup camera.
        /// Also performs an initial one-time alignment behind the target marker on first use.
        /// </summary>
        private void HandleSetupTouchInput()
        {
            if (ballTransform == null) FindBall();

            if (!_setupCameraPositioned)
                TryAlignSetupCameraToMarker();
            else if (SetupCamera != null)
                HandleSetupCameraTouch();
        }

        /// <summary>
        /// Each frame, keeps the <see cref="AimTargetAnchor"/> at the ball's position,
        /// oriented toward the current aim direction, and smoothly moves the aim camera.
        /// </summary>
        private void HandleAimAnchorTracking()
        {
            if (ballTransform == null || ballInput == null || AimTargetAnchor == null) return;

            AimTargetAnchor.position = ballTransform.position;
            AimTargetAnchor.rotation = Quaternion.LookRotation(ResolveAimDirection());

            UpdateAimCameraLookAt();
        }

        /// <summary>
        /// Per-frame update during the Roll sub-state.
        /// Putting keeps the AimCamera and lets it slowly drift to follow the ball.
        /// Normal shots drive the RollCamera manually for a cinematic follow.
        /// </summary>
        private void HandleFlightRollUpdate()
        {
            if (IsPutting)
                UpdateAimCameraLookAt();   // AimCamera drifts slowly — feels stable, not locked
            else
                HandleRollAnchorTracking(); // RollCamera with velocity-aware smooth damp
        }

        // ─────────────────────────────────────────────────────────────
        // Setup camera helpers
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// On the first frame the target marker is available, aligns the Setup camera
        /// behind the ball relative to the marker direction and saves the local offset.
        /// </summary>
        private void TryAlignSetupCameraToMarker()
        {
            if (SetupCamera == null || ballInput == null || ballInput.ActiveTargetMarker == null) return;

            Vector3    ballPos     = ballTransform.position;
            Vector3    aimDir      = GetAimDirectionToMarker(fallback: Vector3.forward);
            Quaternion aimRotation = Quaternion.LookRotation(aimDir);

            if (!_hasSavedManualSetup)
            {
                Quaternion inverseAim = Quaternion.Inverse(aimRotation);
                _localCameraOffset    = inverseAim * (SetupCamera.transform.position - ballPos);
                _localCameraRotation  = inverseAim * SetupCamera.transform.rotation;
                _hasSavedManualSetup  = true;
            }

            SetupCamera.transform.position = ballPos + (aimRotation * _localCameraOffset);
            SetupCamera.transform.rotation = aimRotation * _localCameraRotation;
            _setupCameraPositioned         = true;
        }

        /// <summary>
        /// Handles touch input on the Setup camera: one finger to pan, two fingers to zoom.
        /// Suppressed while the player is dragging the target marker.
        /// </summary>
        private void HandleSetupCameraTouch()
        {
            bool isDraggingTarget = ballInput != null && ballInput.IsDraggingTarget;
            bool isSpinDashboardOpen = GolfGame.UI.GameplayUIController.IsSpinDashboardOpen;
            if (isDraggingTarget || isSpinDashboardOpen || Input.touchCount == 0 || global::GameManager.IsPaused) return;

            Vector3 camRight   = SetupCamera.transform.right;
            Vector3 camForward = SetupCamera.transform.forward;
            camRight.y   = 0f; camRight.Normalize();
            camForward.y = 0f; camForward.Normalize();

            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId)) return;

                if (touch.phase == TouchPhase.Moved)
                {
                    Vector3 panMove = (camRight   * -touch.deltaPosition.x
                                    + camForward * -touch.deltaPosition.y) * TouchPanSpeed;
                    
                    Vector3 newPos = SetupCamera.transform.position + panMove;
                    
                    if (UsePanBoundaries && ballTransform != null)
                    {
                        newPos.x = Mathf.Clamp(newPos.x, ballTransform.position.x + MinPanX, ballTransform.position.x + MaxPanX);
                        newPos.z = Mathf.Clamp(newPos.z, ballTransform.position.z + MinPanZ, ballTransform.position.z + MaxPanZ);
                    }
                    
                    SetupCamera.transform.position = newPos;
                }
            }
            else if (Input.touchCount == 2)
            {
                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);

                Vector2 t0Prev = t0.position - t0.deltaPosition;
                Vector2 t1Prev = t1.position - t1.deltaPosition;

                float prevMag    = (t0Prev - t1Prev).magnitude;
                float currentMag = (t0.position - t1.position).magnitude;

                SetupCamera.transform.position -=
                    SetupCamera.transform.forward * ((prevMag - currentMag) * TouchZoomSpeed);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Aim camera smooth update
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Smoothly updates the Aim camera's position and look-at target each frame.
        /// Branches between putting mode and normal shot mode.
        /// </summary>
        private void UpdateAimCameraLookAt()
        {
            if (ballInput == null || ballTransform == null || AimCamera == null) return;

            // Always use normal-shot camera tracking regardless of ground type.
            UpdateAimCameraForNormalShot();
        }

        /// <summary>
        /// Smoothly moves the Aim camera behind the ball looking toward the flag while putting.
        /// </summary>
        // private void UpdateAimCameraForPutting()
        // {
        //     Transform flag    = ballInput.AimVisuals.FlagTransform;
        //     Vector3   flagDir = GetHorizontalDirection(ballTransform.position, flag.position);
        //     if (flagDir.sqrMagnitude < 0.001f) flagDir = Vector3.forward;

        //     Vector3 desiredPos = ballTransform.position
        //         - (flagDir * PuttingCameraDistance)
        //         + (Vector3.up * PuttingCameraHeight);

        //     AimCamera.transform.position = Vector3.Lerp(
        //         AimCamera.transform.position,
        //         desiredPos,
        //         Time.deltaTime * PuttingCameraFollowSpeed
        //     );

        //     AimCamera.transform.LookAt(Vector3.Lerp(ballTransform.position, flag.position, 0.15f));
        // }

        /// <summary>
        /// Smoothly moves the Aim camera behind the ball looking toward the target marker
        /// during a normal shot.
        /// </summary>
        private void UpdateAimCameraForNormalShot()
        {
            Transform flag = ballInput?.AimVisuals?.FlagTransform;
            
            // 1. Calculate direction to the FLAG, not the marker
            Vector3 aimDir = Vector3.forward;
            if (flag != null)
            {
                aimDir = GetHorizontalDirection(ballTransform.position, flag.position);
                aimDir = aimDir.sqrMagnitude > 0.001f ? aimDir.normalized : Vector3.forward;
            }
            else
            {
                aimDir = GetAimDirectionToMarker(fallback: AimTargetAnchor?.forward ?? Vector3.forward);
            }

            // 2. Calculate the desired position straight behind the ball
            Vector3 desiredPos = ballTransform.position
                - (aimDir * AimCameraDistance)
                + (Vector3.up * AimCameraHeight);

            AimCamera.transform.position = Vector3.Lerp(
                AimCamera.transform.position,
                desiredPos,
                Time.deltaTime * PuttingCameraFollowSpeed
            );

            // 3. Stare directly at the flag
            AimCamera.transform.LookAt(flag != null ? flag : ballTransform);
        }

        // ─────────────────────────────────────────────────────────────
        // Flight camera configuration
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Sets up LookAt / Follow targets and initial positions for all flight cameras
        /// at the moment the shot starts.
        /// </summary>
        private void ConfigureFlightCameras()
        {
            Vector3 shotDir   = GetHorizontalDirection(_shotStartPosition, _shotTargetPosition).normalized;
            Vector3 shotRight = Vector3.Cross(Vector3.up, shotDir).normalized;

            // 1. Flight (launch) Camera – low behind tee, looks up at rising ball
            if (FlightCamera != null)
            {
                FlightCamera.Priority  = 10;
                FlightCamera.LookAt    = ballTransform;
                FlightCamera.Follow    = null; // planted
                FlightCamera.transform.position = _shotStartPosition
                    - (shotDir * 4f)
                    + (Vector3.up * 1.5f);
            }

            // 2. Apex Camera – side view, planted on the ground looking up
            if (ApexCamera != null)
            {
                ApexCamera.LookAt = ballTransform;
                ApexCamera.Follow = null; // planted
            }

            // 3. Landing Camera – planted near the landing zone
            if (LandingCamera != null)
            {
                LandingCamera.LookAt = ballTransform;
                LandingCamera.Follow = null; // planted
            }

            // 4. Roll Camera – strictly tracks the ball
            if (RollCamera != null)
            {
                RollCamera.Follow = ballTransform;
                RollCamera.LookAt = ballTransform;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Flight sub-state machine
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Drives the four-stage flight camera sequence:
        /// Launch → Descent (landing planted) → Bounce → Roll.
        /// Also refines the predicted landing position once the ball is in motion.
        /// </summary>
        private void HandleFlightSubStateTransitions()
        {
            if (ballRigidbody == null || ballInput == null) return;

            TryRefineLandingPrediction();

            float currentProgress = CalculateShotProgress();
            bool  hasHitGround    = ballInput.PhysicsController.BounceCount > 0;

            // ── Transition 1: Launch → Descent ──────────────────────────────────
            // Activates the landing camera early (55% progress) so it's already in
            // position before the ball drops.
            if (_flightSubState == FlightState_Launch && currentProgress > 0.55f)
            {
                TransitionToDescentCamera();
            }
            // ── Transition 2: Descent → Bounce ──────────────────────────────────
            // Triggers on first ground contact; landing camera keeps watching.
            else if (_flightSubState == FlightState_Descent && hasHitGround)
            {
                _flightSubState = FlightState_Bounce;
                if (LandingCamera != null)
                {
                    _bounceStartCamPos = LandingCamera.transform.position;
                    _bounceZoomProgress = 0f;
                    _bounceZoomTween?.Kill();
                    // LandingZoomSpeed is converted to duration (e.g. 0.15 speed = 6.6 seconds)
                    float duration = 1f / Mathf.Max(0.01f, LandingZoomSpeed);
                    _bounceZoomTween = DOVirtual.Float(0f, 1f, duration, v => _bounceZoomProgress = v)
                        .SetEase(Ease.InOutSine);
                }
            }
            // ── Transition 3: Bounce → Roll ──────────────────────────────────────
            // Triggers once the ball's speed drops, indicating big bounces are over.
            else if (_flightSubState == FlightState_Bounce && ballRigidbody.linearVelocity.magnitude < 5f)
            {
                TransitionToRollCamera();
            }
        }

        /// <summary>
        /// Activates the Landing camera and plants it beside the predicted landing spot.
        /// Called at ~55% shot progress so the camera is ready before impact.
        /// </summary>
        private void TransitionToDescentCamera()
        {
            _flightSubState = FlightState_Descent;
            if (FlightCamera != null) FlightCamera.Priority = 0;

            if (LandingCamera == null) return;

            LandingCamera.Priority = 10;
            LandingCamera.Follow   = null;
            LandingCamera.LookAt   = ballTransform;

            Vector3 shotDir   = GetHorizontalDirection(_shotStartPosition, _shotTargetPosition).normalized;
            Vector3 shotRight = Vector3.Cross(Vector3.up, shotDir).normalized;

            LandingCamera.transform.position = _shotTargetPosition
                + (shotRight  * LandingCameraPlantedOffset.x)
                + (Vector3.up * LandingCameraPlantedOffset.y)
                + (shotDir    * LandingCameraPlantedOffset.z);
        }

        /// <summary>
        /// After the first bounce, smoothly zooms the landing camera toward the ball
        /// for a cinematic close-up while it bounces and settles.
        /// </summary>
        private void HandleLandingBounceZoom()
        {
            if (LandingCamera == null || ballTransform == null) return;

            // Use the original fixed shot directions to maintain a strict side-view angle
            Vector3 shotDir   = GetHorizontalDirection(_shotStartPosition, _shotTargetPosition).normalized;
            Vector3 shotRight = Vector3.Cross(Vector3.up, shotDir).normalized;

            // Calculate ideal tracking position: alongside the ball, but zoomed in
            Vector3 desiredPos = ballTransform.position
                + (shotRight * LandingZoomMinDistance)
                + (Vector3.up * LandingZoomHeight);

            // Smoothly blend from the exact starting position to the tracking position using DOTween's progress
            // This guarantees a smooth start with zero jumping, regardless of where the ball bounced
            LandingCamera.transform.position = Vector3.Lerp(
                _bounceStartCamPos,
                desiredPos,
                _bounceZoomProgress
            );
        }


        /// <summary>
        /// Switches from the Landing camera to the Roll camera once the ball settles.
        /// </summary>
        private void TransitionToRollCamera()
        {
            _flightSubState  = FlightState_Roll;
            _smoothedRollDir = GetHorizontalDirection(_shotStartPosition, _shotTargetPosition).normalized;
            if (_smoothedRollDir.sqrMagnitude < 0.001f) _smoothedRollDir = Vector3.forward;

            if (LandingCamera != null) LandingCamera.Priority = 0;
            if (RollCamera    == null) return;

            RollCamera.Priority = 10;
            RollCamera.Follow   = null;           // We drive the transform manually for smooth organic feel
            RollCamera.LookAt   = ballTransform;  // Cinemachine still handles look-at damping

            // Seed position behind ball so there is no pop on first frame
            _rollCamVelocityRef    = Vector3.zero;
            _currentRollDistance   = RollCameraDistance;
            _currentRollHeight     = RollCameraHeight;
            RollCamera.transform.position = ballTransform.position
                - (_smoothedRollDir * _currentRollDistance)
                + (Vector3.up       * _currentRollHeight);
        }

        /// <summary>
        /// Keeps the <see cref="AimTargetAnchor"/> locked at the ball's position
        /// with rotation pointing along the original shot direction during the roll phase.
        /// </summary>
        /// <summary>
        /// Smoothly drives the RollCamera behind the ball using the ball's live velocity direction.
        /// Uses SmoothDamp for position (gives natural lag/inertia) and Slerp for direction
        /// (prevents snapping when the ball curves or slows).
        /// </summary>
        private void HandleRollAnchorTracking()
        {
            if (ballTransform == null || RollCamera == null) return;

            // ── 1. Resolve the target travel direction ─────────────────────────
            // Blend the original shot direction with the ball's live velocity direction.
            // This means the camera slowly swings around as the ball curves or rolls.
            Vector3 shotDir = GetHorizontalDirection(_shotStartPosition, _shotTargetPosition).normalized;
            if (shotDir.sqrMagnitude < 0.001f) shotDir = Vector3.forward;

            Vector3 velDir = shotDir; // fallback
            if (ballRigidbody != null)
            {
                Vector3 flatVel = new Vector3(ballRigidbody.linearVelocity.x, 0f, ballRigidbody.linearVelocity.z);
                if (flatVel.sqrMagnitude > 0.04f)   // ignore micro-movements (< ~0.2 m/s)
                    velDir = flatVel.normalized;
            }

            Vector3 targetDir = Vector3.Slerp(shotDir, velDir, RollCameraVelocityInfluence);

            // ── 2. Smooth the direction to avoid jitter ────────────────────────
            _smoothedRollDir = Vector3.Slerp(
                _smoothedRollDir,
                targetDir,
                Time.deltaTime * RollCameraLookSpeed
            );
            if (_smoothedRollDir.sqrMagnitude < 0.001f) _smoothedRollDir = shotDir;

            // ── 3. Slowly zoom in by reducing distance and height ───────────
            _currentRollDistance = Mathf.Lerp(_currentRollDistance, RollZoomTargetDistance, Time.deltaTime * RollZoomSpeed);
            _currentRollHeight  = Mathf.Lerp(_currentRollHeight,  RollZoomTargetHeight,  Time.deltaTime * RollZoomSpeed);

            // ── 4. Compute desired camera position behind ball ─────────────────
            Vector3 desiredPos = ballTransform.position
                - (_smoothedRollDir * _currentRollDistance)
                + (Vector3.up       * _currentRollHeight);

            // ── 5. SmoothDamp to desired position (gives organic inertia lag) ──
            RollCamera.transform.position = Vector3.SmoothDamp(
                RollCamera.transform.position,
                desiredPos,
                ref _rollCamVelocityRef,
                1f / Mathf.Max(RollCameraFollowSpeed, 0.01f)
            );

            // ── 6. Keep AimTargetAnchor in sync (other systems may read it) ────
            if (AimTargetAnchor != null)
            {
                AimTargetAnchor.position = ballTransform.position;
                AimTargetAnchor.rotation = Quaternion.LookRotation(_smoothedRollDir);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Landing prediction
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Once the ball has real velocity, uses simple projectile physics to refine
        /// the predicted landing position (raycasts to find actual ground height).
        /// Only runs once per shot.
        /// </summary>
        private void TryRefineLandingPrediction()
        {
            if (_flightSubState != FlightState_Launch) return;
            if (_hasCalculatedRealTarget) return;
            if (ballRigidbody.linearVelocity.magnitude <= 1f) return;

            _hasCalculatedRealTarget = true;

            Vector3 v0         = ballRigidbody.linearVelocity;
            float   timeToLand = (2f * v0.y) / Mathf.Abs(Physics.gravity.y);

            if (timeToLand <= 0f) return;

            Vector3 predictedXZ = _shotStartPosition + new Vector3(v0.x, 0f, v0.z) * timeToLand;

            // Raycast down to find actual terrain height at the predicted XZ position
            if (Physics.Raycast(predictedXZ + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f))
                _shotTargetPosition = hit.point;
            else
            {
                predictedXZ.y       = _shotStartPosition.y;
                _shotTargetPosition = predictedXZ;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Utility helpers
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Finds the ball GameObject by the "Player" tag and caches its components.
        /// </summary>
        private void FindBall()
        {
            GameObject ball = GameObject.FindWithTag("Player");
            if (ball == null) return;

            ballTransform = ball.transform;
            ballRigidbody = ball.GetComponent<Rigidbody>();
            ballInput     = ball.GetComponent<PlayerInputController>();
        }

        /// <summary>Sets all virtual camera priorities to 0 (inactive).</summary>
        private void ResetAllCameraPriorities()
        {
            if (SetupCamera   != null) SetupCamera.Priority   = 0;
            if (AimCamera     != null) AimCamera.Priority     = 0;
            if (FlightCamera  != null) FlightCamera.Priority  = 0;
            if (ApexCamera    != null) ApexCamera.Priority    = 0;
            if (LandingCamera != null) LandingCamera.Priority = 0;
            if (RollCamera    != null) RollCamera.Priority    = 0;
        }

        /// <summary>
        /// Returns the horizontal (Y=0) direction from <paramref name="from"/>
        /// to <paramref name="to"/>, not normalised.
        /// </summary>
        private static Vector3 GetHorizontalDirection(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            dir.y = 0f;
            return dir;
        }

        /// <summary>Returns the horizontal distance between two positions (ignoring Y).</summary>
        private static float HorizontalDistance(Vector3 a, Vector3 b) =>
            Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z));

        /// <summary>
        /// Resolves the current aim direction.
        /// Priority: putting flag → target marker → fixed aim direction.
        /// </summary>
        private Vector3 ResolveAimDirection()
        {
            Vector3 dir = ballInput.FixedAimDirection;

            if (IsPutting && HasFlagReference())
            {
                Vector3 toFlag = GetHorizontalDirection(ballTransform.position,
                                                        ballInput.AimVisuals.FlagTransform.position);
                if (toFlag.sqrMagnitude > 0.001f) dir = toFlag.normalized;
            }
            else if (ballInput.ActiveTargetMarker != null)
            {
                Vector3 toMarker = GetHorizontalDirection(ballTransform.position,
                                                          ballInput.ActiveTargetMarker.transform.position);
                if (toMarker.sqrMagnitude > 0.001f) dir = toMarker.normalized;
            }

            return dir;
        }

        /// <summary>
        /// Returns a normalised horizontal aim direction from the ball toward the target marker.
        /// Falls back to <paramref name="fallback"/> when no marker is available.
        /// </summary>
        private Vector3 GetAimDirectionToMarker(Vector3 fallback)
        {
            if (ballInput?.ActiveTargetMarker == null || ballTransform == null) return fallback;

            Vector3 toMarker = GetHorizontalDirection(ballTransform.position,
                                                      ballInput.ActiveTargetMarker.transform.position);
            return toMarker.sqrMagnitude > 0.001f ? toMarker.normalized : fallback;
        }

        /// <summary>Returns how far along the shot the ball has travelled as a 0-1 value.</summary>
        private float CalculateShotProgress()
        {
            if (_totalShotDistance <= 0.001f || ballTransform == null) return 0f;
            return Mathf.Clamp01(HorizontalDistance(_shotStartPosition, ballTransform.position) / _totalShotDistance);
        }

        /// <summary>
        /// Returns <c>true</c> when the ball input has a valid AimVisuals and FlagTransform.
        /// </summary>
        private bool HasFlagReference() =>
            ballInput?.AimVisuals != null && ballInput.AimVisuals.FlagTransform != null;
    }
}
