using UnityEngine;
using Unity.Cinemachine;

namespace GolfGame.Controllers
{
    public class CinemachineAimController : MonoBehaviour
    {
        [Header("Cinemachine References")]
        [Tooltip("The camera used for the top-down Setup phase.")]
        public CinemachineCamera SetupCamera;

        [Tooltip("The camera used for the behind-the-ball Aiming phase.")]
        public CinemachineCamera AimCamera;

        // NEW: Added the Flight Camera reference
        [Tooltip("The camera used to follow the ball during the Flight phase.")]
        public CinemachineCamera FlightCamera;

        private Transform ballTransform;
        private Transform arrowTransform;
        private PlayerInputController ballInput;
        private PlayerInputController arrowInput;
        public Transform AimTargetAnchor;

        [Header("Setup Camera Controls")]
        public float PanSpeed = 20f;
        public float ZoomSpeed = 50f;
        public float MinZoom = 10f;
        public float MaxZoom = 60f;
        [Tooltip("Speed of touch and mouse drag panning.")]
        public float TouchPanSpeed = 0.05f;
        [Tooltip("Speed of touch pinch zooming.")]
        public float TouchZoomSpeed = 0.05f;

        private Vector3 lastMousePos;
        private bool _setupCameraPositioned = false;

        private Vector3 _localCameraOffset;
        private Quaternion _localCameraRotation;
        private bool _hasSavedManualSetup = false;

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
                ballInput = ball.GetComponent<PlayerInputController>();
            }

            GameObject arrow = GameObject.FindWithTag("Arrow");
            if (arrow != null)
            {
                arrowTransform = arrow.transform;
                arrowInput = arrow.GetComponent<PlayerInputController>();
            }
        }

        private void OnGameStateChanged(GameStateManager.GameState newState)
        {
            FindBall();

            // NEW: Reset all camera priorities to 0 first to prevent conflicts
            if (SetupCamera != null) SetupCamera.Priority = 0;
            if (AimCamera != null) AimCamera.Priority = 0;
            if (FlightCamera != null) FlightCamera.Priority = 0;

            if (newState == GameStateManager.GameState.Setup)
            {
                _setupCameraPositioned = false;
                if (SetupCamera != null) SetupCamera.Priority = 10;
            }
            else if (newState == GameStateManager.GameState.Aiming)
            {
                if (AimCamera != null && ballTransform != null)
                {
                    AimCamera.Priority = 10;
                    
                    AimCamera.Follow = null;
                    AimCamera.LookAt = null;
                }
            }
            // NEW: Added logic for the Flight state
            else if (newState == GameStateManager.GameState.Flight)
            {
                if (FlightCamera != null && ballTransform != null)
                {
                    FlightCamera.Priority = 10;
                    
                    // Assign the ball to the Flight Camera so Cinemachine automatically handles tracking
                    FlightCamera.Follow = ballTransform;
                    FlightCamera.LookAt = ballTransform;
                }
            }
        }

        private void Update()
        {
            if (GameStateManager.Instance == null) return;

            // --- SETUP CAMERA LOGIC ---
            if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Setup)
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

                    if (!isDraggingTarget)
                    {
                        Vector3 camRight = SetupCamera.transform.right;
                        Vector3 camForward = SetupCamera.transform.forward;
                        camRight.y = 0f; 
                        camForward.y = 0f;
                        camRight.Normalize();
                        camForward.Normalize();

                        if (Input.touchCount > 0)
                        {
                            if (Input.touchCount == 1)
                            {
                                Touch touch = Input.GetTouch(0);
                                if (touch.phase == TouchPhase.Moved)
                                {
                                    Vector2 delta = touch.deltaPosition;
                                    Vector3 panMove = (camRight * -delta.x + camForward * -delta.y) * TouchPanSpeed;
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
                                float deltaMag = prevMag - currentMag;

                                SetupCamera.transform.position -= SetupCamera.transform.forward * (deltaMag * TouchZoomSpeed);
                            }
                        }
                        
                        if (Input.touchCount == 0)
                        {
                            if (Input.GetMouseButtonDown(0))
                            {
                                lastMousePos = Input.mousePosition;
                            }
                            else if (Input.GetMouseButton(0))
                            {
                                Vector3 delta = Input.mousePosition - lastMousePos;
                                Vector3 panMove = (camRight * -delta.x + camForward * -delta.y) * TouchPanSpeed;
                                SetupCamera.transform.position += panMove;
                                lastMousePos = Input.mousePosition;
                            }
                        }

                        float h = Input.GetAxis("Horizontal");
                        float v = Input.GetAxis("Vertical");
                        
                        if (h != 0 || v != 0)
                        {
                            Vector3 panMove = (camRight * h + camForward * v) * PanSpeed * Time.deltaTime;
                            SetupCamera.transform.position += panMove;
                        }

                        float scroll = Input.GetAxis("Mouse ScrollWheel");
                        if (scroll == 0f && Input.mouseScrollDelta.y != 0)
                        {
                            scroll = Input.mouseScrollDelta.y * 0.1f;
                        }

                        if (scroll != 0f)
                        {
                            SetupCamera.transform.position += SetupCamera.transform.forward * (scroll * ZoomSpeed);
                        }
                    }
                }
                
                if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Aiming)
                {
                    if (AimTargetAnchor != null && ballTransform != null && ballInput != null)
                    {
                        AimTargetAnchor.position = ballTransform.position;
                        AimTargetAnchor.rotation = Quaternion.LookRotation(ballInput.FixedAimDirection);
                    }
                }
            }
            
            // --- AIM CAMERA LOGIC ---
            if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Aiming)
            {
                if (AimCamera != null && ballTransform != null && ballInput != null)
                {
                    // 1. Start with the input direction from your player controller
                    Vector3 aimDir = ballInput.FixedAimDirection;
                    
                    // 2. UPDATED: Track the ActiveTargetMarker instead of the FlagTransform
                    if (ballInput.ActiveTargetMarker != null)
                    {
                        Vector3 toMarker = ballInput.ActiveTargetMarker.transform.position - ballTransform.position;
                        toMarker.y = 0f; // Keep the look-at horizontal so the camera doesn't tilt up/down
                        
                        if (toMarker.sqrMagnitude > 0.001f)
                        {
                            aimDir = toMarker.normalized;
                        }
                    }

                    // 3. Position the camera 5 units behind the ball and 2 units up
                    Vector3 camPos = ballTransform.position - (aimDir * 5f) + (Vector3.up * 2f);
                    
                    // 4. Update the camera transform
                    AimCamera.transform.position = camPos;
                    AimCamera.transform.rotation = Quaternion.LookRotation(aimDir);
                }
            }
        }
    }
}