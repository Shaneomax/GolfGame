using UnityEngine;

namespace GolfGame.Controllers
{
    /// <summary>
    /// Handles the instantiation of the golf ball and setting up the simulation state.
    /// </summary>
    public class MatchSimulationController : MonoBehaviour
    {
        #region References

        [Header("Current Match Data")]
        [Tooltip("The club currently selected by the player.")]
        public ClubData CurrentClub;

        [Tooltip("The ball data containing physics and configuration for the current match.")]
        public BallData CurrentBall;
        
        [Header("World References")]
        [Tooltip("The prefab of the golf ball to instantiate.")]
        public GameObject BallPrefab;

        [Tooltip("The transform representing the tee-off location.")]
        public Transform TeeTransform;

        #endregion

        #region Private Fields

        private GameObject activeBall; 
        private Rigidbody activeBallRb;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            GameStateManager.Instance.OnStateEnter += HandleStateEntered;
            GameStateManager.Instance.ChangeState(GameStateManager.GameState.Setup);
        }

        private void OnDestroy()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnStateEnter -= HandleStateEntered;
            }
        }

        #endregion

        #region State Handling

        private void HandleStateEntered(GameStateManager.GameState state)
        {
            // Execute the pure simulation logic based on the state
            switch (state)
            {
                case GameStateManager.GameState.Setup:
                    InitializeTeeOff();
                    break;
                case GameStateManager.GameState.Aiming:
                    UnlockAimControls();
                    break;
                case GameStateManager.GameState.Swinging:
                    StartSwingMeter();
                    break;
                case GameStateManager.GameState.Flight:
                    EnterFlight();
                    break;
                case GameStateManager.GameState.Resolution:
                    EnterResolution();
                    break;
            }
        }

        #endregion

        #region Simulation Logic

        private void InitializeTeeOff()
        {
            if (CurrentBall != null)
                Debug.Log($"[Simulation] Spawning {CurrentBall.BallName} at the Tee..."); 
            else
                Debug.LogWarning("[Simulation] CurrentBall data is missing!"); 

            // 1. Grab the exact position and rotation (Z-axis alignment) of the Tee
            Vector3 exactTeePosition = TeeTransform.position;
            Quaternion exactTeeRotation = TeeTransform.rotation;

            if (activeBall == null)
            {
                // 2. Spawn using the Tee's rotation instead of Quaternion.identity
                activeBall = Instantiate(BallPrefab, exactTeePosition, exactTeeRotation);
                activeBallRb = activeBall.GetComponent<Rigidbody>();
            }
            else
            {
                // 3. CRITICAL FIX: Force the existing ball back to the Tee's exact Z position and rotation
                activeBall.transform.position = exactTeePosition;
                activeBall.transform.rotation = exactTeeRotation;
                
                activeBallRb.linearVelocity = Vector3.zero; 
                activeBallRb.angularVelocity = Vector3.zero; 
            }

            if (CurrentBall != null)
            {
                activeBallRb.mass = CurrentBall.Mass;
                PlayerInputController inputController = activeBall.GetComponent<PlayerInputController>();
                if (inputController != null)
                {
                    inputController.CurrentBall = CurrentBall; 
                    inputController.CurrentClub = CurrentClub;
                    inputController.ApplyBallData(); 
                }
            }
        }
        public void TransitionSetupToAiming()
        {
            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Setup)
            {
                GameStateManager.Instance.ChangeState(GameStateManager.GameState.Aiming);
            }
        }
        
        private void UnlockAimControls() { /* TODO: Implement logic */ }
        private void StartSwingMeter() { /* TODO: Implement logic */ }
        private void EnterFlight() { /* TODO: Implement logic */ }
        private void EnterResolution() { /* TODO: Implement logic */ }

        #endregion
    }
}