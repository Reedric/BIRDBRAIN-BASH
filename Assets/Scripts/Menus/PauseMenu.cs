using System.Collections;
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
    private Gamepad optionsGamepad;
    private GameObject optionsReturnSelection;
    private PauseMenuCursor cursor;
    private InputActionAsset onlineMenuActions;

    void Awake()
    {
        Instance = this;
        AddButtonSounds(gameObject);
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
        // Checks the Input List and Maps the Pause Action to pauseAction
        
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
            ShowCursor(GetOnlinePlayerIndex());
        }
        else
        {
            AudioManager.PlayDefaultBackground();
            HideCursor();
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

            // There's no mouse in the menus, but clicking on a window to focus it would still clear the selection,
            // which is what both navigating and the cursor go by
            inputModule.deselectOnBackgroundClick = false;
        }

        // Only while it's open, so that playing the match doesn't also move around the menu
        InputActionMap menuMap = onlineMenuActions.FindActionMap("UI");
        if (menuOpen) menuMap.Enable();
        else menuMap.Disable();
    }

    void OnDestroy()
    {
        if (onlineMenuActions != null) Destroy(onlineMenuActions);
    }

    // Shows the cursor of the player using this menu. Each player has their own art for it, in Resources.
    private void ShowCursor(int playerIndex)
    {
        HideCursor();

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

        // Enable UI for all players — guarded against null because AI birds have no PlayerInput
        // (demo mode spawns 4 AIs, none of which carry a PlayerInput component)
        PlayerInput pi1 = GameManager.Instance.leftPlayer1?.GetComponent<PlayerInput>();
        PlayerInput pi2 = GameManager.Instance.leftPlayer2?.GetComponent<PlayerInput>();
        PlayerInput pi3 = GameManager.Instance.rightPlayer1?.GetComponent<PlayerInput>();
        PlayerInput pi4 = GameManager.Instance.rightPlayer2?.GetComponent<PlayerInput>();

        if (pi1 != null) pi1.actions.FindActionMap("UI").Enable();
        if (pi2 != null) pi2.actions.FindActionMap("UI").Enable();
        if (pi3 != null) pi3.actions.FindActionMap("UI").Enable();
        if (pi4 != null) pi4.actions.FindActionMap("UI").Enable();

        AudioManager.PlayDefaultBackground();
    }

    // Pauses Gameplay
    public void Pause()
    {
        AudioManager.PlayPauseTrack();
        
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        GameIsPaused = true;
        ShowCursor(pausedPlayerID);

        // Enable UI for the player that paused it — guarded against null because AI birds have no
        // PlayerInput (demo mode spawns 4 AIs, none of which carry a PlayerInput component).
        // If no PlayerInput exists on any slot the pause UI still shows, the game still freezes,
        // but we simply skip the input-routing logic that would crash.
        BallInteract lp1 = GameManager.Instance.leftPlayer1.GetComponent<BallInteract>();
        BallInteract lp2 = GameManager.Instance.leftPlayer2.GetComponent<BallInteract>();
        BallInteract rp1 = GameManager.Instance.rightPlayer1.GetComponent<BallInteract>();
        BallInteract rp2 = GameManager.Instance.rightPlayer2.GetComponent<BallInteract>();

        PlayerInput pi1 = GameManager.Instance.leftPlayer1.GetComponent<PlayerInput>();
        PlayerInput pi2 = GameManager.Instance.leftPlayer2.GetComponent<PlayerInput>();
        PlayerInput pi3 = GameManager.Instance.rightPlayer1.GetComponent<PlayerInput>();
        PlayerInput pi4 = GameManager.Instance.rightPlayer2.GetComponent<PlayerInput>();

        if (lp1.playerID == pausedPlayerID)
        {
            if (pi1 != null) pi1.ActivateInput();
            if (pi2 != null) pi2.DeactivateInput();
            if (pi3 != null) pi3.DeactivateInput();
            if (pi4 != null) pi4.DeactivateInput();
            if (pi1 != null) AssignUIActions(pi1);
        }
        else if (lp2 != null && lp2.playerID == pausedPlayerID) // Assume that if player 2, player 1 exists
        {
            if (pi1 != null) pi1.DeactivateInput();
            if (pi2 != null) pi2.ActivateInput();
            if (pi3 != null) pi3.DeactivateInput();
            if (pi4 != null) pi4.DeactivateInput();
            if (pi2 != null) AssignUIActions(pi2);
        }
        else if (rp1 != null && rp1.playerID == pausedPlayerID) // Assume that if player 3, player 1 and 2 exists
        {
            if (pi1 != null) pi1.DeactivateInput();
            if (pi2 != null) pi2.DeactivateInput();
            if (pi3 != null) pi3.ActivateInput();
            if (pi4 != null) pi4.DeactivateInput();
            if (pi3 != null) AssignUIActions(pi3);
        }
        else if (rp2 != null && rp2.playerID == pausedPlayerID) // Assume that if player 4, player 1, 2, and 3 exists
        {
            if (pi1 != null) pi1.DeactivateInput();
            if (pi2 != null) pi2.DeactivateInput();
            if (pi3 != null) pi3.DeactivateInput();
            if (pi4 != null) pi4.ActivateInput();
            if (pi4 != null) AssignUIActions(pi4);
        }
    }

    private void AssignUIActions(PlayerInput pausedPlayer)
    {
        // Assign input module actions to specifically disallow the mouse from working
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
                () => NetworkSession.IsOnline ? Gamepad.current : optionsGamepad, true, () => CloseOptions());
            AddButtonSounds(optionsMenu.Panel);
        }

        optionsGamepad = FindPausedPlayerGamepad();
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
        GameManager manager = GameManager.Instance;
        GameObject[] birds = { manager.leftPlayer1, manager.leftPlayer2, manager.rightPlayer1, manager.rightPlayer2 };

        foreach (GameObject bird in birds)
        {
            if (bird == null || !bird.TryGetComponent(out PlayerInput playerInput)) continue;
            if (!bird.TryGetComponent(out BallInteract ballInteract) || ballInteract.playerID != pausedPlayerID) continue;

            foreach (InputDevice device in playerInput.devices)
            {
                if (device is Gamepad gamepad) return gamepad;
            }
            return null;
        }

        return Gamepad.current;
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

    public void OnPointerEnter(PointerEventData eventData) => PlayHoverSound();

    public void OnSelect(BaseEventData eventData) => PlayHoverSound();

    private void PlayHoverSound()
    {
        if (lastPlayedFrame == Time.frameCount) return;

        lastPlayedFrame = Time.frameCount;
        AudioManager.PlayButtonHoverSound();
    }
}