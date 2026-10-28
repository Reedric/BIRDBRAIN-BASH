using Unity.Netcode;
using UnityEngine;

public class BallManager : NetworkBehaviour
{
    private Vector3 goingToLocal; // Where the ball is going to (local multiplayer)
    private NetworkVariable<Vector3> goingToNet; // Where the ball is going to (online multplayer)
    public Vector3 goingTo // Where the ball is going to (API version)
    {
        get => GameManager.Instance.isLocal ? goingToLocal : goingToNet.Value;
        set
        {
            if (GameManager.Instance.isLocal) goingToLocal = value;
            else goingToNet.Value = value;
        }
    }

    private GameObject unblockableOwnerLocal; // If set, this player's spike cannot be blocked (local multiplayer)
    private NetworkVariable<ulong> unblockableOwnerNet; // If set, this player's spike cannot be blocked (online multiplayer)
    public GameObject unblockableOwner // If set, this player's spike cannot be blocked (API version)
    {
        get
        {
            if (GameManager.Instance.isLocal) return unblockableOwnerLocal;
            else if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(unblockableOwnerNet.Value, out var netObj)) return netObj.gameObject;
            return null;
        }
        set
        {
            if (GameManager.Instance.isLocal) unblockableOwnerLocal = value;
            else unblockableOwnerNet.Value = value.GetComponent<NetworkObject>().NetworkObjectId;;
        }
    }

    private bool offCourseLocal; // Boolean for whether the ball is off course of where it is supposed to go (local multiplayer)
    private NetworkVariable<bool> offCourseNet; // Boolean for whether the ball is off course of where it is supposed to go (online multplayer)
    public bool offCourse // Boolean for whether the ball is off course of where it is supposed to go (API version)
    {
        get => GameManager.Instance.isLocal ? offCourseLocal : offCourseNet.Value;
        set
        {
            if (GameManager.Instance.isLocal) offCourseLocal = value;
            else offCourseNet.Value = value;
        }
    }

    private Rigidbody rb; // Rigidbody of the ball
    public System.Action<Collision> onBallCollision; // Christofort: Event for when the ball collides with something
    private float addedSpikeSpeed; // ducky: Additional spike speed to speed up the ball
    
    private float xSign; // ducky: Sign of x-component of ball velocity
    private float zSign; // ducky: Sign of z-component of ball velocity (no y b/c y should always be negative)

    private static BallManager instance; // Private instance of the GameManager that other classes cannot reference
    public static BallManager Instance => instance; // Public instance of GameManager that other classes can reference


    void Awake()
    {
        // Initialize singleton to this script
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // Initialize all network variables if it is a online multiplayer match
        if (!GameManager.Instance.isLocal)
        {
            goingToNet = new NetworkVariable<Vector3>();
            unblockableOwnerNet = new NetworkVariable<ulong>();
            offCourseNet = new NetworkVariable<bool>();
        }
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Get the ball's rigidbody
        rb = GetComponent<Rigidbody>();

        // Set off course to false
        offCourse = false;
    }

    void FixedUpdate()
    {
        // Freeze ball during serve setup
        if (GameManager.Instance.gameState == GameManager.GameState.PointStart)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // Calls whenever the character collides with another collider or rigidbody
    void OnCollisionEnter(Collision other)
    {
        onBallCollision?.Invoke(other);
        // Activate gravity it's not already activated and the game state is NOT serving
        if (!rb.useGravity && !GameManager.Instance.gameState.Equals(GameManager.GameState.PointStart))
        {
            rb.useGravity = true;
        }

        // Set flag to true if ball collided with something other than ground
        if (!(other.collider.tag.Equals("Side1") || other.collider.tag.Equals("Side2") || other.collider.tag.Equals("Out")))
        {
            offCourse = true;
        }

        if (other.gameObject.CompareTag("Player"))
        {
            ScoreManager.Instance.lastPhysicalTouch = other.gameObject;
        }

        // Clear unblockable owner on any collision (spike has reached a target)
        if (unblockableOwner != null)
        {
            unblockableOwner = null;
            // Debug.Log("BallManager: cleared unblockable spike owner after collision.");
        }
    }

    // ducky: Add additional spike speed to base speed of ball
    public void addSpikeSpeed()
    {
        // Debug.Log("base speed " + rb.linearVelocity);
        xSign = (rb.linearVelocity.x < 0) ? -1 : 1;
        zSign = (rb.linearVelocity.z < 0) ? -1 : 1;
        rb.linearVelocity += new Vector3(addedSpikeSpeed * xSign, -addedSpikeSpeed, addedSpikeSpeed * zSign);
        // Debug.Log("Added speed to base speed " + rb.linearVelocity);
    }

    // ducky: Increment the additional spike speed by 0.2
    public void incSpikeSpeed()
    {
        if (addedSpikeSpeed < 5.0f)
        {
            addedSpikeSpeed += 0.2f;
        }
        // Debug.Log("increased spike speed " + addedSpikeSpeed);
    }

    // ducky: Reset additional spike speed to 0
    public void resetSpikeSpeed()
    {
        addedSpikeSpeed = 0.0f;
        // Debug.Log("reset spike speed");
    }
}
