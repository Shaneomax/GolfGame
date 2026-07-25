using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using GolfGame.Controllers;

namespace GolfGame.UI
{
    /// <summary>
    /// Manages the core gameplay UI elements:
    /// - ShootButton (visible in Setup state)
    /// - OnAirButton (visible in Aiming state)
    /// - BallButton toggles SpinDashboard (only in Aiming state)
    /// </summary>
    public class GameplayUIController : MonoBehaviour
    {
        [Header("UI Panels & Buttons")]
        [Tooltip("The Shoot Button - visible in Setup state.")]
        public GameObject ShootButton;

        [Tooltip("The OnAir Button - visible in Aiming state.")]
        public GameObject OnAirButton;

        [Tooltip("The Perfect Shot Button (Debugging) - visible in Aiming state.")]
        public GameObject PerfectButton;

        [Tooltip("The Spin Dashboard (Image/Panel) - toggled by BallButton in Aiming state.")]
        public GameObject SpinDashboard;

        private bool _isSpinDashboardActive = false;

        /// <summary>
        /// Static flag read by any script to check if spin dashboard is open.
        /// </summary>
        public static bool IsSpinDashboardOpen { get; private set; } = false;

        /// <summary>
        /// Fired whenever the spin dashboard opens (true) or closes (false).
        /// Runtime-spawned scripts (e.g. ball prefab) subscribe to this in their Awake/OnEnable.
        /// </summary>
        public static event System.Action<bool> OnSpinDashboardToggled;

        private void Start()
        {
            _isSpinDashboardActive = false;

            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnStateEnter += HandleStateChanged;
                // Set initial state based on current game state
                HandleStateChanged(GameStateManager.Instance.CurrentState);
            }
            else
            {
                // Fallback: assume Setup state
                SetButtonsForState(GameStateManager.GameState.Setup);
            }
        }

        private void OnDestroy()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnStateEnter -= HandleStateChanged;
            }
        }

        private void HandleStateChanged(GameStateManager.GameState newState)
        {
            // Close spin dashboard when leaving Setup
            if (newState != GameStateManager.GameState.Setup && _isSpinDashboardActive)
            {
                _isSpinDashboardActive = false;
                IsSpinDashboardOpen = false;
                if (SpinDashboard != null) SpinDashboard.SetActive(false);
            }

            SetButtonsForState(newState);
        }

        /// <summary>
        /// Sets button visibility based on game state.
        /// Setup: ShootButton ON, OnAirButton OFF
        /// Aiming: ShootButton OFF, OnAirButton ON
        /// Other: Both OFF
        /// </summary>
        private void SetButtonsForState(GameStateManager.GameState state)
        {
            switch (state)
            {
                case GameStateManager.GameState.Setup:
                    if (ShootButton != null) ShootButton.SetActive(true);
                    if (OnAirButton != null) OnAirButton.SetActive(false);
                    if (SpinDashboard != null) SpinDashboard.SetActive(false);
                    if (PerfectButton != null) PerfectButton.SetActive(false);
                    break;

                case GameStateManager.GameState.Aiming:
                    if (ShootButton != null) ShootButton.SetActive(false);
                    if (OnAirButton != null) OnAirButton.SetActive(true);
                    if (SpinDashboard != null) SpinDashboard.SetActive(false);
                    if (PerfectButton != null) PerfectButton.SetActive(true);
                    break;

                default:
                    // Flight or other states: hide everything
                    if (ShootButton != null) ShootButton.SetActive(false);
                    if (OnAirButton != null) OnAirButton.SetActive(false);
                    if (SpinDashboard != null) SpinDashboard.SetActive(false);
                    if (PerfectButton != null) PerfectButton.SetActive(false);
                    break;
            }
        }

        /// <summary>
        /// Call this from the BallButton's OnClick(). Toggles SpinDashboard in Setup state only.
        /// </summary>
        public void OnBallButtonClicked()
        {
            if (GameStateManager.Instance == null ||
                GameStateManager.Instance.CurrentState != GameStateManager.GameState.Setup)
            {
                return;
            }

            _isSpinDashboardActive = !_isSpinDashboardActive;
            UpdateSpinDashboard();
        }

        /// <summary>
        /// Hook this up to a "Save" button inside your Spin Dashboard.
        /// Closes the dashboard; spin values are saved automatically.
        /// </summary>
        public void OnSaveSpinButtonClicked()
        {
            CloseSpinDashboard();
        }

        public void OpenSpinDashboard()
        {
            if (GameStateManager.Instance == null ||
                GameStateManager.Instance.CurrentState != GameStateManager.GameState.Setup)
                return;

            if (!_isSpinDashboardActive)
            {
                _isSpinDashboardActive = true;
                UpdateSpinDashboard();
            }
        }

        public void CloseSpinDashboard()
        {
            if (_isSpinDashboardActive)
            {
                _isSpinDashboardActive = false;
                UpdateSpinDashboard();
            }
        }

        /// <summary>
        /// Call this from the OnAir Button's OnClick().
        /// Takes the game back to the Setup state from Aiming.
        /// </summary>
        public void OnOnAirButtonClicked()
        {
            if (GameStateManager.Instance == null) return;
            GameStateManager.Instance.ChangeState(GameStateManager.GameState.Setup);
        }

        public void OnPerfectButtonClicked()
        {
            var playerInput = FindObjectOfType<PlayerInputController>();
            if (playerInput != null)
            {
                playerInput.ForcePerfectShot();
            }
        }

        /// <summary>
        /// Handles DOTween pop animation for the SpinDashboard and toggles OnAirButton accordingly.
        /// </summary>
        private void UpdateSpinDashboard()
        {
            IsSpinDashboardOpen = _isSpinDashboardActive;

            // Notify all spawned scripts (PlayerInputController, AimVisualsController, etc.)
            OnSpinDashboardToggled?.Invoke(_isSpinDashboardActive);
            // When spin dashboard is open in Setup state, hide the Shoot button. When closed, show it again.
            if (ShootButton != null)
            {
                ShootButton.SetActive(!_isSpinDashboardActive);
            }

            if (SpinDashboard != null)
            {
                if (_isSpinDashboardActive)
                {
                    SpinDashboard.SetActive(true);
                    SpinDashboard.transform.localScale = Vector3.zero;
                    SpinDashboard.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
                }
                else
                {
                    SpinDashboard.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack).OnComplete(() => {
                        SpinDashboard.SetActive(false);
                    });
                }
            }
        }
    }
}
