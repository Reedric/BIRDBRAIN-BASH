using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [Header("Left Team")]
    private GameObject leftPlayer1Local; // First player on left side (local multiplayer)
    private GameObject leftPlayer2Local; // Second player on left side (local multiplayer)
    private NetworkVariable<ulong> leftPlayer1Net; // First player on left side (online multiplayer)
    private NetworkVariable<ulong> leftPlayer2Net; // Second player on left side (online multiplayer)
    public GameObject leftPlayer1 // First player on left side (API version)
    {
        get
        {
            if (isLocal) return leftPlayer1Local;
            else if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(leftPlayer1Net.Value, out var netObj)) return netObj.gameObject;
            return null;
        }
        set
        {
            if (isLocal) leftPlayer1Local = value;
            else leftPlayer1Net.Value = value.GetComponent<NetworkObject>().NetworkObjectId;
        }
    } 
    public GameObject leftPlayer2 // Second player on left side (API version)
    {
        get
        {
            if (isLocal) return leftPlayer2Local;
            else if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(leftPlayer2Net.Value, out var netObj)) return netObj.gameObject;
            return null;
        }
        set
        {
            if (isLocal) leftPlayer2Local = value;
            else leftPlayer2Net.Value = value.GetComponent<NetworkObject>().NetworkObjectId;;
        }
    }  

    [Header("Right Team")]
    private GameObject rightPlayer1Local; // First player on right side (local multiplayer)
    private GameObject rightPlayer2Local; // Second player on right side (local multiplayer)
    private NetworkVariable<ulong> rightPlayer1Net; // First player on right side (online multiplayer)
    private NetworkVariable<ulong> rightPlayer2Net; // Second player on right side (online multiplayer)
    public GameObject rightPlayer1 // First player on right side (API version)
    {
        get
        {
            if (isLocal) return rightPlayer1Local;
            else if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(rightPlayer1Net.Value, out var netObj)) return netObj.gameObject;
            return null;
        }
        set
        {
            if (isLocal) rightPlayer1Local = value;
            else rightPlayer1Net.Value = value.GetComponent<NetworkObject>().NetworkObjectId;;
        }
    } 
    public GameObject rightPlayer2 // Second player on right side (API version)
    {
        get
        {
            if (isLocal) return rightPlayer2Local;
            else if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(rightPlayer2Net.Value, out var netObj)) return netObj.gameObject;
            return null;
        }
        set
        {
            if (isLocal) rightPlayer2Local = value;
            else rightPlayer2Net.Value = value.GetComponent<NetworkObject>().NetworkObjectId;;
        }
    } 
    

    [Header("Game Manager Stuff")]
    private GameState gameStateLocal; // State of the match (local multiplayer)
    private NetworkVariable<GameState> gameStateNet; // State of the match (online multiplayer)
    public GameState gameState // State of the match (API version)
    {
        get => isLocal ? gameStateLocal : gameStateNet.Value;
        set
        {
            if (isLocal) gameStateLocal = value;
            else gameStateNet.Value = value;
        }
    }

    private GameObject lastHitLocal; // Player that had the last hit (local multiplayer)
    private NetworkVariable<ulong> lastHitNet; // Player that had the last hit (online multiplayer)
    public GameObject lastHit // Player that had the last hit (API version)
    {
        get
        {
            if (isLocal) return lastHitLocal;
            else if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(lastHitNet.Value, out var netObj)) return netObj.gameObject;
            return null;
        }
        set
        {
            if (isLocal) lastHitLocal = value;
            else lastHitNet.Value = value.GetComponent<NetworkObject>().NetworkObjectId;;
        }
    }


    private GameObject serverLocal; // Player who serves this point (local multiplayer)
    private NetworkVariable<ulong> serverNet; // Player who serves this point (online multiplayer)
    public GameObject server // Player who serves this point (API version)
    {
        get
        {
            if (isLocal) return serverLocal;
            else if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(serverNet.Value, out var netObj)) return netObj.gameObject;
            return null;
        }
        set
        {
            if (isLocal) serverLocal = value;
            else serverNet.Value = value.GetComponent<NetworkObject>().NetworkObjectId;;
        }
    }

    private bool leftAttackLocal; // If left is attacking (local multiplayer)
    private NetworkVariable<bool> leftAttackNet; // If left is attacking (online multiplayer)
    public bool leftAttack // If left is attacking (API version)
    {
        get => isLocal ? leftAttackLocal : leftAttackNet.Value;
        set
        {
            if (isLocal) leftAttackLocal = value;
            else leftAttackNet.Value = value;
        }
    }

    public bool isLocal; // If the game is local or online (not networked bc it is static and would read the same across all clients)
    
    [Header("Countdown Reference")]
    [SerializeField] private countdown countdownScript; // Reference to the countdown script

    private Vector3 leftPlayer1Origin; // The position of the 1st player on the left when the game starts
    private Vector3 leftPlayer2Origin; // The position of the 2nd player on the left when the game starts
    private Vector3 rightPlayer1Origin; // The position of the 1st player on the right when the game starts
    private Vector3 rightPlayer2Origin; // The position of the 2nd player on the right when the game starts
    private Vector3 leftServeLocation; // The positon where the left team will serve from
    private Vector3 rightServeLocation; // The position where the right team will serve from

    private static GameManager instance; // Private instance of the GameManager that other classes cannot reference
    public static GameManager Instance => instance; // Public instance of GameManager that other classes can reference

    // Enum class to represent the game state
    public enum GameState
    {
        PointStart, // State right before a serve
        PointEnd, // Start right after a point is over
        Served, // State when ball has been served
        Bumped, // State when ball has been bumped
        Set, // State when ball has been set
        Spiked, // State when ball has been spiked
        Blocked, // State when ball has been blocked
        GameOver // State when the game is over
    }

    void Awake()
    {
        // Check for duplicates to ensure singleton
        if (instance != null && instance != this)
        {
            Debug.Log("Getting rid of duplicate GameManager.");
            Destroy(this);
            return;
        }

        // Initialize singleton to this script
        instance = this;

        // Set the local or online variable
        isLocal = DataTransferManager.isLocal;
        
        // Auto-find countdown script if not assigned
        if (countdownScript == null)
        {
            countdownScript = FindObjectOfType<countdown>();
        }

        // Initialize all network variables if it is a online multiplayer match
        if (!isLocal)
        {
            leftPlayer1Net = new NetworkVariable<ulong>();
            leftPlayer2Net = new NetworkVariable<ulong>();
            rightPlayer1Net = new NetworkVariable<ulong>();
            rightPlayer2Net = new NetworkVariable<ulong>();
            gameStateNet = new NetworkVariable<GameState>();
            lastHitNet = new NetworkVariable<ulong>();
            serverNet = new NetworkVariable<ulong>();
            leftAttackNet = new NetworkVariable<bool>();
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(InitializeWhenPlayersAreReady());
    }

    private IEnumerator InitializeWhenPlayersAreReady()
    {
        float timeout = 5f;
        while (!HasRequiredMatchObjects() && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (!HasRequiredMatchObjects())
        {
            Debug.LogError("GameManager could not start: four players and a ball with a Rigidbody are required in this scene.");
            yield break;
        }

        // Set the last hit to null
        lastHit = null; 

        // Set the server to the first player on the right team
        server = rightPlayer1;
        leftAttack = false;

        // Assign tags to players for PenguinScript court side detection
        if (leftPlayer1 != null)
        {
            leftPlayer1Origin = leftPlayer1.transform.position;
        }
        else
        {
            Debug.LogError("Left player 1 not set in inspector for Game Manager!");
        }

        if (leftPlayer2 != null)
        {
            leftPlayer2Origin = leftPlayer2.transform.position;
        }
        else
        {
            Debug.LogError("Left player 2 not set in inspector for Game Manager!");
        }

        if (rightPlayer1 != null)
        {
            rightPlayer1Origin = rightPlayer1.transform.position;
        }
        else
        {
            Debug.LogError("Right player 1 not set in inspector for Game Manager!");
        }

        if (rightPlayer2 != null)
        {
            rightPlayer2Origin = rightPlayer2.transform.position;
        }
        else
        {
            Debug.LogError("Right player 2 not set in inspector for Game Manager!");
        }

        // Set the locations for the left and right serve location to be just outside of the court
        leftServeLocation = new Vector3(-10, 1, 0);
        rightServeLocation = new Vector3(10, 1, 0);

        // Start the first point
        NextPoint();
    }

    private bool HasRequiredMatchObjects()
    {
        return leftPlayer1 != null && leftPlayer2 != null
            && rightPlayer1 != null && rightPlayer2 != null
            && FindFirstObjectByType<BallManager>()?.GetComponent<Rigidbody>() != null;
    }

    // Returns true if a point is actively being played
    public static bool PointInProgress()
    {
        return instance.gameState != GameState.PointStart
            && instance.gameState != GameState.PointEnd
            && instance.gameState != GameState.GameOver;
    }
    
    // Returns true if the countdown has finished showing "GO"
    public static bool IsCountdownComplete()
    {
        if (instance.countdownScript == null)
            return true; // Allow game to proceed if countdown script is not assigned
        return instance.countdownScript.IsCountdownComplete;
    }

    // Rotate server when the team who did not serve whens a point
    public static void RotateServer()
    {
        // Order for serve rotation:
        // 1st: RP1, 2nd: LP1, 3rd: RP2, 4th: LP2, then start over
        if (instance.server == instance.rightPlayer1)
        {
            instance.server = instance.leftPlayer1;
            instance.leftAttack = true;
        }
        else if (instance.server == instance.leftPlayer1)
        {
            instance.server = instance.rightPlayer2;
            instance.leftAttack = false;
        }
        else if (instance.server == instance.rightPlayer2)
        {
            instance.server = instance.leftPlayer2;
            instance.leftAttack = true;
        }
        else
        {
            instance.server = instance.rightPlayer1;
            instance.leftAttack = false;
        }
    }

    public static void NextPoint()
    {
        if (instance == null || !instance.HasRequiredMatchObjects())
        {
            Debug.LogWarning("GameManager cannot reset the point until four players and the ball are available.");
            return;
        }

        // Clear any active buffs/debuffs/stuns before resetting positions
        if (BuffsDebuffs.Instance != null)
        {
            BuffsDebuffs.Instance.ClearAllEffects();
        }

        // Brute-force reset all player movement/ragdoll state
        foreach (GameObject player in new[] {
            instance.leftPlayer1, instance.leftPlayer2,
            instance.rightPlayer1, instance.rightPlayer2 })
        {
            if (player == null) continue;

            CharacterMovement movement = player.GetComponent<CharacterMovement>();
            if (movement != null) movement.enabled = true;

            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = true;
            }
        }

        // Reset all positions and velocities for all players
        instance.leftPlayer1.transform.position = instance.leftPlayer1Origin;
        instance.leftPlayer2.transform.position = instance.leftPlayer2Origin;
        instance.rightPlayer1.transform.position = instance.rightPlayer1Origin;
        instance.rightPlayer2.transform.position = instance.rightPlayer2Origin;

        ResetPlayerRigidbody(instance.leftPlayer1);
        ResetPlayerRigidbody(instance.leftPlayer2);
        ResetPlayerRigidbody(instance.rightPlayer1);
        ResetPlayerRigidbody(instance.rightPlayer2);

        // Reset ball physics completely
        BallManager ballManager = FindFirstObjectByType<BallManager>();
        Rigidbody ballRb = ballManager.GetComponent<Rigidbody>();
        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;
        ballRb.useGravity = false;

        // Set server's and ball's position
        if (instance.leftAttack)
        {
            instance.server.transform.position = instance.leftServeLocation;
            ballManager.gameObject.transform.position =
                instance.leftServeLocation + new Vector3(1, 0, 0);
        }
        else
        {
            instance.server.transform.position = instance.rightServeLocation;
            ballManager.gameObject.transform.position =
                instance.rightServeLocation - new Vector3(1, 0, 0);
        }

        // Reset the game manager fields
        instance.gameState = GameState.PointStart;
        instance.lastHit = null;
    }

    private static void ResetPlayerRigidbody(GameObject player)
    {
        if (!player.TryGetComponent(out Rigidbody rb)) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log(
            $"[GM] OnNetworkSpawn | " +
            $"IsServer={IsServer} | " +
            $"IsClient={IsClient} | " +
            $"IsSpawned={IsSpawned} | " +
            $"NetworkObjectId={NetworkObjectId}",
            this
        );
    }
}