using UnityEngine;

public class GameManager : MonoBehaviour 
{
    public static GameManager Instance { get; private set; }

    // --- MACRO MATCH DATA (Server-relevant later) ---
    public string MatchID;          
    public int CurrentHole = 1;      
    public int Player1Score = 0;     
    public int Player2Score = 0;   
    public bool IsPlayer1Turn = true; 

    private void Awake() 
    { 
        if (Instance != null && Instance != this) 
        { 
            Destroy(gameObject); 
            return; 
        }
        Instance = this; 
    }

    public void AddStroke()
    {
        if (IsPlayer1Turn) 
            Player1Score++;
        else 
            Player2Score++;
        
        Debug.Log($"[GameManager] Stroke added! P1: {Player1Score} | P2: {Player2Score}");
    }

    public void SwitchTurn()
    {
        IsPlayer1Turn = !IsPlayer1Turn;
        // Logic to trigger the next player's setup phase
    }
}