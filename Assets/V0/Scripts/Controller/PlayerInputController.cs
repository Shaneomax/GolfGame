using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerInputController : MonoBehaviour
{
    private Rigidbody rb;
    private Vector3 dragStartPosition;
    private bool isDragging = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnMouseUp()
    {
        if (GameStateManager.Instance.CurrentState != GameStateManager.GameState.Aiming) return;
        if (!isDragging) return;
        isDragging = false;

        // Get references from your match controller
        MatchSimulationController match = FindObjectOfType<MatchSimulationController>();
        ClubData club = match.CurrentClub;
        BallData ball = match.CurrentBall;

        // Calculate drag vector
        Vector3 dragVector = dragStartPosition - GetMouseWorldPos();
        
        // 1. Incorporate Club Power and Ball Mass
        // Force = (Drag * Multiplier * ClubPower) / BallMass
        float forceMultiplier = 15f;
        Vector3 launchForce = dragVector.normalized * (dragVector.magnitude * forceMultiplier * club.Power);
        
        // Apply force
        rb.mass = ball.Mass; // Ensure mass matches selected ball
        rb.AddForce(launchForce, ForceMode.Impulse);

        GameStateManager.Instance.ChangeState(GameStateManager.GameState.Flight);
    }

    private void OnMouseDown() 
    { 
        if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Aiming)
        {
            isDragging = true;
            dragStartPosition = GetMouseWorldPos();
        }
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = Camera.main.WorldToScreenPoint(transform.position).z;
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }
}