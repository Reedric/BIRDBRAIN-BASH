using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.InputSystem.Users;

public class MultiplayerManager : MonoBehaviour
{
    [Header("Player Transforms")]
    public Transform[] playerSpawnpoints; // Spawnpoints that the players and AI will use

    [SerializeField] private GameObject aiPrefab; // Prefab for an AI player
    [SerializeField] private RawImage[] playerIndicators; // Ready up indicators for post-game

    // How long to wait for a controller to be connected before giving up
    [SerializeField] private float controllerWaitTimeout = 10f;

    private CharacterManager cManager; // Instance of character manager
    private static MultiplayerManager instance; // Singleton reference to the manager
    private List<bool> isKBMInput; // List of inputs for players (true is KBM, false is Controller) [Only ONE KBM allowed]
    private List<BirdType> selectedBirds; // List of birds each player selected

    // True when the match was launched from the main menu Demo button (0 human players).
    // Detected by an empty isKBMInput list — all 4 slots are Hard AI, but player 1's
    // gamepad can still open the pause menu so the demo can be exited.
    private bool isDemoMode = false;

    // Track PlayerInput per player index so we can re-pair on reconnect
    private Dictionary<int, PlayerInput> playerInputMap = new();

    // HUDManager.Instance is null during Awake() because script execution order isn't guaranteed.
    // We store pending AI registrations here and flush them in Start() once HUDManager exists.
    private List<(int playerIndex, BirdType birdType)> pendingAIRegistrations = new();

    void Awake()
    {
        // Assign the instance
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        else
        {
            instance = this;
            // DontDestroyOnLoad(gameObject);
        }

        cManager = GetComponent<CharacterManager>();
        instance.isKBMInput = DataTransferManager.isKBMInput;
        instance.selectedBirds = DataTransferManager.selectedBirds;

        // An empty isKBMInput list means the demo button was pressed — no human players
        isDemoMode = (isKBMInput == null || isKBMInput.Count == 0);

        // Subscribe to device changes to handle disconnects and reconnects
        InputSystem.onDeviceChange += OnDeviceChange;

        InitializePlayers();
    }

    void Start()
    {
        // Hide player ready indicators
        foreach (RawImage indicator in playerIndicators)
        {
            indicator.enabled = false;
        }

        // Flush deferred AI card registrations now that HUDManager.Instance is guaranteed to exist.
        // MakeAI() stores registrations here instead of calling HUDManager directly during Awake(),
        // because HUDManager.Instance is null at that point (Awake() order is not guaranteed).
        foreach (var (playerIndex, birdType) in pendingAIRegistrations)
        {
            HUDManager.Instance?.RegisterAICard(playerIndex, birdType);
        }
        pendingAIRegistrations.Clear();
    }

    void Update()
    {
        // In demo mode there are no PlayerInput instances, so the normal input pipeline won't
        // route any button presses. Poll player 1's gamepad directly here so they can still
        // open the pause menu to exit the demo without quitting the whole application.
        if (!isDemoMode) return;

        Gamepad pad = Gamepad.all.Count > 0 ? Gamepad.all[0] : null;
        if (pad == null) return;

        if (pad.startButton.wasPressedThisFrame)
        {
            // Player 1's gamepad (Gamepad.all[0]) always owns the pause menu in demo mode
            PauseMenu.Instance.pausedPlayerID = 0;
            if (PauseMenu.Instance.GameIsPaused)
                PauseMenu.Instance.Resume();
            else
                PauseMenu.Instance.Pause();
        }
    }

    void OnDestroy()
    {
        // Always unsubscribe to avoid stale callbacks after scene unload
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    // Handles controller disconnect and reconnect events
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is not Gamepad gamepad) return;

        switch (change)
        {
            case InputDeviceChange.Disconnected:
                Debug.LogWarning($"[MultiplayerManager] Gamepad disconnected: {gamepad.displayName}");
                // todo: pause game, show reconnect UI here
                break;

            case InputDeviceChange.Reconnected:
                Debug.Log($"[MultiplayerManager] Gamepad reconnected: {gamepad.displayName}");
                TryRepairGamepad(gamepad);
                break;
        }
    }

    // Re-pairs a reconnected gamepad to its original PlayerInput if it lost its device
    private void TryRepairGamepad(Gamepad gamepad)
    {
        foreach (var (playerIndex, playerInput) in playerInputMap)
        {
            // If this PlayerInput has no active gamepad device, pair the reconnected one
            if (playerInput != null && !playerInput.devices.Any(d => d is Gamepad))
            {
                InputUser.PerformPairingWithDevice(gamepad, playerInput.user);
                Debug.Log($"[MultiplayerManager] Re-paired {gamepad.displayName} to Player {playerIndex + 1}");
                return;
            }
        }
    }

    void InitializePlayers()
    {
        int playerCount = 0;

        // In demo mode isKBMInput is empty, so this loop is skipped entirely and
        // all four slots fall through to the MakeAI loop below.
        foreach (bool kbm in isKBMInput)
        {
            // set the bird type that was chosen on the selection screen, if available
            BirdType type = BirdType.OTHER;
            if (selectedBirds != null && selectedBirds.Count > playerCount)
            {
                type = selectedBirds[playerCount];
            }

            // If still other, must've opted for random bird, give them a random bird
            if (type == BirdType.OTHER)
            {
                type = (BirdType) UnityEngine.Random.Range(0, (int) type);
                selectedBirds[playerCount] = type; // write resolved type back so HUD can read it
            }

            // Get the prefab for this player
            GameObject birdPrefab = GetBirdModel(type, true, isKBMInput[playerCount]);

            PlayerInput player;
            if (kbm)
            {
                player = InitializeKeyboardPlayer(birdPrefab);
            }
            else
            {
                // Controller init can fail if no pad is connected yet — handle gracefully
                player = InitializeControllerPlayer(birdPrefab, playerCount);
            }

            // Guard: if player failed to initialize (e.g. no controller found), skip safely
            if (player == null)
            {
                Debug.LogError($"[MultiplayerManager] Failed to initialize player {playerCount + 1} — skipping.");
                playerCount++;
                continue;
            }

            // Give the player the necessary scripts to move and interact with the ball
            MakePlayer(player.gameObject, playerCount);
            player.actions.FindActionMap("Player").Enable();
            player.actions.FindActionMap("UI").Enable();

            // Track for reconnect handling
            playerInputMap[playerCount] = player;

            // Increment player count
            playerCount++;

            // Debug.Log("Made player");
        }

        // Instantiate readied up for score manager
        ScoreManager.Instance.readiedUp = new bool[playerCount];

        // Now add AI players, if necessary.
        // In demo mode playerCount starts at 0, so all four slots are filled with AI here.
        while (playerCount < 4)
        {
            // Spawn AI and give it the appropriate components
            MakeAI(playerCount);

            // Increment player count
            playerCount++;
        }
    }

    PlayerInput InitializeKeyboardPlayer(GameObject prefab)
    {
        // Initialize player 1 on keyboard and mouse
        return PlayerInput.Instantiate(
            prefab,
            controlScheme: "Keyboard&Mouse",
            pairWithDevices: new InputDevice[]
            {
                Keyboard.current,
                Mouse.current
            }
        );
    }

    // Now takes playerCount for logging, and starts a retry coroutine if no pad is found
    PlayerInput InitializeControllerPlayer(GameObject prefab, int playerCount)
    {
        // Get an available gamepad if possible
        Gamepad controller = AvailableGamepad();

        // If there is no available gamepad, start a wait coroutine instead of hard erroring
        if (controller == null)
        {
            Debug.LogWarning($"[MultiplayerManager] No available gamepad for Player {playerCount + 1}. Starting wait coroutine.");
            StartCoroutine(WaitForControllerAndInitialize(prefab, playerCount));
            return null;
        }

        // Initialize the controller player
        return PlayerInput.Instantiate(
            prefab,
            controlScheme: "Gamepad",
            pairWithDevice: controller
        );
    }

    // Polls for a controller to become available, up to controllerWaitTimeout seconds
    private IEnumerator WaitForControllerAndInitialize(GameObject prefab, int playerCount)
    {
        float elapsed = 0f;

        while (elapsed < controllerWaitTimeout)
        {
            Gamepad controller = AvailableGamepad();
            if (controller != null)
            {
                Debug.Log($"[MultiplayerManager] Controller found for Player {playerCount + 1} after {elapsed:F1}s wait.");

                // Initialize player input now that a controller is available
                PlayerInput player = PlayerInput.Instantiate(
                    prefab,
                    controlScheme: "Gamepad",
                    pairWithDevice: controller
                );

                // Give the player the necessary scripts to move and interact with the ball
                MakePlayer(player.gameObject, playerCount);
                player.actions.FindActionMap("Player").Enable();
                player.actions.FindActionMap("UI").Enable();

                // Track for reconnect handling
                playerInputMap[playerCount] = player;

                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // If we reach here, no controller was found in time
        Debug.LogError($"[MultiplayerManager] Timed out waiting for controller for Player {playerCount + 1}.");
    }

    Gamepad AvailableGamepad()
    {
        foreach (Gamepad pad in Gamepad.all)
        {
            bool inUse = false;

            foreach (PlayerInput player in PlayerInput.all)
            {
                if (player.devices.Contains(pad))
                {
                    Debug.LogFormat("{0} in use.", pad);
                    inUse = true;
                    break;
                }
            }

            if (!inUse)
            {
                return pad;
            }
        }

        return null;
    }

    GameObject GetBirdModel(BirdType type, bool isPlayer, bool isKBM)
    {
        // Get the model from Character Manager dependent on the bird type chosen
        switch (type)
        {
            case BirdType.PENGUIN:
                if (!isPlayer) return cManager.PenguinAI;
                return isKBM ? cManager.PenguinKBM : cManager.PenguinC;
            case BirdType.SEAGULL:
                if (!isPlayer) return cManager.SeagullAI;
                return isKBM ? cManager.SeagullKBM : cManager.SeagullC;
            case BirdType.LOVEBIRD:
                if (!isPlayer) return cManager.LovebirdAI;
                return isKBM ? cManager.LovebirdKBM : cManager.LovebirdC;
            case BirdType.TOUCAN:
                if (!isPlayer) return cManager.ToucanAI;
                return isKBM ? cManager.ToucanKBM : cManager.ToucanC;
            case BirdType.PUKEKO:
                if (!isPlayer) return cManager.PukekoAI;
                return isKBM ? cManager.PukekoKBM : cManager.PukekoC;
            case BirdType.SCISSORTAIL:
                if (!isPlayer) return cManager.ScissortailAI;
                return isKBM ? cManager.ScissortailKBM : cManager.ScissortailC;
            case BirdType.DODO:
                if (!isPlayer) return cManager.DodoAI;
                return isKBM ? cManager.DodoKBM : cManager.DodoC;
            case BirdType.PELICAN:
                if (!isPlayer) return cManager.PelicanAI;
                return isKBM ? cManager.PelicanKBM : cManager.PelicanC;
            case BirdType.CHICKEN:
                if (!isPlayer) return cManager.ChickenAI;
                return isKBM ? cManager.ChickenKBM : cManager.ChickenC;
            case BirdType.OSTRICH:
                if (!isPlayer) return cManager.OstrichAI;
                return isKBM ? cManager.OstrichKBM : cManager.OstrichC;
            case BirdType.CROW:
                if (!isPlayer) return cManager.CrowAI;
                return isKBM ? cManager.CrowKBM : cManager.CrowC;
            case BirdType.EAGLE:
                if (!isPlayer) return cManager.EagleAI;
                return isKBM ? cManager.EagleKBM : cManager.EagleC;
            case BirdType.KIWI:
                if (!isPlayer) return cManager.KiwiAI;
                return isKBM ? cManager.KiwiKBM : cManager.KiwiC;
            case BirdType.OWL:
                if (!isPlayer) return cManager.OwlAI;
                return isKBM ? cManager.OwlKBM : cManager.OwlC;
            case BirdType.MACAW:
                if (!isPlayer) return cManager.MacawAI;
                return isKBM ? cManager.MacawKBM : cManager.MacawC;
            case BirdType.PHOENIX:
                if (!isPlayer) return cManager.PhoenixAI;
                return isKBM ? cManager.PhoenixKBM : cManager.PhoenixC;
            case BirdType.ROBOPIGEON:
                if (!isPlayer) return cManager.RobopigeonAI;
                return isKBM ? cManager.RobopigeonKBM : cManager.RobopigeonC;
            case BirdType.HUMMINGBIRD:
                if (!isPlayer) return cManager.HummingbirdAI;
                return isKBM ? cManager.HummingbirdKBM : cManager.HummingbirdC;
            case BirdType.SHIMAENAGA:
                if (!isPlayer) return cManager.ShimaenagaAI;
                return isKBM ? cManager.ShimaenagaKBM : cManager.ShimaenagaC;
            default:
                if (!isPlayer) return cManager.PenguinAI;
                return isKBM ? cManager.PenguinKBM : cManager.PenguinC;
        }
    }

    void MakePlayer(GameObject player, int playerIndex)
    {
        GameManager gameManager = GameManager.Instance;

        // --------------------------------------------------------
        // DETERMINE PHYSICAL SPAWN SLOT
        // --------------------------------------------------------

        int spawnSlot = GetSpawnSlot(playerIndex);

        bool onLeft = spawnSlot < 2;

        // --------------------------------------------------------
        // BALL INTERACTION
        // --------------------------------------------------------

        BallInteract ballInteract = player.GetComponent<BallInteract>();

        if (ballInteract != null)
        {
            // Player ID remains their actual player number.
            ballInteract.playerID = playerIndex;

            // Court side is based on the TEAM ARRANGEMENT,
            // not simply whether playerIndex < 2.
            ballInteract.onLeft = onLeft;
        }

        // --------------------------------------------------------
        // SPAWN
        // --------------------------------------------------------

        if (spawnSlot < 0 || spawnSlot >= playerSpawnpoints.Length)
        {
            Debug.LogError(
                $"MultiplayerManager: Invalid spawn slot {spawnSlot} " +
                $"for Player {playerIndex + 1}."
            );

            return;
        }

        player.transform.position =
            playerSpawnpoints[spawnSlot].position;

        player.transform.rotation =
            playerSpawnpoints[spawnSlot].rotation;

        player.transform.name =
            $"Player {playerIndex + 1}";

        // --------------------------------------------------------
        // FOLLOW OBJECT
        // --------------------------------------------------------

        // Followers are keyed to the player's own number (color-coded per
        // player), not to the physical spawn slot, so they stay correct
        // no matter how the team arrangement reshuffles spawn slots.
        FollowObject fo = GetFollowObjectForPlayerIndex(playerIndex);

        if (fo == null)
        {
            Debug.LogError(
                $"MultiplayerManager: Could not find FollowObject " +
                $"for player {playerIndex + 1}."
            );

            return;
        }

        fo.target = player.transform;

        // --------------------------------------------------------
        // GAME MANAGER TEAM ASSIGNMENT
        // --------------------------------------------------------

        if (onLeft)
        {
            // First physical left spawn is leftPlayer1.
            // Second physical left spawn is leftPlayer2.

            if (spawnSlot == 0)
                gameManager.leftPlayer1 = player.gameObject;
            else
                gameManager.leftPlayer2 = player.gameObject;
        }
        else
        {
            // First physical right spawn is rightPlayer1.
            // Second physical right spawn is rightPlayer2.

            if (spawnSlot == 2)
                gameManager.rightPlayer1 = player.gameObject;
            else
                gameManager.rightPlayer2 = player.gameObject;
        }

        // --------------------------------------------------------
        // READY INDICATOR
        // --------------------------------------------------------

        EndScreen endScreen = player.GetComponent<EndScreen>();

        if (endScreen != null &&
            playerIndex >= 0 &&
            playerIndex < playerIndicators.Length)
        {
            endScreen.readyIndicator =
                playerIndicators[playerIndex];
        }
    }

    void MakeAI(int playerIndex)
    {
        // --------------------------------------------------------
        // RANDOM BIRD
        // --------------------------------------------------------

        BirdType birdType =
            (BirdType)(int)(UnityEngine.Random.value * 11);

        GameObject aiModel =
            GetBirdModel(birdType, false, false);

        // Fallback if AI model does not exist.
        if (aiModel == null)
            aiModel = cManager.PenguinAI;

        // --------------------------------------------------------
        // CREATE AI
        // --------------------------------------------------------

        GameObject ai = Instantiate(aiModel);

        if (!ai.activeInHierarchy)
            ai.SetActive(true);

        AIBehavior aiBehavior =
            ai.GetComponent<AIBehavior>();

        if (aiBehavior == null)
        {
            Debug.LogError(
                $"MultiplayerManager: AI prefab for slot " +
                $"{playerIndex + 1} has no AIBehavior."
            );

            return;
        }

        // --------------------------------------------------------
        // DETERMINE PHYSICAL SPAWN SLOT
        // --------------------------------------------------------

        int spawnSlot = GetSpawnSlot(playerIndex);

        bool onLeft = spawnSlot < 2;

        aiBehavior.onLeft = onLeft;
        aiBehavior.playerID = playerIndex;

    // --------------------------------------------------------
    // DIFFICULTY
    // --------------------------------------------------------

    int humanCount =
        isKBMInput.Count(kbm => !kbm);

    bool isAllyAI =
        (humanCount == 1 && playerIndex == 1) ||
        (humanCount == 3 && playerIndex == 3);

    AIBehavior.AIDifficulty difficulty;

    if (isDemoMode || isAllyAI)
    {
        // Demo showcases and AI teammates always play Hard, regardless
        // of the Bot Difficulty chosen in Match Settings.
        difficulty = AIBehavior.AIDifficulty.Hard;
    }
    else
    {
        // Opponent AI follows whatever Bot Difficulty was picked in the
        // Match Settings menu (GameSettings.CurrentBotDifficulty), set by
        // MatchSettingsMenu.
        GameSettings gs = GameSettings.EnsureInstance();
        difficulty = (AIBehavior.AIDifficulty)(int)gs.CurrentBotDifficulty;
    }

    aiBehavior.SetAIDifficulty(difficulty);

        // --------------------------------------------------------
        // SPAWN
        // --------------------------------------------------------

        if (spawnSlot < 0 ||
            spawnSlot >= playerSpawnpoints.Length)
        {
            Debug.LogError(
                $"MultiplayerManager: Invalid AI spawn slot " +
                $"{spawnSlot} for Player {playerIndex + 1}."
            );

            Destroy(ai);
            return;
        }

        ai.transform.position =
            playerSpawnpoints[spawnSlot].position;

        ai.transform.rotation =
            playerSpawnpoints[spawnSlot].rotation;

        ai.transform.name =
            $"AI {playerIndex + 1}";

        // --------------------------------------------------------
        // FOLLOW OBJECT
        // --------------------------------------------------------

        // Followers are keyed to the player's own number (color-coded per
        // player), not to the physical spawn slot, so they stay correct
        // no matter how the team arrangement reshuffles spawn slots.
        FollowObject fo =
            GetFollowObjectForPlayerIndex(playerIndex);

        if (fo != null)
            fo.target = ai.transform;

        // --------------------------------------------------------
        // GAME MANAGER TEAM ASSIGNMENT
        // --------------------------------------------------------

        GameManager gameManager =
            GameManager.Instance;

        if (onLeft)
        {
            if (spawnSlot == 0)
                gameManager.leftPlayer1 = ai;
            else
                gameManager.leftPlayer2 = ai;
        }
        else
        {
            if (spawnSlot == 2)
                gameManager.rightPlayer1 = ai;
            else
                gameManager.rightPlayer2 = ai;
        }

        // --------------------------------------------------------
        // HUD REGISTRATION
        // --------------------------------------------------------

        pendingAIRegistrations.Add(
            (playerIndex, birdType)
        );
    }

    // ============================================================
    // TEAM ARRANGEMENT / SPAWN MAPPING
    // ============================================================

    /// <summary>
    /// Returns the physical spawnpoint that a player should use
    /// based on the selected team arrangement.
    ///
    /// Player index:
    /// 0 = Player 1
    /// 1 = Player 2
    /// 2 = Player 3
    /// 3 = Player 4
    ///
    /// Spawn slots:
    /// 0 = Left team, position 1
    /// 1 = Left team, position 2
    /// 2 = Right team, position 1
    /// 3 = Right team, position 2
    /// </summary>
    private int GetSpawnSlot(int playerIndex)
    {
        GameSettings settings = GameSettings.EnsureInstance();

        switch (settings.CurrentTeamArrangement)
        {
            // ----------------------------------------------------
            // P1 P2 vs P3 P4
            // ----------------------------------------------------
            case GameSettings.TeamArrangement.P1P2_vs_P3P4:

                switch (playerIndex)
                {
                    case 0: return 0; // P1 -> Left 1
                    case 1: return 1; // P2 -> Left 2
                    case 2: return 2; // P3 -> Right 1
                    case 3: return 3; // P4 -> Right 2
                }

                break;

            // ----------------------------------------------------
            // P1 P3 vs P2 P4
            // ----------------------------------------------------
            case GameSettings.TeamArrangement.P1P3_vs_P2P4:

                switch (playerIndex)
                {
                    case 0: return 0; // P1 -> Left 1
                    case 1: return 2; // P2 -> Right 1
                    case 2: return 1; // P3 -> Left 2
                    case 3: return 3; // P4 -> Right 2
                }

                break;

            // ----------------------------------------------------
            // P1 P4 vs P2 P3
            // ----------------------------------------------------
            case GameSettings.TeamArrangement.P1P4_vs_P2P3:

                switch (playerIndex)
                {
                    case 0: return 0; // P1 -> Left 1
                    case 1: return 2; // P2 -> Right 1
                    case 2: return 3; // P3 -> Right 2
                    case 3: return 1; // P4 -> Left 2
                }

                break;
        }

        // Safety fallback
        return Mathf.Clamp(playerIndex, 0, 3);
    }


    /// <summary>
    /// Returns whether a player belongs on the left side of the court.
    /// </summary>
    public bool IsPlayerOnLeft(int playerIndex)
    {
        int spawnSlot = GetSpawnSlot(playerIndex);

        return spawnSlot < 2;
    }


    /// <summary>
    /// Returns the color-coded FollowObject that belongs to a given player
    /// number (0 = Player 1/blue, 1 = Player 2/green, 2 = Player 3/pink,
    /// 3 = Player 4/yellow). Kept independent of spawn slot so the follower
    /// always tracks the same player regardless of team arrangement.
    /// </summary>
    private FollowObject GetFollowObjectForPlayerIndex(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0:
                return GameObject.Find("PlayerOneFollow")
                    .GetComponent<FollowObject>();

            case 1:
                return GameObject.Find("PlayerTwoFollow")
                    .GetComponent<FollowObject>();

            case 2:
                return GameObject.Find("PlayerThreeFollow")
                    .GetComponent<FollowObject>();

            case 3:
                return GameObject.Find("PlayerFourFollow")
                    .GetComponent<FollowObject>();

            default:
                Debug.LogError(
                    $"MultiplayerManager: Invalid player index {playerIndex}."
                );

                return null;
        }
    }
}