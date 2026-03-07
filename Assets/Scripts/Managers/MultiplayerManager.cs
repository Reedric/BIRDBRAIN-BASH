using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MultiplayerManager : MonoBehaviour
{
    [Header("Player Transforms")]
    public Transform[] playerSpawnpoints; // Spawnpoints that the players and AI will use

    [Header("Player Prefabs")]
    [SerializeField] private GameObject keyboardPrefab; // Prefab for a keyboard player
    [SerializeField] private GameObject gamepadPrefab; // Prefab for a controller player
    [SerializeField] private GameObject aiPrefab; // Prefab for an AI player

    [SerializeField] private GameManager gameManager; // Instance of the game manager
    private HashSet<InputDevice> inputDevices = new HashSet<InputDevice>(); // Unique input devices currently being used
    private static MultiplayerManager instance; // Singleton reference to the manager
    private List<bool> isKBMInput; // List of inputs for players (true is KBM, false is Controller) [Only ONE KBM allowed]
    private String mainScene = "RodericM2"; // Name of the scene that will be where the main part of the game is

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
            DontDestroyOnLoad(gameObject);
        }

        instance.isKBMInput = DataTransferManager.isKBMInput;
    }

    void InitializePlayers()
    {
        int playerCount = 0;

        // Initialize the players to play
        foreach (bool kbm in isKBMInput)
        {
            PlayerInput player;
            if (kbm)
            {
                player = InitializeKeyboardPlayer();
            }
            else
            {
                player = InitializeControllerPlayer();
            }

            // Assign this player in the game manager
            if (playerCount == 0)
            {
                gameManager.leftPlayer1 = player.gameObject;
            }
            else if (playerCount == 1)
            {
                gameManager.leftPlayer2 = player.gameObject;
            }
            else if (playerCount == 2)
            {
                gameManager.rightPlayer1 = player.gameObject;
            }
            else
            {
                gameManager.rightPlayer2 = player.gameObject;
            }

            // Give the player the necessary scripts to move and interact with the ball
            MakePlayer(player.gameObject, playerCount);
            player.actions.FindActionMap("Player").Enable();
            
            // Increment player count
            playerCount++;
        }

        // Now add AI players, if necessary
        while (playerCount < 4)
        {
            // Spawn AI and give it the apporpriate components
            MakeAI(playerCount);

            // Increment player count
            playerCount++;
        }
    }

    PlayerInput InitializeKeyboardPlayer()
    {
        // Initialize player 1 on keyboard and mouse
        return PlayerInput.Instantiate(
            keyboardPrefab,
            controlScheme: "Keyboard&Mouse",
            pairWithDevices: new InputDevice[]
            {
                Keyboard.current,
                Mouse.current
            }
        );
    }

    PlayerInput InitializeControllerPlayer()
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
            gamepadPrefab,
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

    // public void OnPlayerJoined(PlayerInput player)
    // {
    //     // Potential trigger of multiple joins from same device check
    //     if (player.devices.Count == 0)
    //     {
    //         Debug.Log("Destroying weird player join trigger.");
    //         Destroy(player.gameObject);
    //         return;
    //     }
        

    //     // If this device already in use, do not create duplicate player
    //     if (inputDevices.Contains(player.devices[0]))
    //     {
    //         Debug.Log("Destroying player trying to join");
    //         Destroy(player.gameObject);
    //         return;
    //     }

        
    //     // Add this input device to the set of devices and make a player for them
    //     inputDevices.Add(player.devices[0]);
    //     MakePlayer(player.gameObject, playerCount);
    //     Debug.Log($"Player {player.playerIndex + 1} joined with {player.devices[0]}");
    // }

    // public void OnPlayerLeave(PlayerInput player)
    // {
    //     // If this player doesn't actually exist, skip
    //     if (player.devices.Count == 0 || !inputDevices.Contains(player.devices[0]))
    //     {
    //         return;
    //     }

    //     // Destroy the player
    //     Destroy(player.gameObject);
    //     inputDevices.Remove(player.devices[0]);
    // }

    void MakePlayer(GameObject player, int playerCount)
    {
        // Add new character movement script
        CharacterMovement characterMovement = player.AddComponent<CharacterMovement>();

        // Set the desired fields for the character movement
        characterMovement.maxGroundSpeed = 4.0f;
        characterMovement.maxAirSpeed = characterMovement.maxGroundSpeed / 2;
        characterMovement.jumpForce = 6.0f;

        // Add ball interact stuff and set fields
        BallInteract ballInteract = player.AddComponent<BallInteract>();
        ballInteract.onLeft = playerCount < 2 ? true : false;
        ballInteract.spikeStat = 15.0f;

        // Assign the transform of the player
        player.transform.position = playerSpawnpoints[playerCount].position;
        player.transform.rotation = playerSpawnpoints[playerCount].rotation;
        player.transform.name = $"Player {playerCount + 1}";
    }

    void MakeAI(int playerCount)
    {
        // Initialize the prefab keyboard and mouse prefab
        GameObject ai = Instantiate(aiPrefab);

        // Give it the ai component and assign the fields
        AIBehavior aIBehavior = ai.AddComponent<AIBehavior>();
        aIBehavior.maxGroundSpeed = 4.0f;
        aIBehavior.maxAirSpeed = aIBehavior.maxGroundSpeed / 2;
        aIBehavior.jumpForce = 6.0f;
        aIBehavior.onLeft = playerCount < 2 ? true : false;

        // Set ai transform
        ai.transform.position = playerSpawnpoints[playerCount].position;
        ai.transform.rotation = playerSpawnpoints[playerCount].rotation;
        ai.transform.name = $"AI {playerCount - isKBMInput.Count + 1}";

        // Assign the ai to its respective spot for the game manager
        if (playerCount == 1)
        {
            gameManager.leftPlayer2 = ai;
        }
        else if (playerCount == 2)
        {
            gameManager.rightPlayer1 = ai;
        }
        else if (playerCount == 3)
        {
            gameManager.rightPlayer2 = ai;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == mainScene)
        {
            InitializePlayers();
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OsDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
