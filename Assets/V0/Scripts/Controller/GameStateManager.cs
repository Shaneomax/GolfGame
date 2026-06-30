using UnityEngine;
using System;

namespace GolfGame.Controllers
{
    /// <summary>
    /// Manages the state machine for the golf game flow.
    /// </summary>
    public class GameStateManager : MonoBehaviour 
    {
        public static GameStateManager Instance { get; private set; }

        public enum GameState 
        { 
            None,
            Setup, 
            Aiming, 
            Swinging, 
            Flight, 
            Resolution 
        }
        
        /// <summary>
        /// The current state of the game loop.
        /// </summary>
        public GameState CurrentState { get; private set; }

        #region Events

        /// <summary>
        /// Fired when entering a new game state.
        /// </summary>
        public event Action<GameState> OnStateEnter;

        /// <summary>
        /// Fired when exiting a game state.
        /// </summary>
        public event Action<GameState> OnStateExit;

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

        #region State Management

        /// <summary>
        /// Transitions the game to a new state and broadcasts the necessary events.
        /// </summary>
        /// <param name="newState">The state to transition to.</param>
        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) 
                return;

            OnStateExit?.Invoke(CurrentState);
            CurrentState = newState;
            
            Debug.Log($"[State Manager] Transitioned to: {CurrentState}");
            OnStateEnter?.Invoke(CurrentState);
        }

        #endregion
    }
}