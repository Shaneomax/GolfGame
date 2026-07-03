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
        
        [Tooltip("This slider WILL move on its own at runtime when you overpower your drag!")]
        [Range(0.1f, 10f)] // Increased max range so you can clearly see it spike
        public float GlobalNeedleSpeed = 1.0f;

        [Header("Drag Power Difficulty")]
        [Tooltip("How much extra speed is ADDED to GlobalNeedleSpeed at maximum drag.")]
        public float MaxDragSpeedBonus = 3f;

        public float LockedAccuracyValue { get; private set; }
        public bool IsLocked { get; private set; }

        private ClubData _currentClub;
        private float _swingProgress = 0f;
        private int _sweepDirection = 1;
        
        // We store your Inspector default here so we can reset it for the next shot
        private float _baseGlobalSpeed = 1.0f;

        private void Start()
        {
            // Remember whatever you set in the Inspector before the game started
            _baseGlobalSpeed = GlobalNeedleSpeed;
            
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
            _swingProgress = 0.5f; 
            _sweepDirection = 1;

            // Reset back to normal speed when a new aim phase starts
            GlobalNeedleSpeed = _baseGlobalSpeed;

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
                
                // We calculate speed using GlobalNeedleSpeed directly.
                // Since OnMouseDrag changes this variable, it updates instantly here!
                float currentHalfDuration = BaseHalfSwingDuration / (speedMultiplier * GlobalNeedleSpeed);
                
                float progressSpeed = 1f / currentHalfDuration;
                _swingProgress += _sweepDirection * progressSpeed * Time.deltaTime;

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
            GlobalNeedleSpeed = _baseGlobalSpeed; // Reset if the shot is cancelled
        }

        public void SetClub(ClubData club) => _currentClub = club;

        public void SetDragPowerMultiplier(float overpowerRatio)
        {
            // OVERWRITE GlobalNeedleSpeed dynamically.
            // overpowerRatio is 0.0 at Normal Drag, and scales up to 1.0 at Max Drag.
            GlobalNeedleSpeed = _baseGlobalSpeed + (MaxDragSpeedBonus * overpowerRatio);
        }
    }
}