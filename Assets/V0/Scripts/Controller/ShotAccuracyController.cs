using System.Collections;
using UnityEngine;

namespace GolfGame.Controllers
{
    /// <summary>
    /// Golf Rival-style shot accuracy controller.
    ///
    /// Place this component anywhere in the scene. Assign:
    ///   - ArrowIndicator : the root GameObject of the arrow/needle UI or world object.
    ///   - NeedlePivot    : a child Transform that will be rotated to show the current accuracy position.
    ///
    /// When the game enters the Aiming state the arrow becomes active and the needle
    /// oscillates back and forth.  The player must call LockAccuracy() (done automatically
    /// by PlayerInputController on mouse-down) to freeze the needle and record the shot
    /// accuracy for that swing.
    ///
    /// LockedAccuracyValue is in the range [-1, 1]:
    ///   0  = perfect centre  → no shot deviation
    ///  ±1  = max off-centre  → maximum deviation angle applied to the shot
    /// </summary>
    public class ShotAccuracyController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        #region Inspector Fields

        [Header("Arrow References")]
        [Tooltip("The root GameObject of the accuracy arrow / needle indicator. " +
                 "SetActive(true) when Aiming begins, SetActive(false) otherwise.")]
        public GameObject ArrowIndicator;

        [Tooltip("A child Transform inside ArrowIndicator that will be rotated around its Z axis " +
                 "to visualise the current accuracy position.")]
        public Transform NeedlePivot;

        [Header("Oscillation Settings")]
        [Tooltip("How far (in degrees) the needle swings left and right from centre.")]
        public float MaxOscillationAngle = 45f;

        [Tooltip("Base oscillation speed (degrees per second at Accuracy = 100). " +
                 "Lower ClubData.Accuracy makes the needle swing faster.")]
        public float BaseOscillationSpeed = 60f;

        [Tooltip("Multiplier applied when club accuracy is 0. Needle swings this many times " +
                 "faster than BaseOscillationSpeed.")]
        public float MaxSpeedMultiplier = 4f;

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>Raw accuracy value in the range [-1, 1] at the moment the shot was locked.</summary>
        public float LockedAccuracyValue { get; private set; }

        /// <summary>True once LockAccuracy() has been called for the current swing.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>
        /// Freeze the needle and record the current accuracy value.
        /// Called by PlayerInputController the instant the player starts dragging the ball.
        /// </summary>
        public void LockAccuracy()
        {
            if (IsLocked) return;
            IsLocked = true;
            LockedAccuracyValue = _currentRawValue;

            // Stop oscillation coroutine so the needle stays frozen.
            if (_oscillationCoroutine != null)
            {
                StopCoroutine(_oscillationCoroutine);
                _oscillationCoroutine = null;
            }

            Debug.Log($"[ShotAccuracy] Locked at {LockedAccuracyValue:F2}  " +
                      $"({(Mathf.Abs(LockedAccuracyValue) < 0.15f ? "PERFECT!" : "off-centre")})");
        }

        /// <summary>
        /// Update the club reference so the needle speed is correct for the selected club.
        /// Called by PlayerInputController when Aiming begins.
        /// </summary>
        public void SetClub(ClubData club)
        {
            _currentClub = club;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Private Fields

        private float _currentRawValue;       // live oscillating value [-1, 1]
        private Coroutine _oscillationCoroutine;
        private ClubData _currentClub;

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Start()
        {
            // Hide the arrow by default.
            if (ArrowIndicator != null) ArrowIndicator.SetActive(false);

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
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region State Handling

        private void OnStateEnter(GameStateManager.GameState newState)
        {
            if (newState == GameStateManager.GameState.Aiming)
            {
                ActivateArrow();
            }
            else
            {
                DeactivateArrow();
            }
        }

        private void ActivateArrow()
        {
            // Reset state for the new swing attempt.
            IsLocked = false;
            LockedAccuracyValue = 0f;
            _currentRawValue = 0f;

            if (ArrowIndicator != null) ArrowIndicator.SetActive(true);

            // Start oscillation.
            if (_oscillationCoroutine != null) StopCoroutine(_oscillationCoroutine);
            _oscillationCoroutine = StartCoroutine(OscillateNeedle());
        }

        private void DeactivateArrow()
        {
            // Stop oscillation if it was running.
            if (_oscillationCoroutine != null)
            {
                StopCoroutine(_oscillationCoroutine);
                _oscillationCoroutine = null;
            }

            if (ArrowIndicator != null) ArrowIndicator.SetActive(false);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Oscillation Coroutine

        private IEnumerator OscillateNeedle()
        {
            float time = 0f;

            while (true)
            {
                // Derive oscillation speed from club accuracy.
                // Accuracy=100 → speed multiplier=1  (slow, easy to time)
                // Accuracy=0   → speed multiplier=MaxSpeedMultiplier (fast, hard)
                float accuracyStat = _currentClub != null ? _currentClub.Accuracy : 50f;
                float t = 1f - Mathf.Clamp01(accuracyStat / 100f);   // 0=easy, 1=hardest
                float speedMultiplier = Mathf.Lerp(1f, MaxSpeedMultiplier, t);
                float oscillationSpeed = BaseOscillationSpeed * speedMultiplier;

                time += Time.deltaTime * oscillationSpeed * Mathf.Deg2Rad;

                // Raw value: -1 to 1
                _currentRawValue = Mathf.Sin(time);

                // Rotate the needle pivot to reflect the current accuracy value.
                if (NeedlePivot != null)
                {
                    float angle = _currentRawValue * MaxOscillationAngle;
                    NeedlePivot.localRotation = Quaternion.Euler(0f, 0f, -angle);
                }

                yield return null;
            }
        }

        #endregion
    }
}
