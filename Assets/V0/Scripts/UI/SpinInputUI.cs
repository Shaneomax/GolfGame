using UnityEngine;
using UnityEngine.EventSystems;

namespace GolfGame.UI
{
    /// <summary>
    /// Handles the Golf Clash style spin input UI.
    /// Attach this script to the background Ball Image and assign the Red Dot marker to it.
    /// </summary>
    public class SpinInputUI : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        [Tooltip("The red dot or crosshair that indicates the current spin.")]
        public RectTransform SpinMarker;

        [Tooltip("The maximum distance the marker can be dragged from the center (in pixels/local units).")]
        public float MaxRadius = 100f;

        /// <summary>
        /// X: Side Spin (-1 = Left, 1 = Right). 
        /// Y: Top/Back Spin (-1 = Back, 1 = Top).
        /// </summary>
        public Vector2 CurrentSpin { get; private set; }

        /// <summary>
        /// Globally accessible spin value so spawned ball prefabs don't need a direct reference to the UI canvas.
        /// </summary>
        public static Vector2 GlobalCurrentSpin { get; private set; }

        private RectTransform _rectTransform;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (SpinMarker != null)
            {
                SpinMarker.anchoredPosition = Vector2.zero;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            UpdateMarkerPosition(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateMarkerPosition(eventData);
        }

        private void UpdateMarkerPosition(PointerEventData eventData)
        {
            if (SpinMarker == null || _rectTransform == null) return;

            // Convert screen position of the mouse/touch to a local position inside the Ball background
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform, 
                eventData.position, 
                eventData.pressEventCamera, 
                out Vector2 localPoint
            );

            // Clamp the local point strictly to a circle
            Vector2 clampedPoint = Vector2.ClampMagnitude(localPoint, MaxRadius);
            
            // Move the marker
            SpinMarker.anchoredPosition = clampedPoint;

            // Calculate normalized spin (-1 to 1) based on the clamp radius.
            Vector2 rawSpin = clampedPoint / MaxRadius;
            CurrentSpin = rawSpin;
            GlobalCurrentSpin = CurrentSpin;
        }

        /// <summary>
        /// Optional: Resets the spin to center (no spin).
        /// </summary>
        public void ResetSpin()
        {
            if (SpinMarker != null)
            {
                SpinMarker.anchoredPosition = Vector2.zero;
            }
            CurrentSpin = Vector2.zero;
            GlobalCurrentSpin = Vector2.zero;
        }
    }
}
