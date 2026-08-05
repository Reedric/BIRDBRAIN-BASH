using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CharacterMovement))]

/// <summary>
/// Slow Falling - When holding jump at the apex of a jump, Chicken briefly hovers
/// then slow falls down to the ground (similar to Peach in Super Mario 3D World) (passive)
/// </summary>
public class ChickenDefensive : PassiveAbility
{
    [Header("Chicken Defensive Settings")]
    [SerializeField] private float slowFallMultiplier = 0.15f; // How much to slow the fall
    [SerializeField] private float apexHoverDuration = 1.5f;   // How long Chicken hangs at the apex
    [SerializeField] private float apexVelocityThreshold = 2f; // How close to zero Y velocity counts as apex

    private Rigidbody rb;
    private CharacterMovement characterMovement; // Used to check grounded state - hover should only trigger mid-air
    private PlayerInput playerInput;             // Polled directly, matching CharacterMovement's input pattern
    private InputAction jumpAction;              // Cached Jump action reference

    private bool _jumpHeld = false;        // Whether the player is currently holding jump
    private bool _apexReached = false;     // Whether we've entered the apex hover window
    private bool _slowFalling = false;     // Whether we're in the slow fall phase
    private float _apexTimer = 0f;         // Tracks how long we've hovered at the apex

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        characterMovement = GetComponent<CharacterMovement>();
        playerInput = GetComponent<PlayerInput>();
    }

    private void Start()
    {
        // Cache the action so we're not calling FindAction every physics step
        jumpAction = playerInput.actions.FindAction("Jump");
    }

    private void FixedUpdate()
    {
        // Poll the Jump action directly instead of relying on the OnJump(InputValue)
        // message - this project doesn't broadcast input messages (CharacterMovement
        // polls the same way), so that callback was never actually firing.
        bool wasHeld = _jumpHeld;
        _jumpHeld = jumpAction != null && jumpAction.IsPressed();

        // Jump was just released this frame - reset hover state and restore gravity
        if (wasHeld && !_jumpHeld)
        {
            _apexReached = false;
            _slowFalling = false;
            _apexTimer = 0f;
            rb.useGravity = true;
        }

        // Only activate if the player is holding jump AND actually airborne - this stops
        // hover from falsely triggering just from holding jump while standing on the ground
        if (!_jumpHeld || characterMovement.grounded)
        {
            return;
        }

        float velY = rb.linearVelocity.y;

        // Detect the apex — near-zero upward velocity after a jump
        if (!_apexReached && !_slowFalling && Mathf.Abs(velY) < apexVelocityThreshold && velY <= 0)
        {
            _apexReached = true;
            _apexTimer = 0f;
        }

        if (_apexReached && !_slowFalling)
        {
            // Freeze Y velocity so Chicken hangs in the air at the apex
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.useGravity = false;

            _apexTimer += Time.fixedDeltaTime;

            // After hovering, transition into slow fall
            if (_apexTimer >= apexHoverDuration)
            {
                _apexReached = false;
                _slowFalling = true;
                rb.useGravity = true;
            }
        }
        else if (_slowFalling && velY < 0) // Only slow the fall, not the ascent
        {
            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                rb.linearVelocity.y * slowFallMultiplier,
                rb.linearVelocity.z
            );
        }
    }

    private void OnDisable()
    {
        // Make sure gravity is always restored if the component is disabled mid-hover
        rb.useGravity = true;
    }
}