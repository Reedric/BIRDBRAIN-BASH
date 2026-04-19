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
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
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
        // Reset all positions and velocities for all players
        instance.leftPlayer1.transform.position = instance.leftPlayer1Origin;
        instance.leftPlayer2.transform.position = instance.leftPlayer2Origin;
        instance.rightPlayer1.transform.position = instance.rightPlayer1Origin;
        instance.rightPlayer2.transform.position = instance.rightPlayer2Origin;

        instance.leftPlayer1.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        instance.leftPlayer2.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        instance.rightPlayer1.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        instance.rightPlayer2.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        BallManager.Instance.gameObject.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;

        // Set server's and ball's position
        if (instance.leftAttack)
        {
            instance.server.transform.position = instance.leftServeLocation;
            BallManager.Instance.gameObject.transform.position = instance.leftServeLocation + new Vector3(1, 0, 0);
        }
        else
        {
            instance.server.transform.position = instance.rightServeLocation;
            BallManager.Instance.gameObject.transform.position = instance.rightServeLocation - new Vector3(1, 0, 0);
        }

        // Disable gravity for the ball
        BallManager.Instance.gameObject.GetComponent<Rigidbody>().useGravity = false;

        // Reset the game manager fields
        instance.gameState = GameState.PointStart;
        instance.lastHit = null;
    }
}
