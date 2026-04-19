using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

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

        // Enable UI for all players
        GameManager.Instance.leftPlayer1.GetComponent<PlayerInput>().actions.FindActionMap("UI").Enable();
        if (GameManager.Instance.leftPlayer2.GetComponent<PlayerInput>() != null)
        {
            GameManager.Instance.leftPlayer2.GetComponent<PlayerInput>().actions.FindActionMap("UI").Enable();
        }
        if (GameManager.Instance.rightPlayer1.GetComponent<PlayerInput>() != null)
        {
            GameManager.Instance.rightPlayer1.GetComponent<PlayerInput>().actions.FindActionMap("UI").Enable();
        }
        if (GameManager.Instance.rightPlayer2.GetComponent<PlayerInput>() != null)
        {
            GameManager.Instance.rightPlayer2.GetComponent<PlayerInput>().actions.FindActionMap("UI").Enable();
        }
    }

    // Pauses Gameplay
    public void Pause()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        GameIsPaused = true;

        // Enable UI for the player that paused it
        BallInteract lp1 = GameManager.Instance.leftPlayer1.GetComponent<BallInteract>();
        BallInteract lp2 = GameManager.Instance.leftPlayer2.GetComponent<BallInteract>();
        BallInteract rp1 = GameManager.Instance.rightPlayer1.GetComponent<BallInteract>();
        BallInteract rp2 = GameManager.Instance.rightPlayer2.GetComponent<BallInteract>();
        if (lp1.playerID == pausedPlayerID)
        {
            GameManager.Instance.leftPlayer1.GetComponent<PlayerInput>().ActivateInput();
            if (lp2 != null) GameManager.Instance.leftPlayer2.GetComponent<PlayerInput>().DeactivateInput();
            if (rp1 != null) GameManager.Instance.rightPlayer1.GetComponent<PlayerInput>().DeactivateInput();
            if (rp2 != null) GameManager.Instance.rightPlayer2.GetComponent<PlayerInput>().DeactivateInput();
            AssignUIActions(GameManager.Instance.leftPlayer1.GetComponent<PlayerInput>());
        }
        else if (lp2 != null && lp2.playerID == pausedPlayerID) // Assume that if player 2, player 1 exists
        {
            GameManager.Instance.leftPlayer1.GetComponent<PlayerInput>().DeactivateInput();
            GameManager.Instance.leftPlayer2.GetComponent<PlayerInput>().ActivateInput();
            if (rp1 != null) GameManager.Instance.rightPlayer1.GetComponent<PlayerInput>().DeactivateInput();
            if (rp2 != null) GameManager.Instance.rightPlayer2.GetComponent<PlayerInput>().DeactivateInput();
            AssignUIActions(GameManager.Instance.leftPlayer2.GetComponent<PlayerInput>());
        }
        else if (rp1 != null && rp1.playerID == pausedPlayerID) // Assume that if player 3, player 1 and 2 exists
        {
            GameManager.Instance.leftPlayer1.GetComponent<PlayerInput>().DeactivateInput();
            GameManager.Instance.leftPlayer2.GetComponent<PlayerInput>().DeactivateInput();
            GameManager.Instance.rightPlayer1.GetComponent<PlayerInput>().ActivateInput();
            if (rp2 != null) GameManager.Instance.rightPlayer2.GetComponent<PlayerInput>().DeactivateInput();
            AssignUIActions(GameManager.Instance.rightPlayer1.GetComponent<PlayerInput>());
        }
        else if (rp2 != null && rp2.playerID == pausedPlayerID) // Assume that if player 4, player 1, 2, and 3 exists
        {
            GameManager.Instance.leftPlayer1.GetComponent<PlayerInput>().DeactivateInput();
            GameManager.Instance.leftPlayer2.GetComponent<PlayerInput>().DeactivateInput();
            GameManager.Instance.rightPlayer1.GetComponent<PlayerInput>().DeactivateInput();
            GameManager.Instance.rightPlayer2.GetComponent<PlayerInput>().ActivateInput();
            AssignUIActions(GameManager.Instance.rightPlayer2.GetComponent<PlayerInput>());
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
