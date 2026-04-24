using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public class BallInteract : MonoBehaviour
{
    [Header("Game Manager")]
    public bool onLeft; // Whether the player is on the left court

    [Header("Ball Interaction")]
    public float interactionRadius = 5f; // How far the ball can be from the player to interact with it

    [Header("Animation")]
    public Animator animator; // animator for player

    [Header("Spike Stat")]
    public float spikeStat; //Spiking power for the bird
    
    [Header("Bird Selection")]
    [SerializeField] private BirdType birdType = BirdType.SEAGULL; // Type of the bird for audio noises; default to penguin
    public int playerID;

    // Change the birdType from other managers
    public void SetBirdType(BirdType type) { birdType = type; }

    // EJ: need read-only access to the current bird type for other scripts
    public BirdType GetBirdType() => birdType;
    private Transform contactPoint; // Reference for interaction radius
    private Rigidbody ballRb; // Rigid body for the ball
    private Vector3 bumpToLocation; // Where the ball will go after bumping
    private Vector3 setToLocation; // Where the ball will go after setting
    private Vector3 spikeToLocation; // Where the ball will go after spiking
    private Vector3 serveToLocation; // Where the ball will go after spiking
    private Vector3 blockToLocation; // Where the ball will go after blocking
    private CharacterMovement serverMovement; //Christofort: Track the server's movement from character movement script
    private float baseSpikeSpeed; // Speed of the ball when spiked
    private float baseHeight = 0f; // How high off the ground the player is for bumping check
    private PlayerInput playerInput; // Input for this specific player
    private bool blockTouch = false; // If the player block touched the ball already

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        serverMovement = GetComponent<CharacterMovement>(); // christofort: gets the character movement script
        playerInput = GetComponent<PlayerInput>();
        baseSpikeSpeed = 10.0f;
        
        ballRb = BallManager.Instance.gameObject.GetComponent<Rigidbody>();
        if (ballRb == null)
        {
            Debug.LogError("Rigidbody for the ball was not found in BallInteract!");
        }

        // locate contact point child safely
        var cpTransform = transform.Find("ContactPoint");
        if (cpTransform != null)
        {
            contactPoint = cpTransform;
        }
        else
        {
            Debug.LogErrorFormat("Could not find contact point for {0}. Using root transform instead.", transform.name);
            contactPoint = transform; // fallback to avoid null reference
        }

        // animator fallback (same pattern as AIBehavior)
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            if (animator == null)
            {
                animator = GetComponentInParent<Animator>();
            }
        }

    }

    // If the player is near the ball
    public bool IsPlayerNearBall()
    {
        if (contactPoint == null)
        {
            Debug.LogWarning("ContactPoint missing in BallInteract");
            return false;
        }
        
        float distance = Vector3.Distance(contactPoint.position, BallManager.Instance.gameObject.transform.position);
        return distance <= interactionRadius;
    }

    private bool IsPlayerNearNet() 
    {
        return Mathf.Abs(transform.position.x) < 1.5f;
    }

    // Update is called once per frame
    void Update()
    {
        // Get the height of bird whilst standing on the ground
        if (baseHeight == 0f && serverMovement.grounded) baseHeight = transform.position.y;

        // Keep ball completely still before serve
        if (GameManager.Instance.gameState == GameManager.GameState.PointStart && ballRb != null)
        {
            ballRb.linearVelocity = Vector3.zero;
            ballRb.useGravity = false;
        }

        CheckState();
    }

    // Check the game state in relation to the player
    private void CheckState()
    {
        // If the player can hit the ball
        if (CanHit())
        {
            // Check the game state
            switch (GameManager.Instance.gameState)
            {
                // Ball has just been spiked or blocked
                case GameManager.GameState.Spiked: case GameManager.GameState.Blocked:
                    // EJ: Since ball can't be blocked on the serve this check can't be related to "Served"
                    // EJ: Moved "Served" to a check by itself and check to bump twice                
                    if (!blockTouch && IsPlayerNearBall() && IsPlayerNearNet() && playerInput.actions.FindAction("Block").WasPressedThisFrame())
                    {
                        BlockBall();
                    }
                    
                    // If the player is close enough to the ball and is pressing the bump button, bump the ball
                    else if (IsPlayerNearBall() && playerInput.actions.FindAction("Defensive Action").WasPressedThisFrame())
                    {
                        BumpBall();
                    }
                    break;

                case GameManager.GameState.Served:
                    // If the player is close enough to the ball and is pressing the bump button, bump the ball
                    if (IsPlayerNearBall() && playerInput.actions.FindAction("Defensive Action").WasPressedThisFrame())
                    {
                        BumpBall();
                    }
                    break;
                // Ball has just been bumped
                case GameManager.GameState.Bumped:
                    // If the player is close enough to the ball and is pressing the set button, set the ball
                    if (IsPlayerNearBall() && playerInput.actions.FindAction("Defensive Action").WasPressedThisFrame())
                    {
                        SetBall();
                    }
                    break;
                // Ball has just been set
                case GameManager.GameState.Set:
                    // If the player is close enough to the ball and is pressing the spike button, spike the ball
                    if (IsPlayerNearBall() && playerInput.actions.FindAction("Offensive Action").WasPressedThisFrame())
                    {
                        SpikeBall();
                    }
                    break;
                // Ball is ready to be served
                case GameManager.GameState.PointStart:
                    // Reset block touch
                    if (blockTouch) blockTouch = false;
                    
                    // Christofort: checks if the player is the server then stops them from moving
                    if (GameManager.Instance.server == gameObject)
                    {
                        serverMovement.controlMovement(false,true);
                        // Force player to face forward toward the net, accounting for rotation offset
                        serverMovement.overrideRotation = true;
                        Vector3 forwardDir = onLeft ? Vector3.right : Vector3.left;
                        Quaternion baseRotation = Quaternion.LookRotation(forwardDir);
                        if (serverMovement.rotationOffsetEuler != Vector3.zero)
                        {
                            baseRotation *= Quaternion.Euler(serverMovement.rotationOffsetEuler);
                        }
                        serverMovement.targetRotation = baseRotation;
                    }
                    else
                    {
                        serverMovement.controlMovement(true, true);
                    }
                    // If this player is the one serving and they press the serve button, serve the ball
                    if (GameManager.Instance.server == gameObject && playerInput.actions.FindAction("Offensive Action").WasPressedThisFrame())
                    {
                        ServeBall();
                    }
                    break;
            }
        }

        // Check if the player is trying to pause or resume the game
        if (!PauseMenu.Instance.GameIsPaused && playerInput.actions.FindAction("Pause").WasPressedThisFrame())
        {
            PauseMenu.Instance.pausedPlayerID = playerID;
            PauseMenu.Instance.inputModule.actionsAsset = playerInput.actions;
            PauseMenu.Instance.Pause();
        }
        else if (PauseMenu.Instance.GameIsPaused && PauseMenu.Instance.pausedPlayerID == playerID
            && playerInput.actions.FindAction("Pause").WasPressedThisFrame())
        {
            PauseMenu.Instance.Resume();
        }
    }

    // Check if the player can hit the ball
    private bool CanHit()
    {
        GameManager gameManager = GameManager.Instance;
        // If the point has ended, they cannot hit the ball
        if (gameManager.gameState.Equals(GameManager.GameState.PointEnd)) return false;
        
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
    public void BumpBall()
    {
        // Check if the player is too far off the ground to bump it
        float groundDist = transform.position.y - baseHeight;
        if (groundDist > 1.0f) return;

        // Set bump to location to front middle of whatever side of the court is bumping
        bumpToLocation = new Vector3(2f, 0f, 0f);
        if (onLeft)
        {
            bumpToLocation *= -1;
        }
        blockTouch = false;

        // The ball will be bumped a minimum of five units
        float height = MathF.Max(5.0f, ballRb.transform.position.y + 3.0f);
        
        // Set the ball's intial velocity and destination and null out the unblockable owner
        SetBallInitVelocity(ballRb, bumpToLocation, height);
        BallManager.Instance.goingTo = bumpToLocation;
        BallManager.Instance.offCourse = false;
        if (BallManager.Instance.unblockableOwner != null) BallManager.Instance.unblockableOwner = null;

        // Play the bump sound for the bird
        AudioManager.PlayBirdSound(birdType, SoundType.BUMP, 1.0f);
        AudioManager.PlayBallPlayerInteractionSound();

        // handle vfx
        HitEffects.Instance.PlayEffect(HitEffects.HitType.BumpSetServe, onLeft);

        // Update game manager fields
        GameManager.Instance.gameState = GameManager.GameState.Bumped;
        GameManager.Instance.lastHit = gameObject;
        GameManager.Instance.leftAttack = onLeft;
        // trigger animation
        if (animator != null)
        {
            animator.SetTrigger("Bump");
        }
    }

    // Set the ball
    public void SetBall()
    {
        // If the ball's velocity is not coming down, then you can't set the ball
        if (BallManager.Instance.gameObject.GetComponent<Rigidbody>().linearVelocity.y >= 0) return;
        
        // Set the setting location to middle of court as default
        setToLocation = new Vector3(2f, 0f, 0f);
        if (onLeft)
        {
            setToLocation *= -1;
        }
        blockTouch = false;

        // Get the direction value
        Vector2 dir = playerInput.actions.FindAction("Direction").ReadValue<Vector2>();
        Debug.LogFormat("ServeToLocation before checking direction: {0}", setToLocation);

        // If player wants to set towards top or bottom, update set to location
        if (dir.y < -0.64f)
        {
            setToLocation -= new Vector3(0, 0, 4); // Lower side of the court
        }
        else if (dir.y > 0.64f)
        {
            setToLocation += new Vector3(0, 0, 4); // Upper side of the court
        }
        Debug.LogFormat("ServeToLocation after checking direction: {0}", setToLocation);

        // The ball will be set a minimum of five units
        float height = MathF.Max(5.0f, ballRb.transform.position.y + 3.0f);

        // Set the ball's initial velocity and destination
        SetBallInitVelocity(ballRb, setToLocation, height);
        BallManager.Instance.goingTo = setToLocation;
        BallManager.Instance.offCourse = false;

        // Play the set sound for the bird
        AudioManager.PlayBirdSound(birdType, SoundType.SET, 1.0f);
        AudioManager.PlayBallPlayerInteractionSound();

        // handle vfx
        HitEffects.Instance.PlayEffect(HitEffects.HitType.BumpSetServe, onLeft);

        // Update game manager fields
        GameManager.Instance.gameState = GameManager.GameState.Set;
        GameManager.Instance.lastHit = gameObject;
        if (animator != null)
        {
            animator.SetTrigger("Set");
        }
    }

    // Spike the ball
    public void SpikeBall()
    {
        // Set the spiking location to middle-back of court on the rightside as default
        spikeToLocation = new Vector3(8, 0, 0);

        // If rightside is spiking, switch to spike towards leftside
        if (!onLeft)
        {
            spikeToLocation *= -1;
        }
        blockTouch = false;

        // Get the direction value
        Vector2 dir = playerInput.actions.FindAction("Direction").ReadValue<Vector2>();

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
        BallManager.Instance.addSpikeSpeed(); // ducky: Add additional spike speed to ball velocity
        BallManager.Instance.incSpikeSpeed(); // ducky: Increment additional spike speed after ball velocity has been increased
        BallManager.Instance.goingTo = spikeToLocation;
        BallManager.Instance.offCourse = false;

        // Play the spike sound for the bird
        AudioManager.PlayBirdSound(birdType, SoundType.SPIKE, 1.0f);
        AudioManager.PlayBallPlayerInteractionSound();

        // handle vfx
        HitEffects.Instance.PlayEffect(HitEffects.HitType.Spike, onLeft);

        // Update game manager fields
        GameManager.Instance.gameState = GameManager.GameState.Spiked;
        GameManager.Instance.lastHit = gameObject;
        if (animator != null)
        {
            animator.SetTrigger("Spike");
        }
    }

    public void ServeBall()
    {
        // Set the serving location to middle-back of court on the rightside as default
        serveToLocation = new Vector3(8, 0, 0);

        // If rightside is spiking, switch to serve towards leftside
        if (!onLeft)
        {
            serveToLocation *= -1;
        }
        blockTouch = false;

        // Get the direction value
        Vector2 dir = playerInput.actions.FindAction("Direction").ReadValue<Vector2>();

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
        BallManager.Instance.goingTo = serveToLocation;
        BallManager.Instance.offCourse = false;

        // Play serve sound
        AudioManager.PlayBallPlayerInteractionSound();

        // handle vfx
        HitEffects.Instance.PlayEffect(HitEffects.HitType.BumpSetServe, onLeft);
        

        // Update game manager fields
        GameManager.Instance.gameState = GameManager.GameState.Served;
        GameManager.Instance.lastHit = gameObject;
        GameManager.Instance.leftAttack = onLeft;
        serverMovement.controlMovement(true,true); // christofort: let the server move after gameState updates
        serverMovement.overrideRotation = false; // allow normal rotation after serve
        if (animator != null)
        {
            animator.SetTrigger("Spike");
        }
    }

    public void BlockBall()
        // Play the block sound for the bird
    {
        AudioManager.PlayBirdSound(birdType, SoundType.BLOCK, 1.0f);
        AudioManager.PlayBallPlayerInteractionSound();

        if (animator != null)
        {
            animator.SetTrigger("Block");
        }

        // handle vfx
        HitEffects.Instance.PlayEffect(HitEffects.HitType.Block, onLeft);
        
        // If the incoming spike is marked unblockable, only allow block
        // when the spike was NOT from the unblockable owner.
        if (BallManager.Instance.unblockableOwner != null)
        {
            // If the last spiker matches the unblockable owner, prevent blocking
            if (GameManager.Instance.lastHit == BallManager.Instance.unblockableOwner)
            {
                Debug.Log("Block attempted but spike is unblockable.");
                return;
            }
            // Otherwise fall through and allow the block
        }

        // Determine where the blocked ball will go depending on whether it was a good block or not
        bool blocked = GetBlockToLocation();

        // Detereming height of ball depending on whether or not the ball was blocked
        float height = blocked ? -1.0f  : 8.0f;

        // Set ball stuff
        SetBallInitVelocity(ballRb, blockToLocation, height);
        BallManager.Instance.goingTo = blockToLocation;
        BallManager.Instance.offCourse = false;

        // Update game state if full block
        if (blocked) 
        {
            GameManager.Instance.gameState = GameManager.GameState.Blocked;
            GameManager.Instance.lastHit = gameObject;
            GameManager.Instance.leftAttack = onLeft;
        }
        if (animator != null)
        {
            animator.SetTrigger("Block");
        }
    }

    private bool GetBlockToLocation()
    {
        // If the ball is close to the player when they try to block, do a successful block
        float blockDist = Vector3.Distance(contactPoint.transform.position + transform.forward, BallManager.Instance.gameObject.transform.position);
        if (blockDist < interactionRadius / 2)
        {
            // sends ball back to attacker's side near the net
            blockToLocation = new Vector3(-6f, 0f, 0f);

            if (onLeft) blockToLocation *= -1;

            // directional control
            Vector2 dir = playerInput.actions.FindAction("Direction").ReadValue<Vector2>();

            if (dir.y < -0.64f) blockToLocation.z -= 3f;
            else if (dir.y > 0.64f) blockToLocation.z += 3f;

            // Return a successful block
            blockTouch = false;
            return true;
        }
        else // Else, blocked far from player, will be a block touch
        {
            // Set block to location to some fraction of a distance to where it was supposed to be spiked
            blockToLocation = BallManager.Instance.goingTo * 0.5f;

            // Return a block touch
            blockTouch = true;
            return false;
        }
    }

    // Setting the ball's velocity when interacting with it
    private void SetBallInitVelocity(Rigidbody ballRb, Vector3 endLocation, float maxHeight)
    {
        // Bumping, setting, serving, or block touch
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

            // Set the ball's initial velocity
            ballRb.linearVelocity = new Vector3(vx, vyInit, vz);
        }
        else // Spiking or full blocking
        {
            // If gravity is enabled, disable it
            if (ballRb.useGravity)
            {
                ballRb.useGravity = false;
            }

            // Calculate the direction the ball will go in
            Vector3 initVel = endLocation - ballRb.transform.position;

            // Set speed of initial velocity
            initVel.Normalize();

            // If blocking, want half of the spike speed stuff (game state has not changed yet)
            if (GameManager.Instance.gameState.Equals(GameManager.GameState.Spiked))
            {
                // Reduce block force
                initVel *= baseSpikeSpeed * (1.0f + spikeStat * 0.1f) * 0.5f;
            }
            else
            {
                initVel *= baseSpikeSpeed * (1.0f + spikeStat * 0.1f);
            }

            // Set the ball's initial velocity
            ballRb.linearVelocity = initVel;
        }
    }
}
