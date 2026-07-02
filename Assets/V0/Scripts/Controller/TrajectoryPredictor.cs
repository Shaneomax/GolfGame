using UnityEngine;

namespace GolfGame.Controllers
{
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
        private Vector3[] pointsArray;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.enabled = false;
        }

        #endregion

        #region Public API
        public void ShowTrajectory(Vector3 startPosition, Vector3 launchVelocity)
        {
            ShowTrajectory(startPosition, launchVelocity, startPosition.y - GroundStopOffset);
        }

        public void ShowTrajectory(Vector3 startPosition, Vector3 launchVelocity, float targetHeight)
        {
            Vector3 position = startPosition;
            Vector3 velocity = launchVelocity;
            
            // Dynamic check up to 500 steps (25 seconds of flight time)
            int maxSteps = 500;
            if (pointsArray == null || pointsArray.Length < maxSteps)
            {
                pointsArray = new Vector3[maxSteps];
            }

            int validSteps = 0;

            for (int i = 0; i < maxSteps; i++)
            {
                pointsArray[i] = position;
                validSteps++;

                velocity += Physics.gravity * TimeStep;
                position += velocity * TimeStep;

                // Stop drawing when falling (velocity.y < 0) and height falls below targetHeight
                if (velocity.y < 0f && position.y < targetHeight)
                {
                    // Add one final point exactly at the target height to touch the ground perfectly
                    if (validSteps < maxSteps)
                    {
                        Vector3 finalPos = position;
                        finalPos.y = targetHeight;
                        pointsArray[validSteps] = finalPos;
                        validSteps++;
                    }
                    break;
                }
            }

            lineRenderer.positionCount = validSteps;
            lineRenderer.SetPositions(pointsArray);
            lineRenderer.enabled = true;
        }

        public void HideTrajectory()
        {
            lineRenderer.enabled      = false;
            lineRenderer.positionCount = 0;
        }

        #endregion
    }
}
