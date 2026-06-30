using UnityEngine;

namespace GolfGame.Controllers
{
    /// <summary>
    /// Manages the high-level match state, scores, and turn progression.
    /// </summary>
    public class GameManager : MonoBehaviour 
    {
        public static GameManager Instance { get; private set; }

        #region Match Data

        [field: Tooltip("Unique identifier for the current match.")]
        [field: SerializeField] public string MatchID { get; set; }

        [field: Tooltip("The current hole number being played.")]
        [field: SerializeField] public int CurrentHole { get; private set; } = 1;

        [field: Tooltip("Score for Player 1.")]
        [field: SerializeField] public int Player1Score { get; private set; } = 0;

        [field: Tooltip("Score for Player 2.")]
        [field: SerializeField] public int Player2Score { get; private set; } = 0;

        [field: Tooltip("Indicates if it is currently Player 1's turn.")]
        [field: SerializeField] public bool IsPlayer1Turn { get; private set; } = true;

        #endregion

        #region Unity Lifecycle

        private void Awake() 
        { 
            if (Instance != null && Instance != this) 
            { 
                Destroy(gameObject); 
                return; 
            }
            Instance = this; 
        }

        #endregion

        #region Game Logic

        /// <summary>
        /// Adds a stroke to the active player's score.
        /// </summary>
        public void AddStroke()
        {
            if (IsPlayer1Turn) 
            {
                Player1Score++;
            }
            else 
            {
                Player2Score++;
            }
            
            Debug.Log($"[GameManager] Stroke added! P1: {Player1Score} | P2: {Player2Score}");
        }

        /// <summary>
        /// Switches the turn to the other player.
        /// </summary>
        public void SwitchTurn()
        {
            IsPlayer1Turn = !IsPlayer1Turn;
            // TODO: Logic to trigger the next player's setup phase
        }

        #endregion
    }
}