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
                        AimCamera.Priority = 0; // Disable normal AimCamera

                        if (_puttingCamera == null)
                        {
                            GameObject camObj = new GameObject("DynamicPuttingCamera");
                            _puttingCamera = camObj.AddComponent<CinemachineCamera>();
                        }
                        
                        _puttingCamera.Priority = 20;

                        // Ensure anchor still aligns with flag for any line rendering logic
                        if (AimTargetAnchor != null)
                        {
                            AimTargetAnchor.position = ballTransform.position;
                            Vector3 toFlag = ballInput.AimVisuals.FlagTransform.position - ballTransform.position;
                            toFlag.y = 0f;
                            if (toFlag.sqrMagnitude > 0.001f) AimTargetAnchor.rotation = Quaternion.LookRotation(toFlag.normalized);
                        }
                    }
                    else
                    {
                        if (_puttingCamera != null) _puttingCamera.Priority = 0;
                        AimCamera.Priority = 10;
                        AimCamera.Follow = ballTransform;
                        AimCamera.LookAt = AimTargetAnchor != null ? AimTargetAnchor : ballTransform;
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
                        
                        // Immediately activate the RollCamera to tightly follow the putt
                        if (RollCamera != null) RollCamera.Priority = 10;
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
                ApexCamera.Follow = null;
                Vector3 midPoint = _shotStartPosition + (shotDirection * (_totalShotDistance * 0.4f));
                ApexCamera.transform.position = midPoint + (shotRight * (_totalShotDistance * 0.3f)) + (Vector3.up * 12f);
            }

            // 3. Landing Camera (Positioned AHEAD and to the RIGHT of the target, looking back)
            if (LandingCamera != null)
            {
                LandingCamera.LookAt = ballTransform;
                LandingCamera.Follow = null;
                LandingCamera.transform.position = _shotTargetPosition + (shotDirection * 12f) + (shotRight * 4f) + (Vector3.up * 3f);
            }

            // 4. Roll Camera (Follows the stable Anchor)
            if (RollCamera != null)
            {
                RollCamera.Follow = AimTargetAnchor != null ? AimTargetAnchor : ballTransform;
                RollCamera.LookAt = ballTransform;
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

                // If we are in the Roll phase, update the side-tracking anchor
                if (_flightSubState == 3)
                {
                    HandleRollAnchorTracking();
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
            if (AimCamera == null || ballInput == null) return;

            bool isPutting = ballInput.PhysicsController != null &&
                             ballInput.PhysicsController.CurrentGround != null &&
                             ballInput.PhysicsController.CurrentGround.IsNiceOn;

            if (isPutting && ballInput.AimVisuals != null && ballInput.AimVisuals.FlagTransform != null && ballTransform != null)
            {
                if (_puttingCamera != null)
                {
                    Transform flag = ballInput.AimVisuals.FlagTransform;
                    Vector3 toFlag = flag.position - ballTransform.position;
                    toFlag.y = 0f;
                    Vector3 flagDir = toFlag.sqrMagnitude > 0.001f ? toFlag.normalized : Vector3.forward;

                    _puttingCamera.transform.position = ballTransform.position - (flagDir * PuttingCameraDistance) + (Vector3.up * PuttingCameraHeight);
                    _puttingCamera.transform.LookAt(flag);
                }
            }
            else
            {
                AimCamera.LookAt = AimTargetAnchor != null ? AimTargetAnchor : ballTransform;
            }
        }

        private void HandleFlightSubStateTransitions()
        {
            if (ballTransform == null || ballRigidbody == null) return;

            float currentProgress = Vector3.Distance(
                new Vector3(_shotStartPosition.x, 0, _shotStartPosition.z), 
                new Vector3(ballTransform.position.x, 0, ballTransform.position.z)
            ) / _totalShotDistance;

            // 1. Launch -> Apex
            if (_flightSubState == 0 && currentProgress > 0.25f && ballRigidbody.linearVelocity.y > 0)
            {
                _flightSubState = 1;
                if (FlightCamera != null) FlightCamera.Priority = 0;
                if (ApexCamera != null) ApexCamera.Priority = 10;
            }

            // 2. Apex -> Landing View (Trigger as ball falls towards the green)
            if (_flightSubState == 1 && (ballRigidbody.linearVelocity.y < -0.5f || currentProgress > 0.70f))
            {
                _flightSubState = 2;
                if (ApexCamera != null) ApexCamera.Priority = 0;
                if (LandingCamera != null) LandingCamera.Priority = 10;
            }

            // 3. Landing View -> Roll Camera (Trigger when ball hits the ground)
            if (_flightSubState == 2 && ballRigidbody.linearVelocity.y > -0.1f)
            {
                _flightSubState = 3;
                if (LandingCamera != null) LandingCamera.Priority = 0;
                if (RollCamera != null) RollCamera.Priority = 10;
            }
        }
    }
}