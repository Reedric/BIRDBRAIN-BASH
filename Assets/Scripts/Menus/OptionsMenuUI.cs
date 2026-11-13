using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The options screen: volume and display settings. The stick moves up and down between rows the same as any other
/// menu, and left and right change the selected row. With the mouse, the arrows can be clicked, and so can the volume
/// bars (or dragged along).
/// Its layout is in the scenes (put there by MenuScreenBuilder), where it can be edited. This finds the parts by name and
/// makes them work.
/// </summary>
public class OptionsMenuUI : MonoBehaviour
{
    // The options panel once it's showing, so the menu cursor knows to target its buttons
    public static GameObject ActivePanel { get; private set; }

    private const float volumeStep = 0.05f;
    private const float stickDeadzone = 0.5f;
    private const float holdDelay = 0.35f; // Seconds a direction is held before a volume starts repeating
    private const float holdRepeat = 0.08f; // Seconds between each repeat after that
    private const int minResolutionHeight = 720; // The menus don't fit on anything smaller

    private const float arrowSize = 60f;
    private const float arrowFlashDuration = 0.12f;

    // These are in Resources, so that they can match the main menu in a match scene as well
    private const string matchBackgroundPath = "MenuBackground";
    private const string backButtonPath = "MenuBackButton";

    private static readonly Color textColor = MenuStyle.TextColor;
    private static readonly Color accentColor = MenuStyle.AccentColor;
    private static readonly Color arrowPressedColor = new(0.68f, 0.28f, 0.07f);

    private class Row
    {
        public Button button;
        public TMP_Text valueText;
        public RectTransform barFill; // Only volume rows have one of these
        public Image leftArrow;
        public Image rightArrow;
        public Action<int> change;
        public Func<string> getValue;
        public Func<float> getBarValue;

        public bool IsVolume => barFill != null;
    }

    private readonly List<Row> rows = new();
    private readonly List<Vector2Int> resolutions = new();

    private GameObject panel;
    private Button backButton;
    private Button controlsButton; // Only the main menu's options have this
    private int shownFrame = -1;
    private Action onClose;
    private Func<Gamepad> getGamepad;
    private bool closeOnEscape;

    private bool isFullscreen;
    private int resolutionIndex;

    private int heldDirection;
    private float nextRepeatTime;
    private Image flashedArrow;
    private float arrowFlashEndTime;
    private int lastClickFrame = -1;

    public GameObject Panel => panel;
    public Button FirstButton => rows[0].button;
    public Button ControlsButton => controlsButton;

    // Builds the options on the given canvas, using the menu font.
    // gamepad returns whichever controller is using the menu (null if nobody is on one).
    // inMatch is true when opened from the pause menu instead of the main menu, which already has a background
    // showing behind this. There, Escape is also the pause button, which PauseMenu handles.
    // controlsAction opens the controls screen, for the button to it that the main menu's options have. The pause menu
    // has its own controls button, so its options don't.
    public void Build(Canvas canvas, TMP_FontAsset fontAsset, Func<Gamepad> gamepad, bool inMatch, Action closeAction, Action controlsAction = null)
    {
        getGamepad = gamepad;
        closeOnEscape = !inMatch;
        onClose = closeAction;

        // The scenes have the screen in them already (see MenuScreenBuilder). It only gets made here for a scene
        // that doesn't.
        Transform existing = canvas.transform.Find(PanelName);
        panel = existing != null ? existing.gameObject : BuildPanel(canvas, fontAsset, inMatch);

        rows.Clear();
        BindVolumeRow(masterVolumeRow, () => PlayerOptions.MasterVolume, PlayerOptions.SetMasterVolume);
        BindVolumeRow(musicVolumeRow, () => PlayerOptions.MusicVolume, PlayerOptions.SetMusicVolume);
        BindVolumeRow(sfxVolumeRow, () => PlayerOptions.SfxVolume, PlayerOptions.SetSfxVolume);
        BindChoiceRow(fullscreenRow, ToggleFullscreen, () => isFullscreen ? "On" : "Off");
        BindChoiceRow(resolutionRow, ChangeResolution, () => $"{resolutions[resolutionIndex].x} x {resolutions[resolutionIndex].y}");
        BindChoiceRow(vSyncRow, _ => PlayerOptions.SetVSync(!PlayerOptions.VSync), () => PlayerOptions.VSync ? "On" : "Off");

        backButton = panel.transform.Find(backButtonName).GetComponent<Button>();
        AddClickListener(backButton, () => onClose());

        Transform controls = panel.transform.Find(ControlsButtonName);
        if (controls == null && controlsAction != null) controls = BuildControlsButton(panel.transform, fontAsset).transform;
        if (controls != null) controls.gameObject.SetActive(controlsAction != null);

        controlsButton = controls != null && controlsAction != null ? controls.GetComponent<Button>() : null;
        if (controlsButton != null) AddClickListener(controlsButton, controlsAction);

        LinkNavigation();
        panel.SetActive(false);
    }

    public const string PanelName = "Options Panel";
    public const string ControlsButtonName = "Controls";

    private const string masterVolumeRow = "Master Volume";
    private const string musicVolumeRow = "Music Volume";
    private const string sfxVolumeRow = "Sound Effects";
    private const string fullscreenRow = "Fullscreen";
    private const string resolutionRow = "Resolution";
    private const string vSyncRow = "V-Sync";

    private const string backButtonName = "Back";
    private const string valueName = "Value";
    private const string leftArrowName = "Left Arrow";
    private const string rightArrowName = "Right Arrow";
    private const string barName = "Volume Bar";
    private const string barFillName = "Fill";

    // Makes the screen's layout on the given canvas, hidden. Also used in the editor to put it into the scenes, where
    // it can be seen and edited (see MenuScreenBuilder). Build finds its parts by name and makes them work, so they
    // need to keep their names.
    public static GameObject BuildPanel(Canvas canvas, TMP_FontAsset font, bool inMatch)
    {
        GameObject panel = new(PanelName, typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(2176f, 1224f); // The same size as the credits screen

        if (inMatch)
        {
            // Pausing freezes game time, which would otherwise stop it from swaying
            GameObject background = Instantiate(Resources.Load<GameObject>(matchBackgroundPath), panel.transform);
            background.name = "Background";
            background.GetComponent<ImageWobble>().useUnscaledTime = true;
        }

        Vector2 center = new(0.5f, 0.5f);
        MenuStyle.AddText(panel.transform, font, "Options", 90f, new Vector2(0f, 360f), new Vector2(1000f, 120f), center, TextAlignmentOptions.Center).name = "Title";

        int rowIndex = 0;
        foreach (string label in new[] { masterVolumeRow, musicVolumeRow, sfxVolumeRow })
        {
            Transform row = BuildRow(panel.transform, font, label, rowIndex++);
            MenuStyle.AddText(row, font, "100%", 44f, new Vector2(1192f, 0f), new Vector2(110f, 84f), new Vector2(0f, 0.5f), TextAlignmentOptions.Right).name = valueName;
            BuildVolumeBar(row);
        }
        foreach (string label in new[] { fullscreenRow, resolutionRow, vSyncRow })
        {
            Transform row = BuildRow(panel.transform, font, label, rowIndex++);
            MenuStyle.AddText(row, font, "On", 44f, new Vector2(752f, 0f), new Vector2(370f, 84f), new Vector2(0f, 0.5f), TextAlignmentOptions.Center).name = valueName;
        }

        MenuStyle.AddText(panel.transform, font, "Left / Right to change", 36f, new Vector2(0f, -295f), new Vector2(1000f, 60f), center, TextAlignmentOptions.Center).name = "Hint";

        Instantiate(Resources.Load<GameObject>(backButtonPath), panel.transform).name = backButtonName;
        if (!inMatch) BuildControlsButton(panel.transform, font);

        panel.SetActive(false);
        return panel;
    }

    // The button under the rows that opens the controls screen
    public static Button BuildControlsButton(Transform panel, TMP_FontAsset font)
    {
        Button button = MenuStyle.AddPillButton(panel, font, ControlsButtonName, MenuStyle.PillColor.Blue, new Vector2(0f, -410f), new Vector2(380f, 100f), 46f);

        // Stays behind the back button, the same as everything else on the screen
        Transform back = panel.Find(backButtonName);
        if (back != null) button.transform.SetSiblingIndex(back.GetSiblingIndex());
        return button;
    }

    public void Show()
    {
        // Read fresh each time, since the window can also be changed outside of this menu (like with Alt+Enter)
        isFullscreen = Screen.fullScreen;
        LoadResolutions();

        foreach (Row row in rows)
            Refresh(row);

        heldDirection = 0;
        ResetFlashedArrow();
        panel.SetActive(true);
        panel.transform.SetAsLastSibling(); // Things can get added to the canvas during a match
        ActivePanel = panel;
        shownFrame = Time.frameCount;
    }

    public void Hide()
    {
        PlayerOptions.Save();
        panel.SetActive(false);
        ActivePanel = null;
    }

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        // The press of B that came back here from the controls screen would otherwise close this one as well
        if (Time.frameCount == shownFrame) return;

        if (flashedArrow != null && Time.unscaledTime >= arrowFlashEndTime) ResetFlashedArrow();

        Gamepad pad = getGamepad();
        Keyboard keyboard = Keyboard.current;

        if ((pad != null && pad.bButton.wasPressedThisFrame) || (closeOnEscape && keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
        {
            onClose();
            return;
        }

        HandleLeftRight(ReadLeftRight(pad, keyboard));
    }

    private int ReadLeftRight(Gamepad pad, Keyboard keyboard)
    {
        if (pad != null)
        {
            // Only counts when it's mostly sideways, since up and down are for moving between rows
            Vector2 stick = pad.leftStick.ReadValue();
            if (Mathf.Abs(stick.x) >= stickDeadzone && Mathf.Abs(stick.x) > Mathf.Abs(stick.y))
                return stick.x > 0f ? 1 : -1;
        }

        if (keyboard != null)
        {
            if (keyboard.rightArrowKey.isPressed) return 1;
            if (keyboard.leftArrowKey.isPressed) return -1;
        }

        return 0;
    }

    private void HandleLeftRight(int direction)
    {
        if (direction == 0)
        {
            heldDirection = 0;
            return;
        }

        // The menu cursor keeps the EventSystem on the same row as itself, so this works for both of them
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        Row row = rows.Find(candidate => candidate.button.gameObject == selected);
        if (row == null) return;

        bool isHeld = direction == heldDirection;

        // Only volumes repeat, since holding the stick shouldn't flicker the window through every resolution
        if (isHeld && (!row.IsVolume || Time.unscaledTime < nextRepeatTime)) return;

        heldDirection = direction;
        nextRepeatTime = Time.unscaledTime + (isHeld ? holdRepeat : holdDelay);

        Change(row, direction);
    }

    private void Change(Row row, int direction)
    {
        row.change(direction);
        Refresh(row);
        FlashArrow(direction > 0 ? row.rightArrow : row.leftArrow);

        // Also lets the player hear what the sound effects are set to
        AudioManager.PlayButtonHoverSound();
    }

    // For the mouse, which can change a row without it being selected first
    private void Select(Row row)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(row.button.gameObject);
    }

    private void FlashArrow(Image arrow)
    {
        ResetFlashedArrow();

        flashedArrow = arrow;
        flashedArrow.color = arrowPressedColor;
        flashedArrow.rectTransform.localScale = Vector3.one * 0.85f;
        arrowFlashEndTime = Time.unscaledTime + arrowFlashDuration;
    }

    private void ResetFlashedArrow()
    {
        if (flashedArrow == null) return;

        flashedArrow.color = accentColor;
        flashedArrow.rectTransform.localScale = Vector3.one;
        flashedArrow = null;
    }

    private void Refresh(Row row)
    {
        row.valueText.text = row.getValue();
        if (row.IsVolume) row.barFill.anchorMax = new Vector2(row.getBarValue(), 1f);
    }

    private void LoadResolutions()
    {
        resolutions.Clear();
        foreach (Resolution resolution in Screen.resolutions)
        {
            // The same size gets listed once for every refresh rate
            Vector2Int size = new(resolution.width, resolution.height);
            if (size.y >= minResolutionHeight && !resolutions.Contains(size)) resolutions.Add(size);
        }

        if (resolutions.Count == 0) resolutions.Add(new Vector2Int(Screen.width, Screen.height));

        resolutionIndex = 0;
        int closest = int.MaxValue;
        for (int i = 0; i < resolutions.Count; ++i)
        {
            int difference = Mathf.Abs(resolutions[i].x - Screen.width) + Mathf.Abs(resolutions[i].y - Screen.height);
            if (difference < closest)
            {
                closest = difference;
                resolutionIndex = i;
            }
        }
    }

    private void ToggleFullscreen(int direction)
    {
        isFullscreen = !isFullscreen;
        ApplyDisplay();
    }

    private void ChangeResolution(int direction)
    {
        resolutionIndex = (resolutionIndex + direction + resolutions.Count) % resolutions.Count;
        ApplyDisplay();
    }

    private void ApplyDisplay()
    {
        Vector2Int size = resolutions[resolutionIndex];
        Screen.SetResolution(size.x, size.y, isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    // Up/down links between rows for the stick (the menu cursor needs explicit ones).
    // Left and right are left empty, since those change the row instead.
    private void LinkNavigation()
    {
        SetNavigation(backButton, null, rows[0].button);

        for (int i = 0; i < rows.Count; ++i)
        {
            Button up = i > 0 ? rows[i - 1].button : backButton;
            Button down = i < rows.Count - 1 ? rows[i + 1].button : controlsButton;
            SetNavigation(rows[i].button, up, down);
        }

        if (controlsButton != null) SetNavigation(controlsButton, rows[rows.Count - 1].button, null);
    }

    private void SetNavigation(Button button, Button up, Button down)
    {
        button.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down
        };
    }

    private void BindVolumeRow(string label, Func<float> getVolume, Action<float> setVolume)
    {
        Row row = BindRow(label);
        row.getBarValue = getVolume;
        row.getValue = () => $"{Mathf.RoundToInt(getVolume() * 100f)}%";
        row.change = direction => setVolume(getVolume() + direction * volumeStep);

        Transform bar = row.button.transform.Find(barName);
        row.barFill = (RectTransform)bar.Find(barFillName);

        // Clicking or dragging along the bar sets the volume to that point
        bar.GetComponent<OptionsBarPointer>().onPoint = amount =>
        {
            Select(row);
            float before = getVolume();
            setVolume(amount);
            Refresh(row);
            if (getVolume() != before) AudioManager.PlayButtonHoverSound();
        };

        Refresh(row);
    }

    private void BindChoiceRow(string label, Action<int> change, Func<string> getValue)
    {
        Row row = BindRow(label);
        row.getValue = getValue;
        row.change = change;

        // A also works on these, since there's only a next thing for it to go to
        AddClickListener(row.button, () =>
        {
            row.change(1);
            Refresh(row);
            FlashArrow(row.rightArrow);
        });
    }

    // Finds a row's parts and makes its arrows work
    private Row BindRow(string label)
    {
        Transform rowTransform = panel.transform.Find(label);

        Row row = new()
        {
            button = rowTransform.GetComponent<Button>(),
            valueText = rowTransform.Find(valueName).GetComponent<TMP_Text>(),
            leftArrow = rowTransform.Find(leftArrowName).GetComponent<Image>(),
            rightArrow = rowTransform.Find(rightArrowName).GetComponent<Image>()
        };
        row.leftArrow.GetComponent<OptionsArrowPointer>().onClick = () => { Select(row); Change(row, -1); };
        row.rightArrow.GetComponent<OptionsArrowPointer>().onClick = () => { Select(row); Change(row, 1); };

        rows.Add(row);
        return row;
    }

    private static Transform BuildRow(Transform panel, TMP_FontAsset font, string label, int index)
    {
        const float rowSpacing = 92f;

        GameObject rowObject = new(label, typeof(RectTransform), typeof(Image), typeof(Button));
        rowObject.transform.SetParent(panel, false);

        // With the pivot on the left, the menu cursor points at the start of the row and not over its value
        RectTransform rect = rowObject.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(1320f, 84f);
        rect.anchoredPosition = new Vector2(-660f, 250f - index * rowSpacing);

        Button button = rowObject.GetComponent<Button>();
        button.targetGraphic = rowObject.GetComponent<Image>();
        MenuStyle.MakeRounded(rowObject.GetComponent<Image>(), rect.sizeDelta.y);
        MenuStyle.MakeClickable(rowObject.GetComponent<Image>());

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.35f);
        colors.highlightedColor = colors.selectedColor = new Color(1f, 0.82f, 0.3f, 1f);
        colors.pressedColor = new Color(1f, 0.7f, 0.2f, 1f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        MenuStyle.AddText(rowObject.transform, font, label, 44f, new Vector2(90f, 0f), new Vector2(570f, 84f), new Vector2(0f, 0.5f), TextAlignmentOptions.Left).name = "Label";

        BuildArrow(rowObject.transform, font, "<", 680f);
        BuildArrow(rowObject.transform, font, ">", 1134f);
        return rowObject.transform;
    }

    // These show what left and right do, and press in when they get used. The mouse can click them, but they aren't
    // selectable, since stopping on them would get in the way of moving up and down between rows.
    private static void BuildArrow(Transform parent, TMP_FontAsset font, string symbol, float x)
    {
        GameObject arrow = new(symbol == "<" ? leftArrowName : rightArrowName, typeof(RectTransform), typeof(Image), typeof(OptionsArrowPointer));
        arrow.transform.SetParent(parent, false);

        // The pivot stays in the middle so that it presses in evenly, which is why half of its size gets added back on
        RectTransform rect = arrow.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x + arrowSize / 2f, 0f);
        rect.sizeDelta = new Vector2(arrowSize, arrowSize);

        Image image = arrow.GetComponent<Image>();
        image.color = accentColor;
        MenuStyle.MakeRounded(image, arrowSize);
        MenuStyle.MakeClickable(image);

        TMP_Text text = MenuStyle.AddText(arrow.transform, font, symbol, 44f, Vector2.zero, rect.sizeDelta, new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);
        text.color = Color.white;
    }

    private static void BuildVolumeBar(Transform parent)
    {
        const float barHeight = 28f;

        GameObject bar = new(barName, typeof(RectTransform), typeof(Image), typeof(OptionsBarPointer));
        bar.transform.SetParent(parent, false);
        bar.GetComponent<Image>().color = new Color(textColor.r, textColor.g, textColor.b, 0.3f);
        MenuStyle.MakeRounded(bar.GetComponent<Image>(), barHeight);
        MenuStyle.MakeClickable(bar.GetComponent<Image>());

        // Fits between the row's two arrows
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = barRect.anchorMax = barRect.pivot = new Vector2(0f, 0.5f);
        barRect.anchoredPosition = new Vector2(752f, 0f);
        barRect.sizeDelta = new Vector2(370f, barHeight);

        GameObject fill = new(barFillName, typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bar.transform, false);
        fill.GetComponent<Image>().color = accentColor;
        MenuStyle.MakeRounded(fill.GetComponent<Image>(), barHeight);

        // How far it goes across the bar gets set by moving its right anchor
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
    }

    private void AddClickListener(Button button, Action onClick)
    {
        button.onClick.AddListener(() =>
        {
            // Avoid double clicks from one A press
            if (lastClickFrame == Time.frameCount) return;
            lastClickFrame = Time.frameCount;
            onClick();
        });
    }
}
