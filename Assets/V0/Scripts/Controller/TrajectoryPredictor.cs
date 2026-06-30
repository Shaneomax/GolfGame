using UnityEngine;

namespace GolfGame.Controllers
{
    /// <summary>
    /// Predicts and renders the golf ball's flight path as a dotted arc 
    /// using a step-by-step physics simulation while the player is dragging.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class TrajectoryPredictor : MonoBehaviour
    {
        #region Settings

        [Header("Simulation Settings")]
        [Tooltip("Number of steps to simulate. More steps = longer visible arc.")]
        public int SimulationSteps = 90;

        [Tooltip("Time (in seconds) between each simulation step. Lower = more accurate curve.")]
        public float TimeStep = 0.05f;

        [Tooltip("The trajectory stops being drawn if the ball's predicted Y drops this far below the launch point.")]
        public float GroundStopOffset = 0.3f;

        #endregion

        #region Private Fields

        private LineRenderer lineRenderer;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.enabled = false;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Runs a manual physics simulation and draws the predicted arc on the LineRenderer.
        /// </summary>
        /// <param name="startPosition">World-space position of the ball at launch.</param>
        /// <param name="launchVelocity">The initial velocity vector of the ball (impulse / mass).</param>
        /// <param name="airLinearDrag">The linear drag coefficient to apply each step (matches in-flight Rigidbody damping).</param>
        public void ShowTrajectory(Vector3 startPosition, Vector3 launchVelocity, float airLinearDrag)
        {
            Vector3[] points  = new Vector3[SimulationSteps];
            Vector3 position  = startPosition;
            Vector3 velocity  = launchVelocity;
            int validSteps    = SimulationSteps;

            for (int i = 0; i < SimulationSteps; i++)
            {
                points[i] = position;

                // Apply gravity (same as Unity's default)
                velocity += Physics.gravity * TimeStep;

                // Apply linear drag approximation  (matches Rigidbody damping formula)
                velocity *= Mathf.Clamp01(1f - airLinearDrag * TimeStep);

                // Advance position
                position += velocity * TimeStep;

                // Stop the simulation once the predicted arc drops below the launch ground level
                if (position.y < startPosition.y - GroundStopOffset)
                {
                    validSteps = i + 1;
                    break;
                }
            }

            lineRenderer.positionCount = validSteps;
            lineRenderer.SetPositions(points);
            lineRenderer.enabled = true;
        }

        /// <summary>
        /// Hides the trajectory line.
        /// </summary>
        public void HideTrajectory()
        {
            lineRenderer.enabled = false;
            lineRenderer.positionCount = 0;
        }

        #endregion
    }
}
