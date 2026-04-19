using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MultiplayerManager : MonoBehaviour
{
    [Header("Player Transforms")]
    public Transform[] playerSpawnpoints; // Spawnpoints that the players and AI will use

    [SerializeField] private GameObject aiPrefab; // Prefab for an AI player
    [SerializeField] private RawImage[] playerIndicators; // Ready up indicators for post-game

    private CharacterManager cManager; // Instance of character manager
    private static MultiplayerManager instance; // Singleton reference to the manager
    private List<bool> isKBMInput; // List of inputs for players (true is KBM, false is Controller) [Only ONE KBM allowed]
    private List<BirdType> selectedBirds; // List of birds each player selected

    void Awake()
    {
        // Assign the instance
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
            // DontDestroyOnLoad(gameObject);
        }

        cManager = GetComponent<CharacterManager>();
        instance.isKBMInput = DataTransferManager.isKBMInput;
        instance.selectedBirds = DataTransferManager.selectedBirds;
        InitializePlayers();
    }

    void Start()
    {
        // Hide player ready indicators
        foreach (RawImage indicator in playerIndicators)
        {
            indicator.enabled = false;
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

            // Get the prefab for this player
            GameObject birdPrefab = GetBirdModel(type, true, isKBMInput[playerCount]);

            PlayerInput player;
            if (kbm)
            {
                player = InitializeKeyboardPlayer(birdPrefab);
            }
            else
            {
                player = InitializeControllerPlayer(birdPrefab);
            }

            // Give the player the necessary scripts to move and interact with the ball
            MakePlayer(player.gameObject, playerCount);
            player.actions.FindActionMap("Player").Enable();
            player.actions.FindActionMap("UI").Enable();

            // Increment player count
            playerCount++;

            Debug.Log("Made player");
        }

        // Instantiate readied up for score manager
        ScoreManager.Instance.readiedUp = new bool[playerCount];

        // Now add AI players, if necessary
        while (playerCount < 4)
        {
            // Spawn AI and give it the apporpriate components
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

    PlayerInput InitializeControllerPlayer(GameObject prefab)
    {
        // Get an available gamepad if possible
        Gamepad controller = AvailableGamepad();

        // If there is no available gamepad, throw an error (change this to wait until a valid controller is connected)
        if (controller == null)
        {
            Debug.LogError("Not enough controllers for the amount of players selected.");
            return null;
        }

        // Initialize the controller player
        return PlayerInput.Instantiate(
            prefab,
            controlScheme: "Gamepad",
            pairWithDevice: controller
        );
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

        // Initialize the prefab keyboard and mouse prefab
        GameObject ai = Instantiate(aiModel);

        // If it is not enabled, enable it
        if (!ai.activeInHierarchy) ai.SetActive(true);

        // Get the ai component and assign the fields
        AIBehavior aIBehavior = ai.GetComponent<AIBehavior>();
        aIBehavior.onLeft = playerCount < 2 ? true : false;

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
    }
}
