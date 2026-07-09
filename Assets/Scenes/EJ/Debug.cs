using UnityEngine;
using UnityEngine.UIElements;

public class UIDebug : MonoBehaviour
{
    public UIDocument doc;

    void Start()
    {
        doc.rootVisualElement.RegisterCallback<NavigationSubmitEvent>(evt =>
        {
            Debug.Log("GAMEPAD SUBMIT");
        });

        doc.rootVisualElement.RegisterCallback<PointerDownEvent>(evt =>
        {
            Debug.Log("MOUSE CLICK");
        });
    }
}