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

        // Cached last-known-good trajectory to prevent flickering from non-deterministic ghost physics
        private Vector3[] cachedGoodTrajectory;
        private Vector3 lastStartPosition;
        private Vector3 lastLaunchVelocity;

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
            if (lineRenderer == null) return;
            
            Vector2 appliedSpin = GolfGame.UI.SpinInputUI.GlobalCurrentSpin;
            
            // Match PlayerInputController exactly: Curl should be perpendicular to the target, not the offset launch velocity!
            Vector3 flightRightDir;
            AimVisualsController aimVisuals = GetComponent<AimVisualsController>();
            if (aimVisuals != null && aimVisuals.ActiveTargetMarker != null)
            {
                Vector3 toTarget = aimVisuals.ActiveTargetMarker.transform.position - transform.position;
                Vector3 straightFlatDir = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
                flightRightDir = Vector3.Cross(Vector3.up, straightFlatDir).normalized;
            }
            else
            {
                flightRightDir = Vector3.Cross(Vector3.up, new Vector3(launchVelocity.x, 0, launchVelocity.z)).normalized;
            }

            if (PhysicsSimulator.Instance == null)
            {
                GameObject simObj = new GameObject("PhysicsSimulator");
                simObj.AddComponent<PhysicsSimulator>();
            }

            if (PhysicsSimulator.Instance != null)
            {
                // Enforce safe minimums in case the Inspector cached the old values
                int safeSteps = Mathf.Max(SimulationSteps, 500);
                float safeTimeStep = 0.02f; // Force this to match FixedDeltaTime for accurate friction!

                PhysicsSimulator.Instance.SimulateTrajectory(
                    this.gameObject,
                    startPosition,
                    launchVelocity,
                    appliedSpin,
                    flightRightDir,
                    safeSteps,
                    safeTimeStep,
                    out pointsArray,
                    out velocitiesArray,
                    out bool landedOnGreen,
                    out bool hasBounced
                );
                
                lineRenderer.positionCount = pointsArray.Length;
                lineRenderer.SetPositions(pointsArray);
                lineRenderer.enabled = true;

                // ── TRAJECTORY STABILITY CHECK ──────────────────────────────
                // The ghost physics is non-deterministic: on some frames the ghost ball
                // clips the edge of a collider (e.g. the NiceOn Cube's side wall) and 
                // bounces sideways, producing a short false trajectory. On the next frame
                // it doesn't clip, producing the correct long arc. This causes the 
                // lineRenderer to flicker between two wildly different paths.
                //
                // Fix: If we have a cached good trajectory from the previous frame with
                // the SAME launch parameters, and the new trajectory endpoint is wildly
                // different (>50% shorter), use the cached one instead.
                bool useCached = false;
                if (cachedGoodTrajectory != null && cachedGoodTrajectory.Length > 2 &&
                    pointsArray.Length > 2 &&
                    Vector3.Distance(lastStartPosition, startPosition) < 0.01f &&
                    Vector3.Distance(lastLaunchVelocity, launchVelocity) < 0.1f)
                {
                    float cachedDist = Vector3.Distance(cachedGoodTrajectory[0], cachedGoodTrajectory[cachedGoodTrajectory.Length - 1]);
                    float newDist = Vector3.Distance(pointsArray[0], pointsArray[pointsArray.Length - 1]);
                    
                    // If the new trajectory is drastically shorter, it's a false bounce
                    if (newDist < cachedDist * 0.5f)
                    {
                        useCached = true;
                    }
                }

                if (useCached)
                {
                    lineRenderer.positionCount = cachedGoodTrajectory.Length;
                    lineRenderer.SetPositions(cachedGoodTrajectory);
                }
                else
                {
                    // This is a good trajectory, cache it
                    cachedGoodTrajectory = (Vector3[])pointsArray.Clone();
                }

                lastStartPosition = startPosition;
                lastLaunchVelocity = launchVelocity;

                // ── LANDING SURFACE DEBUG LOG ──────────────────────────────────
                // Fires a downward Raycast from the last point of the arc
                // to reveal exactly what surface/layer the line is landing on.
                // Uses QueryTriggerInteraction.Ignore so triggers (like the Flag) don't pollute the log.
                if (pointsArray.Length > 0)
                {
                    Vector3 lastPt = pointsArray[pointsArray.Length - 1];
                    Vector3 rayOrigin = lastPt + Vector3.up * 0.5f;
                    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit landHit, 5f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        Debug.Log($"[TrajLanding] Object='{landHit.collider.gameObject.name}'" +
                                  $" | Tag='{landHit.collider.gameObject.tag}'" +
                                  $" | Layer='{LayerMask.LayerToName(landHit.collider.gameObject.layer)}'" +
                                  $" | ColliderType='{landHit.collider.GetType().Name}'");
                    }
                    else
                    {
                        Debug.Log($"[TrajLanding] No surface found below last point {lastPt}");
                    }
                }
                // ─────────────────────────────────────────────────────────────
                
                BallPhysicsController physics = GetComponent<BallPhysicsController>();
                float drag = physics != null && physics.DefaultGround != null ? physics.DefaultGround.LinearDrag : 0.5f;

                DetectAndDrawPostCollisionArc(pointsArray.Length, targetHeight, physics, drag);
            }
            else
            {
                lineRenderer.enabled = false;
                HidePostCollisionLine();
            }
        }

        public void HideTrajectory()
        {
            lineRenderer.enabled       = false;
            lineRenderer.positionCount = 0;
            HidePostCollisionLine();
            cachedGoodTrajectory = null; // Clear cache so stale data doesn't persist across aim changes
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
            // Grab the target marker so we can skip it — the trajectory flies
            // straight toward it, so its Box Collider would always be "hit" otherwise.
            AimVisualsController aimVisuals = GetComponent<AimVisualsController>();
            GameObject targetMarker = aimVisuals != null ? aimVisuals.ActiveTargetMarker : null;

            float distanceFromStart = 0f;

            for (int i = 0; i < primaryStepCount - 1; i++)
            {
                Vector3 from = pointsArray[i];
                Vector3 to   = pointsArray[i + 1];
                Vector3 dir  = to - from;
                float   dist = dir.magnitude;

                distanceFromStart += dist;
                if (dist < 0.001f) continue;
                
                // CRITICAL FIX: Skip the first 0.5 meters of the trajectory to prevent the SphereCast 
                // from instantly hitting the player model, golf club, or tee and reflecting sideways!
                if (distanceFromStart < 0.5f) continue;

                // ── Height guard ──────────────────────────────────────────────────
                // Skip segments at or below the terrain surface (bounce snap points).
                float terrainYAtFrom = targetHeight;
                if (Terrain.activeTerrain != null)
                    terrainYAtFrom = Terrain.activeTerrain.SampleHeight(from)
                                   + Terrain.activeTerrain.transform.position.y;

                if (from.y < terrainYAtFrom + CollisionDetectionRadius * 2f) continue;

                // ── SphereCast ────────────────────────────────────────────────────
                if (Physics.SphereCast(from, CollisionDetectionRadius, dir.normalized,
                        out RaycastHit hit, dist, CollisionLayerMask,
                        QueryTriggerInteraction.Ignore))
                {
                    // ── Skip aiming helpers and the ball itself ───────────────
                    // The trajectory shouldn't bounce off aiming indicators, camera pivots, or the real golf ball.
                    string objName = hit.collider.gameObject.name;
                    if (hit.collider.GetComponent<BallPhysicsController>() != null ||
                        hit.collider.GetComponentInParent<BallPhysicsController>() != null ||
                        objName.IndexOf("Ball", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        objName.Contains("Marker") || 
                        objName.Contains("Pivot"))
                    {
                        continue;
                    }

                    // ── Skip terrain, putting greens, and flags ────────────────────────────────
                    bool isTerrain = hit.collider is TerrainCollider
                                  || hit.collider.GetComponent<TerrainCollider>() != null
                                  || hit.collider.GetComponent<Terrain>() != null;

                    Transform checkTransform = hit.collider.transform;
                    while (checkTransform != null && !isTerrain)
                    {
                        string tName = checkTransform.name;
                        if (checkTransform.CompareTag("Terrain") || 
                            checkTransform.CompareTag("NiceOn") ||
                            checkTransform.CompareTag("Flag") ||
                            LayerMask.LayerToName(checkTransform.gameObject.layer) == "NiceOn" ||
                            LayerMask.LayerToName(checkTransform.gameObject.layer) == "Terrain" ||
                            tName.IndexOf("Hole", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            tName.IndexOf("Cup", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            isTerrain = true;
                            break;
                        }
                        checkTransform = checkTransform.parent;
                    }
                    
                    if (isTerrain)
                    {
                        continue;
                    }

                    // Found a real non-terrain, non-marker physics object — this WILL draw the PostCollisionLine!
                    Debug.Log($"[TrajPostCol] DRAWING post-collision arc! Hit Object='{objName}'" +
                              $" | Tag='{hit.collider.gameObject.tag}'" +
                              $" | Layer='{LayerMask.LayerToName(hit.collider.gameObject.layer)}'" +
                              $" | ColliderType='{hit.collider.GetType().Name}'" +
                              $" | Step={i}/{primaryStepCount}");
                    SimulatePostCollisionArc(
                        hit.point,
                        hit.normal,
                        velocitiesArray[i],
                        targetHeight,
                        drag);
                    return;
                }
            }

            // Nothing in the way — hide the secondary line
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
