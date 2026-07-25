using UnityEngine;

namespace GolfGame.Controllers
{
    public class ShotAccuracyController : MonoBehaviour
    {
        [Header("References")]
        public GameObject ArrowIndicator;
        public Transform NeedlePivot;

        [Header("Needle Sweep")]
        public float NeedleMinAngle = 0f;
        public float NeedleMaxAngle = 180f;

        [Header("Shot Deviation")]
        public float DeviationMultiplier = 22f;

        [Header("Game Balancing (Tweak These!)")]
        public float BaseHalfSwingDuration = 0.8f;
        public float MaxSpeedMultiplier = 4f;
        
        [Tooltip("Universally speeds up or slows down the needle. Test this in the inspector while aiming!")]
        [Range(0.1f, 3f)]
        public float GlobalNeedleSpeed = 0.32f;

        [Header("Drag Power Difficulty")]
        [Tooltip("Match this to the ExtremeForceThreshold in your AimVisualsController (default is 0.5).")]
        public float ExtremeForceThreshold = 0.5f;

        [Tooltip("How much faster the needle goes during a normal overpower (Yellow/Green).")]
        public float OverpowerSpeedMultiplier = 1.5f;

        [Tooltip("How much faster the needle goes when drag reaches the RED zone. Set high (e.g., 4) for a massive speed increase!")]
        public float RedZoneSpeedMultiplier = 4.0f;

        public float LockedAccuracyValue { get; private set; }
        public bool IsLocked { get; private set; }

        private ClubData _currentClub;
        private float _swingProgress = 0f;
        private float _baseGlobalNeedleSpeed;
        private int _sweepDirection = 1;

        private void Start()
        {
            _baseGlobalNeedleSpeed = GlobalNeedleSpeed;
            if (ArrowIndicator != null) ArrowIndicator.SetActive(false);
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnStateEnter += OnStateEnter;
        }

        private void OnDestroy()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnStateEnter -= OnStateEnter;
        }

        private void OnStateEnter(GameStateManager.GameState newState)
        {
            bool isPutting = false;
            var physics = FindObjectOfType<BallPhysicsController>();
            if (physics != null && physics.CurrentGround != null)
            {
                isPutting = physics.CurrentGround.IsNiceOn;
            }

            if (newState == GameStateManager.GameState.Aiming && !isPutting) 
                ActivateArrow();
            else 
                DeactivateArrow();
        }

        private void ActivateArrow()
        {
            IsLocked = false;
            LockedAccuracyValue = 0f;
            _swingProgress = 0.5f; // Start the needle in the middle
            _sweepDirection = 1;
            GlobalNeedleSpeed = _baseGlobalNeedleSpeed;

            if (ArrowIndicator != null) ArrowIndicator.SetActive(true);
        }

        private void DeactivateArrow()
        {
            if (ArrowIndicator != null) ArrowIndicator.SetActive(false);
        }

        private void Update()
        {
            if (!IsLocked && ArrowIndicator != null && ArrowIndicator.activeSelf && NeedlePivot != null)
            {
                float accuracyStat = _currentClub != null ? _currentClub.Accuracy : 50f;
                float t = 1f - Mathf.Clamp01(accuracyStat / 100f);
                
                float speedMultiplier = Mathf.Lerp(1f, MaxSpeedMultiplier, t);
                
                // Combine all speed factors dynamically
                float currentHalfDuration = BaseHalfSwingDuration / (speedMultiplier * GlobalNeedleSpeed);
                
                // Calculate how much progress to add this exact frame
                float progressSpeed = 1f / currentHalfDuration;

                // Move the needle
                _swingProgress += _sweepDirection * progressSpeed * Time.deltaTime;

                // Bounce back and forth between 0 and 1
                if (_swingProgress >= 1f)
                {
                    _swingProgress = 1f;
                    _sweepDirection = -1;
                }
                else if (_swingProgress <= 0f)
                {
                    _swingProgress = 0f;
                    _sweepDirection = 1;
                }

                UpdateNeedleVisuals();
            }
        }

        private void UpdateNeedleVisuals()
        {
            if (NeedlePivot != null)
            {
                // Swap NeedleMaxAngle and NeedleMinAngle here
                float currentAngle = Mathf.Lerp(NeedleMaxAngle, NeedleMinAngle, _swingProgress);
                NeedlePivot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
            }
        }

        public void LockAccuracy()
        {
            if (IsLocked) return;
            IsLocked = true;
            LockedAccuracyValue = (_swingProgress - 0.5f) * 2f;
        }

        public void ForcePerfectAccuracy()
        {
            IsLocked = true;
            LockedAccuracyValue = 0f;
            // No UpdateNeedleVisuals() so needle stays where it is!
        }

        public void ResetLock()
        {
            IsLocked = false;
            LockedAccuracyValue = 0f;
        }

        public void SetClub(ClubData club) => _currentClub = club;

        public void SetDragPowerMultiplier(float overpowerRatio, float? customThreshold = null)
        {
            float threshold = customThreshold ?? ExtremeForceThreshold;
            float multiplier = 1f;

            // Instantly updates the multiplier used in the Update() loop
            if (overpowerRatio >= threshold)
            {
                // RED ZONE: Instantly snap to the high-speed multiplier
                multiplier = RedZoneSpeedMultiplier;
            }
            else if (overpowerRatio > 0f)
            {
                // NORMAL OVERPOWER: Slightly scale up speed
                multiplier = Mathf.Lerp(1f, OverpowerSpeedMultiplier, overpowerRatio / threshold);
            }
            
            // Directly modify GlobalNeedleSpeed so the value visibly changes in the Unity Inspector
            GlobalNeedleSpeed = _baseGlobalNeedleSpeed * multiplier;
        }
    }
}