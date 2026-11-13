using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The controls screen in the pause menu, with a tab each for keyboard and controller. A tab shows its artwork from
/// MenuArt (Resources/MenuArt), or until that's been added, a list of the controls read from the input actions.
/// </summary>
public class ControlsMenuUI : MonoBehaviour
{
    // The controls panel once it's showing
    public static GameObject ActivePanel { get; private set; }

    // These are in Resources, so that they can match the main menu in a match scene as well
    private const string backgroundPath = "MenuBackground";
    private const string backButtonPath = "MenuBackButton";

    private static readonly Vector2 artPosition = new(0f, -130f);
    private static readonly Vector2 artSize = new(1500f, 760f);
    private const float inactiveTabScale = 0.82f;

    // The controls listed until there's artwork, in order, with the names players know them by
    private static readonly (string action, string name)[] listedControls =
    {
        ("Player/Move", "Move"),
        ("Player/Jump", "Jump"),
        ("Player/Defensive Action", "Bump / Set"),
        ("Player/Offensive Action", "Serve / Spike"),
        ("Player/Block", "Block"),
        ("Player/Defensive Ability", "Defensive Ability"),
        ("Player/Offensive Ability", "Offensive Ability"),
        ("UI/Pause", "Pause")
    };

    private GameObject panel;
    private Button keyboardTab;
    private Button controllerTab;
    private Image art;
    private TMP_Text controlsList;
    private bool showingKeyboard = true;

    public GameObject Panel => panel;
    public Button FirstButton => showingKeyboard ? keyboardTab : controllerTab;

    public const string PanelName = "Controls Panel";

    private const string keyboardTabName = "Keyboard";
    private const string controllerTabName = "Controller";
    private const string artName = "Controls Art";
    private const string listName = "Controls List";
    private const string backButtonName = "Back";

    // Gets the screen on the given canvas ready to show. The map scenes have its layout in them already (see
    // MenuScreenBuilder), so it only gets made here, in the menu font, for a scene that doesn't.
    // inMatch is true for the pause menu. The main menu already has a background showing behind this.
    public void Build(Canvas canvas, TMP_FontAsset font, Action onClose, bool inMatch = true)
    {
        Transform existing = canvas.transform.Find(PanelName);
        panel = existing != null ? existing.gameObject : BuildPanel(canvas, font, inMatch);
        Transform root = panel.transform;

        keyboardTab = root.Find(keyboardTabName).GetComponent<Button>();
        controllerTab = root.Find(controllerTabName).GetComponent<Button>();
        art = root.Find(artName).GetComponent<Image>();
        controlsList = root.Find(listName).GetComponent<TMP_Text>();
        Button backButton = root.Find(backButtonName).GetComponent<Button>();

        keyboardTab.onClick.AddListener(() => ShowTab(true));
        controllerTab.onClick.AddListener(() => ShowTab(false));
        backButton.onClick.AddListener(() => onClose());

        // Explicit links for the stick, the same as the other menus
        SetNavigation(backButton, null, keyboardTab, null, null);
        SetNavigation(keyboardTab, backButton, null, null, controllerTab);
        SetNavigation(controllerTab, backButton, null, keyboardTab, null);

        panel.SetActive(false);
    }

    // Makes the screen's layout on the given canvas, hidden. Also used in the editor to put it into the map scenes,
    // where it can be seen and edited (see MenuScreenBuilder). Build finds its parts by name and makes them work, so
    // they need to keep their names.
    public static GameObject BuildPanel(Canvas canvas, TMP_FontAsset font, bool inMatch = true)
    {
        Vector2 center = new(0.5f, 0.5f);

        GameObject panel = new(PanelName, typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(2176f, 1224f); // The same size as the options screen

        if (inMatch)
        {
            // Pausing freezes game time, which would otherwise stop it from swaying
            GameObject background = Instantiate(Resources.Load<GameObject>(backgroundPath), panel.transform);
            background.name = "Background";
            background.GetComponent<ImageWobble>().useUnscaledTime = true;
        }

        MenuStyle.AddText(panel.transform, font, "Controls", 90f, new Vector2(0f, 400f), new Vector2(1000f, 120f), center, TextAlignmentOptions.Center).name = "Title";

        MenuStyle.AddPillButton(panel.transform, font, keyboardTabName, MenuStyle.PillColor.Yellow, new Vector2(-230f, 285f), new Vector2(420f, 100f), 46f);
        MenuStyle.AddPillButton(panel.transform, font, controllerTabName, MenuStyle.PillColor.Orange, new Vector2(230f, 285f), new Vector2(420f, 100f), 46f);

        // The tab's artwork from MenuArt goes here, or the list of controls until there is some
        Image art = new GameObject(artName, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        art.transform.SetParent(panel.transform, false);
        art.rectTransform.anchoredPosition = artPosition;
        art.rectTransform.sizeDelta = artSize;
        art.preserveAspect = true;
        art.raycastTarget = false;
        art.enabled = false;

        MenuStyle.AddText(panel.transform, font, string.Empty, 46f, artPosition, artSize, center, TextAlignmentOptions.Center).name = listName;

        Instantiate(Resources.Load<GameObject>(backButtonPath), panel.transform).name = backButtonName;

        panel.SetActive(false);
        return panel;
    }

    public void Show()
    {
        ShowTab(showingKeyboard);
        panel.SetActive(true);
        panel.transform.SetAsLastSibling(); // Things can get added to the canvas during a match
        ActivePanel = panel;
    }

    public void Hide()
    {
        panel.SetActive(false);
        ActivePanel = null;
    }

    private void ShowTab(bool keyboard)
    {
        showingKeyboard = keyboard;

        // The tab that's showing is the bigger of the two
        keyboardTab.transform.localScale = Vector3.one * (keyboard ? 1f : inactiveTabScale);
        controllerTab.transform.localScale = Vector3.one * (keyboard ? inactiveTabScale : 1f);

        MenuArt menuArt = MenuArt.Instance;
        Sprite sprite = menuArt == null ? null : keyboard ? menuArt.keyboardControls : menuArt.controllerControls;

        art.sprite = sprite;
        art.enabled = sprite != null;
        controlsList.text = sprite != null ? string.Empty : ListControls(keyboard);
    }

    // Each control and what it's bound to on the keyboard and mouse, or on a controller
    private static string ListControls(bool keyboard)
    {
        InputActionAsset actions = InputSystem.actions;
        if (actions == null) return string.Empty;

        StringBuilder text = new();
        foreach ((string actionPath, string name) in listedControls)
        {
            InputAction action = actions.FindAction(actionPath);
            if (action == null) continue;

            string bindings = BindingsFor(action, keyboard);
            if (bindings.Length > 0) text.AppendLine($"{name}:  {bindings}");
        }
        return text.ToString();
    }

    private static string BindingsFor(InputAction action, bool keyboard)
    {
        List<string> shown = new();
        for (int i = 0; i < action.bindings.Count; ++i)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isPartOfComposite) continue;

            // A composite (like WASD) counts if its parts are on the right device
            bool matches = binding.isComposite
                ? CompositeMatches(action, i, keyboard)
                : IsOnDevice(binding.path, keyboard);

            if (!matches) continue;

            string display = action.GetBindingDisplayString(i);
            if (display.Length > 0 && !shown.Contains(display)) shown.Add(display);
        }
        return string.Join(" / ", shown);
    }

    private static bool CompositeMatches(InputAction action, int compositeIndex, bool keyboard)
    {
        for (int i = compositeIndex + 1; i < action.bindings.Count && action.bindings[i].isPartOfComposite; ++i)
        {
            if (IsOnDevice(action.bindings[i].path, keyboard)) return true;
        }
        return false;
    }

    private static bool IsOnDevice(string path, bool keyboard)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return keyboard
            ? path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>")
            : path.StartsWith("<Gamepad>");
    }

    private static void SetNavigation(Selectable selectable, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        selectable.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down,
            selectOnLeft = left,
            selectOnRight = right
        };
    }
}
