using UnityEngine;
using UnityEngine.InputSystem;

public class PenguinScript : MonoBehaviour
{
    [Header("Dash Ability")]
    public float dashCooldown = 5.0f; 
    public float dashDuration = 1.0f;
    public float dashSpeed = 10.0f; // Forward movement speed during dash
    public float rotationSpeed = 8.0f; // How fast penguin rotates

    public float penguinHeight; // christofort: height of the penguin for ground check
        
    [HideInInspector] public bool isDashing = false;
    private bool isReturningUpright = false; // Ensure penguin returns to upright after dash
    private float dashTimer = 0.0f; // Tracker for the cooldown
    private float cooldownTimer = 0.0f;
    private CharacterMovement characterMovement; // Track movement from character movement script
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        characterMovement = GetComponent<CharacterMovement>(); 
        // christofort: automatically sets canJump and canMove to True
        characterMovement.controlMovement(true,true);
    }

    void Update()
    {
        
        // Handle dash input using Input System
        InputAction dash = InputSystem.actions.FindAction("Defensive Ability");
        bool dashPressed = (dash != null && dash.WasPressedThisFrame()) || 
                          (dash == null && Keyboard.current?.spaceKey.wasPressedThisFrame == true);
        
        penguinHeight = transform.position.y; //christofort: grabs the Y value of the penguin
        // chrIStofort: added a check for the penguin's y value, to make sure it isn't higher than the ground
        if (dashPressed && cooldownTimer <= 0 && !isDashing && penguinHeight < 0.76)
        {
            StartDash();
        }
        
        // Update timers
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0) EndDash();
        }

        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
        }
        
        // Handle returning to upright position
        if (isReturningUpright && characterMovement != null)
        {
            float angleFromUpright = Vector3.Angle(transform.up, Vector3.up);
            if (angleFromUpright < 5.0f)
            {
                isReturningUpright = false;
                characterMovement.overrideRotation = false;
            }
        }
    }

    void StartDash()
    {
        characterMovement.controlMovement(true,false); //christofort: makes canJump false
        isDashing = true;
        dashTimer = dashDuration;
        // Apply forward force in the direction penguin is currently facing
        rb.AddForce(transform.forward * dashSpeed, ForceMode.Impulse);
        
        // Override CharacterMovement rotation to do belly slide
        if (characterMovement != null)
        {
            characterMovement.overrideRotation = true;
            characterMovement.targetRotation = transform.rotation * Quaternion.Euler(90, 0, 0);
        }

        // Play slide sound
        AudioManager.PlayBirdSound(BirdType.PENGUIN, SoundType.DEFENSIVE, 1.0f);
    }

    void EndDash()
    {
        isDashing = false;
        characterMovement.controlMovement(true, true); //christofort: sets canJump back to True
        cooldownTimer = dashCooldown;
        
        // Start transition back to upright position
        if (characterMovement != null)
        {
            Vector3 currentEuler = transform.eulerAngles;
            Quaternion uprightRotation = Quaternion.Euler(0, currentEuler.y, 0);
            characterMovement.targetRotation = uprightRotation;
            isReturningUpright = true;
        }
    }
}
