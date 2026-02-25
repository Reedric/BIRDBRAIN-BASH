using UnityEngine;
using System;

public class AIBehavior : MonoBehaviour
{
    [Header("Game Manager")]
    public GameManager gameManager; // Script that manages the game logic
    public bool onLeft; // Whether this AI is on the left side of the net or not

    [Header("Ball Manager")]
    public BallManager ballManager; // Script that manages ball collisions
    public float interactionRadius = 5f; // How far the character can interact with the ball

    [Header("Movement Attributes")]
    public float maxGroundSpeed = 1.0f; // Max speed that the character can move on the ground
    public float maxAirSpeed = 1.0f; // Max speed that the character can move in the air
    public float jumpForce = 1.0f; // Force the character uses to jump 
    public float rotationSpeed = 10.0f; // How fast the AI rotates to movement direction
    private float directionChangeWeight = 15f; // How quickly the character can change direction
    private bool grounded = false; // If the character is touching the ground
    private GameObject ball; // The ball in the game
    private Rigidbody ballRb; // The rigidbody of the ball
    private Vector3 bumpToLocation; // Where the ball will go after bumping
    private Vector3 setToLocation; // Where the ball will go after setting
    private Vector3 spikeToLocation; // Where the ball will go after spiking
    private Vector3 serveToLocation; // Where the ball will go after serving
    private float spikeSpeed; // Speed of the ball when spiked
    private float timeTilServe; // Time remaining until AI can serve
    private ParticleSystem dustParticles; // Particle system for ground dust
    [HideInInspector] public bool overrideRotation = false; // Allow external override of rotation
    [HideInInspector] public Quaternion targetRotation; // Target rotation when not overridden

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Get the ball, if its not null, get its rigidbody
        ball = GameObject.FindGameObjectWithTag("Ball");
        if (ball != null)
        {
            ballRb = ball.GetComponent<Rigidbody>();

            // If the rigidbody is null, log an error
            if (ballRb == null)
            {
                Debug.LogError("Ball rigidbody was not found for AIBehavior!");
            }
        }
        else // Ball is null, log an error
        {
            Debug.LogError("Ball game object was not found for AIBehavior!");
        }

        // Check to see the ball manager was set in the inspector
        if (ballManager == null)
        {
            Debug.LogError("Ball Manager was not set in inspector for AIBehavior!");
        }

        // Set the spike speed and the amount of time AI takes to serve
        spikeSpeed = 10f;
        timeTilServe = 2f;

        // Initialize target rotation to current rotation
        targetRotation = transform.rotation;

        // Get particle system for ground dust
        dustParticles = GetComponent<ParticleSystem>();
    }

    // Update is called once per frame
    void Update()
    {
        // If the ball and its rigidbody exist, check the AI's state
        if (ball != null && ballRb != null)
        {
            CheckState();
        }
    }

    // Apply rotation smoothly
    void FixedUpdate()
    {
        if (!overrideRotation || Vector3.Distance(transform.eulerAngles, targetRotation.eulerAngles) > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    // Check the current state of the AI
    private void CheckState()
    {
        // Check if they AI can hit the ball
        if (CanHit())
        {
            // Check the game state
            switch (gameManager.gameState)
            {
                // If the ball was just spiked or served
                case GameManager.GameState.Spiked: case GameManager.GameState.Served: case GameManager.GameState.Blocked:
                    // If the AI is near the ball and the ball is on its way down, bump the ball
                    if (IsAINearBall() && ballRb.linearVelocity.y < 0)
                    {
                        BumpBall();
                    }
                    else // Just move the AI to get into a position to hit the ball
                    {
                        MoveAI(true);
                    }
                    break;
                // If the ball was just bumped
                case GameManager.GameState.Bumped:
                    // If the AI is near the ball and the ball is on its way down, bump the ball
                    if (IsAINearBall() && ballRb.linearVelocity.y < 0)
                    {
                        SetBall();
                    }
                    else // Just move the AI to get into a position to hit the ball
                    {
                        MoveAI(true);
                    }
                    break;
                // If the ball was just set
                case GameManager.GameState.Set:
                    // Get the position of the AI and the ball
                    Vector2 aiPos = new Vector2(transform.position.x, transform.position.z);
                    Vector2 ballPos = new Vector2(ballRb.transform.position.x, ballRb.transform.position.z);

                    // If the ball is on its way down
                    if (ballRb.linearVelocity.y < 0)
                    {
                        // If the AI not under the ball, move them to a better position
                        if (Vector2.Distance(aiPos, ballPos) > 1f)
                        {
                            MoveAI(true);
                        }
                        else if (grounded) // Else if the AI is under the ball and grounded, jump
                        {
                            GetComponent<Rigidbody>().linearVelocity += new Vector3(0, jumpForce, 0);
                            grounded = false;
                        }
                        else if (IsAINearBall()) // Else if the AI is under the ball, not grounded, and is close to the ball, spike it
                        {
                            SpikeBall();
                        }
                    }
                    else // Else, the ball is not on its way down
                    {
                        // If the AI is not under the ball, move them to a better position
                        if (Vector2.Distance(aiPos, ballPos) > 1f)
                        {
                            MoveAI(true);
                        }
                    }
                    break;
                // If the point is about to start (ball needs to be served)
                case GameManager.GameState.PointStart:
                    // If this AI is the one serving
                    if (gameManager.server == gameObject)
                    {
                        // If the AI can serve ball, do so. Otherwise, wait.
                        if (timeTilServe < 0f)
                        {
                            ServeBall();
                        }
                        else
                        {
                            timeTilServe -= Time.deltaTime;
                        }
                    }
                    break;
            }
        }
        // Reposition for defense ONLY IF the ball is not about to be served AND the AI cannot hit it
        else if (!gameManager.gameState.Equals(GameManager.GameState.PointStart))
        {
            MoveAI(false);
        }
    }

    // Check if the AI is legally able to hit the ball
    private bool CanHit()
    {
        // If this AI just hit the ball, they cannot hit it again
        if (gameObject.Equals(gameManager.lastHit)) return false;

        // If the ball has been served by the other team, they can hit
        if (!gameManager.leftAttack.Equals(onLeft) && gameManager.gameState.Equals(GameManager.GameState.Served)) return true;

        // If it's the AI's turn to serve, they can hit
        if (gameManager.leftAttack.Equals(onLeft) && gameManager.gameState.Equals(GameManager.GameState.PointStart)
            && gameManager.server == gameObject) return true;

        // If the ball has been served by a teammate, they cannot hit it
        if (gameManager.leftAttack.Equals(onLeft) && gameManager.gameState.Equals(GameManager.GameState.Served)) return false;

        // If the ball is on this side of the court and it has not been spiked yet, they can hit it
        if (gameManager.leftAttack.Equals(onLeft) && !gameManager.gameState.Equals(GameManager.GameState.Spiked)) return true;

        // If the ball is on the other side of the court and has been spiked, they can hit, else they cannot
        return !gameManager.leftAttack.Equals(onLeft) && gameManager.gameState.Equals(GameManager.GameState.Spiked);
    }

    // Check if the AI is near the ball
    private bool IsAINearBall()
    {
        // Get the distance the AI is from the ball, return whether it is less than or equal that interation radius
        float distance = Vector3.Distance(transform.position, ball.transform.position);
        return distance <= interactionRadius;
    }
    
    // Move the AI towards the ball or not
    private void MoveAI(bool towardsBall)
    {
        // Initialize target for AI
        Vector3 target = new Vector3(5, 0, 0);

        // If the AI is on the left side of the court
        if (onLeft)
        {
            // Change target to left side of the court
            target *= -1;
            
            // If the AI is the first player on the left side, push them to the top of the screen
            if (gameManager.leftPlayer1.Equals(gameObject))
            {
                target += new Vector3(0, 0, 2);
            }
            else // Push them to the bottom of the screen
            {
                target -= new Vector3(0, 0, 2);
            }
        }
        else
        {
            // If the AI is the first player on the right side, push them to the the top of the screen
            if (gameManager.rightPlayer1.Equals(gameObject))
            {
                target += new Vector3(0, 0, 2);
            }
            else // Push them to the bottom of the screen
            {
                target -= new Vector3(0, 0, 2);
            }
        }


        // If the AI should move to hit the ball, move them to where the ball is supposed to go
        if (towardsBall)
        {
            target = ballManager.goingTo;
        }

        // Get the AI's rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();

        // Get the direction the AI needs to move in
        float dx = target.x - transform.position.x;
        float dz = target.z - transform.position.z;
        Vector2 dir = new Vector2(dx, dz);

        // If the AI is not already at the target
        if (!dir.Equals(Vector2.zero))
        {
            // Calculate new velocity
            Vector2 newVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z) + dir * Time.fixedDeltaTime * directionChangeWeight;

            // If the AI is grounded and the new velocity exceeds the max ground speed, cap the speed
            if (grounded && newVelocity.magnitude > maxGroundSpeed)
            {
                newVelocity.Normalize();
                newVelocity *= maxGroundSpeed;
            }
            // Else if the AI is in the air and the new velocity exceeds the max air speed, cap the speed
            else if (!grounded && newVelocity.magnitude > maxAirSpeed)
            {
                newVelocity.Normalize();
                newVelocity *= maxAirSpeed;
            }

            // Update target rotation to face the movement direction
            if (!overrideRotation)
            {
                Vector3 movementDirection = new Vector3(newVelocity.x, 0, newVelocity.y);
                if (movementDirection.magnitude > 0.1f)
                {
                    targetRotation = Quaternion.LookRotation(movementDirection);
                }
            }

            // Assign the AI's velocity to the new velocity
            rb.linearVelocity = new Vector3(newVelocity.x, rb.linearVelocity.y, newVelocity.y);
        }
    }

    // Bumping the ball
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

        // Play sounds
        AudioManager.PlayBallPlayerInteractionSound();

        // Update game manager fields
        gameManager.gameState = GameManager.GameState.Bumped;
        gameManager.lastHit = gameObject;
        gameManager.leftAttack = onLeft;
    }

    // Setting the ball
    private void SetBall()
    {        
        // Set the setting location to middle of court as default
        setToLocation = bumpToLocation;

        // Randomly decide to set it elsewhere
        float rand = UnityEngine.Random.Range(0f, 1f);
        if (rand < 0.33f)
        {
            setToLocation += new Vector3(0, 0, 4); // Sets to the upper side of the court
        }
        else if (rand < 0.66f)
        {
            setToLocation -= new Vector3(0, 0, 4); // Sets to the lower side of the court
        }

        // Set the ball's initial velocity and destination
        SetBallInitVelocity(ballRb, setToLocation, 6.0f);
        ballManager.goingTo = setToLocation;

        // Play sounds
        AudioManager.PlayBallPlayerInteractionSound();

        // Update game manager fields
        gameManager.gameState = GameManager.GameState.Set;
        gameManager.lastHit = gameObject;
    }

    // Spiking the ball
    private void SpikeBall()
    {
        // Set the spiking location to middle-back of court on the rightside as default
        spikeToLocation = new Vector3(8, 0, 0);

        // If rightside is spiking, switch to spike towards leftside
        if (!onLeft)
        {
            spikeToLocation *= -1;
        }

        // Randomly decide to set it elsewhere
        float rand = UnityEngine.Random.Range(0f, 1f);
        if (rand < 0.33f)
        {
            spikeToLocation += new Vector3(0, 0, 4); // Spikes to the upper side of the court
        }
        else if (rand < 0.66f)
        {
            spikeToLocation -= new Vector3(0, 0, 4); // Spikes to the lower side of the court
        }

        // Set the ball's initial velocity and destination
        SetBallInitVelocity(ballRb, spikeToLocation, -1.0f);
        ballManager.goingTo = spikeToLocation;

        // Play sounds
        AudioManager.PlayBallPlayerInteractionSound();

        // Update game manager fields
        gameManager.gameState = GameManager.GameState.Spiked;
        gameManager.lastHit = gameObject;
    }

    // Serving the ball
    private void ServeBall()
    {
        // Set the serving location to middle-back of court on the rightside as default
        serveToLocation = new Vector3(8, 0, 0);

        // If rightside is spiking, switch to serve towards leftside
        if (!onLeft)
        {
            serveToLocation *= -1;
        }

        // Randomly decide to set it elsewhere
        float rand = UnityEngine.Random.Range(0f, 1f);
        if (rand < 0.33f)
        {
            serveToLocation += new Vector3(0, 0, 4); // Serves to the upper side of the court
        }
        else if (rand < 0.66f)
        {
            serveToLocation -= new Vector3(0, 0, 4); // Serves to the lower side of the court
        }

        // Set the ball's initial velocity and destination
        SetBallInitVelocity(ballRb, serveToLocation, 5.0f);
        ballManager.goingTo = serveToLocation;

        // Update game manager fields
        gameManager.gameState = GameManager.GameState.Served;
        gameManager.lastHit = gameObject;
        gameManager.leftAttack = onLeft;

        // Play sounds
        AudioManager.PlayBallPlayerInteractionSound();
        
        // Reset timer for serve
        timeTilServe = 2.0f;
    }

    // Setting the ball's velocity after interacting with it
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
            initVel *= spikeSpeed;

            // Set the ball's intial velocity
            ballRb.linearVelocity = initVel;
        }
    }

    // Calls whenever the character collides with another collider or rigidbody
    void OnCollisionEnter(Collision other)
    {
        // If the character collides with the court, it is now grounded
        if (other.gameObject.layer == 6)
        {
            grounded = true;
            // Resume particle emission when landing
            if (dustParticles != null)
            {
                dustParticles.Play();
            }
        }
    }

    // Calls whenever the character stops colliding with another collider or rigidbody
    void OnCollisionExit(Collision other)
    {
        // If the character stops colliding with the court, it is no longer grounded
        if (other.gameObject.layer == 6)
        {
            grounded = false;
            // Stop particle emission when airborne
            if (dustParticles != null)
            {
                dustParticles.Stop();
            }
        }
    }
}
