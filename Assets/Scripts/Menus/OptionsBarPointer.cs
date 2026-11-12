using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Lets the mouse set a volume by clicking or dragging along its bar. onPoint gets how far along the bar that is, from 0 to 1.
public class OptionsBarPointer : MonoBehaviour, IPointerDownHandler, IInitializePotentialDragHandler, IDragHandler, IPointerClickHandler
{
    public Action<float> onPoint;

    public void OnPointerDown(PointerEventData eventData) => Point(eventData);

    // Follows the mouse from the moment it's pressed, rather than once it has moved a few pixels
    public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

    public void OnDrag(PointerEventData eventData) => Point(eventData);

    // Taking the click keeps the row underneath from getting it too
    public void OnPointerClick(PointerEventData eventData) { }

    private void Point(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        RectTransform rect = (RectTransform)transform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            onPoint?.Invoke(Mathf.InverseLerp(rect.rect.xMin, rect.rect.xMax, local.x));
    }
}
