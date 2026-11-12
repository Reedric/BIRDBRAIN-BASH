using System.Collections;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    // Initially Sets Paused Game State to false
    public bool GameIsPaused = false;
    public int pausedPlayerID = -1;

    [Header("Pause Menu UI")]
    public GameObject pauseMenuUI;

    [Header("Controls")]
    public InputSystemUIInputModule inputModule;

    private InputActionMap menuActions;
    private InputAction pauseAction;

    public static PauseMenu Instance; // Private instance of the GameManager that other classes cannot reference

    // The canvas and font that the options and controls screens go by, which the editor builds into the scene
    public Canvas MenuCanvas => pauseMenuUI.GetComponentInParent<Canvas>(true);

    // The font of the menu's text. The big buttons' labels have a font of their own, which isn't for anything else.
    public TMP_FontAsset MenuFont
    {
        get
        {
            TMP_FontAsset buttonFont = MenuArt.Instance != null ? MenuArt.Instance.buttonFont : null;
            TMP_Text[] texts = pauseMenuUI.GetComponentsInChildren<TMP_Text>(true);

            foreach (TMP_Text text in texts)
            {
                if (text.font != null && text.font != buttonFont) return text.font;
            }
            return texts.Length > 0 ? texts[0].font : null;
        }
    }

    private OptionsMenuUI optionsMenu;
    private ControlsMenuUI controlsMenu;
    private GameObject subMenuReturnSelection; // What to select again when the options or controls close
    private PauseMenuCursor cursor;

    // Moving around the menu (see UpdateNavigation)
    private const float navigateDeadzone = 0.5f;
    private const float navigateRepeatDelay = 0.35f; // Seconds a direction is held before it starts repeating
    private const float navigateRepeatRate = 0.12f;
    private Vector2Int heldDirection;
    private float nextNavigateTime;
    private InputActionAsset onlineMenuActions;
    private InputActionAsset mouseActions;

    // Whether the menu is being used with the mouse, in which case the cursor follows it instead of the selection
    public bool MouseInUse { get; private set; }

    void Awake()
    {
        Instance = this;
        StyleButtons();

        // The map scenes have the buttons' highlights in them already (see MenuScreenBuilder), so this is only for a
        // scene that doesn't
        foreach (Button button in pauseMenuUI.GetComponentsInChildren<Button>(true))
        {
            if (button.TryGetComponent(out Image image) && MenuStyle.IsPill(image) && !MenuStyle.HasHighlight(button)) MenuStyle.AddHighlight(button);
        }
        AddButtonSounds(gameObject);

        // Clicking on empty space would clear the selection, which the stick and the cursor both go by
        if (inputModule != null) inputModule.deselectOnBackgroundClick = false;
    }

    // The buttons match the art of the other menus' buttons. The four map scenes already have them set up that way,
    // so this only does anything for a scene whose pause menu still has the old plain text buttons (like test scenes).
    // They keep their labels and stay where they were across, stacked from the top one down.
    private void StyleButtons()
    {
        const float buttonSpacing = 120f;
        Vector2 buttonSize = new(380f, 100f);

        Button[] buttons = pauseMenuUI.GetComponentsInChildren<Button>(true)
            .Where(button => button.TryGetComponent(out Image image) && !MenuStyle.IsPill(image))
            .OrderByDescending(button => ((RectTransform)button.transform).anchoredPosition.y)
            .ToArray();
        if (buttons.Length == 0) return;

        float topY = ((RectTransform)buttons[0].transform).anchoredPosition.y;
        for (int i = 0; i < buttons.Length; ++i)
        {
            TMP_Text oldLabel = buttons[i].GetComponentInChildren<TMP_Text>(true);
            string label = oldLabel != null ? oldLabel.text : buttons[i].name;

            RectTransform rect = (RectTransform)buttons[i].transform;
            rect.localScale = Vector3.one;
            rect.sizeDelta = buttonSize;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, topY - i * buttonSpacing);

            MenuStyle.MakePill(buttons[i], MenuStyle.PillColor.Pink, label, MenuFont, 52f);
        }
    }

    private void AddButtonSounds(GameObject root)
    {
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            button.onClick.AddListener(() => AudioManager.PlayButtonSelectSound());
            if (button.GetComponent<PauseMenuButtonAudio>() == null)
                button.gameObject.AddComponent<PauseMenuButtonAudio>();
        }
    }

    void Start()
    {
        // Nothing in a match uses the mouse, so it stays locked away until the menu needs it
        MenuMouse.Lock();
    }

    void Update()
    {
        if (!GameIsPaused) return;

        UpdateMouse();
        UpdateNavigation();
    }

    // Moves around whichever screen of the menu is showing with the stick, d-pad or arrow keys, and presses with A,
    // Enter or Space. B goes back from the controls (the options handle B themselves).
    // This is done here rather than by the EventSystem, whose navigation is off while paused (see
    // SetEventSystemNavigation). It went through the paused player's input actions, which in local multiplayer didn't
    // reliably reach the menu, so the controller couldn't move around it.
    private void UpdateNavigation()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        Gamepad pad = MenuGamepad();
        Keyboard keyboard = PausedPlayerOwnsKeyboardAndMouse() ? Keyboard.current : null;

        Vector2 move = Vector2.zero;
        if (pad != null)
        {
            move = pad.leftStick.ReadValue();
            if (move.sqrMagnitude < 0.01f) move = pad.dpad.ReadValue();
        }
        if (keyboard != null)
        {
            Vector2 keys = new(
                (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed ? 1f : 0f));
            if (keys != Vector2.zero) move = keys;
        }
        Navigate(eventSystem, move);

        bool submit = (pad != null && pad.buttonSouth.wasPressedThisFrame)
            || (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame));
        GameObject selected = eventSystem.currentSelectedGameObject;
        if (submit && selected != null && selected.activeInHierarchy)
            ExecuteEvents.Execute(selected, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);

        if (pad != null && pad.buttonEast.wasPressedThisFrame && ControlsMenuUI.ActivePanel != null)
            CloseSubMenu();
    }

    private void Navigate(EventSystem eventSystem, Vector2 move)
    {
        Vector2Int direction = Vector2Int.zero;
        if (Mathf.Abs(move.y) >= navigateDeadzone && Mathf.Abs(move.y) >= Mathf.Abs(move.x))
            direction.y = move.y > 0f ? 1 : -1;
        // Left and right change the selected row on the options screen, which it does itself
        else if (Mathf.Abs(move.x) >= navigateDeadzone && OptionsMenuUI.ActivePanel == null)
            direction.x = move.x > 0f ? 1 : -1;

        if (direction == Vector2Int.zero)
        {
            heldDirection = direction;
            return;
        }

        bool isHeld = direction == heldDirection;
        if (isHeld && Time.unscaledTime < nextNavigateTime) return;

        heldDirection = direction;
        nextNavigateTime = Time.unscaledTime + (isHeld ? navigateRepeatRate : navigateRepeatDelay);

        GameObject selected = eventSystem.currentSelectedGameObject;
        Selectable current = selected != null && selected.activeInHierarchy ? selected.GetComponent<Selectable>() : null;
        if (current == null)
        {
            eventSystem.SetSelectedGameObject(FirstButtonOnScreen());
            return;
        }

        Selectable next = direction.y > 0 ? current.FindSelectableOnUp()
            : direction.y < 0 ? current.FindSelectableOnDown()
            : direction.x > 0 ? current.FindSelectableOnRight()
            : current.FindSelectableOnLeft();

        if (next != null) eventSystem.SetSelectedGameObject(next.gameObject);
    }

    private GameObject FirstButtonOnScreen()
    {
        if (ControlsMenuUI.ActivePanel != null) return controlsMenu.FirstButton.gameObject;
        if (OptionsMenuUI.ActivePanel != null) return optionsMenu.FirstButton.gameObject;

        Button first = pauseMenuUI.GetComponentInChildren<Button>();
        return first != null ? first.gameObject : null;
    }

    // The EventSystem's own navigation is off while the menu is open, since UpdateNavigation does it instead.
    // Leaving both on would move twice for one press.
    private static void SetEventSystemNavigation(bool on)
    {
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = on;
    }

    // The controller that can use the menu: the paused player's own in a local match, or any of them online (where
    // this machine has the one player) and in demo mode (where there are no players)
    private Gamepad MenuGamepad()
    {
        if (NetworkSession.IsOnline || FindPausedPlayerInput() == null)
            return LocalPlayerDevices.LastUsedGamepad ?? Gamepad.current;

        return LocalPlayerDevices.GetGamepad(pausedPlayerID) ?? FindPausedPlayerGamepad();
    }

    // The mouse works in the menu for whoever it belongs to. While it's being used it moves freely and the cursor
    // follows it. Using a controller locks it away again, so a pointer left on a button doesn't keep it highlighted.
    private void UpdateMouse()
    {
        bool canUseMouse = Mouse.current != null && PausedPlayerOwnsKeyboardAndMouse();

        InputActionMap mouseMap = mouseActions != null ? mouseActions.actionMaps[0] : null;
        if (mouseMap != null && mouseMap.enabled != canUseMouse)
        {
            if (canUseMouse) mouseMap.Enable();
            else mouseMap.Disable();
        }

        if (canUseMouse && MenuMouse.UsedThisFrame())
        {
            if (!MouseInUse) MenuMouse.Release();
            MouseInUse = true;
        }
        else if (MouseInUse && (!canUseMouse || MenuControllerUsedThisFrame()))
        {
            MenuMouse.Lock();
            MouseInUse = false;
        }
        else if (MouseInUse && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            MouseInUse = false;
        }
    }

    private bool MenuControllerUsedThisFrame()
    {
        if (NetworkSession.IsOnline) return Gamepad.all.Any(LocalPlayerDevices.WasUsedThisFrame);
        return LocalPlayerDevices.WasUsedThisFrame(MenuGamepad());
    }

    // Stops using the mouse in the menu, for when the menu closes
    private void PutAwayMouse()
    {
        MouseInUse = false;
        MenuMouse.Lock();
        if (mouseActions != null) mouseActions.Disable();
    }

    // The menu reads the mouse through actions of its own. Going through a player's actions, it only worked if the
    // mouse was paired with that player under a control scheme that lets it through, and a bird set up for a
    // controller doesn't let it through. UpdateMouse still only turns these on for whoever the mouse belongs to.
    private void UseOwnMouseActions()
    {
        if (inputModule == null) return;

        if (mouseActions == null)
        {
            mouseActions = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = mouseActions.AddActionMap("Pause Menu Mouse");
            map.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position", expectedControlLayout: "Vector2");
            map.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton", expectedControlLayout: "Button");
        }

        inputModule.point = InputActionReference.Create(mouseActions["Point"]);
        inputModule.leftClick = InputActionReference.Create(mouseActions["Click"]);
    }

    // Online, this machine only has the one player. Locally, only the player who paused gets to use the menu, so the
    // keyboard and mouse only work in it if they're that player's. Demo mode has no players, so anyone can use it there.
    private bool PausedPlayerOwnsKeyboardAndMouse()
    {
        return NetworkSession.IsOnline || FindPausedPlayerInput() == null || LocalPlayerDevices.OwnsKeyboard(pausedPlayerID);
    }
    
    // Opens or closes the pause menu for an online player. The match can't be frozen for everyone else,
    // so it keeps going while this player's bird stands still (see PlayerNetworkController).
    public void ToggleOnlinePause()
    {
        if (CloseSubMenu()) return;

        GameIsPaused = !GameIsPaused;
        pauseMenuUI.SetActive(GameIsPaused);
        SetOnlineMenuInput(GameIsPaused);
        SetEventSystemNavigation(!GameIsPaused);

        if (GameIsPaused)
        {
            AudioManager.PlayPauseTrack();
            UseOwnMouseActions();
            LabelLeaveButton();
            ShowCursor(GetOnlinePlayerIndex());
        }
        else
        {
            AudioManager.PlayDefaultBackground();
            HideCursor();
            PutAwayMouse();
        }
    }

    // Online, the host's leave button takes everyone back to the lobby instead (see MainMenu), so it says so
    private void LabelLeaveButton()
    {
        if (!NetworkSession.IsServer) return;

        foreach (Button button in pauseMenuUI.GetComponentsInChildren<Button>(true))
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); ++i)
            {
                if (button.onClick.GetPersistentMethodName(i) != nameof(MainMenu)) continue;

                // The label that's showing, rather than a switched off copy (like the old text shadow)
                TMP_Text label = button.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.gameObject.activeSelf);
                if (label != null) label.text = "Return to Lobby";
            }
        }
    }

    // Online, the menu gets its own copy of the input actions, like PlayerNetworkController does for gameplay.
    // The birds' PlayerInputs would otherwise be using the same ones as the menu, and a local match only
    // avoids that by giving the menu the actions of whoever paused, which there's no equivalent of online.
    private void SetOnlineMenuInput(bool menuOpen)
    {
        if (onlineMenuActions == null)
        {
            onlineMenuActions = Instantiate(inputModule.actionsAsset);
            inputModule.actionsAsset = onlineMenuActions;
        }

        // Only while it's open, so that playing the match doesn't also move around the menu
        InputActionMap menuMap = onlineMenuActions.FindActionMap("UI");
        if (menuOpen) menuMap.Enable();
        else menuMap.Disable();
    }

    void OnDestroy()
    {
        if (onlineMenuActions != null) Destroy(onlineMenuActions);
        if (mouseActions != null)
        {
            mouseActions.Disable();
            Destroy(mouseActions);
        }
    }

    // Shows the cursor of the player using this menu. Each player has their own art for it, in Resources.
    private void ShowCursor(int playerIndex)
    {
        HideCursor();

        // The menu goes in front of everything else on its canvas. The scenes have a full-screen fade image after
        // it, which is invisible during a match but would still catch every click and stop the mouse reaching the menu.
        pauseMenuUI.transform.SetAsLastSibling();

        // The cursor follows the selection, so make sure that the pause menu has one
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (EventSystem.current != null && (selected == null || !selected.transform.IsChildOf(pauseMenuUI.transform)))
            EventSystem.current.SetSelectedGameObject(pauseMenuUI.GetComponentInChildren<Button>().gameObject);

        GameObject prefab = Resources.Load<GameObject>($"MenuCursor{Mathf.Clamp(playerIndex, 0, 3) + 1}");
        cursor = Instantiate(prefab, pauseMenuUI.transform.parent).AddComponent<PauseMenuCursor>();
    }

    private void HideCursor()
    {
        if (cursor != null) Destroy(cursor.gameObject);
    }

    // Online, each machine only has one player, and it's whichever bird this client owns
    private int GetOnlinePlayerIndex()
    {
        NetworkObject playerObject = NetworkManager.Singleton.LocalClient?.PlayerObject;
        return playerObject != null && playerObject.TryGetComponent(out BallInteract ballInteract) ? ballInteract.playerID : 0;
    }

    // Resumes Gameplay
    public void Resume()
    {
        // The pause button only goes back one screen while the options or controls are open
        if (CloseSubMenu()) return;

        if (NetworkSession.IsOnline)
        {
            if (GameIsPaused) ToggleOnlinePause();
            return;
        }

        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        GameIsPaused = false;
        pausedPlayerID = -1;
        HideCursor();
        PutAwayMouse();
        SetEventSystemNavigation(true);

        // Give everyone their controls back. AI birds have no PlayerInput, so they aren't in this list
        // (demo mode spawns 4 AIs, none of which carry a PlayerInput component)
        foreach (PlayerInput playerInput in PlayerInput.all.ToArray())
        {
            // Pause() turns off everyone else's gameplay controls, so they have to be turned back on here
            playerInput.ActivateInput();
            playerInput.actions.FindActionMap("UI")?.Enable();
        }

        AudioManager.PlayDefaultBackground();
    }

    // The PlayerInput of whoever paused a local match, or null if that's nobody's (demo mode).
    // Looks through every active PlayerInput, since Phoenix's chicken stands in for a bird while its own is deactivated.
    private PlayerInput FindPausedPlayerInput()
    {
        foreach (PlayerInput playerInput in PlayerInput.all)
        {
            if (playerInput.TryGetComponent(out BallInteract ballInteract) && ballInteract.playerID == pausedPlayerID)
                return playerInput;
        }

        return null;
    }

    // Pauses Gameplay
    public void Pause()
    {
        AudioManager.PlayPauseTrack();
        
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        GameIsPaused = true;
        MouseInUse = false;
        SetEventSystemNavigation(false);
        ShowCursor(pausedPlayerID);

        // Only the player that paused can use the menu, so everyone else's controls go quiet until Resume().
        // AI birds have no PlayerInput, and none of the birds in demo mode do.
        PlayerInput pausedPlayer = FindPausedPlayerInput();
        if (pausedPlayer != null)
        {
            foreach (PlayerInput playerInput in PlayerInput.all.ToArray())
            {
                if (playerInput == pausedPlayer) playerInput.ActivateInput();
                else playerInput.DeactivateInput();
            }

            AssignUIActions(pausedPlayer);
        }

        UseOwnMouseActions();
    }

    private void AssignUIActions(PlayerInput pausedPlayer)
    {
        // The menu listens to the paused player's actions, which only have their own devices, so nobody else's
        // controller can use it. The mouse gets replaced by UseOwnMouseActions.
        Instance.inputModule.point = InputActionReference.Create(pausedPlayer.actions["UI/Point"]);
        Instance.inputModule.leftClick = InputActionReference.Create(pausedPlayer.actions["UI/Click"]);
        Instance.inputModule.rightClick = InputActionReference.Create(pausedPlayer.actions["UI/RightClick"]);
        Instance.inputModule.middleClick = InputActionReference.Create(pausedPlayer.actions["UI/MiddleClick"]);
        Instance.inputModule.scrollWheel = InputActionReference.Create(pausedPlayer.actions["UI/ScrollWheel"]);
        Instance.inputModule.move = InputActionReference.Create(pausedPlayer.actions["UI/Navigate"]);
        Instance.inputModule.submit = InputActionReference.Create(pausedPlayer.actions["UI/Submit"]);
        Instance.inputModule.cancel = InputActionReference.Create(pausedPlayer.actions["UI/Cancel"]);
    }

    public void LoadOptions()
    {
        if (OptionsMenuUI.ActivePanel != null) return;

        if (optionsMenu == null)
        {
            optionsMenu = gameObject.AddComponent<OptionsMenuUI>();
            optionsMenu.Build(MenuCanvas, MenuFont,
                // Looked up each time, so it follows the player who paused if they switch controllers
                MenuGamepad, true, () => CloseSubMenu());
            AddButtonSounds(optionsMenu.Panel);
        }

        subMenuReturnSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        pauseMenuUI.SetActive(false);
        optionsMenu.Show();
        StartCoroutine(SelectNextFrame(optionsMenu.FirstButton.gameObject));
    }

    // Opens the controls screen, which has a tab each for keyboard and controller
    public void LoadControls()
    {
        if (ControlsMenuUI.ActivePanel != null) return;

        if (controlsMenu == null)
        {
            controlsMenu = gameObject.AddComponent<ControlsMenuUI>();
            controlsMenu.Build(MenuCanvas, MenuFont, () => CloseSubMenu());
            AddButtonSounds(controlsMenu.Panel);
        }

        subMenuReturnSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        pauseMenuUI.SetActive(false);
        controlsMenu.Show();
        StartCoroutine(SelectNextFrame(controlsMenu.FirstButton.gameObject));
    }

    // Goes back from the options or controls to the pause menu. Returns whether either was open.
    private bool CloseSubMenu()
    {
        if (OptionsMenuUI.ActivePanel != null) optionsMenu.Hide();
        else if (ControlsMenuUI.ActivePanel != null) controlsMenu.Hide();
        else return false;

        pauseMenuUI.SetActive(true);
        StartCoroutine(SelectNextFrame(subMenuReturnSelection));
        return true;
    }

    // Clears the selection for a frame first, so the press that got here can't also click the new selection.
    private IEnumerator SelectNextFrame(GameObject selection)
    {
        if (EventSystem.current == null) yield break;

        EventSystem.current.SetSelectedGameObject(null);
        yield return null;
        EventSystem.current.SetSelectedGameObject(selection);
    }

    // In a local match, the options only listen to the player that paused the game, the same as the pause menu.
    // That's nobody's controller if they're on keyboard. Demo mode has no players, so it's whichever was used last.
    private Gamepad FindPausedPlayerGamepad()
    {
        PlayerInput pausedPlayer = FindPausedPlayerInput();
        if (pausedPlayer == null) return Gamepad.current;

        foreach (InputDevice device in pausedPlayer.devices)
        {
            if (device is Gamepad gamepad) return gamepad;
        }
        return null;
    }

    // Leaves the match. Online, the host ends it early and takes everyone back to the lobby, the same as when a
    // match finishes, while a client leaves the session on their own.
    public void MainMenu()
    {
        if (NetworkSession.IsOnline)
        {
            if (NetworkSession.IsServer) NetworkSession.ReturnToLobby();
            else NetworkSession.LeaveToMainMenu();
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void BackToPause()
    {
        Debug.Log("Going Back to Pause Menu.....");
    }

    public void LoadKeybinds()
    {
        Debug.Log("Going to Keybind Menu.....");
    }

    public void SFXValue(float value)
    {
        Debug.Log("SFX Volume: " + value);
    }
    public void MusicValue(float value)
    {
        Debug.Log("Music Volume: " + value);
    }
}

public class PauseMenuButtonAudio : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    private int lastPlayedFrame = -1;

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayHoverSound();

        // Pointing at a button selects it, so the keys or stick carry on from there
        if (EventSystem.current != null && PauseMenu.Instance != null && PauseMenu.Instance.MouseInUse)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnSelect(BaseEventData eventData) => PlayHoverSound();

    private void PlayHoverSound()
    {
        if (lastPlayedFrame == Time.frameCount) return;

        lastPlayedFrame = Time.frameCount;
        AudioManager.PlayButtonHoverSound();
    }
}