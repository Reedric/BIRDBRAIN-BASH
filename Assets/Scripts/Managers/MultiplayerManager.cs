using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.InputSystem.Users;
using Unity.Collections;
using Unity.Netcode.Components;
using Unity.Netcode;

/// <summary>
/// What clients need to know about each bird in an online match.
/// </summary>
public struct OnlineRosterEntry : INetworkSerializable
{
    public NetworkObjectReference bird;
    public BirdType birdType;
    public bool onLeft;
    public bool isAI;
    public FixedString128Bytes playerName; // Empty for AI, or a player whose name never arrived

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref bird);
        serializer.SerializeValue(ref birdType);
        serializer.SerializeValue(ref onLeft);
        serializer.SerializeValue(ref isAI);
        serializer.SerializeValue(ref playerName);
    }
}

public class MultiplayerManager : NetworkBehaviour
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
    // Detected by an empty isKBMInput list. All 4 slots are Hard AI, but player 1's
    // gamepad can still open the pause menu so the demo can be exited.
    private bool isDemoMode = false;

    // Track PlayerInput per player index so we can re-pair on reconnect
    private Dictionary<int, PlayerInput> playerInputMap = new();

    // HUDManager.Instance is null during Awake() because script execution order isn't guaranteed.
    // We store pending AI registrations here and flush them in Start() once HUDManager exists.
    private List<(int playerIndex, BirdType birdType)> pendingAIRegistrations = new();

    // Online: how long to wait for clients to load the match scene before starting without them
    private const float clientLoadTimeout = 120f;

    // Online: the bird in each player slot, sent to clients once they have all been spawned
    private readonly OnlineRosterEntry[] onlineRoster = new OnlineRosterEntry[4];

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

        // Debug.Log("Getting character manager...");
        cManager = GetComponent<CharacterManager>();

        // Subscribe to device changes to handle disconnects and reconnects
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    void Start()
    {
        // Online matches get their players from the lobby, and only the host sets them up
        if (!DataTransferManager.isLocal)
        {
            HidePlayerIndicators();
            if (NetworkSession.IsServer) StartCoroutine(InitializeOnlinePlayers());
            return;
        }

        instance.isKBMInput = DataTransferManager.isKBMInput;
        instance.selectedBirds = DataTransferManager.selectedBirds;

        // An empty isKBMInput list means the demo button was pressed (no human players)
        isDemoMode = (isKBMInput == null || isKBMInput.Count == 0);

        // Initialize the players
        InitializePlayers();

        // Hide player ready indicators
        HidePlayerIndicators();

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
        if (!isDemoMode)
        {
            if (DataTransferManager.isLocal) SyncPlayerDevices();
            return;
        }

        // Any controller's start button or the keyboard's escape, so there's always a way out of the demo.
        bool pausePressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            || Gamepad.all.Any(pad => pad.startButton.wasPressedThisFrame);

        if (pausePressed)
        {
            // Player 1's gamepad (Gamepad.all[0]) always owns the pause menu in demo mode
            PauseMenu.Instance.pausedPlayerID = 0;
            if (PauseMenu.Instance.GameIsPaused)
                PauseMenu.Instance.Resume();
            else
                PauseMenu.Instance.Pause();
        }
    }

    // Gives each local player's bird exactly the devices LocalPlayerDevices says are theirs, so they can switch
    // between keyboard and controller (or pick up a new controller) at any point in the match, including while
    // paused. Checked every frame, since Phoenix's chickens and birds coming back from being one get new PlayerInputs.
    private static void SyncPlayerDevices()
    {
        foreach (PlayerInput input in PlayerInput.all)
        {
            if (!input.user.valid || !input.TryGetComponent(out BallInteract ballInteract)) continue;

            int slot = ballInteract.playerID;
            if (slot < 0 || slot >= 4) continue;

            List<InputDevice> wanted = LocalPlayerDevices.DevicesFor(slot);

            foreach (InputDevice device in input.user.pairedDevices.ToArray())
            {
                if (!wanted.Contains(device)) input.user.UnpairDevice(device);
            }

            foreach (InputDevice device in wanted)
            {
                if (!input.user.pairedDevices.Contains(device)) InputUser.PerformPairingWithDevice(device, input.user);
            }

            // A control scheme only lets through the bindings for its own devices (keyboard or gamepad), which would
            // stop the other one working. Only the player's paired devices can reach their actions anyway.
            if (input.actions != null && input.actions.bindingMask.HasValue) input.actions.bindingMask = null;
        }
    }

    public override void OnDestroy()
    {
        // Always unsubscribe to avoid stale callbacks after scene unload
        InputSystem.onDeviceChange -= OnDeviceChange;
        base.OnDestroy();
    }

    private void HidePlayerIndicators()
    {
        foreach (RawImage indicator in playerIndicators)
        {
            indicator.enabled = false;
        }
    }

    // Online players

    // Host only. Waits for everyone to load, then spawns a bird for each player in the lobby and AI for
    // the rest of the slots. Spawning them through NGO is what makes them exist on the clients.
    private IEnumerator InitializeOnlinePlayers()
    {
        float elapsed = 0f;
        while (!NetworkSession.MatchSceneLoaded && elapsed < clientLoadTimeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        List<LobbyPlayer> lobbyPlayers = DataTransferManager.onlineRoster ?? new List<LobbyPlayer>();

        // Everyone online counts as a controller player for AI difficulty purposes
        isKBMInput = new List<bool>();
        selectedBirds = new List<BirdType>();
        foreach (LobbyPlayer lobbyPlayer in lobbyPlayers)
        {
            isKBMInput.Add(false);
            selectedBirds.Add(lobbyPlayer.bird);
        }

        ScoreManager.Instance.readiedUp = new bool[4];

        for (int playerIndex = 0; playerIndex < 4; ++playerIndex)
        {
            if (playerIndex < lobbyPlayers.Count) SpawnOnlinePlayer(playerIndex, lobbyPlayers[playerIndex]);
            else MakeAI(playerIndex);
        }

        ApplyRosterRpc(onlineRoster, GameSettings.EnsureInstance().CurrentTeamArrangement);
        NetworkFx.StartCountdownEverywhere();
    }

    private void SpawnOnlinePlayer(int playerIndex, LobbyPlayer lobbyPlayer)
    {
        ulong clientId = lobbyPlayer.clientId;
        BirdType type = selectedBirds[playerIndex];
        if (type == BirdType.OTHER)
        {
            type = (BirdType)UnityEngine.Random.Range(0, (int)BirdType.OTHER);
            selectedBirds[playerIndex] = type;
        }

        GameObject bird = Instantiate(GetBirdModel(type, true, false));
        int spawnSlot = GetSpawnSlot(playerIndex);

        // Place the bird before spawning it so clients don't see it slide over from somewhere else
        bird.transform.SetPositionAndRotation(playerSpawnpoints[spawnSlot].position, playerSpawnpoints[spawnSlot].rotation);

        // If this player leaves, keep their bird on the court so the match can continue
        NetworkObject netObj = bird.GetComponent<NetworkObject>();
        netObj.DontDestroyWithOwner = true;
        netObj.SpawnAsPlayerObject(clientId, true);

        MakePlayer(bird, playerIndex);

        onlineRoster[playerIndex] = new OnlineRosterEntry
        {
            bird = netObj,
            birdType = type,
            onLeft = spawnSlot < 2,
            isAI = false,
            playerName = lobbyPlayer.name
        };
    }

    // Everything about the birds that clients need but can't work out on their own. Also used by the
    // host, since it fills in the HUD the same way.
    [Rpc(SendTo.ClientsAndHost)]
    private void ApplyRosterRpc(OnlineRosterEntry[] roster, GameSettings.TeamArrangement teamArrangement)
    {
        GameSettings.EnsureInstance().CurrentTeamArrangement = teamArrangement;
        StartCoroutine(ApplyRoster(roster));
    }

    private IEnumerator ApplyRoster(OnlineRosterEntry[] roster)
    {
        // The birds were spawned just before this message was sent, but wait for them to exist to be safe
        NetworkObject[] birds = new NetworkObject[roster.Length];
        float elapsed = 0f;
        while (elapsed < clientLoadTimeout)
        {
            bool allFound = true;
            for (int i = 0; i < roster.Length; ++i)
                allFound &= roster[i].bird.TryGet(out birds[i]);

            if (allFound) break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // HUDManager.RefreshPlayerCard reads this list for every card in an online match
        DataTransferManager.selectedBirds = new List<BirdType>();
        foreach (OnlineRosterEntry entry in roster)
            DataTransferManager.selectedBirds.Add(entry.birdType);

        for (int playerIndex = 0; playerIndex < roster.Length; ++playerIndex)
        {
            OnlineRosterEntry entry = roster[playerIndex];
            HUDManager.Instance?.RegisterAICard(playerIndex, entry.birdType);

            // Online, a player's card shows their name (see PlayerNames). AI cards keep their bird's name.
            if (!entry.isAI && entry.playerName.Length > 0)
                HUDManager.Instance?.SetPlayerName(playerIndex, entry.playerName.ToString());

            NetworkObject bird = birds[playerIndex];
            if (IsServer || bird == null) continue;

            bird.name = entry.isAI ? $"AI {playerIndex + 1}" : $"Player {playerIndex + 1}";

            if (bird.TryGetComponent(out BallInteract ballInteract))
            {
                ballInteract.playerID = playerIndex;
                ballInteract.onLeft = entry.onLeft;
            }

            if (bird.TryGetComponent(out AIBehavior aiBehavior))
                aiBehavior.SetIdentity(entry.onLeft, playerIndex);

            FollowObject follower = GetFollowObjectForPlayerIndex(playerIndex);
            if (follower != null) follower.target = bird.transform;
        }

        HUDManager.Instance?.UpdateTeamCardBackgrounds();
    }

    // Lights up a player's ready indicator on the end screen for clients.
    public static void ShowReadyIndicatorOnClients(int playerIndex)
    {
        if (NetworkSession.IsServer && instance != null) instance.ShowReadyIndicatorRpc(playerIndex);
    }

    [Rpc(SendTo.NotServer)]
    private void ShowReadyIndicatorRpc(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < playerIndicators.Length)
            playerIndicators[playerIndex].enabled = true;
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
                // LocalPlayerDevices hands it back to its player the first time it's used, and SyncPlayerDevices pairs it
                Debug.Log($"[MultiplayerManager] Gamepad reconnected: {gamepad.displayName}");
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

        // One entry for each slot. MakeAI marks the AI slots as already ready.
        ScoreManager.Instance.readiedUp = new bool[4];

        // Keeps the controllers players had in character select
        if (!isDemoMode) LocalPlayerDevices.Configure(isKBMInput);

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
                // Controller init can fail if no pad is connected yet
                player = InitializeControllerPlayer(birdPrefab, playerCount);
            }

            // If the player failed to initialize (e.g. no controller found), skip them
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

            Debug.Log("Made player");
        }

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

    // Takes playerCount for logging, and starts a retry coroutine if no pad is found
    PlayerInput InitializeControllerPlayer(GameObject prefab, int playerCount)
    {
        // The controller this player had in character select, if any
        // (Not just any free one: Configure already handed those out, so a free one here would be another player's)
        Gamepad controller = LocalPlayerDevices.GetGamepad(playerCount);

        // With no controller, the bird is still made straight away, and the player gets the next controller anyone
        // presses (see LocalPlayerDevices). Waiting for one would leave the match a player short, which it can't
        // start without.
        if (controller == null)
        {
            Debug.LogWarning($"[MultiplayerManager] No controller for Player {playerCount + 1} yet. Press any button on one to take control.");
            return PlayerInput.Instantiate(prefab, playerIndex: playerCount);
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

        // Local or online multiplayer components
        if (GameManager.Instance.isLocal)
        {
            // Not every prefab has these. The KBM birds never go online, for example.
            if (player.TryGetComponent(out NetworkTransform networkTransform)) networkTransform.enabled = false;
            if (player.TryGetComponent(out PlayerNetworkController networkController)) networkController.enabled = false;
        }

        // Determine physical spawn slot

        int spawnSlot = GetSpawnSlot(playerIndex);

        bool onLeft = spawnSlot < 2;

        // Ball interaction

        BallInteract ballInteract = player.GetComponent<BallInteract>();

        if (ballInteract != null)
        {
            // Player ID remains their actual player number.
            ballInteract.playerID = playerIndex;

            // Court side is based on the team arrangement,
            // not simply whether playerIndex < 2.
            ballInteract.onLeft = onLeft;
        }

        // Spawn

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

        // Follow object

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

        // Game manager team assignment

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

        // Ready indicator

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
        // Random bird

        BirdType birdType =
            (BirdType)(int)(UnityEngine.Random.value * 11);

        GameObject aiModel =
            GetBirdModel(birdType, false, false);

        // Fallback if AI model does not exist.
        if (aiModel == null)
        {
            aiModel = cManager.PenguinAI;
            birdType = BirdType.PENGUIN;
        }

        // Create AI

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

        // Determine physical spawn slot

        int spawnSlot = GetSpawnSlot(playerIndex);

        bool onLeft = spawnSlot < 2;

        aiBehavior.onLeft = onLeft;
        aiBehavior.playerID = playerIndex;

    // Difficulty

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

        // Spawn

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

        // AI never needs to ready up on the end screen
        ScoreManager.Instance.readiedUp[spawnSlot] = true;

        if (DataTransferManager.isLocal)
        {
            if (ai.TryGetComponent(out NetworkTransform networkTransform)) networkTransform.enabled = false;
        }
        else
        {
            // Must happen before GameManager is told about this bird, since it stores the network id
            NetworkObject netObj = ai.GetComponent<NetworkObject>();
            netObj.Spawn(true);

            onlineRoster[playerIndex] = new OnlineRosterEntry
            {
                bird = netObj,
                birdType = birdType,
                onLeft = onLeft,
                isAI = true
            };
        }

        // Follow object

        // Followers are keyed to the player's own number (color-coded per
        // player), not to the physical spawn slot, so they stay correct
        // no matter how the team arrangement reshuffles spawn slots.
        FollowObject fo =
            GetFollowObjectForPlayerIndex(playerIndex);

        if (fo != null)
            fo.target = ai.transform;

        // Game manager team assignment

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

        // HUD registration

        // Online, the roster fills in every card instead
        if (DataTransferManager.isLocal)
        {
            pendingAIRegistrations.Add(
                (playerIndex, birdType)
            );
        }
    }

    // Team arrangement / spawn mapping

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
            // P1 P2 vs P3 P4
            case GameSettings.TeamArrangement.P1P2_vs_P3P4:

                switch (playerIndex)
                {
                    case 0: return 0; // P1 -> Left 1
                    case 1: return 1; // P2 -> Left 2
                    case 2: return 2; // P3 -> Right 1
                    case 3: return 3; // P4 -> Right 2
                }

                break;

            // P1 P3 vs P2 P4
            case GameSettings.TeamArrangement.P1P3_vs_P2P4:

                switch (playerIndex)
                {
                    case 0: return 0; // P1 -> Left 1
                    case 1: return 2; // P2 -> Right 1
                    case 2: return 1; // P3 -> Left 2
                    case 3: return 3; // P4 -> Right 2
                }

                break;

            // P1 P4 vs P2 P3
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

        // Fallback
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