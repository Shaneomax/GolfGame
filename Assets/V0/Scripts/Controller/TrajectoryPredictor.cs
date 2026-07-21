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
            
            // Try to find Physics controller to get ground data and spin multipliers
            BallPhysicsController physics = GetComponent<BallPhysicsController>();
            if (physics == null || physics.DefaultGround == null) return;
            
            // Dynamic check up to 800 steps
            int maxSteps = 800;
            if (pointsArray == null || pointsArray.Length < maxSteps)
            {
                pointsArray = new Vector3[maxSteps];
            }

            int validSteps = 0;
            int currentBounce = 0;
            int maxBounces = 3; // Show up to 3 bounces
            float lastBounceUpVelocity = 0f;

            for (int i = 0; i < maxSteps; i++)
            {
                pointsArray[i] = position;
                validSteps++;

                velocity += Physics.gravity * TimeStep;
                position += velocity * TimeStep;

                // Stop drawing when falling and hit ground
                if (velocity.y < 0f && position.y <= targetHeight)
                {
                    // Clamp to ground perfectly
                    position.y = targetHeight;
                    pointsArray[validSteps - 1] = position;
                    
                    currentBounce++;

                    if (currentBounce >= maxBounces)
                    {
                        break; // Stop after max bounces
                    }

                    // --- SIMULATE BOUNCE LOGIC ---
                    Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
                    float forwardSpeed = horizontalVel.magnitude;
                    float impactDownSpeed = Mathf.Abs(velocity.y);

                    float bounceUpVelocity = 0f;
                    Vector3 newHorizontal = Vector3.zero;
                    Vector2 appliedSpin = GolfGame.UI.SpinInputUI.GlobalCurrentSpin;

                    if (currentBounce == 1)
                    {
                        bounceUpVelocity = (impactDownSpeed * physics.DefaultGround.FirstBounceImpactScale)
                                         + (forwardSpeed * physics.DefaultGround.ForwardToBounceConversion);

                        float newForwardSpeed = forwardSpeed * physics.DefaultGround.FirstBounceForwardKill;

                        if (appliedSpin.y > 0)
                            newForwardSpeed *= Mathf.Lerp(1f, physics.MaxTopSpinForwardMultiplier, appliedSpin.y);
                        else if (appliedSpin.y < 0)
                        {
                            newForwardSpeed *= Mathf.Lerp(1f, physics.MaxBackSpinForwardMultiplier, Mathf.Abs(appliedSpin.y));
                            bounceUpVelocity += physics.MaxBackSpinUpwardBonus * Mathf.Abs(appliedSpin.y);
                        }

                        newHorizontal = horizontalVel.sqrMagnitude > 0.001f ? horizontalVel.normalized * newForwardSpeed : Vector3.zero;

                        if (Mathf.Abs(appliedSpin.x) > 0.01f && horizontalVel.sqrMagnitude > 0.001f)
                        {
                            Vector3 rightDir = Vector3.Cross(Vector3.up, horizontalVel.normalized).normalized;
                            newHorizontal += rightDir * (appliedSpin.x * physics.MaxSideSpinVelocity);
                        }
                    }
                    else
                    {
                        lastBounceUpVelocity *= physics.DefaultGround.BounceDecayRatio;
                        bounceUpVelocity = lastBounceUpVelocity;

                        float forwardRetention = physics.DefaultGround.ForwardRetentionPerBounce;

                        if (appliedSpin.y > 0)
                            forwardRetention *= Mathf.Lerp(1f, physics.MaxTopSpinForwardMultiplier, appliedSpin.y);
                        else if (appliedSpin.y < 0)
                            forwardRetention *= Mathf.Lerp(1f, physics.MaxBackSpinForwardMultiplier, Mathf.Abs(appliedSpin.y));

                        newHorizontal = horizontalVel * forwardRetention;

                        if (Mathf.Abs(appliedSpin.x) > 0.01f && horizontalVel.sqrMagnitude > 0.001f)
                        {
                            Vector3 rightDir = Vector3.Cross(Vector3.up, horizontalVel.normalized).normalized;
                            newHorizontal += rightDir * (appliedSpin.x * physics.MaxSideSpinVelocity * 0.5f);
                        }
                    }

                    lastBounceUpVelocity = bounceUpVelocity;
                    velocity = new Vector3(newHorizontal.x, bounceUpVelocity, newHorizontal.z);
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
