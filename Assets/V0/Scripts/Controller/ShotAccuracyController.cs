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
        [Tooltip("Maximum extra speed multiplier applied to the needle at maximum drag overpower.")]
        public float MaxDragSpeedBonus = 3f;

        public float LockedAccuracyValue { get; private set; }
        public bool IsLocked { get; private set; }

        private ClubData _currentClub;
        private float _swingProgress = 0f;
        private float _dragPowerMultiplier = 1f;
        private int _sweepDirection = 1;

        private void Start()
        {
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
            if (newState == GameStateManager.GameState.Aiming) ActivateArrow();
            else DeactivateArrow();
        }

        private void ActivateArrow()
        {
            IsLocked = false;
            LockedAccuracyValue = 0f;
            _swingProgress = 0.5f; // Start the needle in the middle
            _sweepDirection = 1;
            _dragPowerMultiplier = 1f;

            if (ArrowIndicator != null) ArrowIndicator.SetActive(true);
        }

        private void DeactivateArrow()
        {
            if (ArrowIndicator != null) ArrowIndicator.SetActive(false);
        }

        private void Update()
        {
            // We handle the needle swing manually here every frame!
            // This allows you to change variables in the inspector at runtime and see instant results.
            if (!IsLocked && ArrowIndicator != null && ArrowIndicator.activeSelf && NeedlePivot != null)
            {
                float accuracyStat = _currentClub != null ? _currentClub.Accuracy : 50f;
                float t = 1f - Mathf.Clamp01(accuracyStat / 100f);
                
                float speedMultiplier = Mathf.Lerp(1f, MaxSpeedMultiplier, t);
                
                // Combine all speed factors dynamically
                float currentHalfDuration = BaseHalfSwingDuration / (speedMultiplier * GlobalNeedleSpeed * _dragPowerMultiplier);
                
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
                float currentAngle = Mathf.Lerp(NeedleMinAngle, NeedleMaxAngle, _swingProgress);
                NeedlePivot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
            }
        }

        public void LockAccuracy()
        {
            if (IsLocked) return;
            IsLocked = true;
            
            LockedAccuracyValue = (_swingProgress - 0.5f) * 2f;
        }

        public void ResetLock()
        {
            IsLocked = false;
            LockedAccuracyValue = 0f;
        }

        public void SetClub(ClubData club) => _currentClub = club;

        public void SetDragPowerMultiplier(float overpowerRatio)
        {
            // Instantly updates the multiplier used in the Update() loop
            // When drag is near maximum (red colour), boost the needle speed more aggressively
            if (overpowerRatio > 0.9f)
            {
                // Apply a stronger boost (double the MaxDragSpeedBonus) for high drag
                _dragPowerMultiplier = Mathf.Lerp(1f, 1f + MaxDragSpeedBonus * 2f, overpowerRatio);
            }
            else
            {
                _dragPowerMultiplier = Mathf.Lerp(1f, 1f + MaxDragSpeedBonus, overpowerRatio);
            }
        }
    }
}