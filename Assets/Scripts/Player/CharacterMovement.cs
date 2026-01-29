using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterMovement : MonoBehaviour
{
    [Header("Character Attributes")]
    public float maxGroundSpeed = 1.0f; // Max speed that the character can move on the ground
    public float maxAirSpeed = 1.0f; // Max speed that the character can move in the air
    public float jumpForce = 1.0f; // Force the character uses to jump 
    public float rotationSpeed = 10.0f; // How fast the character rotates to face movement direction
    private float directionChangeWeight = 15f; // How quickly the character can change direction
    private Rigidbody rb; // Rigid body of the character
    // christofort: changed grounded to public to allow PenguinScript to access it
    public bool grounded = false; // If the character is touching the ground

    private bool canJump = false; // christofort: defaulted to false, ability scripts must set this to true
    private bool canMove = false; // christofort: defaulted to false, ability scripts must set this to true
    private PenguinScript penguinScript; // Reference to penguin dash script
    
    [HideInInspector] public bool overrideRotation = false; // Allow other scripts to override rotation
    [HideInInspector] public Quaternion targetRotation; // Target rotation when overridden

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Get the Rigidbody of the character
        rb = GetComponent<Rigidbody>();
        penguinScript = GetComponent<PenguinScript>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // Check for player inputs for lateral movement
        Vector2 inputDirection = InputSystem.actions.FindAction("Move").ReadValue<Vector2>();

        // Don't process movement input if penguin is dashing
        bool isDashing = penguinScript != null && penguinScript.isDashing;
        
        // Update the current direction and speed of the character based on player input
        // christofort: added check for canMove to be true
        if (!inputDirection.Equals(Vector2.zero) && !isDashing && canMove)
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
                targetRotation = Quaternion.LookRotation(movementDirection);
            }
        }
        
        // Apply rotation (either from movement or override)
        if (!overrideRotation || Vector3.Distance(transform.eulerAngles, targetRotation.eulerAngles) > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }

        // Check for player input for vertical movement
        InputAction jump = InputSystem.actions.FindAction("Jump");

        // If character touching ground AND player presses jump button, character jumps
        // christofort: addded a check for canJump to prevent jumping if ability script hasn't allowed for it
        if (canJump && grounded && jump.IsPressed())
        {
            rb.linearVelocity += new Vector3(0, jumpForce, 0);
            grounded = false;
        }
    }

    // Calls whenever the character collides with another collider or rigidbody
    void OnCollisionEnter(Collision other)
    {
        // If the character collides with the court, it is now grounded
        if (other.gameObject.layer == 6)
        {
            grounded = true;
        }
    }

    // Calls whenever the character stops colliding with another collider or rigidbody
    void OnCollisionExit(Collision other)
    {
        // If the character stops colliding with the court, it is no longer grounded
        if (other.gameObject.layer == 6)
        {
            grounded = false;
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
        StartCoroutine(BuffTimer(increase, time));
    }

    public IEnumerator BuffTimer(int increase, int time)
    {
        Debug.Log("BUFFING...");
        Debug.Log("ORIGINAL = " + maxGroundSpeed);
        
        float originalMaxGroundSpeed = maxGroundSpeed;
        float originalMaxAirSpeed = maxAirSpeed;
        float originalJumpForce = jumpForce;

        maxGroundSpeed += increase;
        maxAirSpeed += increase;
        jumpForce += increase;

        Debug.Log("NEW = "+ maxGroundSpeed);

        yield return new WaitForSeconds(time);

        maxGroundSpeed = originalMaxGroundSpeed;
        maxAirSpeed = originalMaxAirSpeed;
        jumpForce = originalJumpForce;
    }
}
