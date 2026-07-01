using UnityEngine;
using Unity.Cinemachine;

namespace GolfGame.Controllers
{
    /// <summary>
    /// Dynamically positions Cinemachine cameras for Golf Clash-style gameplay.
    /// Setup state: top-down camera directly above the ball looking down.
    /// Aiming state: behind-the-ball camera aligned with the locked aim direction.
    /// Both cameras are positioned entirely by script — no manual placement needed.
    /// </summary>
    public class CinemachineAimController : MonoBehaviour
    {
        [Header("Cinemachine References")]
        [Tooltip("The camera used for the top-down Setup phase.")]
        public CinemachineCamera SetupCamera;

        [Tooltip("The camera used for the behind-the-ball Aiming phase.")]
        public CinemachineCamera AimCamera;

        [Header("Setup Camera Settings")]
        [Tooltip("Height above the ball for the top-down setup view.")]
        public float SetupCameraHeight = 20f;

        [Header("Aim Camera Settings")]
        [Tooltip("Distance behind the ball for the aim view.")]
        public float AimCameraDistance = 5f;

        [Tooltip("Height above the ball for the aim view.")]
        public float AimCameraHeight = 2f;

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
                if (SetupCamera != null && ballTransform != null)
                {
                    SetupCamera.Follow = ballTransform;
                    SetupCamera.LookAt = ballTransform;

                    // Position top-down above the ball
                    SetupCamera.transform.position = ballTransform.position + Vector3.up * SetupCameraHeight;
                    SetupCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

                    SetupCamera.Priority = 5;
                }

                if (AimCamera != null)
                {
                    AimCamera.Priority = 2;
                }
            }
            else if (newState == GameStateManager.GameState.Aiming)
            {
                if (AimCamera != null && ballTransform != null)
                {
                    Vector3 aimDir = Vector3.forward;
                    if (ballInput != null)
                    {
                        aimDir = ballInput.FixedAimDirection;
                    }

                    // Position behind the ball, opposite to the aim direction
                    Vector3 camPos = ballTransform.position 
                        - aimDir * AimCameraDistance 
                        + Vector3.up * AimCameraHeight;
                    AimCamera.transform.position = camPos;

                    // Look at a point slightly ahead of the ball in the aim direction
                    Vector3 lookTarget = ballTransform.position + aimDir * 2f;
                    AimCamera.transform.rotation = Quaternion.LookRotation(lookTarget - camPos);

                    AimCamera.Follow = ballTransform;
                    AimCamera.LookAt = ballTransform;
                    AimCamera.Priority = 2;
                }

                if (SetupCamera != null)
                {
                    SetupCamera.Priority = 1;
                }
            }
            else
            {
                if (SetupCamera != null) SetupCamera.Priority = 0;
                if (AimCamera != null) AimCamera.Priority = 0;
            }
        }
    }
}