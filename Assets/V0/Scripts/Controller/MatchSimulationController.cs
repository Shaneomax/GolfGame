using UnityEngine;

public class MatchSimulationController : MonoBehaviour
{
    [Header("Current Match Data")]
    public ClubData CurrentClub;
    public BallData CurrentBall;
    
    [Header("World References")]
    public GameObject BallPrefab;
    public Transform TeeTransform;

    private GameObject activeBall; 
    private Rigidbody activeBallRb;

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

    private void InitializeTeeOff()
    {
        Debug.Log($"[Simulation] Spawning {CurrentBall.BallName} at the Tee...");

        if (activeBall == null)
        {
            activeBall = Instantiate(BallPrefab, TeeTransform.position, Quaternion.identity);
            activeBallRb = activeBall.GetComponent<Rigidbody>();
        }
        else
        {
            activeBallRb.linearVelocity = Vector3.zero;
            activeBallRb.angularVelocity = Vector3.zero;
            activeBall.transform.position = TeeTransform.position;
        }

        activeBallRb.mass = CurrentBall.Mass;
        GameStateManager.Instance.ChangeState(GameStateManager.GameState.Aiming);
    }
    
    private void UnlockAimControls() { /* Logic */ }
    private void StartSwingMeter() { /* Logic */ }
    private void EnterFlight() { /* Logic */ }
    private void EnterResolution() { /* Logic */ }
}