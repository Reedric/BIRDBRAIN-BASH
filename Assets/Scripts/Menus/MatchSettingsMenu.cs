using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Match Settings overlay for the Character Select screen.
///
/// This is an OVERLAY, not a separate scene/menu.
///
/// Features:
/// - Number of Sets cycles circularly through configurable values.
/// - Each Sets value has its own stylized RawImage texture.
/// - Bot Difficulty cycles Easy -> Medium -> Hard.
/// - Each difficulty has its own stylized RawImage texture.
/// - Maps can be individually enabled/disabled.
/// - At least one map is always enabled.
/// - The player who opens the menu becomes the only player allowed
///   to control it.
/// - Only that player's cursor is visible.
/// - Unity UI beneath the overlay is blocked while the menu is open.
/// - Pressing the Match Settings button again closes the overlay.
/// - Settings are saved when the overlay closes.
/// </summary>
public class MatchSettingsMenu : MonoBehaviour
{
    // ============================================================
    // MENU OVERLAY
    // ============================================================

    [Header("Menu Overlay")]

    [Tooltip("The panel containing the entire Match Settings UI.")]
    [SerializeField] private GameObject settingsPanel;

    [Tooltip(
        "Optional CanvasGroup on the settings panel. " +
        "Used to block UI underneath the overlay."
    )]
    [SerializeField] private CanvasGroup settingsCanvasGroup;
    // Persistent match settings singleton — same one TeamArrangementToggle reads from.
    private GameSettings gameSettings;

    // ============================================================
    // NUMBER OF SETS
    // ============================================================

    [Header("Number of Sets")]

    [SerializeField] private Button setsLeftButton;
    [SerializeField] private Button setsRightButton;

    [Tooltip("RawImage displaying the stylized sets artwork.")]
    [SerializeField] private RawImage setsDisplay;
    [SerializeField] private TMP_Text setsValueText;

    [Tooltip(
        "Available number-of-sets values. " +
        "These correspond by index with Sets Textures."
    )]
    [SerializeField] private int[] setsValues = { 1, 3, 5, 9 };

    [Tooltip(
        "Stylized textures for each sets value. " +
        "Element 0 corresponds to Sets Values element 0, etc."
    )]
    [SerializeField] private Texture2D[] setsTextures;

    // ============================================================
    // BOT DIFFICULTY
    // ============================================================

    [Header("Bot Difficulty")]

    [SerializeField] private Button difficultyLeftButton;
    [SerializeField] private Button difficultyRightButton;

    [Tooltip("RawImage displaying the stylized difficulty artwork.")]
    [SerializeField] private RawImage difficultyDisplay;
    [SerializeField] private TMP_Text difficultyValueText;

    [SerializeField] private Texture2D easyTexture;
    [SerializeField] private Texture2D mediumTexture;
    [SerializeField] private Texture2D hardTexture;

    // ============================================================
    // MAPS
    // ============================================================

    [Header("Maps In Rotation")]

    [SerializeField] private Toggle beachToggle;
    [SerializeField] private Toggle parkToggle;
    [SerializeField] private Toggle icebergToggle;
    [SerializeField] private Toggle volcanoToggle;

    // ============================================================
    // MATCH SETTINGS BUTTON
    // ============================================================

    [Header("Match Settings Toggle Button")]

    [Tooltip(
        "The button used to open/close this overlay. " +
        "If this button is underneath the overlay, use the " +
        "player-specific ToggleForPlayer methods instead."
    )]
    [SerializeField] private Button matchSettingsButton;

    // ============================================================
    // PLAYER CURSORS
    // ============================================================

    [Header("Player Cursors")]

    // Dynamic cursors created at runtime by CharacterSelectManager; index-aligned with player index.
    private List<Transform> runtimePlayerCursors;

    // ============================================================
    // EVENT SYSTEM
    // ============================================================

    [Header("UI Focus")]

    [Tooltip(
        "Optional UI element to automatically select when the menu opens. " +
        "Usually the Sets Left or Sets Right button."
    )]
    [SerializeField] private Selectable firstSelectedControl;

    // ============================================================
    // STATE
    // ============================================================

    private int currentSetsIndex;
    private BotDifficulty currentDifficulty;

    private int controllingPlayer = -1;

    private bool menuOpen = false;

    // ============================================================
    // BOT DIFFICULTY
    // ============================================================

    public enum BotDifficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2
    }

    // ============================================================
    // PUBLIC ACCESSORS
    // ============================================================

    public int NumberOfSets
    {
        get
        {
            if (setsValues == null || setsValues.Length == 0)
                return 3;

            if (currentSetsIndex < 0 ||
                currentSetsIndex >= setsValues.Length)
            {
                return 3;
            }

            return setsValues[currentSetsIndex];
        }
    }

    public BotDifficulty CurrentBotDifficulty =>
        currentDifficulty;

    public int ControllingPlayer =>
        controllingPlayer;

    public bool IsMenuOpen =>
        menuOpen;

    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        // Make sure a persistent GameSettings object exists before we read from it.
        gameSettings = GameSettings.EnsureInstance();

        EnsureRuntimeUI();
        InitializeDefaults();
        RegisterListeners();

        UpdateSetsDisplay();
        UpdateDifficultyDisplay();

        // The settings overlay starts closed.
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        menuOpen = false;
        controllingPlayer = -1;

        HideAllCursors();

        ConfigureCanvasGroup();
    }

    private void EnsureRuntimeUI()
    {
        if (settingsPanel != null)
            return;

        settingsPanel = new GameObject("MatchSettingsPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        settingsPanel.transform.SetParent(transform, false);

        RectTransform panelTransform = settingsPanel.GetComponent<RectTransform>();
        panelTransform.anchorMin = Vector2.zero;
        panelTransform.anchorMax = Vector2.one;
        panelTransform.offsetMin = Vector2.zero;
        panelTransform.offsetMax = Vector2.zero;

        Image panelImage = settingsPanel.GetComponent<Image>();
        panelImage.color = new Color(0.04f, 0.06f, 0.1f, 0.98f);

        settingsCanvasGroup = settingsPanel.GetComponent<CanvasGroup>();
        CreateRuntimeText("Title", "MATCH SETTINGS", 64, new Vector2(0f, 330f), settingsPanel.transform);

        CreateRuntimeRow("Sets", "NUMBER OF SETS", new Vector2(0f, 160f), settingsPanel.transform,
            out setsLeftButton, out setsValueText, out setsRightButton);
        CreateRuntimeRow("Difficulty", "BOT DIFFICULTY", new Vector2(0f, 40f), settingsPanel.transform,
            out difficultyLeftButton, out difficultyValueText, out difficultyRightButton);

        CreateRuntimeText("MapsLabel", "MAPS IN ROTATION", 30, new Vector2(0f, -80f), settingsPanel.transform);
        beachToggle = CreateRuntimeToggle("Beach", "Beach", new Vector2(-180f, -145f), settingsPanel.transform);
        parkToggle = CreateRuntimeToggle("Park", "Park", new Vector2(-60f, -145f), settingsPanel.transform);
        icebergToggle = CreateRuntimeToggle("Iceberg", "Iceberg", new Vector2(70f, -145f), settingsPanel.transform);
        volcanoToggle = CreateRuntimeToggle("Volcano", "Volcano", new Vector2(200f, -145f), settingsPanel.transform);

        Button closeButton = CreateRuntimeButton("Close", "CLOSE", new Vector2(0f, -300f), settingsPanel.transform);
        closeButton.onClick.AddListener(CloseMenu);
    }

    private void CreateRuntimeRow(string name, string label, Vector2 position, Transform parent,
        out Button leftButton, out TMP_Text valueText, out Button rightButton)
    {
        CreateRuntimeText(name + "Label", label, 30, position + new Vector2(-220f, 0f), parent);
        leftButton = CreateRuntimeButton(name + "Left", "<", position + new Vector2(90f, 0f), parent);
        valueText = CreateRuntimeText(name + "Value", string.Empty, 34, position + new Vector2(170f, 0f), parent);
        rightButton = CreateRuntimeButton(name + "Right", ">", position + new Vector2(250f, 0f), parent);
    }

    private TMP_Text CreateRuntimeText(string objectName, string value, float fontSize, Vector2 position, Transform parent)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        RectTransform rectTransform = text.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(360f, 70f);
        rectTransform.anchoredPosition = position;
        return text;
    }

    private Button CreateRuntimeButton(string objectName, string label, Vector2 position, Transform parent)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.24f, 0.36f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        RectTransform rectTransform = button.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(70f, 60f);
        rectTransform.anchoredPosition = position;
        CreateRuntimeText("Label", label, 30, Vector2.zero, buttonObject.transform);
        return button;
    }

    private Toggle CreateRuntimeToggle(string objectName, string label, Vector2 position, Transform parent)
    {
        GameObject toggleObject = new GameObject(objectName, typeof(RectTransform), typeof(Toggle));
        toggleObject.transform.SetParent(parent, false);
        Toggle toggle = toggleObject.GetComponent<Toggle>();
        RectTransform rectTransform = toggleObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(120f, 50f);
        rectTransform.anchoredPosition = position;

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(toggleObject.transform, false);
        Image background = backgroundObject.GetComponent<Image>();
        background.color = Color.gray;
        RectTransform backgroundTransform = background.rectTransform;
        backgroundTransform.anchorMin = new Vector2(0f, 0.5f);
        backgroundTransform.anchorMax = new Vector2(0f, 0.5f);
        backgroundTransform.sizeDelta = new Vector2(28f, 28f);
        toggle.targetGraphic = background;

        CreateRuntimeText("Label", label, 20, new Vector2(45f, 0f), toggleObject.transform);
        return toggle;
    }

    // ============================================================
    // INITIALIZATION
    // ============================================================

    private void InitializeDefaults()
    {
        if (gameSettings == null)
            gameSettings = GameSettings.EnsureInstance();

        // --------------------------------------------------------
        // SETS — read from GameSettings so this reflects whatever was
        // last saved, instead of always resetting to 3.
        // --------------------------------------------------------

        currentSetsIndex = FindSetsIndex(gameSettings.BestOfSets);

        // --------------------------------------------------------
        // DIFFICULTY
        // --------------------------------------------------------

        currentDifficulty = (BotDifficulty)(int)gameSettings.CurrentBotDifficulty;

        // --------------------------------------------------------
        // MAPS
        // --------------------------------------------------------

        if (beachToggle != null)
            beachToggle.SetIsOnWithoutNotify(gameSettings.BeachEnabled);

        if (parkToggle != null)
            parkToggle.SetIsOnWithoutNotify(gameSettings.ParkEnabled);

        if (icebergToggle != null)
            icebergToggle.SetIsOnWithoutNotify(gameSettings.IcebergEnabled);

        if (volcanoToggle != null)
            volcanoToggle.SetIsOnWithoutNotify(gameSettings.VolcanoEnabled);
    }

    private void RegisterListeners()
    {
        // ========================================================
        // SETS
        // ========================================================

        if (setsLeftButton != null)
            setsLeftButton.onClick.AddListener(PreviousSets);

        if (setsRightButton != null)
            setsRightButton.onClick.AddListener(NextSets);

        // ========================================================
        // DIFFICULTY
        // ========================================================

        if (difficultyLeftButton != null)
            difficultyLeftButton.onClick.AddListener(PreviousDifficulty);

        if (difficultyRightButton != null)
            difficultyRightButton.onClick.AddListener(NextDifficulty);

        // ========================================================
        // MATCH SETTINGS BUTTON
        // ========================================================

        if (matchSettingsButton != null &&
            matchSettingsButton.onClick.GetPersistentEventCount() == 0)
        {
            matchSettingsButton.onClick.AddListener(
                ToggleMenuFromButton
            );
        }

        // ========================================================
        // MAP TOGGLES
        // ========================================================
        if (beachToggle != null)
        {
            beachToggle.onValueChanged.AddListener(
                value => ValidateMapSelection(beachToggle, value)
            );

            // TEMP DIAGNOSTIC — remove once this is resolved.
            beachToggle.onValueChanged.AddListener(
                value => Debug.Log($"[MatchSettings Diag] onValueChanged BeachToggle -> {value} @ frame {Time.frameCount}")
            );
        }

        if (parkToggle != null)
        {
            parkToggle.onValueChanged.AddListener(
                value => ValidateMapSelection(parkToggle, value)
            );
        }

        if (icebergToggle != null)
        {
            icebergToggle.onValueChanged.AddListener(
                value => ValidateMapSelection(icebergToggle, value)
            );
        }

        if (volcanoToggle != null)
        {
            volcanoToggle.onValueChanged.AddListener(
                value => ValidateMapSelection(volcanoToggle, value)
            );
        }
    }

    // ============================================================
    // OPEN / CLOSE TOGGLE
    // ============================================================

    /// <summary>
    /// Toggles the menu using whichever player is already controlling
    /// the UI.
    ///
    /// Useful when the Match Settings button itself is already tied
    /// to a specific player's cursor.
    /// </summary>
    private void ToggleMenuFromButton()
    {
        if (menuOpen)
        {
            CloseMenu();
            return;
        }

        // If a player has already been assigned, keep that player; otherwise default to Player 1.
        OpenForPlayer(controllingPlayer >= 0 ? controllingPlayer : 0);
    }

    // ============================================================
    // OPEN FOR PLAYER
    // ============================================================

    /// <summary>
    /// Opens the settings overlay for the specified player (0 = Player 1, etc.),
    /// caching the dynamic cursors created by CharacterSelectManager.
    /// </summary>
    public void OpenForPlayer(int playerIndex, List<Transform> dynamicCursors)
    {
        runtimePlayerCursors = dynamicCursors;
        OpenForPlayer(playerIndex);
    }

    private void OpenForPlayer(int playerIndex)
    {
        // Ensure host object is active so coroutines can run
        gameObject.SetActive(true);

        // Pull in anything that changed elsewhere while this was closed.
        Refresh();

        controllingPlayer = playerIndex;
        menuOpen = true;

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            settingsPanel.transform.SetAsLastSibling();
        }

        if (matchSettingsButton != null)
            matchSettingsButton.transform.SetAsLastSibling();

        ConfigureCanvasGroup();
        ShowOnlyCursor(playerIndex);

        // Refresh CharacterSelectManager targets so snapping targets the settings options
        if (CharacterSelectManager.Instance != null)
        {
            CharacterSelectManager.Instance.CollectUITargets();
        }

        StartCoroutine(FocusMenuNextFrame());
    }

    // ============================================================
    // PLAYER-SPECIFIC TOGGLE
    // ============================================================

    /// <summary>
    /// Toggles the menu for a specific player. Assign this directly to a
    /// per-player button in the Inspector (passing 0-3 as the argument).
    ///
    /// If closed: opens for that player.
    /// If already open: only the controlling player can close it.
    /// </summary>
    public void ToggleForPlayer(int playerIndex)
    {
        if (menuOpen)
        {
            if (playerIndex == controllingPlayer)
                CloseMenu();

            return;
        }

        OpenForPlayer(playerIndex);
    }

    // ============================================================
    // CLOSE
    // ============================================================

    /// <summary>
    /// Closes the overlay and saves the current settings.
    /// </summary>
    public void CloseMenu()
    {
        if (!menuOpen) return;

        SaveSettings();
        menuOpen = false;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // Restore visibility for all dynamic cursors
        if (runtimePlayerCursors != null)
        {
            foreach (var cursor in runtimePlayerCursors)
            {
                if (cursor != null) cursor.gameObject.SetActive(true);
            }
        }

        if (CharacterSelectManager.Instance != null)
        {
            CharacterSelectManager.Instance.CollectUITargets();
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        controllingPlayer = -1;
    }

    // ============================================================
    // SETS
    // ============================================================

    private void PreviousSets()
    {
        if (!menuOpen)
            return;

        if (setsValues == null ||
            setsValues.Length == 0)
        {
            return;
        }

        currentSetsIndex--;

        // Circular:
        //
        // 1 <- 3 <- 5 <- 9 <- 1
        if (currentSetsIndex < 0)
            currentSetsIndex = setsValues.Length - 1;

        UpdateSetsDisplay();
        PushSetsToGameSettings();
    }

    private void NextSets()
    {
        if (!menuOpen)
            return;

        if (setsValues == null ||
            setsValues.Length == 0)
        {
            return;
        }

        currentSetsIndex++;

        // Circular:
        //
        // 1 -> 3 -> 5 -> 9 -> 1
        if (currentSetsIndex >= setsValues.Length)
            currentSetsIndex = 0;

        UpdateSetsDisplay();
        PushSetsToGameSettings();
    }

    // Writes Number of Sets straight to GameSettings — same live-update
    // pattern as TeamArrangementToggle.OnTogglePressed().
    private void PushSetsToGameSettings()
    {
        if (gameSettings == null)
            gameSettings = GameSettings.EnsureInstance();

        gameSettings.BestOfSets = NumberOfSets;
    }

    private void UpdateSetsDisplay()
    {
        if (setsValues == null ||
            setsValues.Length == 0)
        {
            Debug.LogWarning(
                "MatchSettingsMenu: No Sets Values assigned."
            );

            return;
        }

        if (currentSetsIndex < 0 ||
            currentSetsIndex >= setsValues.Length)
        {
            return;
        }

        if (setsValueText != null)
            setsValueText.text = setsValues[currentSetsIndex].ToString();

        if (setsTextures == null ||
            setsTextures.Length == 0)
        {
            Debug.LogWarning(
                "MatchSettingsMenu: No Sets Textures assigned."
            );

            return;
        }

        if (currentSetsIndex >= setsTextures.Length)
        {
            return;
        }

        Texture2D texture =
            setsTextures[currentSetsIndex];

        if (texture != null)
        {
            if (setsDisplay != null)
                setsDisplay.texture = texture;
        }

    }

    private int FindSetsIndex(int value)
    {
        if (setsValues == null ||
            setsValues.Length == 0)
        {
            return 0;
        }

        for (int i = 0; i < setsValues.Length; i++)
        {
            if (setsValues[i] == value)
                return i;
        }

        Debug.LogWarning(
            $"MatchSettingsMenu: {value} is not present in Sets Values. " +
            "Using the first available value."
        );

        return 0;
    }

    // ============================================================
    // DIFFICULTY
    // ============================================================

    private void PreviousDifficulty()
    {
        if (!menuOpen)
            return;

        int count =
            Enum.GetValues(typeof(BotDifficulty)).Length;

        int value = (int)currentDifficulty;

        value--;

        if (value < 0)
            value = count - 1;

        currentDifficulty =
            (BotDifficulty)value;

        UpdateDifficultyDisplay();
        PushDifficultyToGameSettings();
    }

    private void NextDifficulty()
    {
        if (!menuOpen)
            return;

        int count =
            Enum.GetValues(typeof(BotDifficulty)).Length;

        int value = (int)currentDifficulty;

        value++;

        if (value >= count)
            value = 0;

        currentDifficulty =
            (BotDifficulty)value;

        UpdateDifficultyDisplay();
        PushDifficultyToGameSettings();
    }

    private void UpdateDifficultyDisplay()
    {
        if (difficultyValueText != null)
            difficultyValueText.text = currentDifficulty.ToString();

        if (difficultyDisplay == null)
            return;

        switch (currentDifficulty)
        {
            case BotDifficulty.Easy:

                difficultyDisplay.texture =
                    easyTexture;

                break;

            case BotDifficulty.Medium:

                difficultyDisplay.texture =
                    mediumTexture;

                break;

            case BotDifficulty.Hard:

                difficultyDisplay.texture =
                    hardTexture;

                break;
        }
    }

    private void PushDifficultyToGameSettings()
    {
        if (gameSettings == null)
            gameSettings = GameSettings.EnsureInstance();

        gameSettings.CurrentBotDifficulty = (GameSettings.BotDifficulty)(int)currentDifficulty;
    }

    // ============================================================
    // MAPS
    // ============================================================

    private void ValidateMapSelection(Toggle changedToggle, bool newValue)
    {
        if (!menuOpen)
            return;

        // Turning a map ON is always allowed.
        if (newValue)
        {
            PushMapsToGameSettings();
            return;
        }

        // If at least one other map remains selected, everything is fine.
        if (GetSelectedMapCount() > 0)
        {
            PushMapsToGameSettings();
            return;
        }

        // The player attempted to turn off the final map. Restore THAT SAME map.
        if (changedToggle != null)
            changedToggle.SetIsOnWithoutNotify(true);
        else
            RestoreAnyMap();

        PushMapsToGameSettings();
    }

    // Writes the current map toggle states straight to GameSettings.
    // Reuses GameSettings.SetMapRotation(), which has its own
    // "at least one map enabled" safety net too.
    private void PushMapsToGameSettings()
    {
        if (gameSettings == null)
            gameSettings = GameSettings.EnsureInstance();

        gameSettings.SetMapRotation(
            beachToggle != null && beachToggle.isOn,
            parkToggle != null && parkToggle.isOn,
            icebergToggle != null && icebergToggle.isOn,
            volcanoToggle != null && volcanoToggle.isOn
        );
    }

    private int GetSelectedMapCount()
    {
        int count = 0;

        if (beachToggle != null &&
            beachToggle.isOn)
        {
            count++;
        }

        if (parkToggle != null &&
            parkToggle.isOn)
        {
            count++;
        }

        if (icebergToggle != null &&
            icebergToggle.isOn)
        {
            count++;
        }

        if (volcanoToggle != null &&
            volcanoToggle.isOn)
        {
            count++;
        }

        return count;
    }

    private void RestoreAnyMap()
    {
        if (beachToggle != null)
        {
            beachToggle.SetIsOnWithoutNotify(true);
            return;
        }

        if (parkToggle != null)
        {
            parkToggle.SetIsOnWithoutNotify(true);
            return;
        }

        if (icebergToggle != null)
        {
            icebergToggle.SetIsOnWithoutNotify(true);
            return;
        }

        if (volcanoToggle != null)
        {
            volcanoToggle.SetIsOnWithoutNotify(true);
        }
    }

    // ============================================================
    // SAVE
    // ============================================================

    private void SaveSettings()
    {
        // Make absolutely sure there is a map.
        if (GetSelectedMapCount() <= 0)
            RestoreAnyMap();

        PushSetsToGameSettings();
        PushDifficultyToGameSettings();
        PushMapsToGameSettings();

        Debug.Log(
            $"Match Settings Saved | " +
            $"Sets: {NumberOfSets} | " +
            $"Difficulty: {currentDifficulty} | " +
            $"Maps: {GetSelectedMapCount()}"
        );
    }

    // ============================================================
    // CURSOR CONTROL
    // ============================================================

    // Update cursor visibility for runtime cursors
    private void ShowOnlyCursor(int activePlayerIndex)
    {
        if (runtimePlayerCursors == null) return;

        for (int i = 0; i < runtimePlayerCursors.Count; i++)
        {
            if (runtimePlayerCursors[i] != null)
            {
                // Only show the cursor of the player controlling the settings menu
                runtimePlayerCursors[i].gameObject.SetActive(i == activePlayerIndex);
            }
        }
    }

    private void HideAllCursors()
    {
        if (runtimePlayerCursors == null)
            return;

        foreach (Transform cursor in runtimePlayerCursors)
        {
            if (cursor != null)
                cursor.gameObject.SetActive(false);
        }
    }

    private void ShowAllCursors()
    {
        if (runtimePlayerCursors == null)
            return;

        foreach (Transform cursor in runtimePlayerCursors)
        {
            if (cursor != null)
                cursor.gameObject.SetActive(true);
        }
    }

    // ============================================================
    // UI FOCUS / INPUT BLOCKING
    // ============================================================

    /// <summary>
    /// Configures the CanvasGroup so the settings overlay blocks
    /// raycasts from reaching the Character Select UI underneath.
    /// </summary>
    private void ConfigureCanvasGroup()
    {
        if (settingsCanvasGroup == null &&
            settingsPanel != null)
        {
            settingsCanvasGroup =
                settingsPanel.GetComponent<CanvasGroup>();

            if (settingsCanvasGroup == null)
            {
                settingsCanvasGroup =
                    settingsPanel.AddComponent<CanvasGroup>();
            }
        }

        if (settingsCanvasGroup == null)
            return;

        if (menuOpen)
        {
            settingsCanvasGroup.alpha = 1f;

            // THIS is important.
            //
            // It makes the overlay consume UI raycasts so
            // buttons underneath the menu cannot be clicked.
            settingsCanvasGroup.blocksRaycasts = true;

            settingsCanvasGroup.interactable = true;
        }
        else
        {
            settingsCanvasGroup.blocksRaycasts = false;
            settingsCanvasGroup.interactable = false;
        }
    }

    /// <summary>
    /// Forces Unity's EventSystem selection onto the settings menu.
    ///
    /// This waits a frame before selecting, matching MainMenu's
    /// SelectFirstButtonNextFrame pattern - otherwise Unity re-selects
    /// the button that was clicked to open this menu, right after this
    /// runs, and the highlight/cursor never actually moves.
    /// </summary>
    private IEnumerator FocusMenuNextFrame()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            yield break;

        eventSystem.SetSelectedGameObject(null);

        yield return null;

        Selectable target = firstSelectedControl != null
            ? firstSelectedControl
            : (Selectable)setsLeftButton ?? setsRightButton;

        if (target != null)
            eventSystem.SetSelectedGameObject(target.gameObject);
    }

    // ============================================================
    // EXTERNAL CONTROL API
    // ============================================================

    /// <summary>
    /// Returns true if this player currently owns the menu.
    ///
    /// Your player cursor script can use this to prevent itself
    /// from interacting with Character Select while the settings
    /// menu is open.
    /// </summary>
    public bool IsPlayerControllingMenu(int playerIndex)
    {
        return menuOpen &&
               controllingPlayer == playerIndex;
    }

    /// <summary>
    /// Returns true if Character Select should currently be
    /// prevented from receiving player input.
    /// </summary>
    public bool ShouldBlockCharacterSelectInput()
    {
        return menuOpen;
    }

    /// <summary>
    /// Re-reads Number of Sets, Bot Difficulty, and Map Rotation from
    /// GameSettings and refreshes the displayed values.
    ///
    /// Useful if the match settings menu is opened again after changing
    /// settings elsewhere (same idea as TeamArrangementToggle.Refresh()).
    /// </summary>
    public void Refresh()
    {
        InitializeDefaults();
        UpdateSetsDisplay();
        UpdateDifficultyDisplay();
    }
}