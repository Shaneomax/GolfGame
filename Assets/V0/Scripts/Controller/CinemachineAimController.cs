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

        private void OnGameStateChanged(GameStateManager.GameState newState)
        {
            FindBall();

            if (newState == GameStateManager.GameState.Setup)
            {
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

                // If the marker has spawned, assign it to Cinemachine so it tracks it!
                if (SetupCamera != null && ballInput != null && ballInput.ActiveTargetMarker != null)
                {
                    if (SetupCamera.Follow != ballInput.ActiveTargetMarker.transform)
                    {
                        SetupCamera.Follow = ballInput.ActiveTargetMarker.transform;
                        SetupCamera.LookAt = ballInput.ActiveTargetMarker.transform;
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