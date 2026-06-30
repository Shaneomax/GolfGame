using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerInputController : MonoBehaviour
{
    [Header("Settings")]
    public float ForceMultiplier = 15f;
    public float MaxDragDistance = 3f;

    private Rigidbody rb;
    private Vector3 dragStartPosition;
    private Vector3 dragCurrentPosition;
    private bool isDragging = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnMouseDown()
    {
        // Only allow input if in Aiming state
        if (GameStateManager.Instance.CurrentState != GameStateManager.GameState.Aiming)
            return;

        isDragging = true;
        dragStartPosition = GetMouseWorldPos();
    }

    private void OnMouseDrag()
    {
        if (!isDragging) return;

        dragCurrentPosition = GetMouseWorldPos();
        
        // Optional: Visual feedback (e.g., drawing a line renderer here)
    }

    private void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;

        // Calculate direction and magnitude
        Vector3 dragVector = dragStartPosition - GetMouseWorldPos();
        
        // Clamp the force so the shot isn't too powerful
        float dragMagnitude = Mathf.Clamp(dragVector.magnitude, 0, MaxDragDistance);
        Vector3 launchForce = dragVector.normalized * dragMagnitude * ForceMultiplier;

        // Apply physics
        rb.AddForce(launchForce, ForceMode.Impulse);

        // Transition state to flight
        GameStateManager.Instance.ChangeState(GameStateManager.GameState.Swinging);
        
        // Small delay or logic to switch to Flight state
        GameStateManager.Instance.ChangeState(GameStateManager.GameState.Flight);
    }

    private Vector3 GetMouseWorldPos()
    {
        // Convert screen mouse position to world position
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = Camera.main.WorldToScreenPoint(transform.position).z;
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }
}