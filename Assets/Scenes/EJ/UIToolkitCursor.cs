using UnityEngine;
using UnityEngine.UIElements;

public class UISelectionVisualizer : MonoBehaviour
{
    private UIDocument _uiDocument;
    private VisualElement _root;

    private VisualElement _previousSelected;


    private void Start()
    {
        _uiDocument = GetComponent<UIDocument>();
        _root = _uiDocument.rootVisualElement;
    }


    private void Update()
    {
        if (_root.panel == null)
            return;

        if (_root.panel.focusController.focusedElement is not VisualElement selected || selected == _previousSelected)
            return;

        // Remove old highlight
        _previousSelected?.RemoveFromClassList("gamepad-selected");

        // Add new highlight
        selected.AddToClassList("gamepad-selected");

        _previousSelected = selected;

        Debug.Log("Selected: " + selected.name);
    }
}