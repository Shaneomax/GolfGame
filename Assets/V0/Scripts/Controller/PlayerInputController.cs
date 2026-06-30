using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerInputController : MonoBehaviour
{
    [Header("Settings")]
    public float ForceMultiplier = 15f;
    public float MaxDragDistance = 3f;

    [Header("Data References")]
    public BallData CurrentBall; // Assign your ScriptableObject here in the Inspector!

    private Rigidbody rb;
    private Vector3 dragStartPosition;
    private Vector3 dragCurrentPosition;
    private bool isDragging = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        // 1. Apply the ScriptableObject data to the Rigidbody physics
        if (CurrentBall != null)
        {
            rb.mass = CurrentBall.Mass;
            rb.linearDamping = CurrentBall.LinearDrag;
            rb.angularDamping = CurrentBall.AngularDrag;
        }
        else
        {
            Debug.LogWarning("No BallData assigned to PlayerInputController!");
        }
    }

    private void FixedUpdate()
    {
        // 2. Check if the ball is in flight/rolling and moving very slowly
        if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Flight)
        {
            // We use sqrMagnitude instead of magnitude because it is faster for the CPU to calculate
            if (rb.linearVelocity.sqrMagnitude < (CurrentBall.StopThreshold * CurrentBall.StopThreshold))
            {
                StopBall();
            }
        }
    }

    private void StopBall()
    {
        // Force the velocity to exactly zero
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        // Force the Unity Physics engine to put the Rigidbody to sleep
        rb.Sleep(); 

        // Transition back to aiming so the player can take their next shot
        GameStateManager.Instance.ChangeState(GameStateManager.GameState.Aiming);
    }

    private void OnMouseDown()
    {
        if (GameStateManager.Instance.CurrentState != GameStateManager.GameState.Aiming)
            return;

        isDragging = true;
        dragStartPosition = GetMouseWorldPos();
    }

    private void OnMouseDrag()
    {
        if (!isDragging) return;
        dragCurrentPosition = GetMouseWorldPos();
    }

    private void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;

        Vector3 dragVector = dragStartPosition - GetMouseWorldPos();
        float dragMagnitude = Mathf.Clamp(dragVector.magnitude, 0, MaxDragDistance);
        Vector3 launchForce = dragVector.normalized * dragMagnitude * ForceMultiplier;

        rb.AddForce(launchForce, ForceMode.Impulse);

        // Transition state to flight
        GameStateManager.Instance.ChangeState(GameStateManager.GameState.Flight);
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = Camera.main.WorldToScreenPoint(transform.position).z;
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }
}