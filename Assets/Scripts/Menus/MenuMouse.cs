using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// How the mouse behaves in the menus. While it's being used, it moves freely like it does anywhere else, and the
/// menu's cursor art follows it exactly in place of the system cursor. While a controller is being used, the mouse
/// gets locked away instead, so that a pointer left sitting on a button doesn't keep that button highlighted.
/// </summary>
public static class MenuMouse
{
    // Whether the mouse was moved or clicked this frame. A tiny nudge doesn't count.
    public static bool UsedThisFrame()
    {
        Mouse mouse = Mouse.current;
        return mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 4f
            || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
    }

    // Lets the mouse move freely. The system cursor stays hidden, since each menu draws its own cursor in its place.
    public static void Release()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
    }

    // Locks the mouse in the middle of the screen, which also stops the EventSystem from pointing it at anything
    public static void Lock()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Cursor art is drawn on top of everything, so it mustn't catch the clicks meant for the buttons under it
    public static void MakeClickThrough(GameObject cursor)
    {
        foreach (Graphic graphic in cursor.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
    }
}
