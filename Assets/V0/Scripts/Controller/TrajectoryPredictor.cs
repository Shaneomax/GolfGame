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

        [Header("Post-Collision Line")]
        [Tooltip("Assign a second LineRenderer (on a child GameObject) to show the arc after hitting a non-terrain object. Leave empty to disable.")]
        public LineRenderer PostCollisionLineRenderer;

        [Tooltip("Radius of the sphere cast used to detect collision objects in the flight path.")]
        public float CollisionDetectionRadius = 0.12f;

        [Tooltip("Number of steps to simulate for the post-collision bounce arc.")]
        public int PostCollisionSteps = 60;

        [Tooltip("Layer mask for objects that can cause a mid-air collision (exclude Terrain and Ignore Raycast).")]
        public LayerMask CollisionLayerMask = Physics.DefaultRaycastLayers;

        #endregion

        #region Private Fields

        private LineRenderer lineRenderer;

        // Primary arc points
        private Vector3[] pointsArray;

        // Velocity at each primary arc step — needed for post-collision arc simulation
        private Vector3[] velocitiesArray;

        // Post-collision arc points
        private Vector3[] postCollisionPoints;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.enabled = false;

            if (PostCollisionLineRenderer != null)
                PostCollisionLineRenderer.enabled = false;
        }

        #endregion

        #region Public API

        public void ShowTrajectory(Vector3 startPosition, Vector3 launchVelocity)
        {
            ShowTrajectory(startPosition, launchVelocity, startPosition.y - GroundStopOffset);
        }

        public void ShowTrajectory(Vector3 startPosition, Vector3 launchVelocity, float targetHeight)
        {
            BallPhysicsController physics = GetComponent<BallPhysicsController>();
            if (physics == null || physics.DefaultGround == null) return;

            float effectiveCurlAccel = physics.CurlAcceleration;
            float drag               = physics.DefaultGround.LinearDrag;

            int maxSteps = 800;
            if (pointsArray    == null || pointsArray.Length    < maxSteps) pointsArray    = new Vector3[maxSteps];
            if (velocitiesArray == null || velocitiesArray.Length < maxSteps) velocitiesArray = new Vector3[maxSteps];

            Vector2 appliedSpin = GolfGame.UI.SpinInputUI.GlobalCurrentSpin;

            AimVisualsController aimVisuals = GetComponent<AimVisualsController>();
            Vector3 straightFlatDir = new Vector3(launchVelocity.x, 0f, launchVelocity.z).normalized;
            if (aimVisuals != null && aimVisuals.ActiveTargetMarker != null)
            {
                Vector3 toTarget = aimVisuals.ActiveTargetMarker.transform.position - startPosition;
                straightFlatDir  = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
            }
            Vector3 flightRightDir = Vector3.Cross(Vector3.up, straightFlatDir).normalized;

            // ─────────────────────────────────────────────────────────────────────
            // PASS 1 — Simulate the full primary arc, completely ignoring any
            //          physics objects.  Only terrain/ground stops it.
            // ─────────────────────────────────────────────────────────────────────
            int     validSteps          = 0;
            int     currentBounce       = 0;
            int     maxBounces          = 3;
            float   lastBounceUpVel     = 0f;
            Vector3 position            = startPosition;
            Vector3 velocity            = launchVelocity;

            for (int i = 0; i < maxSteps; i++)
            {
                if (validSteps >= maxSteps) break;

                // Record position AND velocity for each step
                pointsArray[validSteps]    = position;
                velocitiesArray[validSteps] = velocity;
                validSteps++;

                // 1. Ground height at current position
                float currentGroundY = targetHeight;
                if (Terrain.activeTerrain != null)
                    currentGroundY = Terrain.activeTerrain.SampleHeight(position) + Terrain.activeTerrain.transform.position.y;

                // 2. Curl (first arc only, while airborne)
                if (position.y > currentGroundY + 0.05f && Mathf.Abs(appliedSpin.x) > 0.01f && currentBounce == 0)
                    velocity += flightRightDir * (appliedSpin.x * effectiveCurlAccel) * TimeStep;

                // 3. Air resistance
                velocity *= Mathf.Clamp01(1f - drag * TimeStep);

                // 4. Gravity → next position
                velocity    += Physics.gravity * TimeStep;
                Vector3 nextPos = position + velocity * TimeStep;

                // 5. Ground height at next position
                float nextGroundY = targetHeight;
                if (Terrain.activeTerrain != null)
                    nextGroundY = Terrain.activeTerrain.SampleHeight(nextPos) + Terrain.activeTerrain.transform.position.y;

                // 6. Terrain bounce / stop
                if (velocity.y <= 0f && nextPos.y <= nextGroundY)
                {
                    // Snap to ground — fixes the truncated arc end
                    nextPos.y = nextGroundY;

                    // Always record the precise landing point
                    if (validSteps < maxSteps)
                    {
                        pointsArray[validSteps]    = nextPos;
                        velocitiesArray[validSteps] = velocity;
                        validSteps++;
                    }

                    currentBounce++;
                    if (currentBounce >= maxBounces) break;

                    // Bounce physics
                    Vector3 hVel          = new Vector3(velocity.x, 0f, velocity.z);
                    float   fwdSpeed      = hVel.magnitude;
                    float   impactDown    = Mathf.Abs(velocity.y);
                    GolfGame.Data.GroundData localGround = physics.GetGroundDataAtPosition(nextPos);

                    float bounceUp   = 0f;
                    Vector3 newHoriz = Vector3.zero;

                    if (currentBounce == 1)
                    {
                        bounceUp = (impactDown * localGround.FirstBounceImpactScale)
                                 + (fwdSpeed   * localGround.ForwardToBounceConversion);

                        float newFwdSpeed = fwdSpeed * localGround.FirstBounceForwardKill;
                        if (appliedSpin.y > 0)  newFwdSpeed *= Mathf.Lerp(1f, physics.MaxTopSpinForwardMultiplier,              appliedSpin.y);
                        else if (appliedSpin.y < 0) newFwdSpeed *= Mathf.Lerp(1f, physics.MaxBackSpinForwardMultiplier, Mathf.Abs(appliedSpin.y));

                        float maxBounce = Mathf.Max(impactDown * 0.45f, 1.2f);
                        if (bounceUp > maxBounce) bounceUp = maxBounce;

                        Vector3 fwdDir = hVel.sqrMagnitude > 0.001f ? hVel.normalized : flightRightDir;
                        if (Mathf.Abs(appliedSpin.x) > 0.01f)
                        {
                            fwdDir    = Quaternion.AngleAxis(appliedSpin.x * 45f, Vector3.up) * fwdDir;
                            newFwdSpeed *= Mathf.Lerp(1f, 0.4f, Mathf.Abs(appliedSpin.x));
                        }
                        newHoriz = fwdDir * newFwdSpeed;
                    }
                    else
                    {
                        lastBounceUpVel *= Mathf.Clamp(localGround.BounceDecayRatio, 0f, 0.9f);
                        bounceUp         = lastBounceUpVel;
                        float retention  = localGround.ForwardRetentionPerBounce;
                        if (appliedSpin.y > 0)  retention *= Mathf.Lerp(1f, physics.MaxTopSpinForwardMultiplier,              appliedSpin.y);
                        else if (appliedSpin.y < 0) retention *= Mathf.Lerp(1f, physics.MaxBackSpinForwardMultiplier, Mathf.Abs(appliedSpin.y));

                        Vector3 fwdDir = hVel.sqrMagnitude > 0.001f ? hVel.normalized : Vector3.forward;
                        if (Mathf.Abs(appliedSpin.x) > 0.01f)
                        {
                            fwdDir    = Quaternion.AngleAxis(appliedSpin.x * 25f, Vector3.up) * fwdDir;
                            retention *= Mathf.Lerp(1f, 0.7f, Mathf.Abs(appliedSpin.x));
                        }
                        newHoriz = fwdDir * (hVel.magnitude * retention);
                    }

                    lastBounceUpVel = bounceUp;
                    velocity        = new Vector3(newHoriz.x, bounceUp, newHoriz.z);
                    position        = nextPos;
                }
                else
                {
                    position = nextPos;
                }
            }

            // Apply primary line (full arc, ignores all objects)
            lineRenderer.positionCount = validSteps;
            lineRenderer.SetPositions(pointsArray);
            lineRenderer.enabled = true;

            // ─────────────────────────────────────────────────────────────────────
            // PASS 2 — Walk the stored primary arc segments and SphereCast for the
            //          first non-terrain physics object hit.
            //          The primary line is NEVER affected by this scan.
            // ─────────────────────────────────────────────────────────────────────
            if (PostCollisionLineRenderer != null)
                DetectAndDrawPostCollisionArc(validSteps, targetHeight, physics, drag);
        }

        public void HideTrajectory()
        {
            lineRenderer.enabled       = false;
            lineRenderer.positionCount = 0;
            HidePostCollisionLine();
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Walks the already-computed primary arc (pointsArray / velocitiesArray)
        /// and fires a SphereCast between each consecutive pair of points.
        /// On the first non-terrain hit, draws the post-collision arc and returns.
        /// If nothing is hit, hides the secondary line.
        /// </summary>
        private void DetectAndDrawPostCollisionArc(
            int primaryStepCount, float targetHeight,
            BallPhysicsController physics, float drag)
        {
            for (int i = 0; i < primaryStepCount - 1; i++)
            {
                Vector3 from = pointsArray[i];
                Vector3 to   = pointsArray[i + 1];
                Vector3 dir  = to - from;
                float   dist = dir.magnitude;

                if (dist < 0.001f) continue;

                if (Physics.SphereCast(from, CollisionDetectionRadius, dir.normalized,
                        out RaycastHit hit, dist, CollisionLayerMask,
                        QueryTriggerInteraction.Ignore))
                {
                    // Skip terrain hits — only non-terrain physics objects count
                    bool isTerrain = hit.collider.GetComponent<Terrain>() != null
                                  || hit.collider.CompareTag("Terrain");
                    if (isTerrain) continue;

                    // Found a real physics object — simulate the post-collision arc
                    SimulatePostCollisionArc(
                        hit.point,
                        hit.normal,
                        velocitiesArray[i],   // velocity of the ball at this step
                        targetHeight,
                        drag);
                    return; // stop scanning after first hit
                }
            }

            // No object in the way — hide the secondary line
            HidePostCollisionLine();
        }

        /// <summary>
        /// Simulates a short arc starting at <paramref name="hitPoint"/>,
        /// reflecting the incoming velocity off <paramref name="hitNormal"/>,
        /// and renders it on <see cref="PostCollisionLineRenderer"/>.
        /// </summary>
        private void SimulatePostCollisionArc(
            Vector3 hitPoint, Vector3 hitNormal, Vector3 incomingVelocity,
            float targetHeight, float drag)
        {
            // Reflect the incoming velocity off the surface normal, lose ~50 % energy
            Vector3 vel = Vector3.Reflect(incomingVelocity, hitNormal) * 0.5f;

            int cap = PostCollisionSteps + 2;
            if (postCollisionPoints == null || postCollisionPoints.Length < cap)
                postCollisionPoints = new Vector3[cap];

            int     validSteps = 0;
            Vector3 pos        = hitPoint;

            for (int i = 0; i < PostCollisionSteps; i++)
            {
                if (validSteps >= postCollisionPoints.Length) break;

                postCollisionPoints[validSteps] = pos;
                validSteps++;

                vel *= Mathf.Clamp01(1f - drag * TimeStep);
                vel += Physics.gravity * TimeStep;

                Vector3 nextPos    = pos + vel * TimeStep;
                float   nextGround = targetHeight;
                if (Terrain.activeTerrain != null)
                    nextGround = Terrain.activeTerrain.SampleHeight(nextPos) + Terrain.activeTerrain.transform.position.y;

                if (vel.y <= 0f && nextPos.y <= nextGround)
                {
                    nextPos.y = nextGround;
                    postCollisionPoints[validSteps] = nextPos;
                    validSteps++;
                    break;
                }

                pos = nextPos;
            }

            PostCollisionLineRenderer.positionCount = validSteps;
            PostCollisionLineRenderer.SetPositions(postCollisionPoints);
            PostCollisionLineRenderer.enabled = true;
        }

        private void HidePostCollisionLine()
        {
            if (PostCollisionLineRenderer == null) return;
            PostCollisionLineRenderer.enabled       = false;
            PostCollisionLineRenderer.positionCount = 0;
        }

        #endregion
    }
}
