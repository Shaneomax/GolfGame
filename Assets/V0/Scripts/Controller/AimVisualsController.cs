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
        
        public GameObject ActiveTargetMarker => activeTargetMarker;
        public Vector3 FixedAimDirection => fixedAimDirection;
        public Vector3 InitialAimDirection => initialAimDirection;

        private void Awake()
        {
            trajectoryPredictor = GetComponent<TrajectoryPredictor>();
            mainCamera = Camera.main;
            
            SphereCollider sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider != null)
            {
                _localCenterOffset = sphereCollider.center;
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

            // Permanently lock the trail renderer to the true center of the ball
            if (BallTrail != null)
            {
                BallTrail.transform.localPosition = _localCenterOffset;
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
            // NEW: Always stop the shrink coroutine when changing states so it doesn't fight the reset
            if (_shrinkCoroutine != null)
            {
                StopCoroutine(_shrinkCoroutine);
                _shrinkCoroutine = null;
            }

            if (newState == GameStateManager.GameState.Setup)
            {
                if (BallTrail != null) 
                {
                    // Reset to default sizes before the next shot
                    BallTrail.time = _defaultTrailTime;
                    BallTrail.widthMultiplier = _defaultTrailWidth;
                    
                    BallTrail.emitting = false;
                    BallTrail.Clear(); 
                }
            }
            else if (newState == GameStateManager.GameState.Aiming)
            {
                // NEW: Also reset the trail here! 
                // If the ball stops on the green, it skips "Setup" and goes straight here.
                if (BallTrail != null) 
                {
                    BallTrail.time = _defaultTrailTime;
                    BallTrail.widthMultiplier = _defaultTrailWidth;
                    BallTrail.emitting = false;
                }

                if (trajectoryPredictor != null) trajectoryPredictor.HideTrajectory();
                if (activeTargetMarker != null) activeTargetMarker.SetActive(false);
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

                // Only emit trail if we are NOT putting
                if (BallTrail != null) BallTrail.emitting = !isPutting;
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
                
                if (FlagTransform != null)
                {
                    Vector3 toFlag = FlagTransform.position - transform.position;
                    toFlag.y = 0f; 
                    fixedAimDirection = toFlag.normalized;
                    
                    if (toFlag.magnitude < targetDistance)
                    {
                        targetDistance = toFlag.magnitude;
                    }
                }

                targetDistance = Mathf.Clamp(targetDistance, MinTargetDistance, maxRange);

                Vector3 spawnPos = transform.position + fixedAimDirection * targetDistance;
                spawnPos.y = 0f;
                activeTargetMarker = Instantiate(TargetMarkerPrefab, spawnPos, Quaternion.identity);
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
                
                if (FlagTransform != null)
                {
                    Vector3 toFlag = FlagTransform.position - transform.position;
                    toFlag.y = 0f;
                    fixedAimDirection = toFlag.normalized;
                    
                    if (toFlag.magnitude < targetDistance)
                    {
                        targetDistance = toFlag.magnitude;
                    }
                }

                targetDistance = Mathf.Clamp(targetDistance, MinTargetDistance, maxRange);

                Vector3 newPos = transform.position + fixedAimDirection * targetDistance;
                newPos.y = 0f;
                activeTargetMarker.transform.position = newPos;
                activeTargetMarker.SetActive(true);
                
                initialAimDirection = fixedAimDirection; 
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
                trajectoryPredictor.ShowTrajectory(transform.position, launchVelocity, activeTargetMarker.transform.position.y);
            }
        }

        public void HideTrajectory()
        {
            if (trajectoryPredictor != null) trajectoryPredictor.HideTrajectory();
        }

        public void UpdateDragLine(float dragMagnitude, float overpowerRatio, Vector3 startPos)
        {
            if (DragLineRenderer == null) return;
            DragLineRenderer.enabled = true;

            // Shift the start position to the true center of the ball
            Vector3 centerStartPos = transform.TransformPoint(_localCenterOffset);

            Color activeColor = LowForceColor; 
            if (overpowerRatio >= ExtremeForceThreshold)
                activeColor = ExtremeForceColor;
            else if (overpowerRatio > 0)
                activeColor = NormalForceColor;
                
            UpdateDragLineColor(activeColor);

            Vector3 visualPullBackDir = Vector3.back; 
            
            Vector3 endPoint = centerStartPos + (visualPullBackDir * (dragMagnitude * DragVisualMultiplier));
            endPoint.y = centerStartPos.y;
            
            DragLineRenderer.SetPosition(0, centerStartPos);
            DragLineRenderer.SetPosition(1, endPoint);
        }

        public void UpdatePuttingLine(float dragMagnitude, float overpowerRatio, Vector3 startPos, float dragAngle)
        {
            if (DragLineRenderer == null) return;
            DragLineRenderer.enabled = true;

            // Shift the start position to the true center of the ball
            Vector3 centerStartPos = transform.TransformPoint(_localCenterOffset);

            Color activeColor = LowForceColor; 
            if (overpowerRatio >= ExtremeForceThreshold)
                activeColor = ExtremeForceColor;
            else if (overpowerRatio > 0)
                activeColor = NormalForceColor;
                
            UpdateDragLineColor(activeColor);

            Vector3 baseForwardDir = Vector3.forward;
            if (FlagTransform != null)
            {
                Vector3 toFlag = FlagTransform.position - centerStartPos;
                baseForwardDir = new Vector3(toFlag.x, 0f, toFlag.z).normalized;
            }
            
            // Rotate the base direction based on the player's drag (slingshot aim)
            Vector3 finalAimDir = Quaternion.Euler(0f, dragAngle, 0f) * baseForwardDir;
            
            // Use the new exposed variable
            float puttingVisualMultiplier = DragVisualMultiplier * PuttingLineLengthMultiplier; 
            
            Vector3 endPoint = centerStartPos + (finalAimDir * (dragMagnitude * puttingVisualMultiplier));
            endPoint.y = centerStartPos.y;
            
            DragLineRenderer.SetPosition(0, centerStartPos);
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