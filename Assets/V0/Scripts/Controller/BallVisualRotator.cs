using UnityEngine;

namespace GolfGame.Controllers
{
    /// <summary>
    /// Attached to the visual child of the ball parent.
    /// The parent's Rigidbody has all rotation constraints locked (to prevent jitter),
    /// so this script reads the parent's velocity and applies a purely visual "fake roll"
    /// to this child GameObject. The parent's MeshRenderer should be disabled so only
    /// this visual child is rendered.
    /// </summary>
    public class BallVisualRotator : MonoBehaviour
    {
        [Header("Parent Reference")]
        [Tooltip("The parent ball object that holds the Rigidbody. Auto-resolved to transform.parent if left empty.")]
        [SerializeField] private Rigidbody parentRigidbody;

        [Header("Ball Settings")]
        [Tooltip("Ball radius used to compute rolling angle. Auto-detected from parent's SphereCollider if available.")]
        [SerializeField] private float ballRadius = 0.213f; // standard golf ball ~42.67mm radius in Unity units

        [Header("Tuning")]
        [Tooltip("Minimum speed (m/s) before visual rotation is applied. Prevents micro-jitter when stationary.")]
        [SerializeField] private float speedThreshold = 0.05f;

        // ─────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Auto-resolve parent Rigidbody if not assigned in Inspector
            if (parentRigidbody == null && transform.parent != null)
            {
                parentRigidbody = transform.parent.GetComponent<Rigidbody>();
            }

            if (parentRigidbody == null)
            {
                Debug.LogWarning("[BallVisualRotator] No Rigidbody found on parent. Visual rotation will not work.", this);
            }

            // Auto-detect radius from parent's SphereCollider (the non-rotating physical collider)
            if (transform.parent != null)
            {
                SphereCollider sphereCollider = transform.parent.GetComponent<SphereCollider>();
                if (sphereCollider != null)
                {
                    // Use the parent's world scale to get the true physics radius
                    Transform p = transform.parent;
                    float maxScale = Mathf.Max(p.lossyScale.x, p.lossyScale.y, p.lossyScale.z);
                    ballRadius = sphereCollider.radius * maxScale;
                }
            }
        }

        private void Update()
        {
            // Only spin during flight / rolling state
            if (GameStateManager.Instance != null &&
                GameStateManager.Instance.CurrentState != GameStateManager.GameState.Flight)
            {
                return;
            }

            if (parentRigidbody == null) return;

            Vector3 velocity = parentRigidbody.linearVelocity;
            float speed = velocity.magnitude;

            // Stop rotating when the ball is stationary or barely moving
            if (speed <= speedThreshold) return;

            // ── Fake Rolling Rotation ──────────────────────────────────────────
            // Axis: perpendicular to the velocity in the horizontal plane
            // (Cross of world-up and velocity gives the axis a ball would roll around)
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 rotationAxis = Vector3.Cross(Vector3.up, horizontalVelocity).normalized;

            // When moving near-vertically (straight up/down), fall back to using full velocity
            if (rotationAxis.sqrMagnitude < 0.001f)
            {
                rotationAxis = Vector3.Cross(Vector3.right, velocity).normalized;
            }

            // Safety: skip if axis is still degenerate (e.g. zero velocity edge case)
            if (rotationAxis.sqrMagnitude < 0.001f) return;

            // Rolling angle this frame: θ = (distance travelled / radius) in degrees
            float angle = (speed * Time.deltaTime / ballRadius) * Mathf.Rad2Deg;

            // Apply rotation in world space so it is independent of the parent's frozen rotation
            transform.Rotate(rotationAxis, angle, Space.World);
        }
    }
}