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

        [Tooltip("At what overpower ratio (0.0 to 1.0) should the line turn Red?")]
        [Range(0.1f, 1.0f)]
        public float ExtremeForceThreshold = 0.5f;

        private GameObject activeTargetMarker;
        private Vector3 fixedAimDirection = Vector3.forward;
        private Vector3 initialAimDirection = Vector3.forward;
        private TrajectoryPredictor trajectoryPredictor;
        private Camera mainCamera;
        
        public GameObject ActiveTargetMarker => activeTargetMarker;
        public Vector3 FixedAimDirection => fixedAimDirection;
        public Vector3 InitialAimDirection => initialAimDirection;

        private void Awake()
        {
            trajectoryPredictor = GetComponent<TrajectoryPredictor>();
            mainCamera = Camera.main;
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
            if (newState == GameStateManager.GameState.Setup)
            {
                if (BallTrail != null) 
                {
                    BallTrail.emitting = false;
                    BallTrail.Clear(); 
                }
            }
            else if (newState == GameStateManager.GameState.Aiming)
            {
                if (trajectoryPredictor != null) trajectoryPredictor.HideTrajectory();
                if (activeTargetMarker != null) activeTargetMarker.SetActive(false);
                if (BallTrail != null) BallTrail.emitting = false;
            }
            else if (newState == GameStateManager.GameState.Flight)
            {
                if (activeTargetMarker != null) activeTargetMarker.SetActive(false);
                if (BallTrail != null) BallTrail.emitting = true;
                
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

            Color activeColor = LowForceColor; 
            if (overpowerRatio >= ExtremeForceThreshold)
                activeColor = ExtremeForceColor;
            else if (overpowerRatio > 0)
                activeColor = NormalForceColor;
                
            UpdateDragLineColor(activeColor);

            Vector3 visualPullBackDir = Vector3.back; 
            
            Vector3 endPoint = startPos + (visualPullBackDir * (dragMagnitude * DragVisualMultiplier));
            endPoint.y = startPos.y;
            
            DragLineRenderer.SetPosition(0, startPos);
            DragLineRenderer.SetPosition(1, endPoint);
        }

        public void UpdatePuttingLine(float dragMagnitude, float overpowerRatio, Vector3 startPos, float dragAngle)
        {
            if (DragLineRenderer == null) return;
            DragLineRenderer.enabled = true;

            Color activeColor = LowForceColor; 
            if (overpowerRatio >= ExtremeForceThreshold)
                activeColor = ExtremeForceColor;
            else if (overpowerRatio > 0)
                activeColor = NormalForceColor;
                
            UpdateDragLineColor(activeColor);

            Vector3 baseForwardDir = Vector3.forward;
            if (FlagTransform != null)
            {
                Vector3 toFlag = FlagTransform.position - startPos;
                baseForwardDir = new Vector3(toFlag.x, 0f, toFlag.z).normalized;
            }
            
            // Rotate the base direction based on the player's drag (slingshot aim)
            Vector3 finalAimDir = Quaternion.Euler(0f, dragAngle, 0f) * baseForwardDir;
            
            // Use the new exposed variable
            float puttingVisualMultiplier = DragVisualMultiplier * PuttingLineLengthMultiplier; 
            
            Vector3 endPoint = startPos + (finalAimDir * (dragMagnitude * puttingVisualMultiplier));
            endPoint.y = startPos.y;
            
            DragLineRenderer.SetPosition(0, startPos);
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