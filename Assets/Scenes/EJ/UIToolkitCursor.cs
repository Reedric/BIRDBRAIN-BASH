using System.Collections;
using System.ComponentModel.Design.Serialization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class UIToolkitCursor : MonoBehaviour
{
    private UIDocument _uiDocument;
    [SerializeField] private InputActionReference _cursorAction;
    [SerializeField] private float cursorSpeed = 1000f;

    private VisualElement _root; 
    private VisualElement _cursor;
    private Vector2 _cursorPosition;
    private IPanel _panel;
    private VisualElement _lastHoveredElement;

    const float ClickPointX = 15f;
    const float ClickPointY = 20f;

    private void OnEnable()
    {
        _cursorAction.action.Enable();
        _uiDocument = GetComponent<UIDocument>();

        _root = _uiDocument.rootVisualElement;
        _cursor = _root.Q<VisualElement>("Cursor");
        _panel = _root.panel;
    }

    private IEnumerator Start()
    {
        yield return null;

        _cursorPosition = new Vector2(
            _root.layout.width * 0.5f,
            _root.layout.height * 0.5f
        );

        UpdateVisualCursor();
    }

    private void OnDisable()
    {
        _cursorAction.action.Disable();
    }

    private void Update()
    {
        // Debug.Log($"Size: {_root.layout.width} x {_root.layout.height}");
        if (_cursor == null || _panel == null) return;

        MoveCursor();
        UpdateVisualCursor();
        UpdateHoverStates();
    }

    private void MoveCursor()
    {
        Vector2 stickInput = _cursorAction.action.ReadValue<Vector2>();
        
        _cursorPosition += cursorSpeed * Time.deltaTime * new Vector2(stickInput.x, -stickInput.y);
        _cursorPosition.x = Mathf.Clamp(_cursorPosition.x, 0, _root.layout.width);
        _cursorPosition.y = Mathf.Clamp(_cursorPosition.y, 0, _root.layout.height);
    }

    private void UpdateVisualCursor()
    {
        _cursor.style.left = _cursorPosition.x - ClickPointX;
        _cursor.style.top = _cursorPosition.y - ClickPointY;

        // Debug.Log($"Desired: {_cursorPosition}");
        // Debug.Log($"Actual: {_cursor.worldBound.position}");
    }

    private void UpdateHoverStates()
    {
        VisualElement targetElement = _panel.Pick(_cursorPosition);

        if (targetElement == _lastHoveredElement) return;
        _lastHoveredElement?.RemoveFromClassList("hover");
        if (targetElement != null)
        {
            // Debug.Log($"Hovering: {targetElement.name}");
            targetElement.AddToClassList("hover");   
        }
        _lastHoveredElement = targetElement;
    }
}
