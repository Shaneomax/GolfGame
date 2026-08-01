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
                    out velocitiesArray
                );
                
                lineRenderer.positionCount = pointsArray.Length;
                lineRenderer.SetPositions(pointsArray);
                lineRenderer.enabled = true;
                
                BallPhysicsController physics = GetComponent<BallPhysicsController>();
                float drag = physics != null && physics.DefaultGround != null ? physics.DefaultGround.LinearDrag : 0.5f;

                if (PostCollisionLineRenderer != null && physics != null)
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

            for (int i = 0; i < primaryStepCount - 1; i++)
            {
                Vector3 from = pointsArray[i];
                Vector3 to   = pointsArray[i + 1];
                Vector3 dir  = to - from;
                float   dist = dir.magnitude;

                if (dist < 0.001f) continue;

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
                    // ── Skip the ball itself ──────────────────────────────────────
                    if (hit.collider.gameObject == gameObject) continue;

                    // ── Skip the target marker and all its children ───────────────
                    // The trajectory points at the marker — its Box Collider would
                    // always be detected without this exclusion.
                    if (targetMarker != null &&
                        (hit.collider.gameObject == targetMarker ||
                         hit.collider.transform.IsChildOf(targetMarker.transform)))
                        continue;

                    // ── Skip terrain & putting greens ──────────────────────────────────────────────
                    // TerrainCollider is the definitive Unity terrain physics type.
                    bool isTerrain = hit.collider is TerrainCollider
                                  || hit.collider.GetComponent<TerrainCollider>() != null
                                  || hit.collider.GetComponent<Terrain>() != null
                                  || hit.collider.CompareTag("Terrain")
                                  || hit.collider.CompareTag("NiceOn")
                                  || LayerMask.LayerToName(hit.collider.gameObject.layer) == "NiceOn"
                                  || LayerMask.LayerToName(hit.collider.gameObject.layer) == "Terrain"; // Ignore the green so it doesn't trigger a "rock bounce" line
                    if (isTerrain) continue;

                    // Found a real non-terrain, non-marker physics object
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
