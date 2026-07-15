using UnityEngine;

namespace GolfGame.Controllers
{
    public class AimVisualsController : MonoBehaviour
    {
        [Header("Aim Settings")]
        public GameObject TargetMarkerPrefab;
        public float MinTargetDistance = 3f;
        public float MaxAimAngle = 45f;
        public Transform FlagTransform;

        [Header("Visual Settings")]
        public LineRenderer DragLineRenderer;
        public TrailRenderer BallTrail;
        public float DragVisualMultiplier = 0.5f;

        [Header("Putting Settings")]
        [Tooltip("Multiplier for how far the putting line reaches. Increase this in the Inspector to make the line longer.")]
        public float PuttingLineLengthMultiplier = 12f;

        [Header("Line Dynamic Colors")]
        public Color LowForceColor = Color.yellow;
        public Color NormalForceColor = Color.green;
        public Color ExtremeForceColor = Color.red;

        [Header("Trail Shrink Settings")]
        [Tooltip("How fast the trail shrinks when the ball hits the ground.")]
        public float TrailShrinkSpeed = 2f;
        [Tooltip("The final length (time) of the trail after it shrinks.")]
        public float TargetTrailTime = 0f;
        [Tooltip("The final width of the trail after it shrinks.")]
        public float TargetTrailWidth = 0f;

        private float _defaultTrailTime;
        private float _defaultTrailWidth;
        private Coroutine _shrinkCoroutine; // NEW: Track the running coroutine

        [Tooltip("At what overpower ratio (0.0 to 1.0) should the line turn Red?")]
        [Range(0.1f, 1.0f)]
        public float ExtremeForceThreshold = 0.5f;

        private GameObject activeTargetMarker;
        private Vector3 fixedAimDirection = Vector3.forward;
        private Vector3 initialAimDirection = Vector3.forward;
        private TrajectoryPredictor trajectoryPredictor;
        private Camera mainCamera;
        private Vector3 _localCenterOffset;
        private float _ballRadius;
        
        public GameObject ActiveTargetMarker => activeTargetMarker;
        public Vector3 FixedAimDirection => fixedAimDirection;
        public Vector3 InitialAimDirection => initialAimDirection;

        private void Awake()
        {
            // FIX: Force the script to find the active scene flag, overriding any accidentally assigned prefabs.
            GameObject flagObj = GameObject.Find("Flag");
            if (flagObj == null) flagObj = GameObject.FindGameObjectWithTag("Flag");
            
            if (flagObj != null)
            {
                FlagTransform = flagObj.transform;
                Debug.Log("[AimVisuals] Successfully locked onto the active 'Flag' in the scene.");
            }
            else
            {
                Debug.LogWarning("[AimVisuals] No GameObject with the 'Flag' tag was found in the scene!");
            }

            trajectoryPredictor = GetComponent<TrajectoryPredictor>();
            mainCamera = Camera.main;
            
            SphereCollider sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider != null)
            {
                _localCenterOffset = sphereCollider.center;
                // Added a 1.15x padding multiplier to clear the line's thickness
                _ballRadius = sphereCollider.radius * Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z) * 1.15f;
            }

            // Cache default trail values for resetting
            if (BallTrail != null)
            {
                _defaultTrailTime = BallTrail.time;
                _defaultTrailWidth = BallTrail.widthMultiplier;
            }
        }

        private void Start()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnStateEnter += OnStateEnter;
            }
        }

        private void OnDestroy()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnStateEnter -= OnStateEnter;
            }
            if (activeTargetMarker != null) Destroy(activeTargetMarker);
        }

        private void OnStateEnter(GameStateManager.GameState newState)
        {
            if (_shrinkCoroutine != null)
            {
                StopCoroutine(_shrinkCoroutine);
                _shrinkCoroutine = null;
            }

            if (newState == GameStateManager.GameState.Setup)
            {

                if (BallTrail != null) 
                {
                    BallTrail.time = _defaultTrailTime;
                    BallTrail.widthMultiplier = _defaultTrailWidth;
                    BallTrail.emitting = false;
                    BallTrail.Clear(); // Completely clear old trail segments
                }
                HideDragLine(); // FORCE HIDE DRAG LINE HERE
            }
            else if (newState == GameStateManager.GameState.Aiming)
            {
                if (trajectoryPredictor != null) trajectoryPredictor.HideTrajectory();
                if (activeTargetMarker != null) activeTargetMarker.SetActive(false);
                
                if (BallTrail != null) 
                {
                    BallTrail.time = _defaultTrailTime;
                    BallTrail.widthMultiplier = _defaultTrailWidth;
                    BallTrail.emitting = false;
                    BallTrail.Clear(); // Clear trail segments here too
                }
                HideDragLine(); // FORCE HIDE DRAG LINE HERE
            }
            else if (newState == GameStateManager.GameState.Flight)
            {
                if (activeTargetMarker != null) activeTargetMarker.SetActive(false);
                
                bool isPutting = false;
                BallPhysicsController physics = GetComponent<BallPhysicsController>();
                if (physics != null && physics.CurrentGround != null)
                {
                    isPutting = physics.CurrentGround.IsNiceOn;
                }

                if (BallTrail != null) 
                {
                    BallTrail.Clear(); // Clear right before flight begins
                    BallTrail.emitting = !isPutting;
                }
            }
        }

        public void ShrinkTrailOnBounce()
        {
            if (BallTrail != null && gameObject.activeInHierarchy)
            {
                // Stop any existing shrink command before starting a new one
                if (_shrinkCoroutine != null)
                {
                    StopCoroutine(_shrinkCoroutine);
                }
                _shrinkCoroutine = StartCoroutine(ShrinkTrailCoroutine());
            }
        }

        private System.Collections.IEnumerator ShrinkTrailCoroutine()
        {
            // Smoothly reduce both length (time) and width until they hit the targets
            while (BallTrail.time > TargetTrailTime || BallTrail.widthMultiplier > TargetTrailWidth)
            {
                BallTrail.time = Mathf.MoveTowards(BallTrail.time, TargetTrailTime, Time.deltaTime * TrailShrinkSpeed);
                BallTrail.widthMultiplier = Mathf.MoveTowards(BallTrail.widthMultiplier, TargetTrailWidth, Time.deltaTime * TrailShrinkSpeed);
                yield return null;
            }
        }

        public void SpawnTargetMarker(float maxRange)
        {
            if (TargetMarkerPrefab != null && activeTargetMarker == null)
            {
                float targetDistance = maxRange * 0.7f; 
                
                if (FlagTransform == null)
                {
                    GameObject flagObj = GameObject.Find("Flag");
                    if (flagObj == null) flagObj = GameObject.FindGameObjectWithTag("Flag");
                    if (flagObj != null) FlagTransform = flagObj.transform;
                }

                if (FlagTransform != null)
                {
                    Vector3 toFlag = FlagTransform.position - transform.position;
                    toFlag.y = 0f; 
                    if (toFlag.sqrMagnitude > 0.001f)
                    {
                        fixedAimDirection = toFlag.normalized;
                        if (toFlag.magnitude < targetDistance)
                        {
                            targetDistance = toFlag.magnitude;
                        }
                    }
                }
                
                string debugInfo = $"Ball Pos: {transform.position}\nFlag Pos: {(FlagTransform != null ? FlagTransform.position.ToString() : "NULL")}\nAimDir: {fixedAimDirection}\nTargetDist: {targetDistance}\nMaxRange: {maxRange}";
                System.IO.File.WriteAllText("debug_golf.txt", debugInfo);

                targetDistance = Mathf.Clamp(targetDistance, MinTargetDistance, maxRange);

                Vector3 spawnPos = transform.position + fixedAimDirection * targetDistance;
    
                // FIX: Snap the marker to the actual ground terrain instead of hardcoding 0f
                if (Physics.Raycast(spawnPos + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f))
                {
                    spawnPos.y = hit.point.y;
                }
                else
                {
                    spawnPos.y = transform.position.y; // Fallback to ball height
                }
                
                Debug.Log($"[DEBUG Spawn] Final Spawn Pos: {spawnPos}");
                
                Quaternion spawnRot = fixedAimDirection.sqrMagnitude > 0.001f ? Quaternion.LookRotation(fixedAimDirection) : Quaternion.identity;
                activeTargetMarker = Instantiate(TargetMarkerPrefab, spawnPos, spawnRot);
                //spawnPos.y = 0f;
            }
            
            if (activeTargetMarker != null)
            {
                activeTargetMarker.SetActive(true);
                Vector3 diff = activeTargetMarker.transform.position - transform.position;
                fixedAimDirection = new Vector3(diff.x, 0f, diff.z).normalized;
                initialAimDirection = fixedAimDirection;
            }
        }

        public void RepositionTargetMarker(float maxRange)
        {
            if (activeTargetMarker != null)
            {
                float targetDistance = maxRange * 0.7f; 
                
                if (FlagTransform == null)
                {
                    GameObject flagObj = GameObject.Find("Flag");
                    if (flagObj == null) flagObj = GameObject.FindGameObjectWithTag("Flag");
                    if (flagObj != null) FlagTransform = flagObj.transform;
                }

                if (FlagTransform != null)
                {
                    Vector3 toFlag = FlagTransform.position - transform.position;
                    toFlag.y = 0f;
                    if (toFlag.sqrMagnitude > 0.001f)
                    {
                        fixedAimDirection = toFlag.normalized;
                        if (toFlag.magnitude < targetDistance)
                        {
                            targetDistance = toFlag.magnitude;
                        }
                    }
                }

                targetDistance = Mathf.Clamp(targetDistance, MinTargetDistance, maxRange);

                Vector3 newPos = transform.position + fixedAimDirection * targetDistance;
    
                // FIX: Apply the same terrain-snapping fix here
                if (Physics.Raycast(newPos + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f))
                {
                    newPos.y = hit.point.y;
                }
                else
                {
                    newPos.y = transform.position.y; // Fallback to ball height
                }
                
                activeTargetMarker.transform.position = newPos;
                if (fixedAimDirection.sqrMagnitude > 0.001f)
                {
                    activeTargetMarker.transform.rotation = Quaternion.LookRotation(fixedAimDirection);
                }
                activeTargetMarker.SetActive(true);
                
                initialAimDirection = fixedAimDirection;
                //newPos.y = 0f;
            }
        }

        public void UpdateAimDirection(Vector3 newDir)
        {
            fixedAimDirection = newDir;
        }

        public void ShowTrajectory(Vector3 launchVelocity)
        {
            if (trajectoryPredictor != null && activeTargetMarker != null)
            {
                // NEW: Push the starting point out by the ball's radius
                Vector3 edgeStartPos = transform.position + (launchVelocity.normalized * _ballRadius);
                trajectoryPredictor.ShowTrajectory(edgeStartPos, launchVelocity, activeTargetMarker.transform.position.y);
            }
        }

        public void HideTrajectory()
        {
            if (trajectoryPredictor != null) trajectoryPredictor.HideTrajectory();
        }

        public void UpdateDragLine(float dragMagnitude, float overpowerRatio, Vector3 startPos)
        {
            // NEW SAFETY: If the player barely moved the mouse/finger, hide the line instantly
            if (dragMagnitude < 0.05f)
            {
                HideDragLine();
                return;
            }

            if (DragLineRenderer == null) return;
            DragLineRenderer.enabled = true;

            Vector3 centerStartPos = transform.TransformPoint(_localCenterOffset);

            Color activeColor = LowForceColor; 
            if (overpowerRatio >= ExtremeForceThreshold) activeColor = ExtremeForceColor;
            else if (overpowerRatio > 0) activeColor = NormalForceColor;
                
            UpdateDragLineColor(activeColor);

            // FIXED: Pull straight back relative to where we are aiming, not absolute world Z
            Vector3 visualPullBackDir = -fixedAimDirection; 
            
            // Fallback just in case aim direction is completely zeroed out
            if (visualPullBackDir.sqrMagnitude < 0.001f) 
            {
                visualPullBackDir = Vector3.back;
            }
            
            // Push the starting point out by the radius so it doesn't sit inside the mesh
            Vector3 edgeStartPos = centerStartPos + (visualPullBackDir * _ballRadius);
            
            Vector3 endPoint = centerStartPos + (visualPullBackDir * (dragMagnitude * DragVisualMultiplier));
            endPoint.y = centerStartPos.y;
            
            DragLineRenderer.SetPosition(0, edgeStartPos); 
            DragLineRenderer.SetPosition(1, endPoint);
        }

        public void UpdatePuttingLine(float dragMagnitude, float overpowerRatio, Vector3 startPos, float dragAngle)
        {
            // NEW SAFETY: Hide line if drag magnitude is effectively zero
            if (dragMagnitude < 0.05f)
            {
                HideDragLine();
                return;
            }

            if (DragLineRenderer == null) return;
            DragLineRenderer.enabled = true;

            Vector3 centerStartPos = transform.TransformPoint(_localCenterOffset);

            Color activeColor = LowForceColor; 
            if (overpowerRatio >= ExtremeForceThreshold) activeColor = ExtremeForceColor;
            else if (overpowerRatio > 0) activeColor = NormalForceColor;
                
            UpdateDragLineColor(activeColor);

            Vector3 baseForwardDir = Vector3.forward;
            if (FlagTransform != null)
            {
                Vector3 toFlag = FlagTransform.position - centerStartPos;
                baseForwardDir = new Vector3(toFlag.x, 0f, toFlag.z).normalized;
            }
            
            Vector3 finalAimDir = Quaternion.Euler(0f, dragAngle, 0f) * baseForwardDir;
            
            // NEW: Push the starting point out by the radius
            Vector3 edgeStartPos = centerStartPos + (finalAimDir * _ballRadius);
            
            float puttingVisualMultiplier = DragVisualMultiplier * PuttingLineLengthMultiplier; 
            
            Vector3 endPoint = centerStartPos + (finalAimDir * (dragMagnitude * puttingVisualMultiplier));
            endPoint.y = centerStartPos.y;
            
            DragLineRenderer.SetPosition(0, edgeStartPos); // Updated this line
            DragLineRenderer.SetPosition(1, endPoint);
        }

        public void HideDragLine()
        {
            if (DragLineRenderer != null) DragLineRenderer.enabled = false;
        }

        private void UpdateDragLineColor(Color baseColor)
        {
            if (DragLineRenderer == null) return;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(baseColor, 0.0f), new GradientColorKey(baseColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(150f / 255f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            DragLineRenderer.colorGradient = gradient;
        }
    }
}