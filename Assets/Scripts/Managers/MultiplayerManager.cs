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

        // Initialize the players to play
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

            Debug.Log("Made player");
        }

        // Instantiate readied up for score manager
        ScoreManager.Instance.readiedUp = new bool[playerCount];

        // Now add AI players, if necessary
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
            default:
                if (!isPlayer) return cManager.PenguinAI;
                return isKBM ? cManager.PenguinKBM : cManager.PenguinC;
        }
    }

    void MakePlayer(GameObject player, int playerCount)
    {
        // Set side of court for player
        BallInteract ballInteract = player.GetComponent<BallInteract>();
        ballInteract.onLeft = playerCount < 2 ? true : false;
        ballInteract.playerID = playerCount;
        
        // Assign the transform of the player
        player.transform.position = playerSpawnpoints[playerCount].position;
        player.transform.rotation = playerSpawnpoints[playerCount].rotation;
        player.transform.name = $"Player {playerCount + 1}";

        // Find the follow object for this player and set their role in game manager
        FollowObject fo;
        GameManager gameManager = GameManager.Instance;
        if (playerCount == 0)
        {
            fo = GameObject.Find("PlayerOneFollow").GetComponent<FollowObject>();
            gameManager.leftPlayer1 = player.gameObject;
        }
        else if (playerCount == 1)
        {
            fo = GameObject.Find("PlayerTwoFollow").GetComponent<FollowObject>();
            gameManager.leftPlayer2 = player.gameObject;
        }
        else if (playerCount == 2)
        {
            fo = GameObject.Find("PlayerThreeFollow").GetComponent<FollowObject>();
            gameManager.rightPlayer1 = player.gameObject;
        }
        else
        {
            fo = GameObject.Find("PlayerFourFollow").GetComponent<FollowObject>();
            gameManager.rightPlayer2 = player.gameObject;
        }

        // Set the follow object to this player
        fo.target = player.transform;

        // Set the ready up icon for this bird
        player.GetComponent<EndScreen>().readyIndicator = playerIndicators[playerCount];
    }

    void MakeAI(int playerCount)
    {
        // Random bird for the AI
        BirdType birdType = (BirdType) (int) (UnityEngine.Random.value * 11);

        // Get the model for the ai
        GameObject aiModel = GetBirdModel(birdType, false, false);

        // DELETE THIS LATER
        // Currently, there are some birds with controller prefabs that don't have
        // AI prefabs, so as a back up just default to the penguin one
        if (aiModel == null) aiModel = cManager.PenguinAI;

        // Initialize the prefab keyboard and mouse prefab
        GameObject ai = Instantiate(aiModel);

        // If it is not enabled, enable it
        if (!ai.activeInHierarchy) ai.SetActive(true);

        // Get the ai component and assign the fields
        AIBehavior aIBehavior = ai.GetComponent<AIBehavior>();
        aIBehavior.onLeft = playerCount < 2 ? true : false;
        aIBehavior.SetAIDifficulty(playerCount < 2 ? AIBehavior.AIDifficulty.Hard : AIBehavior.AIDifficulty.Medium);        

        // Set ai transform
        ai.transform.position = playerSpawnpoints[playerCount].position;
        ai.transform.rotation = playerSpawnpoints[playerCount].rotation;
        ai.transform.name = $"AI {playerCount - isKBMInput.Count + 1}";

        // Assign the ai to its respective spot for the game manager
        FollowObject fo;
        GameManager gameManager = GameManager.Instance;
        if (playerCount == 1)
        {
            gameManager.leftPlayer2 = ai;
            fo = GameObject.Find("PlayerTwoFollow").GetComponent<FollowObject>();
        }
        else if (playerCount == 2)
        {
            gameManager.rightPlayer1 = ai;
            fo = GameObject.Find("PlayerThreeFollow").GetComponent<FollowObject>();
        }
        else if (playerCount == 3)
        {
            gameManager.rightPlayer2 = ai;
            fo = GameObject.Find("PlayerFourFollow").GetComponent<FollowObject>();
        }
        else // This should never happen as there should always be one human player, but better to be safe than sorry
        {
            fo = GameObject.Find("PlayerOneFollow").GetComponent<FollowObject>();
        }
        fo.target = ai.transform;

        // Store for deferred registration — HUDManager.Instance is null here during Awake(),
        // so we queue this and flush it in Start() instead of calling RegisterAICard directly.
        pendingAIRegistrations.Add((playerCount, birdType));
    }
}