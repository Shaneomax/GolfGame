using DG.Tweening;
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
        [Range(0.1f, 3f)]
        public float GlobalNeedleSpeed = 1.0f;

        [Header("Drag Power Difficulty")]
        [Tooltip("Maximum extra speed multiplier applied to the needle at maximum drag overpower.")]
        public float MaxDragSpeedBonus = 3f;

        public float LockedAccuracyValue { get; private set; }
        public bool IsLocked { get; private set; }

        private Tween _needleTween;
        private ClubData _currentClub;
        private float _swingProgress = 0f;
        private float _dragPowerMultiplier = 1f;

        private void Start()
        {
            if (ArrowIndicator != null) ArrowIndicator.SetActive(false);
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnStateEnter += OnStateEnter;
        }

        private void OnDestroy()
        {
            _needleTween?.Kill();
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
            _swingProgress = 0f;

            if (ArrowIndicator != null) ArrowIndicator.SetActive(true);
            StartNeedleTween();
        }

        private void DeactivateArrow()
        {
            _needleTween?.Kill();
            _needleTween = null;
            if (ArrowIndicator != null) ArrowIndicator.SetActive(false);
        }

        private void StartNeedleTween()
        {
            _needleTween?.Kill();
            if (NeedlePivot == null) return;

            float accuracyStat = _currentClub != null ? _currentClub.Accuracy : 50f;
            float t = 1f - Mathf.Clamp01(accuracyStat / 100f);
            
            float speedMultiplier = Mathf.Lerp(1f, MaxSpeedMultiplier, t);
            // Overpower multiplier dynamically speeds this up
            float halfDuration = BaseHalfSwingDuration / (speedMultiplier * GlobalNeedleSpeed * _dragPowerMultiplier);

            _swingProgress = 0f;
            UpdateNeedleVisuals();

            _needleTween = DOTween.To(() => _swingProgress, x => 
            {
                _swingProgress = x;
                UpdateNeedleVisuals();
            }, 1f, halfDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(UpdateType.Normal);
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
            _needleTween?.Kill();
            _needleTween = null;
            LockedAccuracyValue = (_swingProgress - 0.5f) * 2f;
        }

        public void ResetLock()
        {
            IsLocked = false;
            LockedAccuracyValue = 0f;
            StartNeedleTween();
        }

        public void SetClub(ClubData club) => _currentClub = club;

        /// <summary>
        /// Called by PlayerInputController. overpowerRatio is 0 when drag is normal,
        /// and scales up to 1.0 when max drag overpower is achieved.
        /// </summary>
        public void SetDragPowerMultiplier(float overpowerRatio)
        {
            _dragPowerMultiplier = Mathf.Lerp(1f, 1f + MaxDragSpeedBonus, overpowerRatio);

            if (!IsLocked)
            {
                StartNeedleTween();
            }
        }
    }
}