using DG.Tweening;
using UnityEngine;

namespace GolfGame.Controllers
{
    /// <summary>
    /// Golf Rival-style shot accuracy controller.
    ///
    /// The NeedlePivot is the base/hinge of the needle (not its centre).
    /// DOTween sweeps the pivot's Z rotation from NeedleMinAngle (5°) to
    /// NeedleMaxAngle (177°) in a Yoyo loop.
    ///
    ///   5°  = far left  →  maximum deviation
    ///  90°  = straight up  →  PERFECT shot (LockedAccuracyValue ≈ 0)
    /// 177°  = far right →  maximum deviation
    ///
    /// When LockAccuracy() is called the tween is killed, the current angle is
    /// sampled, and LockedAccuracyValue is mapped to [-1, 1] where 0 = perfect.
    /// </summary>
    public class ShotAccuracyController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        #region Inspector Fields

        [Header("Arrow References")]
        [Tooltip("The root GameObject of the accuracy arrow / needle indicator. " +
                 "SetActive(true) when Aiming begins, SetActive(false) otherwise.")]
        public GameObject ArrowIndicator;

        [Tooltip("The pivot Transform at the BASE of the needle. " +
                 "DOTween rotates this around its local Z axis.")]
        public Transform NeedlePivot;

        [Header("Needle Sweep Range")]
        [Tooltip("Starting angle (degrees, Z-rotation) for the needle. " +
                 "5° = left extreme.")]
        public float NeedleMinAngle = 5f;

        [Tooltip("Ending angle (degrees, Z-rotation) for the needle. " +
                 "177° = right extreme.")]
        public float NeedleMaxAngle = 177f;

        [Tooltip("The angle at which the shot is perfectly accurate. " +
                 "Defaults to 90° (needle pointing straight up from the pivot).")]
        public float PerfectAngle = 90f;

        [Header("Oscillation Speed")]
        [Tooltip("Time (seconds) for one half-sweep at Accuracy = 100. " +
                 "Lower ClubData.Accuracy makes the needle move faster.")]
        public float BaseHalfSwingDuration = 0.8f;

        [Tooltip("At Accuracy = 0 the half-swing is this many times faster than the base.")]
        public float MaxSpeedMultiplier = 4f;

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// Accuracy value in [-1, 1] at the moment the shot was locked.
        ///  0 = perfect centre (needle at PerfectAngle)
        /// ±1 = maximum miss (needle at NeedleMinAngle or NeedleMaxAngle)
        /// </summary>
        public float LockedAccuracyValue { get; private set; }

        /// <summary>True once LockAccuracy() has been called for the current swing.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>
        /// Freeze the needle and record the accuracy value.
        /// Called automatically by PlayerInputController on mouse-down.
        /// </summary>
        public void LockAccuracy()
        {
            if (IsLocked) return;
            IsLocked = true;

            // Kill the tween → needle stays frozen at its current angle.
            _needleTween?.Kill();
            _needleTween = null;

            if (NeedlePivot != null)
            {
                // localEulerAngles.z is in [0, 360). Our sweep is within [5, 177] so
                // no negative-angle remapping is needed for this range.
                float currentAngle = NeedlePivot.localEulerAngles.z;

                // Deviation from the perfect angle, normalized to [-1, 1].
                // Positive deviation  → right miss, negative → left miss.
                float deviation = currentAngle - PerfectAngle;
                float maxDeviation = Mathf.Max(PerfectAngle - NeedleMinAngle,
                                               NeedleMaxAngle - PerfectAngle);

                LockedAccuracyValue = Mathf.Clamp(deviation / maxDeviation, -1f, 1f);
            }
            else
            {
                LockedAccuracyValue = 0f;
            }

            string quality = Mathf.Abs(LockedAccuracyValue) < 0.10f ? "PERFECT!"
                           : Mathf.Abs(LockedAccuracyValue) < 0.30f ? "Good"
                           : "Miss";

            Debug.Log($"[ShotAccuracy] Locked at {LockedAccuracyValue:F2} — {quality}");
        }

        /// <summary>
        /// Pass the current club so the needle speed matches the selected club.
        /// Called by PlayerInputController when the Aiming state begins.
        /// </summary>
        public void SetClub(ClubData club)
        {
            _currentClub = club;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Private Fields

        private Tween _needleTween;
        private ClubData _currentClub;

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

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

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region State Handling

        private void OnStateEnter(GameStateManager.GameState newState)
        {
            if (newState == GameStateManager.GameState.Aiming)
                ActivateArrow();
            else
                DeactivateArrow();
        }

        private void ActivateArrow()
        {
            IsLocked = false;
            LockedAccuracyValue = 0f;

            if (ArrowIndicator != null) ArrowIndicator.SetActive(true);

            // Snap needle to the starting position before the tween begins.
            if (NeedlePivot != null)
                NeedlePivot.localRotation = Quaternion.Euler(0f, 0f, NeedleMinAngle);

            StartNeedleTween();
        }

        private void DeactivateArrow()
        {
            _needleTween?.Kill();
            _needleTween = null;

            if (ArrowIndicator != null) ArrowIndicator.SetActive(false);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region DOTween Needle

        private void StartNeedleTween()
        {
            _needleTween?.Kill();
            if (NeedlePivot == null) return;

            // Speed: higher Accuracy = slower needle (easier to time)
            float accuracyStat   = _currentClub != null ? _currentClub.Accuracy : 50f;
            float t              = 1f - Mathf.Clamp01(accuracyStat / 100f);
            float speedMultiplier = Mathf.Lerp(1f, MaxSpeedMultiplier, t);
            float halfDuration   = BaseHalfSwingDuration / speedMultiplier;

            // Sweep from NeedleMinAngle → NeedleMaxAngle, Yoyo loop.
            // We use RotateMode.Fast so the angle is treated as an absolute
            // local Euler Z — no spinning past 360°.
            _needleTween = NeedlePivot
                .DOLocalRotate(new Vector3(0f, 0f, NeedleMaxAngle), halfDuration, RotateMode.Fast)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(UpdateType.Normal);
        }

        #endregion
    }
}
