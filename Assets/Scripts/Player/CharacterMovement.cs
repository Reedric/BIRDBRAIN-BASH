using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class CharacterMovement : NetworkBehaviour
{
    [Header("Character Attributes")]
    public float maxGroundSpeed = 1.0f; // Max speed that the character can move on the ground
    public float maxAirSpeed = 1.0f; // Max speed that the character can move in the air
    public float jumpForce = 1.0f; // Force the character uses to jump
    public float rotationSpeed = 10.0f; // How fast the character rotates to face movement direction

    [Tooltip("Euler offset applied to facing rotation. Use this if the model's forward axis isn't aligned with world +Z.")]
    public Vector3 rotationOffsetEuler = Vector3.zero; // local rotation adjustment for the prefab

    [Header("Animation")]
    public Animator animator; // optional animator for player character
    private bool isWalking = false; // computed each frame for animation

    private float directionChangeWeight = 15f; // How quickly the character can change direction
    private Coroutine buffCoroutine; // Reference to the currently active buff coroutine

    private PlayerInput playerInput; // Input of the player for local multiplayer functionality
    private bool networkedJumpHeld; // If the player is holding jump, set by the input RPC for online multiplayer
    private bool inputInverted; // If this player's online movement input is flipped
    private Rigidbody rb; // Rigid body of the character
    public bool grounded = false; // If the character is touching the ground

    private bool canJump = true; // christofort: defaulted to false, ability scripts must set this to true
    private bool canMove = true; // christofort: defaulted to false, ability scripts must set this to true
    private ParticleSystem dustParticles; // Reference to particle system for ground dust
    private Vector2 movementInput; // Input from networked object

    [HideInInspector] public bool overrideRotation = false; // Allow other scripts to override rotation
    [HideInInspector] public Quaternion targetRotation; // Target rotation when overridden

    [Header("Net Boundary")]
    [Tooltip("Prevents this character from crossing the center net.")]
    [SerializeField] private bool enforceNetBoundary = false;

    [Tooltip("X position of the center of the net.")]
    [SerializeField] private float netXPosition = 0f;

    [Tooltip("Extra space kept between the character collider and the net.")]
    [SerializeField] private float netBuffer = 0.05f;

    // Which side of the net this character started on.
    // -1 = left side, +1 = right side.
    private float netSide;

    // The stats this bird spawned with, so they can be put back at the start of each point
    private float baseMaxGroundSpeed;
    private float baseMaxAirSpeed;
    private float baseJumpForce;

    void Awake()
    {
        baseMaxGroundSpeed = maxGroundSpeed;
        baseMaxAirSpeed = maxAirSpeed;
        baseJumpForce = jumpForce;
    }

    // Abilities put back the speeds they change when they wear off, but when two overlap (a buff ending during a stun,
    // or a bird turned into a chicken mid-boost) one can put back the wrong value and leave the bird stuck that way.
    public void ResetStats()
    {
        statModifiers.Clear();
        ApplyStatModifiers();
    }

    // Stat changes from abilities (see BirdStats). The stats are always worked out from the ones the bird spawned with.
    private readonly StatModifierStack statModifiers = new StatModifierStack();

    public void SetStatModifier(object key, StatModifier modifier)
    {
        statModifiers.Set(key, modifier);
        ApplyStatModifiers();
    }

    public void RemoveStatModifier(object key)
    {
        if (statModifiers.Remove(key)) ApplyStatModifiers();
    }

    private void ApplyStatModifiers()
    {
        statModifiers.Evaluate(baseMaxGroundSpeed, baseMaxAirSpeed, baseJumpForce,
            out maxGroundSpeed, out maxAirSpeed, out jumpForce);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    IEnumerator Start()
    {
        // Get the Rigidbody of the character
        rb = GetComponent<Rigidbody>();
        dustParticles = GetComponent<ParticleSystem>();

        yield return new WaitUntil(() => GameManager.Instance != null);
        if (GameManager.Instance.isLocal) playerInput = GetComponent<PlayerInput>();

        // Error handling
        if (GameManager.Instance.isLocal && playerInput == null)
        {
            Debug.LogError("Could not find player input on a local multiplayer instance for " + transform.name);
        } 

        // grab animator if not assigned in inspector (mirrors AIBehavior logic)
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

        // Determine which side of the net this character belongs to. (Always, since every bird gets the basic guard)
        netSide = Mathf.Sign(transform.position.x);

        // Safety fallback if the character starts directly on the net.
        if (Mathf.Approximately(netSide, 0f))
        {
            netSide = 1f;
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // Network: wait until the start stuff has happened
        if (GameManager.Instance == null) return;

        Move();

        // Check if jump is being held, from this machine's input or from the input RPC
        if (JumpHeld)
            Jump();
    }

    /// <summary>
    /// Prevents the character from crossing the center net.
    /// Uses the character's collider bounds so the entire body stays
    /// on its assigned side of the net.
    /// </summary>
    private void EnforceNetBoundary()
    {
        if (rb == null || netSide == 0f)
            return;

        // The end screen lines every bird up on one side
        if (GameManager.Instance == null || GameManager.Instance.gameState == GameManager.GameState.GameOver)
            return;

        Vector3 position = rb.position;
        Vector3 velocity = rb.linearVelocity;

        // Every bird is kept from getting its middle over the net, even without enforceNetBoundary. Fast dashes
        // (Hummingbird's is 30 units a second) could otherwise go straight through the net's collider in a single
        // physics step and leave the bird on the other team's court. enforceNetBoundary keeps the whole body back.
        float halfWidth = 0f;

        Collider col = enforceNetBoundary ? GetComponent<Collider>() : null;
        if (col != null)
        {
            halfWidth = col.bounds.extents.x;
        }

        // Keep the collider completely on its assigned side.
        float boundary = netXPosition + netSide * (halfWidth + netBuffer);

        bool crossedNet =
            (netSide < 0f && position.x > boundary) ||
            (netSide > 0f && position.x < boundary);

        if (!crossedNet)
            return;

        // Place the character directly against its boundary.
        position.x = boundary;

        // Stop only velocity that is pushing the character through the net.
        if (netSide < 0f && velocity.x > 0f)
        {
            velocity.x = 0f;
        }
        else if (netSide > 0f && velocity.x < 0f)
        {
            velocity.x = 0f;
        }

        rb.position = position;
        rb.linearVelocity = velocity;
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

    // christofort: encapsulated variables to control player movement from other scripts
    public void controlMovement(bool movementEnabled, bool jumpEnabled)
    {
        canJump = jumpEnabled;
        canMove = movementEnabled;
    }

    public void BuffStats(int increase, int time)
    {
        buffCoroutine = StartCoroutine(BuffTimer(increase, time));
    }

    public void CancelBuffs()
    {
        if (buffCoroutine != null)
        {
            StopCoroutine(buffCoroutine);
            buffCoroutine = null;
        }

        RemoveStatModifier(buffStatsKey);
    }

    private readonly object buffStatsKey = new object();

    public IEnumerator BuffTimer(int increase, int time)
    {
        BuffsDebuffs.Instance.ApplyEffect(
            BuffsDebuffs.EffectType.Buff,
            gameObject,
            5f,
            true
        );

        SetStatModifier(buffStatsKey, StatModifier.Speed(increase));

        yield return new WaitForSeconds(time);

        RemoveStatModifier(buffStatsKey);
    }

    /// <summary>
    /// Moves the player depending on the input from the networked object.
    /// </summary>
    public void Move()
    {
        if (!NetworkSession.HasAuthority) return;

        // Get the input for local if necessary
        if (GameManager.Instance.isLocal && playerInput != null)
        {
            movementInput = playerInput.actions["Move"].ReadValue<Vector2>();
            if (inputInverted) movementInput = -movementInput; // Macaw's Flip Flap
        }

        // Update the current direction and speed of the character based on player input
        // christofort: added check for canMove to be true
        if (!movementInput.Equals(Vector2.zero) && canMove)
        {
            // Calculate new velocity, ensure it doesn't exceed max ground or air speed, then assign the velocity
            Vector2 newVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z) + movementInput * Time.fixedDeltaTime * directionChangeWeight;

            // If on the ground and new velocity exceeds max ground speed, cap the speed
            if (grounded && newVelocity.magnitude > maxGroundSpeed)
            {
                newVelocity.Normalize();
                newVelocity *= maxGroundSpeed;
            }
            // Else if in the air and new velocity exceeds max air speed, cap the speed
            else if (!grounded && newVelocity.magnitude > maxAirSpeed)
            {
                newVelocity.Normalize();
                newVelocity *= maxAirSpeed;
            }

            rb.linearVelocity = new Vector3(newVelocity.x, rb.linearVelocity.y, newVelocity.y);
            
            // Rotate to face movement direction (unless overridden by another script)
            if (!overrideRotation && movementInput.magnitude > 0.1f)
            {
                Vector3 movementDirection = new Vector3(movementInput.x, 0, movementInput.y);
                Quaternion baseRotation = Quaternion.LookRotation(movementDirection);

                if (rotationOffsetEuler != Vector3.zero)
                {
                    baseRotation *= Quaternion.Euler(rotationOffsetEuler);
                }

                targetRotation = baseRotation;
            }
        }

        // Apply rotation (either from movement or override)
        if (!overrideRotation ||
            Vector3.Distance(transform.eulerAngles, targetRotation.eulerAngles) > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }

        // update walking flag for animator
        Rigidbody localRb = rb; // alias for clarity
        isWalking = new Vector2(localRb.linearVelocity.x, localRb.linearVelocity.z).magnitude > 0.1f;
        if (animator != null)
        {
            animator.SetBool("isWalking", isWalking);
        }

        // Make sure the character cannot cross the net.
        // This runs even when normal movement is disabled,
        // so abilities such as Hummingbird's dash are also contained.
        EnforceNetBoundary();

        // Debug.Log($"[SERVER] Movement applied on Object {NetworkObjectId}: {transform.position}");
    }

    public void Jump()
    {
        // If character touching ground and not disabled from jumping, character jumps
        // The actual check for pressing jump button is not handled in PlayerNetworkController
        if (canJump && grounded)
        {
            float jumpAmount = 5 + 0.3f * jumpForce;
            // Assign Y velocity directly rather than += - prevents leftover fall velocity
            // (which isn't always fully zeroed by the physics solver on landing) from
            // stacking with the jump impulse and producing inconsistent jump heights
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpAmount, rb.linearVelocity.z);
            grounded = false;
        }
    }

    // Whether jump is being held. Abilities should use this instead of reading PlayerInput,
    // since online input arrives through PlayerNetworkController.
    public bool JumpHeld => GameManager.Instance.isLocal
        ? playerInput != null && playerInput.actions["Jump"].IsPressed()
        : networkedJumpHeld;

    // Setter for holding jump from the PlayerNetworkController component.
    public void SetJumpHeld(bool held)
    {
        networkedJumpHeld = held;
    }

    // Flips this player's online movement input (Macaw's Flip Flap).
    public void SetInputInverted(bool inverted)
    {
        if (inputInverted != inverted) movementInput = -movementInput;
        inputInverted = inverted;
    }

    /// <summary>
    /// Setter for the input from the PlayerNetworkController component.
    /// </summary>
    /// <param name="input">2D input movement vector.</param>
    public void SetInput(Vector2 input)
    {
        movementInput = inputInverted ? -input : input;
        // Debug.Log($"Updated input for Player{NetworkObjectId}: {networkedInput}");
    }
}