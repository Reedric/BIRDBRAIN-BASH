using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class BallInteract : MonoBehaviour
{
    [Header("Game Manager")]
    public GameManager gameManager; // Manager of the game
    public bool onLeft; // Whether the player is on the left court

    [Header("Ball Manager")]
    public BallManager ballManager; // Manager of the ball
    public float interactionRadius = 5f; // How far the ball can be from the player to interact with it
    
    [SerializeField] private BirdType birdType; // The type of bird the character is
    private GameObject ball; // Game object for the ball
    private Rigidbody ballRb; // Rigid body for the ball
    private Vector3 bumpToLocation; // Where the ball will go after bumping
    private Vector3 setToLocation; // Where the ball will go after setting
    private Vector3 spikeToLocation; // Where the ball will go after spiking
    private Vector3 serveToLocation; // Where the ball will go after spiking
    private float baseSpikeSpeed; // Speed of the ball when spiked

    [Header("Spike Stat")]
    public float spikeStat; //Spiking power for the bird

    private Vector3 blockToLocation; // Where the ball will go after blocking
    private CharacterMovement serverMovement; //Christofort: Track the server's movement from character movement script

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        serverMovement = GetComponent<CharacterMovement>(); // christofort: gets the character movement script
        baseSpikeSpeed = 1f;
        
        ball = GameObject.FindGameObjectWithTag("Ball");
        if (ball != null)
        {
            ballRb = ball.GetComponent<Rigidbody>();
            if (ballRb == null)
            {
                Debug.LogError("Rigidbody for the ball was not found in BallInteract!");
            }
        }
        else
        {
            Debug.LogError("Ball game object was not found in BallInteract!");
        }

        if (ballManager == null)
        {
            Debug.LogError("Ball Manager was not set in inspector for BallInteract!");
        }
    }

    // If the player is near the ball
    private bool IsPlayerNearBall()
    {
        if (ball == null) return false;
        
        float distance = Vector3.Distance(transform.position, ball.transform.position);
        return distance <= interactionRadius;
    }

    private bool IsPlayerNearNet() 
    {
        return Mathf.Abs(ball.transform.position.x) < 1.5f;
    }

    // Update is called once per frame
    void Update()
    {
        CheckState();
    }

    // Check the game state in relation to the player
    private void CheckState()
    {
        // If the player can hit the ball
        if (CanHit())
        {
            // Check the game state
            switch (gameManager.gameState)
            {
                // Ball has just been spiked or served
                case GameManager.GameState.Spiked:
                    // EJ: Since ball can't be blocked on the serve this check can't be related to "Served"
                    // EJ: Moved "Served" to a check by itself and check to bump twice                
                    if (IsPlayerNearBall() && IsPlayerNearNet() && InputSystem.actions.FindAction("Block").WasPressedThisFrame())
                    {
                        BlockBall();
                    }
                    
                    // If the player is close enough to the ball and is pressing the bump button, bump the ball
                    else if (IsPlayerNearBall() && InputSystem.actions.FindAction("Bump").WasPressedThisFrame())
                    {
                        BumpBall();
                    }
                    break;

                case GameManager.GameState.Served: case GameManager.GameState.Blocked:
                    // If the player is close enough to the ball and is pressing the bump button, bump the ball
                    if (IsPlayerNearBall() && InputSystem.actions.FindAction("Bump").WasPressedThisFrame())
                    {
                        BumpBall();
                    }
                    break;
                // Ball has just been bumped
                case GameManager.GameState.Bumped:
                    // If the player is close enough to the ball and is pressing the set button, set the ball
                    if (IsPlayerNearBall() && InputSystem.actions.FindAction("Set").WasPressedThisFrame())
                    {
                        SetBall();
                    }
                    break;
                // Ball has just been set
                case GameManager.GameState.Set:
                    // If the player is close enough to the ball and is pressing the spike button, spike the ball
                    if (IsPlayerNearBall() && InputSystem.actions.FindAction("Spike").WasPressedThisFrame())
                    {
                        SpikeBall();
                    }
                    break;
                // Ball is ready to be served
                case GameManager.GameState.PointStart:
                    // Christofort: checks if the player is the server then stops them from moving
                    if (gameManager.server == gameObject)
                    {
                        serverMovement.controlMovement(false,true);
                    }
                    // If this player is the one serving and they press the serve button, serve the ball
                    if (gameManager.server == gameObject && InputSystem.actions.FindAction("Serve").WasPressedThisFrame())
                    {
                        ServeBall();
                    }
                    break;
            }
        }
    }

    // Check if the player can hit the ball
    private bool CanHit()
    {
        // If this player just hit the ball, they cannot hit it again
        if (gameObject.Equals(gameManager.lastHit)) return false;

        // If the ball has been served by the other team, they can hit
        if (!gameManager.leftAttack.Equals(onLeft) && gameManager.gameState.Equals(GameManager.GameState.Served)) return true;

        // If it's the player's turn to serve, they can hit
        if (gameManager.leftAttack.Equals(onLeft) && gameManager.gameState.Equals(GameManager.GameState.PointStart)
            && gameManager.server == gameObject) return true;

        // If the ball has been served by a teammate, they cannot hit it
        if (gameManager.leftAttack.Equals(onLeft) && gameManager.gameState.Equals(GameManager.GameState.Served)) return false;

        // If the ball is on this side of the court and it has not been spiked yet, they can hit it
        if (gameManager.leftAttack.Equals(onLeft) && !gameManager.gameState.Equals(GameManager.GameState.Spiked)) return true;

        // If the ball is on the other side of the court and has been spiked, they can hit, else they cannot
        return !gameManager.leftAttack.Equals(onLeft) && gameManager.gameState.Equals(GameManager.GameState.Spiked);
    }

    // Bump the ball
    private void BumpBall()
    {
        // Set bump to location to front middle of whatever side of the court is bumping
        bumpToLocation = new Vector3(2f, 0f, 0f);
        if (onLeft)
        {
            bumpToLocation *= -1;
        }
        
        // Set the ball's intial velocity and destination
        SetBallInitVelocity(ballRb, bumpToLocation, 5.0f);
        ballManager.goingTo = bumpToLocation;

        // Play bump sound
        AudioManager.PlayBirdSound(birdType, SoundType.BUMP, 1.0f);

        // Update game manager fields
        gameManager.gameState = GameManager.GameState.Bumped;
        gameManager.lastHit = gameObject;
        gameManager.leftAttack = onLeft;
    }

    // Set the ball
    private void SetBall()
    {
        // Set the setting location to middle of court as default
        setToLocation = bumpToLocation;

        // Get the direction value
        Vector2 dir = InputSystem.actions.FindAction("Direction").ReadValue<Vector2>();

        // If player wants to set towards top or bottom, update set to location
        if (dir.y < -0.64f)
        {
            setToLocation -= new Vector3(0, 0, 4); // Lower side of the court
        }
        else if (dir.y > 0.64f)
        {
            setToLocation += new Vector3(0, 0, 4); // Upper side of the court
        }

        // Set the ball's initial velocity and destination
        SetBallInitVelocity(ballRb, setToLocation, 5.0f);
        ballManager.goingTo = setToLocation;

        // Play set sound
        AudioManager.PlayBirdSound(birdType, SoundType.SET, 1.0f);

        // Update game manager fields
        gameManager.gameState = GameManager.GameState.Set;
        gameManager.lastHit = gameObject;
    }

    // Spike the ball
    private void SpikeBall()
    {
        // Set the spiking location to middle-back of court on the rightside as default
        spikeToLocation = new Vector3(8, 0, 0);

        // If rightside is spiking, switch to spike towards leftside
        if (!onLeft)
        {
            spikeToLocation *= -1;
        }

        // Get the direction value
        Vector2 dir = InputSystem.actions.FindAction("Direction").ReadValue<Vector2>();

        // If player wants to spike towards top or bottom, update set to location
        if (dir.y < -0.64f)
        {
            spikeToLocation.z -= 4; // Lower side of the court
        }
        else if (dir.y > 0.64f)
        {
            spikeToLocation.z += 4; // Upper side of the court
        }

        // Set the ball's initial velocity and destination
        SetBallInitVelocity(ballRb, spikeToLocation, -1.0f);
        ballManager.goingTo = spikeToLocation;

        // Play spike sound
        AudioManager.PlayBirdSound(birdType, SoundType.SPIKE, 1.0f);

        // Update game manager fields
        gameManager.gameState = GameManager.GameState.Spiked;
        gameManager.lastHit = gameObject;
    }

    private void ServeBall()
    {
        // Set the serving location to middle-back of court on the rightside as default
        serveToLocation = new Vector3(8, 0, 0);

        // If rightside is spiking, switch to serve towards leftside
        if (!onLeft)
        {
            serveToLocation *= -1;
        }

        // Get the direction value
        Vector2 dir = InputSystem.actions.FindAction("Direction").ReadValue<Vector2>();

        // If player wants to set towards top or bottom, update set to location
        if (dir.y < -0.64f)
        {
            serveToLocation -= new Vector3(0, 0, 4); // Lower side of the court
        }
        else if (dir.y > 0.64f)
        {
            serveToLocation += new Vector3(0, 0, 4); // Upper side of the court
        }

        // Set the ball's initial velocity and destination
        SetBallInitVelocity(ballRb, serveToLocation, 6.0f);
        ballManager.goingTo = serveToLocation;

        // Update game manager fields
        gameManager.gameState = GameManager.GameState.Served;
        gameManager.lastHit = gameObject;
        gameManager.leftAttack = onLeft;
        serverMovement.controlMovement(true,true); // christofort: let the server move after gameState updates
    }

    private void BlockBall()
    {
        // sends ball back to attacker's side near the net
        blockToLocation = new Vector3(6f, 0f, 0f);

        if (!onLeft) blockToLocation *= -1;

        // directional control
        Vector2 dir = InputSystem.actions.FindAction("Direction").ReadValue<Vector2>();

        if (dir.y < -0.64f) blockToLocation.z -= 3f;
        else if (dir.y > 0.64f) blockToLocation.z += 3f;

        // want fast and flat arc
        SetBallInitVelocity(ballRb, blockToLocation, -1.0f);
        ballManager.goingTo = blockToLocation;

        // Update game state
        gameManager.gameState = GameManager.GameState.Blocked;
        gameManager.lastHit = gameObject;
        gameManager.leftAttack = onLeft;
    }

    // Setting the ball's velocity when interacting with it
    private void SetBallInitVelocity(Rigidbody ballRb, Vector3 endLocation, float maxHeight)
    {
        // Bumping, setting, or serving
        if (maxHeight > ballRb.transform.position.y)
        {
            // If gravity is disabled, enable it
            if (!ballRb.useGravity)
            {
                ballRb.useGravity = true;
            }

            // Calculate the velocity in the y direction for the ball to reach a height of 5 given its current y component
            float gravity = MathF.Abs(Physics.gravity.y);
            float vyInit = MathF.Sqrt(2 * gravity * (maxHeight - ballRb.transform.position.y));

            // Calculate time the ball will be in the air
            float vyFinal = MathF.Sqrt(10 * gravity);
            float t1 = vyInit / gravity;
            float t2 = vyFinal / gravity;
            float t = t1 + t2; 

            // Calculate the x and z velocities of the ball
            float vx = (endLocation.x - ballRb.transform.position.x) / t;
            float vz = (endLocation.z - ballRb.transform.position.z) / t;

            // Set the ball's intial velocity
            ballRb.linearVelocity = new Vector3(vx, vyInit, vz);
        }
        else // Spiking or blocking
        {
            // If gravity is enabled, disable it
            if (ballRb.useGravity)
            {
                ballRb.useGravity = false;
            }

            // Calculate the direction the ball will go in
            Vector3 initVel = endLocation - ballRb.transform.position;

            // Set speed of inital velocity
            initVel.Normalize();
            initVel *= baseSpikeSpeed + (spikeStat * 0.1);

            // Set the ball's intial velocity
            ballRb.linearVelocity = initVel;
        }
    }
}
