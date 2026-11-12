using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Lets the mouse click one of the arrows on an options row
public class OptionsArrowPointer : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    public Action onClick;

    // Taking the press keeps the row underneath from getting the click too
    public void OnPointerDown(PointerEventData eventData) { }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) onClick?.Invoke();
    }
}
