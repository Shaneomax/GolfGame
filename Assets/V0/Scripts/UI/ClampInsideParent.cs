using UnityEngine;

public class ClampInsideParent : MonoBehaviour
{
    private RectTransform rectTransform;
    private RectTransform parentRect;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentRect = transform.parent.GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        // Get the current local position relative to the center of the parent
        Vector2 currentPos = rectTransform.localPosition;

        // Calculate the radius of the parent image (the golf ball)
        // We divide the width by 2 to get the radius from the center
        float parentRadius = parentRect.rect.width / 2f;
        
        // Calculate the radius of the moving object (the red crosshair)
        // We subtract this so the edge of the crosshair doesn't poke outside the ball
        float childRadius = rectTransform.rect.width / 2f;
        
        // The maximum allowed distance from the center
        float maxDistance = parentRadius - childRadius;

        // Vector2.magnitude calculates the exact distance from (0,0) to currentPos
        if (currentPos.magnitude > maxDistance)
        {
            // If it's too far, we find the direction (normalized) and multiply by our max distance limit
            currentPos = currentPos.normalized * maxDistance;
        }

        // Apply the newly clamped circular position
        rectTransform.localPosition = currentPos;
    }
}