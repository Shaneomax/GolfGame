using UnityEngine;
using System;

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
    
    public GameState CurrentState { get; private set; }

    // Events for decoupling
    public event Action<GameState> OnStateEnter;
    public event Action<GameState> OnStateExit;

    private void Awake() 
    { 
        if (Instance != null && Instance != this) 
        { 
            Destroy(gameObject); 
            return; 
        }
        Instance = this; 
    }

    // Only handles switching and broadcasting
    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState) 
            return;

        OnStateExit?.Invoke(CurrentState);
        CurrentState = newState;
        
        Debug.Log($"[State Manager] Transitioned to: {CurrentState}");
        OnStateEnter?.Invoke(CurrentState);
    }
}