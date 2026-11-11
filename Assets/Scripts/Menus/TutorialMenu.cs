using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Video;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

// The tutorial before a match. Online, each player goes through it (or skips it) on their own, and the host starts
// the match once everyone still connected is done.
public class TutorialMenu : MonoBehaviour
{
    // Sent to the host by a client that has finished the tutorial
    private const string tutorialDoneMessage = "TutorialDone";

    // A client keeps telling the host it's done until the match loads, in case the first one arrived before the host
    // was listening for it
    private const float doneResendInterval = 1f;

    public GameObject confirmationPanel; // Panel for confirming if players want to view tutorial
    public GameObject tutorialPanel; // Panel for actual tutorial stuff
    public TMP_Text title; // Title of the tutorial stuff
    public TMP_Text explanation; // Explanation of the controls
    public VideoPlayer videoPlayer; // Video player for the tutorial stuff
    public VideoClip[] videoClips; // Video clips to play for the tutorial

    [Header("Map Scenes")]
    [Tooltip("Scene to load for the Beach map. Tied to GameSettings.BeachEnabled.")]
    [SerializeField] private string beachSceneName = "Game_Beach";

    [Tooltip("Scene to load for the Park map. Tied to GameSettings.ParkEnabled.")]
    [SerializeField] private string parkSceneName = "Game_Forest";

    [Tooltip("Scene to load for the Iceberg map. Tied to GameSettings.IcebergEnabled.")]
    [SerializeField] private string icebergSceneName = "Game_Iceberg";

    [Tooltip("Scene to load for the Volcano map. Tied to GameSettings.VolcanoEnabled.")]
    [SerializeField] private string volcanoSceneName = "Game_Volcano";

    private const string fallbackGameSceneName = "Game_Beach";

    private string[] titles; // Titles for each of the screens
    private string[] explanations; // Which button to press
    private int pageNum; // Page of the tutorial the game is currenlty showing (0 is tutorial confirmation, 1-6 is actual tutorial stuff)
    private bool player1KBM; // Whether player 1 is keyboard and mouse
    private InputDevice p1Device; // Input device of player 1

    // Online
    private bool waitingForOthers; // This player is done, and the match starts once everyone else is
    private float nextDoneSendTime;
    private bool matchLoading;
    private bool listeningForDone;
    private readonly HashSet<ulong> playersDone = new(); // Host only

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
            "Move Order",
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
            "BUMP, SET, SPIKE! You must make actions in that order, alternating between you and your teammate!",
            "Use the LEFT JOYSTICK to move around and BUTTON SOUTH to jump!",
            "Use the LEFT TRIGGER to bump and set!",
            "Use the RIGHT TRIGGER to serve and spike!",
            "Use BUTTON EAST to block!",
            "Use the LEFT BUMPER to use your defensive ability!",
            "Use the RIGHT BUMPER to use your offensive ability!"
        };

        if (NetworkSession.IsServer)
        {
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(tutorialDoneMessage, OnPlayerDone);
            listeningForDone = true;
        }

        // Player 1's devices are looked up on every press instead (see LocalPlayerDevices), so they can use either the
        // keyboard or their controller, and nothing breaks if no controller is plugged in.
        if (!NetworkSession.IsOnline && !LocalPlayerDevices.IsConfigured && DataTransferManager.isKBMInput != null)
            LocalPlayerDevices.Configure(DataTransferManager.isKBMInput);
    }

    void OnDestroy()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (listeningForDone && manager != null && manager.CustomMessagingManager != null)
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(tutorialDoneMessage);
    }

    // Player 1's controller, or whichever one was used last if they don't have one. Online, this machine's one player
    // can use any of its devices.
    private static Gamepad Player1Gamepad =>
        NetworkSession.IsOnline ? LocalPlayerDevices.LastUsedGamepad ?? Gamepad.current
        : LocalPlayerDevices.GetGamepad(0) ?? (LocalPlayerDevices.IsConfigured ? null : LocalPlayerDevices.LastUsedGamepad);

    private static Keyboard Player1Keyboard =>
        NetworkSession.IsOnline || LocalPlayerDevices.OwnsKeyboard(0) || !LocalPlayerDevices.IsConfigured ? Keyboard.current : null;

    void Update()
    {
        if (waitingForOthers)
        {
            UpdateWaiting();
            return;
        }

        // Check for next, back, and skip presses
        if (NextPress())
        {
            // If on last page of tutorial, start game; else go to next page
            if (pageNum == 7)
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
        return (Player1Keyboard != null && Player1Keyboard.dKey.wasPressedThisFrame)
            || (Player1Gamepad != null && Player1Gamepad.buttonSouth.wasPressedThisFrame);
    }

    bool BackPress()
    {
        // A for Keyboard, button east for controller
        return (Player1Keyboard != null && Player1Keyboard.aKey.wasPressedThisFrame)
            || (Player1Gamepad != null && Player1Gamepad.buttonEast.wasPressedThisFrame);
    }

    bool SkipPress()
    {
        // W for Keyboard, button north for controller
        return (Player1Keyboard != null && Player1Keyboard.wKey.wasPressedThisFrame)
            || (Player1Gamepad != null && Player1Gamepad.buttonNorth.wasPressedThisFrame);
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

        if (NetworkSession.IsOnline)
        {
            FinishOnline();
            return;
        }

        // Only maps currently enabled in the Match Settings menu are eligible.
        GameSettings gs = GameSettings.EnsureInstance();

        List<string> playableScenes = new();
        AddSceneIfEnabled(playableScenes, gs.BeachEnabled, beachSceneName);
        AddSceneIfEnabled(playableScenes, gs.ParkEnabled, parkSceneName);
        AddSceneIfEnabled(playableScenes, gs.IcebergEnabled, icebergSceneName);
        AddSceneIfEnabled(playableScenes, gs.VolcanoEnabled, volcanoSceneName);

        if (playableScenes.Count == 0 && Application.CanStreamedLevelBeLoaded(fallbackGameSceneName))
            playableScenes.Add(fallbackGameSceneName);

        if (playableScenes.Count == 0)
        {
            Debug.LogError("No playable game scenes are configured in TutorialMenu.");
            return;
        }

        SceneManager.LoadScene(playableScenes[Random.Range(0, playableScenes.Count)]);
    }

    // Online, the match can't start until everyone is through the tutorial, so this player waits for the rest
    private void FinishOnline()
    {
        waitingForOthers = true;

        confirmationPanel.SetActive(false);
        tutorialPanel.SetActive(true);
        title.text = "Ready!";
        explanation.text = "Waiting for everyone else to finish the tutorial...";

        if (NetworkSession.IsServer) playersDone.Add(NetworkManager.Singleton.LocalClientId);
        else SendDone();
    }

    private void UpdateWaiting()
    {
        if (!NetworkSession.IsServer)
        {
            if (Time.unscaledTime >= nextDoneSendTime) SendDone();
            return;
        }

        // Checked every frame, so that someone leaving partway through doesn't hold everyone else up
        if (matchLoading) return;
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (!playersDone.Contains(clientId)) return;
        }

        matchLoading = true;
        NetworkSession.LoadMatchScene();
    }

    private void SendDone()
    {
        nextDoneSendTime = Time.unscaledTime + doneResendInterval;

        using FastBufferWriter writer = new(1, Allocator.Temp);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(tutorialDoneMessage, NetworkManager.ServerClientId, writer);
    }

    // Host only
    private void OnPlayerDone(ulong senderClientId, FastBufferReader reader)
    {
        playersDone.Add(senderClientId);
    }

    // Adds sceneName to the pool only if its map is enabled in GameSettings
    // and the scene is included in Build Settings.
    private void AddSceneIfEnabled(List<string> playableScenes, bool mapEnabled, string sceneName)
    {
        if (!mapEnabled) return;
        if (string.IsNullOrWhiteSpace(sceneName)) return;

        string trimmedSceneName = sceneName.Trim();
        if (Application.CanStreamedLevelBeLoaded(trimmedSceneName))
            playableScenes.Add(trimmedSceneName);
        else
            Debug.LogWarning($"Game scene '{trimmedSceneName}' is not included in Build Settings and will be skipped.");
    }
}
