using Unity.Netcode;
using UnityEngine;

public class BallManager : NetworkBehaviour
{
    private Vector3 goingToLocal; // Where the ball is going to (local multiplayer)
    private NetworkVariable<Vector3> goingToNet = new(); // Where the ball is going to (online multplayer)
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
    private NetworkVariable<ulong> unblockableOwnerNet = new(); // If set, this player's spike cannot be blocked (online multiplayer)
    public GameObject unblockableOwner // If set, this player's spike cannot be blocked (API version)
    {
        get
        {
            return GameManager.Instance.isLocal ? unblockableOwnerLocal : NetworkSession.Resolve(unblockableOwnerNet.Value);
        }
        set
        {
            if (GameManager.Instance.isLocal) unblockableOwnerLocal = value;
            else unblockableOwnerNet.Value = NetworkSession.IdOf(value);
        }
    }

    private bool offCourseLocal; // Boolean for whether the ball is off course of where it is supposed to go (local multiplayer)
    private NetworkVariable<bool> offCourseNet = new(); // Boolean for whether the ball is off course of where it is supposed to go (online multplayer)
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
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Get the ball's rigidbody
        rb = GetComponent<Rigidbody>();

        // Set off course to false. Online, the host owns all of the ball's state
        if (NetworkSession.HasAuthority) offCourse = false;
    }

    void FixedUpdate()
    {
        // Clients only display the ball
        if (!NetworkSession.HasAuthority) return;

        // Freeze ball during serve setup
        if (GameManager.Instance.gameState == GameManager.GameState.PointStart)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        CheckForStuckBall();
    }

    // Calls whenever the character collides with another collider or rigidbody
    void OnCollisionEnter(Collision other)
    {
        if (!NetworkSession.HasAuthority) return;

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

        GameObject bird = BirdLookup.FindBird(other.collider != null ? other.collider.gameObject : other.gameObject);
        if (bird != null)
        {
            HandleBodyTouch(bird);
            ScoreManager.Instance.lastPhysicalTouch = bird;
        }

        // Clear unblockable owner on any collision (spike has reached a target)
        if (unblockableOwner != null)
        {
            unblockableOwner = null;
            // Debug.Log("BallManager: cleared unblockable spike owner after collision.");
        }
    }

    // The ball came over from the other team and bounced off a bird without it pressing anything, like a bird
    // jumping at the net in the way of a spike. If that changed nothing, the ball would stay "spiked" by the other
    // team, who aren't allowed to play their own spike, so if it bounced back to them they couldn't touch it and
    // couldn't win the point. Counting it as a touch by the bird's team (the same as a block touch) means whoever
    // the ball ends up with can play it, and if it lands, the point goes to the right side.
    private void HandleBodyTouch(GameObject bird)
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || !GameManager.PointInProgress()) return;

        GameManager.GameState state = gameManager.gameState;
        if (state != GameManager.GameState.Served
            && state != GameManager.GameState.Spiked
            && state != GameManager.GameState.Blocked)
            return;

        // A bird on the team that sent the ball over (e.g. the server, right as it serves) didn't stop anything
        if (!ScoreManager.TryGetSide(bird, out bool birdOnLeft) || birdOnLeft == gameManager.leftAttack) return;

        gameManager.gameState = GameManager.GameState.Blocked;
        gameManager.leftAttack = birdOnLeft;
        // lastHit is left alone, so the bird that got hit can still bump the ball (out balls go by lastPhysicalTouch)
        goingTo = transform.position;
    }

    // Safety net so a point always finishes. If the ball stops somewhere that isn't the court (balanced on a bird,
    // wedged on the net) or falls out of the world, nothing would ever score it and the match would be stuck.
    private const float stallSeconds = 1.5f;
    private const float fallOutHeight = -5f;
    private float stalledTime;
    private int stallNudges;

    private void CheckForStuckBall()
    {
        if (GameManager.Instance == null || !GameManager.PointInProgress() || ScoreManager.Instance == null)
        {
            stalledTime = 0f;
            stallNudges = 0;
            return;
        }

        if (rb.position.y < fallOutHeight)
        {
            ScoreManager.Instance.HandleBallLost();
            return;
        }

        if (rb.linearVelocity.sqrMagnitude > 0.25f)
        {
            stalledTime = 0f;
            return;
        }

        stalledTime += Time.fixedDeltaTime;
        if (stalledTime < stallSeconds) return;
        stalledTime = 0f;

        // Tried knocking it loose a few times already, so score it as if it went out
        if (++stallNudges > 3)
        {
            ScoreManager.Instance.HandleBallLost();
            return;
        }

        // Knock it loose. Near the net it falls back on the side of the team that last played it (it didn't make it
        // over), otherwise it falls on whichever side it is already on.
        float direction = Mathf.Abs(rb.position.x) < 1f
            ? (GameManager.Instance.leftAttack ? -1f : 1f)
            : Mathf.Sign(rb.position.x);

        rb.useGravity = true;
        rb.linearVelocity = new Vector3(direction * 2f, 2f, 0f);
        offCourse = true;
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
