using UnityEngine;
using Unity.Cinemachine;

namespace GolfGame.Controllers
{
    public class CinemachineAimController : MonoBehaviour
    {
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

        [Header("Putting / NiceOn")]
        [Tooltip("How far behind the ball the camera sits when putting.")]
        public float PuttingCameraDistance = 4f;
        [Tooltip("How high above the ball the camera sits when putting.")]
        public float PuttingCameraHeight = 1.5f;
        [Tooltip("How smoothly the putting camera follows the rolling ball.")]
        public float PuttingCameraFollowSpeed = 3f;

        [Header("Normal Aim Camera")]
        [Tooltip("How far behind the ball the aim camera sits for normal shots.")]
        public float AimCameraDistance = 5f;
        [Tooltip("How high above the ball the aim camera sits for normal shots.")]
        public float AimCameraHeight = 2f;

        [Header("Landing Camera (First Bounce) Settings")]
        [Tooltip("How close the camera gets to the ball on the first bounce. Lower = Closer.")]
        public float LandingCameraDistance = 3.5f;
        [Tooltip("How high up the camera sits to look down on the first bounce. Higher = Higher Y axis.")]
        public float LandingCameraHeight = 2.5f;

        [Header("Setup Camera Controls (Mobile Touch Only)")]
        public float TouchPanSpeed = 0.05f;
        public float TouchZoomSpeed = 0.05f;

        private Transform ballTransform;
        private Rigidbody ballRigidbody;
        private PlayerInputController ballInput;

        private bool _setupCameraPositioned = false;
        private Vector3 _localCameraOffset;
        private Quaternion _localCameraRotation;
        private bool _hasSavedManualSetup = false;

        // Flight phase tracking states
        private Vector3 _shotStartPosition;
        private Vector3 _shotTargetPosition;
        private float _totalShotDistance;
        private int _flightSubState = 0; // 0=Launch, 1=Apex, 2=Landing, 3=Rolling

        private CinemachineCamera _puttingCamera;

        private void Start()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnStateEnter += OnGameStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnStateEnter -= OnGameStateChanged;
            }
        }

        private void FindBall()
        {
            GameObject ball = GameObject.FindWithTag("Player");
            if (ball != null)
            {
                ballTransform = ball.transform;
                ballRigidbody = ball.GetComponent<Rigidbody>();
                ballInput = ball.GetComponent<PlayerInputController>();
            }
        }

        private void OnGameStateChanged(GameStateManager.GameState newState)
        {
            FindBall();

            // Reset all camera priorities
            if (SetupCamera != null) SetupCamera.Priority = 0;
            if (AimCamera != null) AimCamera.Priority = 0;
            if (FlightCamera != null) FlightCamera.Priority = 0;
            if (ApexCamera != null) ApexCamera.Priority = 0;
            if (LandingCamera != null) LandingCamera.Priority = 0;
            if (RollCamera != null) RollCamera.Priority = 0;

            if (newState == GameStateManager.GameState.Setup)
            {
                _setupCameraPositioned = false;
                if (SetupCamera != null) SetupCamera.Priority = 10;
            }
            else if (newState == GameStateManager.GameState.Aiming)
            {
                if (AimCamera != null && ballTransform != null)
                {
                    bool isPutting = ballInput != null && ballInput.PhysicsController != null && 
                                     ballInput.PhysicsController.CurrentGround != null && 
                                     ballInput.PhysicsController.CurrentGround.IsNiceOn;

                    if (isPutting && ballInput.AimVisuals != null && ballInput.AimVisuals.FlagTransform != null)
                    {
                        AimCamera.Priority = 10;
                        var orbitalFollow = AimCamera.GetComponent<CinemachineOrbitalFollow>();
                        if (orbitalFollow != null) orbitalFollow.enabled = false;

                        // Snap anchor toward flag
                        if (AimTargetAnchor != null)
                        {
                            AimTargetAnchor.position = ballTransform.position;
                            Vector3 toFlag = ballInput.AimVisuals.FlagTransform.position - ballTransform.position;
                            toFlag.y = 0f;
                            if (toFlag.sqrMagnitude > 0.001f) AimTargetAnchor.rotation = Quaternion.LookRotation(toFlag.normalized);
                        }

                        // Snap camera immediately on state entry (no lerp yet)
                        Vector3 flagDir = (ballInput.AimVisuals.FlagTransform.position - ballTransform.position);
                        flagDir.y = 0f;
                        flagDir = flagDir.sqrMagnitude > 0.001f ? flagDir.normalized : Vector3.forward;
                        AimCamera.transform.position = ballTransform.position - (flagDir * PuttingCameraDistance) + (Vector3.up * PuttingCameraHeight);
                        AimCamera.transform.LookAt(Vector3.Lerp(ballTransform.position, ballInput.AimVisuals.FlagTransform.position, 0.15f));
                    }
                    else
                    {
                        // Normal shot
                        AimCamera.Priority = 10;
                        var orbitalFollow = AimCamera.GetComponent<CinemachineOrbitalFollow>();
                        if (orbitalFollow != null) orbitalFollow.enabled = false;

                        // Determine look direction: toward target marker, or fall back to AimTargetAnchor forward
                        Vector3 aimDir = AimTargetAnchor != null ? AimTargetAnchor.forward : Vector3.forward;
                        if (ballInput != null && ballInput.ActiveTargetMarker != null)
                        {
                            Vector3 toMarker = ballInput.ActiveTargetMarker.transform.position - ballTransform.position;
                            toMarker.y = 0f;
                            if (toMarker.sqrMagnitude > 0.001f) aimDir = toMarker.normalized;
                        }

                        // Snap camera immediately on state entry
                        AimCamera.transform.position = ballTransform.position - (aimDir * AimCameraDistance) + (Vector3.up * AimCameraHeight);
                        Transform lookTarget = ballInput != null && ballInput.ActiveTargetMarker != null 
                            ? ballInput.ActiveTargetMarker.transform : (AimTargetAnchor != null ? AimTargetAnchor : ballTransform);
                        AimCamera.transform.LookAt(lookTarget);
                    }
                }
            }
            else if (newState == GameStateManager.GameState.Flight)
            {
                if (ballTransform != null && ballInput != null && ballInput.ActiveTargetMarker != null)
                {
                    _shotStartPosition = ballTransform.position;
                    _shotTargetPosition = ballInput.ActiveTargetMarker.transform.position;
                    _totalShotDistance = Vector3.Distance(new Vector3(_shotStartPosition.x, 0, _shotStartPosition.z), new Vector3(_shotTargetPosition.x, 0, _shotTargetPosition.z));

                    // Configure all cameras first to ensure references and anchors are set
                    ConfigureFlightCameras();

                    // Check if we are currently on the green (NiceOn)
                    bool isPutting = ballInput.PhysicsController != null && 
                                     ballInput.PhysicsController.CurrentGround != null && 
                                     ballInput.PhysicsController.CurrentGround.IsNiceOn;

                    if (isPutting)
                    {
                        // Instantly bypass Launch, Apex, and Landing phases
                        _flightSubState = 3; 
                        
                        // Disable the FlightCamera priority that was just activated by ConfigureFlightCameras
                        if (FlightCamera != null) FlightCamera.Priority = 0;
                        
                        // Keep the smooth AimCamera active for putting instead of RollCamera
                        if (AimCamera != null) AimCamera.Priority = 10;
                        if (RollCamera != null) RollCamera.Priority = 0;
                    }
                    else
                    {
                        // Standard shot, start at the Launch phase
                        _flightSubState = 0; 
                    }
                }
            }
        }

        private void ConfigureFlightCameras()
        {
            Vector3 shotDirection = (_shotTargetPosition - _shotStartPosition).normalized;
            shotDirection.y = 0;
            Vector3 shotRight = Vector3.Cross(Vector3.up, shotDirection).normalized;

            // 1. Flight Camera
            if (FlightCamera != null)
            {
                FlightCamera.Priority = 10;
                FlightCamera.LookAt = ballTransform;
                FlightCamera.Follow = null; 
                FlightCamera.transform.position = _shotStartPosition - (shotDirection * 4f) + (Vector3.up * 1.5f);
            }

            // 2. Apex Camera
            if (ApexCamera != null)
            {
                ApexCamera.LookAt = ballTransform;
                ApexCamera.Follow = null; // Planted firmly on the ground looking up
            }

            // 3. Landing Camera 
            if (LandingCamera != null)
            {
                LandingCamera.LookAt = ballTransform; 
                LandingCamera.Follow = null; // Planted firmly near the landing zone
            }

            // 4. Roll Camera 
            if (RollCamera != null)
            {
                RollCamera.Follow = ballTransform; // Strictly follows the ball now
                RollCamera.LookAt = ballTransform; // Strictly looks at the ball
            }
        }

        private void HandleRollAnchorTracking()
        {
            if (ballTransform != null && AimTargetAnchor != null)
            {
                // 1. Anchor moves with the ball
                AimTargetAnchor.position = ballTransform.position;

                // 2. Anchor rotation stays locked strictly to the original shot direction
                Vector3 shotDir = (_shotTargetPosition - _shotStartPosition).normalized;
                shotDir.y = 0;
                if (shotDir.sqrMagnitude > 0.001f)
                {
                    AimTargetAnchor.rotation = Quaternion.LookRotation(shotDir);
                }
            }
        }

        private void Update()
        {
            if (GameStateManager.Instance == null) return;

            if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Setup)
            {
                HandleSetupTouchInput();
            }
            else if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Aiming)
            {
                HandleAimAnchorTracking();
            }
            else if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Flight)
            {
                HandleFlightSubStateTransitions();

                if (_flightSubState == 3)
                {
                    // Check if this is a NiceOn putt - if so, use the smooth putting camera instead of roll anchor
                    bool isPuttingRoll = ballInput != null && ballInput.PhysicsController != null &&
                                        ballInput.PhysicsController.CurrentGround != null &&
                                        ballInput.PhysicsController.CurrentGround.IsNiceOn;

                    if (isPuttingRoll)
                    {
                        UpdateAimCameraLookAt(); // Smoothly follow rolling ball with putting camera
                    }
                    else
                    {
                        HandleRollAnchorTracking();
                    }
                }
            }
        }

        private void HandleSetupTouchInput()
        {
            if (ballTransform == null) FindBall();

            if (!_setupCameraPositioned && SetupCamera != null && ballInput != null && ballInput.ActiveTargetMarker != null)
            {
                Vector3 markerPos = ballInput.ActiveTargetMarker.transform.position;
                Vector3 ballPos = ballTransform.position;
                
                Vector3 aimDir = (markerPos - ballPos).normalized;
                aimDir.y = 0f; 
                if (aimDir.sqrMagnitude < 0.001f) aimDir = Vector3.forward;

                Quaternion aimRotation = Quaternion.LookRotation(aimDir);

                if (!_hasSavedManualSetup)
                {
                    Quaternion inverseAimRotation = Quaternion.Inverse(aimRotation);
                    _localCameraOffset = inverseAimRotation * (SetupCamera.transform.position - ballPos);
                    _localCameraRotation = inverseAimRotation * SetupCamera.transform.rotation;
                    _hasSavedManualSetup = true;
                }
                
                SetupCamera.transform.position = ballPos + (aimRotation * _localCameraOffset);
                SetupCamera.transform.rotation = aimRotation * _localCameraRotation;
                _setupCameraPositioned = true;
            }
            else if (_setupCameraPositioned && SetupCamera != null)
            {
                bool isDraggingTarget = (ballInput != null && ballInput.IsDraggingTarget);

                if (!isDraggingTarget && Input.touchCount > 0)
                {
                    Vector3 camRight = SetupCamera.transform.right;
                    Vector3 camForward = SetupCamera.transform.forward;
                    camRight.y = 0f; camForward.y = 0f;
                    camRight.Normalize(); camForward.Normalize();

                    if (Input.touchCount == 1)
                    {
                        Touch touch = Input.GetTouch(0);
                        if (touch.phase == TouchPhase.Moved)
                        {
                            Vector3 panMove = (camRight * -touch.deltaPosition.x + camForward * -touch.deltaPosition.y) * TouchPanSpeed;
                            SetupCamera.transform.position += panMove;
                        }
                    }
                    else if (Input.touchCount == 2)
                    {
                        Touch touchZero = Input.GetTouch(0);
                        Touch touchOne = Input.GetTouch(1);

                        Vector2 touchZeroPrev = touchZero.position - touchZero.deltaPosition;
                        Vector2 touchOnePrev = touchOne.position - touchOne.deltaPosition;

                        float prevMag = (touchZeroPrev - touchOnePrev).magnitude;
                        float currentMag = (touchZero.position - touchOne.position).magnitude;
                        
                        SetupCamera.transform.position -= SetupCamera.transform.forward * ((prevMag - currentMag) * TouchZoomSpeed);
                    }
                }
            }
        }

        private void HandleAimAnchorTracking()
        {
            if (ballTransform != null && ballInput != null && AimTargetAnchor != null)
            {
                AimTargetAnchor.position = ballTransform.position;

                Vector3 aimDir = ballInput.FixedAimDirection;
                
                bool isPutting = ballInput.PhysicsController != null && 
                                 ballInput.PhysicsController.CurrentGround != null && 
                                 ballInput.PhysicsController.CurrentGround.IsNiceOn;
                                 
                if (isPutting && ballInput.AimVisuals != null && ballInput.AimVisuals.FlagTransform != null)
                {
                    Vector3 toFlag = ballInput.AimVisuals.FlagTransform.position - ballTransform.position;
                    toFlag.y = 0f;
                    if (toFlag.sqrMagnitude > 0.001f) aimDir = toFlag.normalized;
                }
                else if (ballInput.ActiveTargetMarker != null)
                {
                    Vector3 toMarker = ballInput.ActiveTargetMarker.transform.position - ballTransform.position;
                    toMarker.y = 0f;
                    if (toMarker.sqrMagnitude > 0.001f) aimDir = toMarker.normalized;
                }
                
                AimTargetAnchor.rotation = Quaternion.LookRotation(aimDir);
                UpdateAimCameraLookAt();
            }
        }

        private void UpdateAimCameraLookAt()
        {
            if (ballInput == null || ballTransform == null || AimCamera == null) return;

            bool isPutting = ballInput.PhysicsController != null &&
                             ballInput.PhysicsController.CurrentGround != null &&
                             ballInput.PhysicsController.CurrentGround.IsNiceOn;

            if (isPutting && ballInput.AimVisuals != null && ballInput.AimVisuals.FlagTransform != null)
            {
                // --- PUTTING: camera stays behind ball, looks at flag ---
                Transform flag = ballInput.AimVisuals.FlagTransform;
                Vector3 toFlag = flag.position - ballTransform.position;
                toFlag.y = 0f;
                Vector3 flagDir = toFlag.sqrMagnitude > 0.001f ? toFlag.normalized : Vector3.forward;

                AimCamera.transform.position = Vector3.Lerp(
                    AimCamera.transform.position,
                    ballTransform.position - (flagDir * PuttingCameraDistance) + (Vector3.up * PuttingCameraHeight),
                    Time.deltaTime * PuttingCameraFollowSpeed
                );

                Vector3 lookTarget = Vector3.Lerp(ballTransform.position, flag.position, 0.15f);
                AimCamera.transform.LookAt(lookTarget);
            }
            else
            {
                // --- NORMAL SHOT: camera stays behind ball, looks at target marker ---
                Vector3 aimDir = AimTargetAnchor != null ? AimTargetAnchor.forward : Vector3.forward;
                if (ballInput.ActiveTargetMarker != null)
                {
                    Vector3 toMarker = ballInput.ActiveTargetMarker.transform.position - ballTransform.position;
                    toMarker.y = 0f;
                    if (toMarker.sqrMagnitude > 0.001f) aimDir = toMarker.normalized;
                }

                AimCamera.transform.position = Vector3.Lerp(
                    AimCamera.transform.position,
                    ballTransform.position - (aimDir * AimCameraDistance) + (Vector3.up * AimCameraHeight),
                    Time.deltaTime * PuttingCameraFollowSpeed
                );

                Transform lookAt = ballInput.ActiveTargetMarker != null
                    ? ballInput.ActiveTargetMarker.transform
                    : (AimTargetAnchor != null ? AimTargetAnchor : ballTransform);
                AimCamera.transform.LookAt(lookAt);
            }
        }

        private void HandleFlightSubStateTransitions()
        {
            if (ballTransform == null || ballRigidbody == null || ballInput == null || ballInput.PhysicsController == null) return;

            // Calculate how far the ball has traveled relative to the total shot distance (0.0 to 1.0)
            float currentProgress = Vector3.Distance(
                new Vector3(_shotStartPosition.x, 0, _shotStartPosition.z), 
                new Vector3(ballTransform.position.x, 0, ballTransform.position.z)
            ) / _totalShotDistance;

            Vector3 shotDir = (_shotTargetPosition - _shotStartPosition).normalized;
            shotDir.y = 0;
            Vector3 shotRight = Vector3.Cross(Vector3.up, shotDir).normalized;

            // MOVED THIS UP HERE: Check if the ball has touched the ground this frame
            bool hasHitGround = ballInput.PhysicsController.CurrentGround != null;

            // ---------------------------------------------------------
            // TRANSITION 1: Launch -> Bounce Anticipation Camera
            // Triggers when ball is far away and barely visible
            // ---------------------------------------------------------
            if (_flightSubState == 0 && currentProgress > 0.55f)
            {
                _flightSubState = 1; 
                
                if (FlightCamera != null) FlightCamera.Priority = 0;
                
                if (ApexCamera != null)
                {
                    ApexCamera.Priority = 10;
                    
                    // Positioned slightly forward and sideways from the anticipated first bounce (target position)
                    Vector3 bounceCamPos = _shotTargetPosition + (shotDir * 5f) + (shotRight * 8f);
                    bounceCamPos.y = Mathf.Max(_shotTargetPosition.y + 4f, 2f); 
                    
                    ApexCamera.transform.position = bounceCamPos;
                    ApexCamera.LookAt = ballTransform;
                    ApexCamera.Follow = null; // Planted firmly
                }
            }
            // ---------------------------------------------------------
            // TRANSITION 2: Apex -> Landing Camera
            // Triggers exactly on the first bounce (ground contact)
            // ---------------------------------------------------------
            else if (_flightSubState == 1 && hasHitGround)
            {
                _flightSubState = 2; // Transition to Landing
                
                if (ApexCamera != null) ApexCamera.Priority = 0;
                
                if (LandingCamera != null)
                {
                    LandingCamera.Priority = 10;
                    
                    // Tell Cinemachine to follow and look at the ball as it moves
                    LandingCamera.LookAt = ballTransform; 
                    LandingCamera.Follow = ballTransform; 

                    // NEW: Automatically apply your Inspector tweaks to the camera tracking components
                    var cmFollow = LandingCamera.GetComponent<CinemachineFollow>();
                    if (cmFollow != null)
                    {
                        // Invert the distance value so it sits perfectly behind the ball's movement path
                        cmFollow.FollowOffset = new Vector3(0f, LandingCameraHeight, -LandingCameraDistance);
                    }

                    var cm3rdPerson = LandingCamera.GetComponent<Cinemachine3rdPersonFollow>();
                    if (cm3rdPerson != null)
                    {
                        cm3rdPerson.CameraDistance = LandingCameraDistance;
                        // You can manually adjust the vertical pivot or tracking offset in the inspector if needed
                    }
                }
            }
            // ---------------------------------------------------------
            // TRANSITION 3: Landing -> Sideview (Roll) Camera
            // Triggers when the ball's speed drops, indicating the big bounces 
            // are over and it's settling into a roll
            // ---------------------------------------------------------
            else if (_flightSubState == 2 && ballRigidbody.linearVelocity.magnitude < 5f)
            {
                _flightSubState = 3;
                
                if (LandingCamera != null) LandingCamera.Priority = 0;
                
                if (RollCamera != null)
                {
                    RollCamera.Priority = 10;
                    
                    // Re-enable Follow so the camera tightly tracks the final roll
                    RollCamera.Follow = ballTransform;
                    RollCamera.LookAt = ballTransform;
                }
            }
        }
    }
}