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

        // --- Added to track your manual Editor setup ---
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

            if (newState == GameStateManager.GameState.Setup)
            {
                _setupCameraPositioned = false;
                if (SetupCamera != null) SetupCamera.Priority = 10;
                if (AimCamera != null) AimCamera.Priority = 0;
            }
            else if (newState == GameStateManager.GameState.Aiming)
            {
                if (SetupCamera != null) SetupCamera.Priority = 0;
                
                if (AimCamera != null && ballTransform != null && arrowTransform != null)
                {
                    AimCamera.Priority = 10;
                    
                    // Keep AimCamera locked to the ball
                    AimCamera.Follow = ballTransform;
                    AimCamera.LookAt = arrowTransform;
                }
            }
            else
            {
                if (SetupCamera != null) SetupCamera.Priority = 0;
                if (AimCamera != null) AimCamera.Priority = 0;
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
                    
                    // Calculate the flat direction from the ball to the target marker
                    Vector3 aimDir = (markerPos - ballPos).normalized;
                    aimDir.y = 0f; 
                    if (aimDir.sqrMagnitude < 0.001f) aimDir = Vector3.forward;

                    // Create a rotation that points exactly down the aim line
                    Quaternion aimRotation = Quaternion.LookRotation(aimDir);

                    // If we haven't saved your manual Editor setup yet, do it now
                    if (!_hasSavedManualSetup)
                    {
                        // Calculate the inverse rotation to find the offset *relative* to the aim line
                        Quaternion inverseAimRotation = Quaternion.Inverse(aimRotation);
                        
                        // Save the manual position and rotation relative to the ball and aim direction
                        _localCameraOffset = inverseAimRotation * (SetupCamera.transform.position - ballPos);
                        _localCameraRotation = inverseAimRotation * SetupCamera.transform.rotation;
                        
                        _hasSavedManualSetup = true;
                    }
                    
                    // Apply your saved manual setup to the ball's current position and aiming direction
                    SetupCamera.transform.position = ballPos + (aimRotation * _localCameraOffset);
                    SetupCamera.transform.rotation = aimRotation * _localCameraRotation;

                    _setupCameraPositioned = true;
                }
                else if (_setupCameraPositioned && SetupCamera != null)
                {
                    bool isDraggingTarget = (ballInput != null && ballInput.IsDraggingTarget);

                    if (!isDraggingTarget)
                    {
                        // Calculate camera-relative directions (flattened so panning doesn't change height)
                        Vector3 camRight = SetupCamera.transform.right;
                        Vector3 camForward = SetupCamera.transform.forward;
                        camRight.y = 0f; 
                        camForward.y = 0f;
                        camRight.Normalize();
                        camForward.Normalize();

                        if (Input.touchCount > 0)
                        {
                            // --- Touch Controls (Mobile) ---
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
                                // Pinch to Zoom
                                Touch touchZero = Input.GetTouch(0);
                                Touch touchOne = Input.GetTouch(1);

                                Vector2 touchZeroPrev = touchZero.position - touchZero.deltaPosition;
                                Vector2 touchOnePrev = touchOne.position - touchOne.deltaPosition;

                                float prevMag = (touchZeroPrev - touchOnePrev).magnitude;
                                float currentMag = (touchZero.position - touchOne.position).magnitude;
                                float deltaMag = prevMag - currentMag;

                                // Zoom along the camera's angled forward vector
                                SetupCamera.transform.position -= SetupCamera.transform.forward * (deltaMag * TouchZoomSpeed);
                            }
                        }
                        
                        // --- PC / Editor Fallback Controls ---
                        if (Input.touchCount == 0)
                        {
                            // Mouse drag to pan
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

                        // WASD / Arrows to pan
                        float h = Input.GetAxis("Horizontal");
                        float v = Input.GetAxis("Vertical");
                        
                        if (h != 0 || v != 0)
                        {
                            Vector3 panMove = (camRight * h + camForward * v) * PanSpeed * Time.deltaTime;
                            SetupCamera.transform.position += panMove;
                        }

                        // Scroll wheel to zoom
                        float scroll = Input.GetAxis("Mouse ScrollWheel");
                        if (scroll == 0f && Input.mouseScrollDelta.y != 0)
                        {
                            scroll = Input.mouseScrollDelta.y * 0.1f;
                        }

                        if (scroll != 0f)
                        {
                            // Zoom smoothly along the angled view
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
                    Vector3 aimDir = ballInput.FixedAimDirection;
                    Vector3 camPos = ballTransform.position - aimDir * 5f + Vector3.up * 2f;
                    AimCamera.transform.position = camPos;
                }
            }
        }
    }
}