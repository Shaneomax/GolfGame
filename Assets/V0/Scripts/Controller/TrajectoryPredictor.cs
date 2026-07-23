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
        
        [Header("Spin Settings")]
        [Tooltip("Lateral acceleration applied in flight for full side spin. Match PlayerInputController!")]
        public float CurlAcceleration = 5f;

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
            
            BallPhysicsController physics = GetComponent<BallPhysicsController>();
            if (physics == null || physics.DefaultGround == null) return;
            
            int maxSteps = 800;
            if (pointsArray == null || pointsArray.Length < maxSteps)
            {
                pointsArray = new Vector3[maxSteps];
            }

            int validSteps = 0;
            int currentBounce = 0;
            int maxBounces = 3; 
            float lastBounceUpVelocity = 0f;

            Vector2 appliedSpin = GolfGame.UI.SpinInputUI.GlobalCurrentSpin;
            
            AimVisualsController aimVisuals = GetComponent<AimVisualsController>();
            Vector3 straightFlatDir = new Vector3(launchVelocity.x, 0f, launchVelocity.z).normalized;
            
            if (aimVisuals != null && aimVisuals.ActiveTargetMarker != null)
            {
                Vector3 toTarget = aimVisuals.ActiveTargetMarker.transform.position - startPosition;
                straightFlatDir = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
            }
            Vector3 flightRightDir = Vector3.Cross(Vector3.up, straightFlatDir).normalized;

            for (int i = 0; i < maxSteps; i++)
            {
                if (validSteps >= maxSteps) break;
                
                pointsArray[validSteps] = position;
                validSteps++;

                // 1. Sample current ground height
                float currentGroundY = targetHeight;
                if (Terrain.activeTerrain != null)
                {
                    currentGroundY = Terrain.activeTerrain.SampleHeight(position) + Terrain.activeTerrain.transform.position.y;
                }

                // 2. Apply curl if airborne
                if (position.y > currentGroundY + 0.05f && Mathf.Abs(appliedSpin.x) > 0.01f && currentBounce == 0)
                {
                    velocity += flightRightDir * (appliedSpin.x * CurlAcceleration) * TimeStep;
                }

                // 2.5 Apply air resistance (linear drag) exactly like the Rigidbody does in flight
                float drag = physics.DefaultGround.LinearDrag;
                velocity *= Mathf.Clamp01(1f - drag * TimeStep);

                // 3. Apply gravity and predict next position
                velocity += Physics.gravity * TimeStep;
                Vector3 nextPosition = position + velocity * TimeStep;

                // 4. Sample ground height at next position
                float nextGroundY = targetHeight;
                if (Terrain.activeTerrain != null)
                {
                    nextGroundY = Terrain.activeTerrain.SampleHeight(nextPosition) + Terrain.activeTerrain.transform.position.y;
                }

                // 5. Check if we hit the ground on this step
                if (velocity.y <= 0f && nextPosition.y <= nextGroundY)
                {
                    // Snap precisely to ground surface
                    nextPosition.y = nextGroundY;
                    
                    if (validSteps < maxSteps)
                    {
                        pointsArray[validSteps] = nextPosition;
                        validSteps++;
                    }

                    currentBounce++;
                    if (currentBounce >= maxBounces) break;

                    // Calculate bounce physics
                    Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
                    float forwardSpeed = horizontalVel.magnitude;
                    float impactDownSpeed = Mathf.Abs(velocity.y);
                    
                    GolfGame.Data.GroundData localGround = physics.GetGroundDataAtPosition(nextPosition);

                    float bounceUpVelocity = 0f;
                    Vector3 newHorizontal = Vector3.zero;

                    if (currentBounce == 1)
                    {
                        bounceUpVelocity = (impactDownSpeed * localGround.FirstBounceImpactScale)
                                         + (forwardSpeed * localGround.ForwardToBounceConversion);

                        float newForwardSpeed = forwardSpeed * localGround.FirstBounceForwardKill;

                        if (appliedSpin.y > 0)
                        {
                            newForwardSpeed *= Mathf.Lerp(1f, physics.MaxTopSpinForwardMultiplier, appliedSpin.y);
                            bounceUpVelocity *= Mathf.Lerp(1f, 0.4f, appliedSpin.y); // Topspin heavily reduces bounce height visually
                        }
                        else if (appliedSpin.y < 0)
                        {
                            newForwardSpeed *= Mathf.Lerp(1f, physics.MaxBackSpinForwardMultiplier, Mathf.Abs(appliedSpin.y));
                            bounceUpVelocity += physics.MaxBackSpinUpwardBonus * Mathf.Abs(appliedSpin.y);
                        }

                        // HARD CLAMP FOR VISUALS: Guarantee the visual bounce is always smaller than the main flight.
                        float absoluteMaxBounce = Mathf.Max(impactDownSpeed * 0.6f, 1.5f);
                        if (bounceUpVelocity > absoluteMaxBounce) bounceUpVelocity = absoluteMaxBounce;

                        newHorizontal = horizontalVel.sqrMagnitude > 0.001f ? horizontalVel.normalized * newForwardSpeed : Vector3.zero;
                    }
                    else
                    {
                        float decayRatio = Mathf.Clamp(localGround.BounceDecayRatio, 0f, 0.9f);
                        lastBounceUpVelocity *= decayRatio;
                        bounceUpVelocity = lastBounceUpVelocity;
                        float forwardRetention = localGround.ForwardRetentionPerBounce;

                        if (appliedSpin.y > 0)
                            forwardRetention *= Mathf.Lerp(1f, physics.MaxTopSpinForwardMultiplier, appliedSpin.y);
                        else if (appliedSpin.y < 0)
                            forwardRetention *= Mathf.Lerp(1f, physics.MaxBackSpinForwardMultiplier, Mathf.Abs(appliedSpin.y));

                        newHorizontal = horizontalVel * forwardRetention;
                    }

                    lastBounceUpVelocity = bounceUpVelocity;
                    velocity = new Vector3(newHorizontal.x, bounceUpVelocity, newHorizontal.z);
                    position = nextPosition; // Start next arc cleanly from ground contact
                }
                else
                {
                    position = nextPosition;
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
