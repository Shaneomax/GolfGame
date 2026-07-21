using UnityEngine;
using UnityEngine.EventSystems;

// Implement the three drag interfaces
public class DragDropUI : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        
        // We need the canvas to scale the mouse movement correctly
        canvas = GetComponentInParent<Canvas>();

        // CanvasGroup is used to change visual alpha and raycast blocking during the drag
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Give visual feedback that the object is picked up
        canvasGroup.alpha = 0.7f;
        
        // Turn off raycasts so the mouse doesn't "get stuck" on this object 
        // if you ever want to drop it onto a specific target behind it
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Move the UI element by the exact amount the mouse moved.
        // We divide by scaleFactor so the drag speed matches the mouse perfectly, 
        // regardless of screen resolution or canvas scaling.
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Reset the object when dropped
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }
}