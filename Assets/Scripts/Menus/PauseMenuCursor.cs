using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// The cursor for the pause menu and its options. It follows the mouse while that's being used. Otherwise the
/// EventSystem does the actual navigating in a match, so this just follows whatever it has selected, moving the
/// same way as the cursors in the other menus.
/// </summary>
public class PauseMenuCursor : MonoBehaviour
{
    private const float moveSmoothTime = 0.06f;
    private const float bobAmplitude = 6f;
    private const float bobFrequency = 3f;

    private RectTransform rect;
    private Vector3 position;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        rect.pivot = new Vector2(0f, 1f); // Top-left pivot for pointer-style cursor accuracy
        MenuMouse.MakeClickThrough(gameObject);

        if (!TryGetTargetPosition(out position)) position = rect.position;
        rect.position = position;
    }

    void Update()
    {
        // The options panel gets moved to the top when it opens, which would otherwise be drawn over the cursor
        if (transform.GetSiblingIndex() != transform.parent.childCount - 1) transform.SetAsLastSibling();

        if (PauseMenu.Instance != null && PauseMenu.Instance.MouseInUse && TryGetMousePosition(out Vector3 mousePosition))
        {
            position = mousePosition;
            rect.position = position;
            return;
        }

        // Keeps its position if there's no selection, since there isn't one for a frame whenever the screen changes.
        // All of this uses unscaled time, since pausing a local match freezes game time.
        if (TryGetTargetPosition(out Vector3 target))
            position = Vector3.Lerp(position, target, Mathf.Clamp01(Time.unscaledDeltaTime / moveSmoothTime));

        float intensity = Mathf.Sin(Time.unscaledTime * bobFrequency * Mathf.PI * 2f);
        float bob = intensity * bobAmplitude * transform.parent.lossyScale.x;
        rect.position = position + new Vector3(-bob, -bob, 0f);
    }

    private bool TryGetMousePosition(out Vector3 mousePosition)
    {
        mousePosition = default;
        if (Mouse.current == null) return false;

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)transform.parent,
            Mouse.current.position.ReadValue(), cam, out mousePosition);
    }

    private bool TryGetTargetPosition(out Vector3 target)
    {
        target = default;

        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null || !selected.activeInHierarchy || !selected.TryGetComponent(out RectTransform selectedRect)) return false;

        // Points at the middle of the button, the same as the cursors in the other menus
        target = selectedRect.position;
        return true;
    }
}
