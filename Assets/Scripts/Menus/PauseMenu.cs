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

    private OptionsMenuUI optionsMenu;
    private GameObject optionsReturnSelection;
    private PauseMenuCursor cursor;
    private InputActionAsset onlineMenuActions;
    private InputActionAsset mouseActions;

    // Whether the menu is being used with the mouse, in which case the cursor follows it instead of the selection
    public bool MouseInUse { get; private set; }

    void Awake()
    {
        Instance = this;
        AddButtonSounds(gameObject);

        // Clicking on empty space would clear the selection, which the stick and the cursor both go by
        if (inputModule != null) inputModule.deselectOnBackgroundClick = false;
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
        SubmitWithKeyboard();
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
        return LocalPlayerDevices.WasUsedThisFrame(FindPausedPlayerGamepad());
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

    // The UI actions only submit with a controller's A button, so a keyboard player could move around the menu but
    // never press anything (including Main Menu). Handled here rather than in the shared input actions so the
    // menus elsewhere don't change.
    private void SubmitWithKeyboard()
    {
        if (EventSystem.current == null) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (!keyboard.enterKey.wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame && !keyboard.spaceKey.wasPressedThisFrame) return;

        if (!PausedPlayerOwnsKeyboardAndMouse()) return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !selected.activeInHierarchy) return;

        ExecuteEvents.Execute(selected, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
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
        if (CloseOptions()) return;

        GameIsPaused = !GameIsPaused;
        pauseMenuUI.SetActive(GameIsPaused);
        SetOnlineMenuInput(GameIsPaused);

        if (GameIsPaused)
        {
            AudioManager.PlayPauseTrack();
            UseOwnMouseActions();
            ShowCursor(GetOnlinePlayerIndex());
        }
        else
        {
            AudioManager.PlayDefaultBackground();
            HideCursor();
            PutAwayMouse();
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
        // The pause button only goes back one screen while the options are open
        if (CloseOptions()) return;

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
            TMP_Text fontSource = pauseMenuUI.GetComponentInChildren<TMP_Text>(true);

            optionsMenu = gameObject.AddComponent<OptionsMenuUI>();
            optionsMenu.Build(pauseMenuUI.GetComponentInParent<Canvas>(true), fontSource != null ? fontSource.font : null,
                // Looked up each time, so it follows the player who paused if they switch controllers
                () => NetworkSession.IsOnline ? Gamepad.current : FindPausedPlayerGamepad(), true, () => CloseOptions());
            AddButtonSounds(optionsMenu.Panel);
        }

        optionsReturnSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        pauseMenuUI.SetActive(false);
        optionsMenu.Show();
        StartCoroutine(SelectNextFrame(optionsMenu.FirstButton.gameObject));
    }

    // Returns whether the options were open
    private bool CloseOptions()
    {
        if (OptionsMenuUI.ActivePanel == null) return false;

        optionsMenu.Hide();
        pauseMenuUI.SetActive(true);
        StartCoroutine(SelectNextFrame(optionsReturnSelection));
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

    public void MainMenu()
    {
        if (NetworkSession.IsOnline)
        {
            NetworkSession.LeaveToMainMenu();
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