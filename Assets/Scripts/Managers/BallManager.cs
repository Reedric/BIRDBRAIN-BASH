using UnityEngine;

public class BallManager : MonoBehaviour
{
    public Vector3 goingTo; // Where the ball is going to
    public GameManager gameManager; // Game manager object
    public GameObject unblockableOwner; // If set, this player's spike cannot be blocked
    public bool offCourse; // Boolean for whether the ball is off course of where it is supposed to go
    private Rigidbody rb; // Rigidbody of the ball

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Get the ball's rigidbody
        rb = GetComponent<Rigidbody>();

        // Set off course to false
        offCourse = false;

        if (gameManager == null)
        {
            Debug.LogError("Game Manager was not set in the inspector for Ball Manager!");
        }
    }

    // Calls whenever the character collides with another collider or rigidbody
    void OnCollisionEnter(Collision other)
    {
        // Activate gravity it's not already activated and the game state is NOT serving
        if (!rb.useGravity && !gameManager.gameState.Equals(GameManager.GameState.PointStart))
        {
            rb.useGravity = true;
        }

        // Set flag to true if ball collided with something other than ground
        if (!(other.collider.tag.Equals("Side1") || other.collider.tag.Equals("Side2") || other.collider.tag.Equals("Out")))
        {
            offCourse = true;
        }

        // Clear unblockable owner on any collision (spike has reached a target)
        if (unblockableOwner != null)
        {
            unblockableOwner = null;
            Debug.Log("BallManager: cleared unblockable spike owner after collision.");
        }
    }
}
