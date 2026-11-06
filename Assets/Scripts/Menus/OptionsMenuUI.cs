using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The options screen: volume and display settings. It's made for a controller, so the stick moves up and down
/// between rows the same as any other menu, left and right changes the selected row, and nothing needs a mouse.
/// It's built in code and pretty bare bones for now, so it can get swapped out for a proper designed screen later.
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
    private TMP_FontAsset font;
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

    // Builds the options on the given canvas, using the menu font.
    // gamepad returns whichever controller is using the menu (null if nobody is on one).
    // inMatch is true when opened from the pause menu instead of the main menu, which already has a background
    // showing behind this. There, Escape is also the pause button, which PauseMenu handles.
    public void Build(Canvas canvas, TMP_FontAsset fontAsset, Func<Gamepad> gamepad, bool inMatch, Action closeAction)
    {
        font = fontAsset;
        getGamepad = gamepad;
        closeOnEscape = !inMatch;
        onClose = closeAction;

        panel = new GameObject("Options Panel", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(2176f, 1224f); // The same size as the credits screen

        if (inMatch)
        {
            // Pausing freezes game time, which would otherwise stop it from swaying
            GameObject background = Instantiate(Resources.Load<GameObject>(matchBackgroundPath), panel.transform);
            background.GetComponent<ImageWobble>().useUnscaledTime = true;
        }

        AddText(panel.transform, "Options", 90f, new Vector2(0f, 360f), new Vector2(1000f, 120f), new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);

        AddVolumeRow("Master Volume", () => PlayerOptions.MasterVolume, PlayerOptions.SetMasterVolume);
        AddVolumeRow("Music Volume", () => PlayerOptions.MusicVolume, PlayerOptions.SetMusicVolume);
        AddVolumeRow("Sound Effects", () => PlayerOptions.SfxVolume, PlayerOptions.SetSfxVolume);
        AddChoiceRow("Fullscreen", ToggleFullscreen, () => isFullscreen ? "On" : "Off");
        AddChoiceRow("Resolution", ChangeResolution, () => $"{resolutions[resolutionIndex].x} x {resolutions[resolutionIndex].y}");
        AddChoiceRow("V-Sync", _ => PlayerOptions.SetVSync(!PlayerOptions.VSync), () => PlayerOptions.VSync ? "On" : "Off");

        AddText(panel.transform, "Left / Right to change", 36f, new Vector2(0f, -295f), new Vector2(1000f, 60f), new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);

        backButton = Instantiate(Resources.Load<GameObject>(backButtonPath), panel.transform).GetComponent<Button>();
        AddClickListener(backButton, () => onClose());

        LinkNavigation();
        panel.SetActive(false);
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

        row.change(direction);
        Refresh(row);
        FlashArrow(direction > 0 ? row.rightArrow : row.leftArrow);

        // Also lets the player hear what the sound effects are set to
        AudioManager.PlayButtonHoverSound();
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
            Button down = i < rows.Count - 1 ? rows[i + 1].button : null;
            SetNavigation(rows[i].button, up, down);
        }
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

    private void AddVolumeRow(string label, Func<float> getVolume, Action<float> setVolume)
    {
        Row row = AddRow(label);
        row.getBarValue = getVolume;
        row.getValue = () => $"{Mathf.RoundToInt(getVolume() * 100f)}%";
        row.change = direction => setVolume(getVolume() + direction * volumeStep);

        row.valueText = AddText(row.button.transform, string.Empty, 44f, new Vector2(1192f, 0f), new Vector2(110f, 84f), new Vector2(0f, 0.5f), TextAlignmentOptions.Right);
        row.barFill = AddVolumeBar(row.button.transform);

        Refresh(row);
    }

    private void AddChoiceRow(string label, Action<int> change, Func<string> getValue)
    {
        Row row = AddRow(label);
        row.getValue = getValue;
        row.change = change;

        row.valueText = AddText(row.button.transform, string.Empty, 44f, new Vector2(752f, 0f), new Vector2(370f, 84f), new Vector2(0f, 0.5f), TextAlignmentOptions.Center);

        // A also works on these, since there's only a next thing for it to go to
        AddClickListener(row.button, () =>
        {
            row.change(1);
            Refresh(row);
            FlashArrow(row.rightArrow);
        });
    }

    private Row AddRow(string label)
    {
        const float rowSpacing = 92f;

        GameObject rowObject = new(label, typeof(RectTransform), typeof(Image), typeof(Button));
        rowObject.transform.SetParent(panel.transform, false);

        // With the pivot on the left, the menu cursor points at the start of the row and not over its value
        RectTransform rect = rowObject.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(1320f, 84f);
        rect.anchoredPosition = new Vector2(-660f, 250f - rows.Count * rowSpacing);

        Button button = rowObject.GetComponent<Button>();
        button.targetGraphic = rowObject.GetComponent<Image>();
        MenuStyle.MakeRounded(rowObject.GetComponent<Image>(), rect.sizeDelta.y);

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.35f);
        colors.highlightedColor = colors.selectedColor = new Color(1f, 0.82f, 0.3f, 1f);
        colors.pressedColor = new Color(1f, 0.7f, 0.2f, 1f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        AddText(rowObject.transform, label, 44f, new Vector2(90f, 0f), new Vector2(570f, 84f), new Vector2(0f, 0.5f), TextAlignmentOptions.Left);

        Row row = new() { button = button };
        row.leftArrow = AddArrow(rowObject.transform, "<", 680f);
        row.rightArrow = AddArrow(rowObject.transform, ">", 1134f);
        rows.Add(row);
        return row;
    }

    // These show what left and right do, and press in when they get used. They aren't selectable, since there's no
    // mouse to click them with, and stopping on them would get in the way of moving up and down between rows.
    private Image AddArrow(Transform parent, string symbol, float x)
    {
        GameObject arrow = new(symbol == "<" ? "Left Arrow" : "Right Arrow", typeof(RectTransform), typeof(Image));
        arrow.transform.SetParent(parent, false);

        // The pivot stays in the middle so that it presses in evenly, which is why half of its size gets added back on
        RectTransform rect = arrow.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x + arrowSize / 2f, 0f);
        rect.sizeDelta = new Vector2(arrowSize, arrowSize);

        Image image = arrow.GetComponent<Image>();
        image.color = accentColor;
        MenuStyle.MakeRounded(image, arrowSize);

        TMP_Text text = AddText(arrow.transform, symbol, 44f, Vector2.zero, rect.sizeDelta, new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);
        text.color = Color.white;
        return image;
    }

    private RectTransform AddVolumeBar(Transform parent)
    {
        const float barHeight = 28f;

        GameObject bar = new("Volume Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        bar.GetComponent<Image>().color = new Color(textColor.r, textColor.g, textColor.b, 0.3f);
        MenuStyle.MakeRounded(bar.GetComponent<Image>(), barHeight);

        // Fits between the row's two arrows
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = barRect.anchorMax = barRect.pivot = new Vector2(0f, 0.5f);
        barRect.anchoredPosition = new Vector2(752f, 0f);
        barRect.sizeDelta = new Vector2(370f, barHeight);

        GameObject fill = new("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bar.transform, false);
        fill.GetComponent<Image>().color = accentColor;
        MenuStyle.MakeRounded(fill.GetComponent<Image>(), barHeight);

        // How far it goes across the bar gets set by moving its right anchor
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        return fillRect;
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

    private TMP_Text AddText(Transform parent, string message, float fontSize, Vector2 position, Vector2 size, Vector2 anchor, TextAlignmentOptions alignment)
    {
        return MenuStyle.AddText(parent, font, message, fontSize, position, size, anchor, alignment);
    }
}
