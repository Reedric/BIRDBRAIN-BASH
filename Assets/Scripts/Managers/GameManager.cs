using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Left Team")]
    public GameObject leftPlayer1; // First player on left side
    public GameObject leftPlayer2; // Second player on left side

    [Header("Right Team")]
    public GameObject rightPlayer1; // First player on right side
    public GameObject rightPlayer2; // Second player on right side

    [Header("Game Manager Stuff")]
    public GameState gameState; // State of the match
    public GameObject lastHit; // Player that had the last hit
    public GameObject server; // Player who serves this point
    public bool leftAttack; // If left is attacking
    
    [Header("Countdown Reference")]
    [SerializeField] private countdown countdownScript; // Reference to the countdown script

    private Vector3 leftPlayer1Origin; // The position of the 1st player on the left when the game starts
    private Vector3 leftPlayer2Origin; // The position of the 2nd player on the left when the game starts
    private Vector3 rightPlayer1Origin; // The position of the 1st player on the right when the game starts
    private Vector3 rightPlayer2Origin; // The position of the 2nd player on the right when the game starts
    private Vector3 leftServeLocation; // The positon where the left team will serve from
    private Vector3 rightServeLocation; // The position where the right team will serve from

    private static GameManager instance; // Private instance of the GameManager that other classes cannot reference
    public static GameManager Instance // Public instance of GameManager that other classes can reference
    {
        get
        {
            if (instance == null)
            {
                instance = new GameManager();
            }
            return instance;
        }
    }

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
        // Initialize singleton to this script
        instance = this;
        
        // Auto-find countdown script if not assigned
        if (countdownScript == null)
        {
            countdownScript = FindObjectOfType<countdown>();
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
        BuffsDebuffs.Instance.ClearAllEffects();

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
}
