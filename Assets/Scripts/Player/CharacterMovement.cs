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
    private float originalMaxGroundSpeed = 1.0f; // Original max ground speed before buff
    private float originalMaxAirSpeed = 1.0f; // Original max air speed before buff

    private Rigidbody rb; // Rigid body of the character
    public bool grounded = false; // If the character is touching the ground

    private bool canJump = true; // christofort: defaulted to false, ability scripts must set this to true
    private bool canMove = true; // christofort: defaulted to false, ability scripts must set this to true
    private ParticleSystem dustParticles; // Reference to particle system for ground dust
    private PlayerInput playerInput; // Input for the player

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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Get the Rigidbody of the character
        playerInput = GetComponent<PlayerInput>();
        rb = GetComponent<Rigidbody>();
        dustParticles = GetComponent<ParticleSystem>();

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

        // Determine which side of the net this character belongs to.
        if (enforceNetBoundary)
        {
            netSide = Mathf.Sign(transform.position.x);

            // Safety fallback if the character starts directly on the net.
            if (Mathf.Approximately(netSide, 0f))
            {
                netSide = 1f;
            }
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // Check for player inputs for lateral movement
        Vector2 inputDirection = playerInput.actions.FindAction("Move").ReadValue<Vector2>();

        // Update the current direction and speed of the character based on player input
        // christofort: added check for canMove to be true
        if (!inputDirection.Equals(Vector2.zero) && canMove)
        {
            // Calculate new velocity, ensure it doesn't exceed max ground or air speed, then assign the velocity
            Vector2 newVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z) + inputDirection * Time.fixedDeltaTime * directionChangeWeight;

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
            if (!overrideRotation && inputDirection.magnitude > 0.1f)
            {
                Vector3 movementDirection = new Vector3(inputDirection.x, 0, inputDirection.y);
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

        // Check for player input for vertical movement
        InputAction jump = playerInput.actions.FindAction("Jump");

        // If character touching ground AND player presses jump button, character jumps
        // christofort: addded a check for canJump to prevent jumping if ability script hasn't allowed for it
        if (canJump && grounded && jump.IsPressed())
        {
            float jumpAmount = 5 + 0.3f * jumpForce;
            // Assign Y velocity directly rather than += - prevents leftover fall velocity
            // (which isn't always fully zeroed by the physics solver on landing) from
            // stacking with the jump impulse and producing inconsistent jump heights
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpAmount, rb.linearVelocity.z);
            grounded = false;
        }

        // Make sure the character cannot cross the net.
        // This runs even when normal movement is disabled,
        // so abilities such as Hummingbird's dash are also contained.
        EnforceNetBoundary();
    }

    /// <summary>
    /// Prevents the character from crossing the center net.
    /// Uses the character's collider bounds so the entire body stays
    /// on its assigned side of the net.
    /// </summary>
    private void EnforceNetBoundary()
    {
        if (!enforceNetBoundary || rb == null)
            return;

        Vector3 position = rb.position;
        Vector3 velocity = rb.linearVelocity;

        Collider col = GetComponent<Collider>();

        float halfWidth = 0f;

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

        maxGroundSpeed = originalMaxGroundSpeed;
        maxAirSpeed = originalMaxAirSpeed;
        // originalJumpForce = jumpForce;
    }

    public IEnumerator BuffTimer(int increase, int time)
    {
        Debug.Log("BUFFING...");
        Debug.Log("ORIGINAL = " + maxGroundSpeed);

        BuffsDebuffs.Instance.ApplyEffect(
            BuffsDebuffs.EffectType.Buff,
            gameObject,
            5f,
            true
        );

        originalMaxGroundSpeed = maxGroundSpeed;
        originalMaxAirSpeed = maxAirSpeed;
        // originalJumpForce = jumpForce;

        maxGroundSpeed += increase;
        maxAirSpeed += increase;

        // Clamp it so that the stat cannot go below 1
        maxGroundSpeed = Mathf.Max(maxGroundSpeed, 1f);
        maxAirSpeed = Mathf.Max(maxAirSpeed, 1f);
        // jumpForce += increase;

        Debug.Log("NEW = " + maxGroundSpeed);

        yield return new WaitForSeconds(time);

        maxGroundSpeed = originalMaxGroundSpeed;
        maxAirSpeed = originalMaxAirSpeed;
        // jumpForce = originalJumpForce;
    }
}