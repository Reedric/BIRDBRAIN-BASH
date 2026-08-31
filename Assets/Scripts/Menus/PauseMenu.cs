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

    void Awake()
    {
        Instance = this;

        foreach (Button button in GetComponentsInChildren<Button>(true))
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
    
    // Resumes Gameplay
    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        GameIsPaused = false;
        pausedPlayerID = -1;

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
        Debug.Log("Loading Options Menu......");
    }

    public void MainMenu()
    {
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