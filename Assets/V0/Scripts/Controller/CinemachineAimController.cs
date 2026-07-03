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
        private PlayerInputController ballInput;
        public Transform AimTargetAnchor;

        [Header("Setup Camera Controls")]
        public float PanSpeed = 20f;
        public float ZoomSpeed = 50f;
        public float MinZoom = 10f;
        public float MaxZoom = 60f;

        private float currentZoom = 30f;

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
        }

        private bool _setupCameraPositioned = false;

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
                
                if (AimCamera != null && ballTransform != null)
                {
                    AimCamera.Priority = 10;
                    
                    // Keep AimCamera locked to the ball
                    AimCamera.Follow = ballTransform;
                    AimCamera.LookAt = ballTransform;
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
                    currentZoom = 30f;
                    
                    // Position camera directly above the marker
                    SetupCamera.transform.position = markerPos + Vector3.up * currentZoom;
                    
                    // Look straight down
                    SetupCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                    _setupCameraPositioned = true;
                }
                else if (_setupCameraPositioned && SetupCamera != null)
                {
                    // Panning using WASD / Arrow keys
                    float h = Input.GetAxis("Horizontal");
                    float v = Input.GetAxis("Vertical");
                    
                    if (h != 0 || v != 0)
                    {
                        Vector3 panMove = new Vector3(h, 0f, v) * PanSpeed * Time.deltaTime;
                        SetupCamera.transform.position += panMove;
                    }

                    // Zooming using Scroll Wheel
                    float scroll = Input.GetAxis("Mouse ScrollWheel");
                    if (scroll != 0f)
                    {
                        currentZoom -= scroll * ZoomSpeed;
                        currentZoom = Mathf.Clamp(currentZoom, MinZoom, MaxZoom);
                        
                        Vector3 pos = SetupCamera.transform.position;
                        pos.y = currentZoom;
                        SetupCamera.transform.position = pos;
                    }
                }
                if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Aiming)
                {
                    // Update the anchor's position and rotation
                    // The Camera will smoothly interpolate to this anchor automatically
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
                // Force the AimCamera to stay behind the ball based on your aiming direction
                if (AimCamera != null && ballTransform != null && ballInput != null)
                {
                    Vector3 aimDir = ballInput.FixedAimDirection;
                    // Note: You should ideally use an invisible target object for the AimCamera too, 
                    // but this keeps it simple based on your current setup.
                    Vector3 camPos = ballTransform.position - aimDir * 5f + Vector3.up * 2f;
                    AimCamera.transform.position = camPos;
                }
            }
        }
    }
}