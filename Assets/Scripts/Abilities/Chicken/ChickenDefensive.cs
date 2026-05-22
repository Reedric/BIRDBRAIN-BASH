using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CharacterMovement))]

/// <summary>
/// Slow Falling - When holding jump at the apex of a jump, Chicken briefly hovers
/// then slow falls down to the ground (similar to Peach in Super Mario 3D World) (passive)
/// </summary>
public class ChickenDefensive : BirdAbility
{
    [Header("Chicken Defensive Settings")]
    [SerializeField] private float slowFallMultiplier = 0.15f; // How much to slow the fall
    [SerializeField] private float apexHoverDuration = 1.5f;   // How long Chicken hangs at the apex
    [SerializeField] private float apexVelocityThreshold = 2f; // How close to zero Y velocity counts as apex

    private Rigidbody rb;
    private bool _jumpHeld = false;        // Whether the player is currently holding jump
    private bool _apexReached = false;     // Whether we've entered the apex hover window
    private bool _slowFalling = false;     // Whether we're in the slow fall phase
    private float _apexTimer = 0f;         // Tracks how long we've hovered at the apex

    private void Awake() { rb = GetComponent<Rigidbody>(); }

    // Called by Unity's Input System when the jump action fires
    public void OnJump(InputValue value)
    {
        _jumpHeld = value.isPressed;

        // Reset hover state and restore gravity when jump is released
        if (!_jumpHeld)
        {
            _apexReached = false;
            _slowFalling = false;
            _apexTimer = 0f;
            rb.useGravity = true; // Always restore gravity on release
        }
    }

    private void FixedUpdate()
    {
        // Only activate if player is holding jump
        if (!_jumpHeld) return;

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