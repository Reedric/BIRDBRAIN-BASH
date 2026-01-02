using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterMovement : MonoBehaviour
{
    public float maxGroundSpeed = 1.0f; // Max speed that the character can move on the ground
    public float maxAirSpeed = 1.0f; // Max speed that the character can move in the air
    public float jumpHeight = 1.0f; // Height the character can jump 
    private float directionChangeWeight = 15f; // How quickly the character can change direction
    private Rigidbody rb; // Rigid body of the character
    private bool grounded = false; // If the character is touching the ground

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Get the Rigidbody of the character
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // Check for player inputs for lateral movement
        Vector2 inputDirection = InputSystem.actions.FindAction("Move").ReadValue<Vector2>();

        // Update the current direction and speed of the character based on player input
        if (!inputDirection.Equals(Vector2.zero))
        {
            // Calculate new velocity, ensure it doesn't exceed max ground or air speed, then assign the velocity
            Vector2 newVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z) + inputDirection * Time.fixedDeltaTime * directionChangeWeight;

            if (grounded && newVelocity.magnitude > maxGroundSpeed)
            {
                newVelocity.Normalize();
                newVelocity *= maxGroundSpeed;
            }
            else if (!grounded && newVelocity.magnitude > maxAirSpeed)
            {
                newVelocity.Normalize();
                newVelocity *= maxAirSpeed;
            }

            rb.linearVelocity = new Vector3(newVelocity.x, rb.linearVelocity.y, newVelocity.y);
        }

        // Check for player input for vertical movement
        InputAction jump = InputSystem.actions.FindAction("Jump");

        // If character touching ground AND player presses jump button, character jumps
        if (grounded && jump.IsPressed())
        {
            rb.linearVelocity += new Vector3(0, jumpHeight, 0);
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
}
