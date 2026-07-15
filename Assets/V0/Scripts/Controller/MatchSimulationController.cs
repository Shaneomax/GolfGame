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
        public Transform FlagTransform;

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
                    AlignBallToTarget(); // <-- ADD THIS LINE HERE to snap the ball's face to the flag!
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

            // 1. SPAWN OR RESET THE BALL
            if (activeBall == null)
            {
                activeBall = Instantiate(BallPrefab, TeeTransform.position, TeeTransform.rotation);
                activeBallRb = activeBall.GetComponent<Rigidbody>();
            }
            else
            {
                // REMOVE OR COMMENT OUT THESE LINES:
                // activeBall.transform.position = TeeTransform.position;
                // activeBall.transform.rotation = TeeTransform.rotation;
                
                activeBallRb.linearVelocity = Vector3.zero; 
                activeBallRb.angularVelocity = Vector3.zero; 
            }

            PlayerInputController inputController = activeBall.GetComponent<PlayerInputController>();

            // 3. APPLY BALL DATA
            if (CurrentBall != null)
            {
                activeBallRb.mass = CurrentBall.Mass;
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
        public void AlignBallToTarget()
        {
            if (activeBall == null || FlagTransform == null) 
            {
                // Comment this out to prevent spam
                // Debug.LogWarning("[Simulation] Missing Ball or Flag Transform for alignment.");
                return;
            }

            // 1. Find the direction from the ball to the flag
            Vector3 directionToFlag = FlagTransform.position - activeBall.transform.position;
            
            // 2. Flatten the Y-axis so the ball's Z-axis doesn't tilt into the ground or sky
            directionToFlag.y = 0f;

            // 3. Apply the new rotation to the ball
            if (directionToFlag != Vector3.zero) 
            {
                activeBall.transform.rotation = Quaternion.LookRotation(directionToFlag);
                
                // --- COMMENT THIS OUT TO PREVENT CONSOLE SPAM ---
                // Debug.Log("[Simulation] Ball Z-axis is now locked onto the flag.");
            }
        }
        
        private void UnlockAimControls() { /* TODO: Implement logic */ }
        private void StartSwingMeter() { /* TODO: Implement logic */ }
        private void EnterFlight() { /* TODO: Implement logic */ }
        private void EnterResolution() { /* TODO: Implement logic */ }

        #endregion
    }
}