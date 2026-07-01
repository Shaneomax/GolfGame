using UnityEngine;

namespace GolfGame.Controllers
{
    /// <summary>
    /// Predicts and renders the golf ball's ideal flight path using a pure gravity simulation
    /// (no air drag). The actual ball will always land slightly shorter than the line shows,
    /// creating a dynamic feel where the player must learn to overshoot their target.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class TrajectoryPredictor : MonoBehaviour
    {
        #region Settings

        [Header("Simulation Settings")]
        [Tooltip("Number of steps to simulate. More steps = longer visible arc.")]
        public int SimulationSteps = 90;

        [Tooltip("Time (in seconds) between each simulation step. Lower = smoother curve.")]
        public float TimeStep = 0.05f;

        [Tooltip("The arc stops drawing when the predicted Y drops this far below the launch point.")]
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
        /// Simulates the ball's ideal path using only gravity (zero air drag).
        /// The actual ball will land shorter due to real air resistance — intentionally dynamic.
        /// </summary>
        /// <param name="startPosition">World-space position of the ball at launch.</param>
        /// <param name="launchVelocity">The initial velocity vector of the ball.</param>
        public void ShowTrajectory(Vector3 startPosition, Vector3 launchVelocity)
        {
            Vector3[] points = new Vector3[SimulationSteps];
            Vector3 position = startPosition;
            Vector3 velocity = launchVelocity;
            int validSteps   = SimulationSteps;

            for (int i = 0; i < SimulationSteps; i++)
            {
                points[i] = position;
                velocity += Physics.gravity * TimeStep;
                position += velocity * TimeStep;

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
            lineRenderer.enabled      = false;
            lineRenderer.positionCount = 0;
        }

        #endregion
    }
}
