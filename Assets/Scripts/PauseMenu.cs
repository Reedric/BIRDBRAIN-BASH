using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    // Initially Sets Paused Game State to false
    public static bool GameIsPaused = false;

    [Header("Pause Menu UI")]
    public GameObject pauseMenuUI;

    [Header("Controls")]
    public InputActionAsset controls;

    private InputActionMap menuActions;
    private InputAction pauseAction;

    void Start()
    {
        // Checks the Input List and Maps the Pause Action to pauseAction
        if (controls != null)
        {
            menuActions = controls.FindActionMap("UI");
            pauseAction = menuActions.FindAction("Pause");
            if (pauseAction == null)
            {
                Debug.Log("Pause Input Cannot be found");
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Pauses Game If Start on Gamepad or Tab on Keyboard triggered
        if ((pauseAction != null && pauseAction.WasPerformedThisFrame()))
        {
            if (GameIsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

    }
    
    // Resumes Gameplay
    public void Resume ()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        GameIsPaused = false;
    }

    // Pauses Gameplay
    void Pause ()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        GameIsPaused = true;
    }

    public void LoadOptions()
    {
        Debug.Log("Loading Options Menu......");
    }

    public void MainMenu()
    {
        Debug.Log("Going to Main Menu.....");
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
