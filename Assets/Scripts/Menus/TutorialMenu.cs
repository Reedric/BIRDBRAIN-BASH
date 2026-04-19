using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Video;

public class TutorialMenu : MonoBehaviour
{
    public GameObject confirmationPanel; // Panel for confirming if players want to view tutorial
    public GameObject tutorialPanel; // Panel for actual tutorial stuff
    public TMP_Text title; // Title of the tutorial stuff
    public TMP_Text explanation; // Explanation of the controls
    public VideoPlayer videoPlayer; // Video player for the tutorial stuff
    public VideoClip[] videoClips; // Video clips to play for the tutorial
    private string[] titles; // Titles for each of the screens
    private string[] explanations; // Which button to press
    private int pageNum; // Page of the tutorial the game is currenlty showing (0 is tutorial confirmation, 1-6 is actual tutorial stuff)
    private bool player1KBM; // Whether player 1 is keyboard and mouse
    private InputDevice p1Device; // Input device of player 1

    void Awake()
    {
        // Make sure that confirmation panel is visible and tutorial panel is not
        confirmationPanel.SetActive(true);
        tutorialPanel.SetActive(false);

        // Start on first page of tutorial
        pageNum = 0;

        // Initialize titles
        titles = new string[]
        {
            "Movement",
            "Bumping/Setting",
            "Serving/Spiking",
            "Blocking",
            "Defensive Ability",
            "Offensive Ability"
        };

        // Initialize explanations
        explanations = new string[]
        {
            "Use the left joystick to move around and button south to jump!",
            "Use the left trigger to bump and set!",
            "Use the right trigger to serve and spike!",
            "Use button east to block!",
            "Use the left bumper to use your defensive ability!",
            "Use the right bumper to use your offensive ability!"
        };

        // Assign player 1's input device
        player1KBM = DataTransferManager.isKBMInput[0];

        // Get the device associated with player 1
        if (player1KBM)
        {
            p1Device = Keyboard.current;
        }
        else
        {
            p1Device = Gamepad.all[0];
        }
    }

    void Update()
    {
        // Check for next, back, and skip presses
        if (NextPress())
        {
            // If on last page of tutorial, start game; else go to next page
            if (pageNum == 6)
            {
                StartGame();
            }
            else
            {
                NextPage();
            }
        }
        else if (BackPress())
        {
            // If on page where you can skip tutorial, start game; else, go back a page
            if (pageNum == 0)
            {
                StartGame();
            }
            else if (pageNum > 1)
            {
                PreviousPage();
            }
        }
        else if (SkipPress() && pageNum > 0)
        {
            StartGame();
        }
    }

    bool NextPress()
    {
        // D for Keyboard, button south for controller
        if (player1KBM)
        {
            return ((Keyboard) p1Device).dKey.wasPressedThisFrame;
        }
        else
        {
            return ((Gamepad) p1Device).buttonSouth.wasPressedThisFrame;
        }
    }

    bool BackPress()
    {
        // A for Keyboard, button west for controller
        if (player1KBM)
        {
            return ((Keyboard) p1Device).aKey.wasPressedThisFrame;
        }
        else
        {
            return ((Gamepad) p1Device).buttonEast.wasPressedThisFrame;
        }
    }

    bool SkipPress()
    {
        // W for Keyboard, button north for controller
        if (player1KBM)
        {
            return ((Keyboard) p1Device).wKey.wasPressedThisFrame;
        }
        else
        {
            return ((Gamepad) p1Device).buttonNorth.wasPressedThisFrame;
        }
    }

    void NextPage()
    {
        // If going to tutorial from confirmation, change visible panels
        if (pageNum == 0)
        {
            confirmationPanel.SetActive(false);
            tutorialPanel.SetActive(true);
        }
        
        // Set the title and explanation texts
        title.text = titles[pageNum];
        explanation.text = explanations[pageNum];

        // Set the video
        videoPlayer.clip = videoClips[pageNum];

        // Increment page number
        ++pageNum;
    }

    void PreviousPage()
    {
        // Set the title and explanation texts (offset by 2)
        title.text = titles[pageNum - 2];
        explanation.text = explanations[pageNum - 2];

        // Set the video (offset by 2)
        videoPlayer.clip = videoClips[pageNum - 2];

        // Decrement page number
        --pageNum;
    }

    void StartGame()
    {
        // LET'S BASH SOME BIRDBRAINS!!!!
        SceneManager.LoadScene("Game");
    }
}
