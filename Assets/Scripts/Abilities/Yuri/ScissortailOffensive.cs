using System.Collections;
using UnityEngine;

public class ScissortailOffensive : BirdAbility
{
    private bool shotHappened; // Whether or not the ability happened for return purposes
    override protected bool Activate()
    {
        StartCoroutine(ScissorShot());

        Debug.Log(shotHappened);
        return shotHappened; // This might not work due to race conditions but we will see
    }

    private IEnumerator ScissorShot()
    {
        // Ability has not yet happened
        shotHappened = false;

        // Get the ball's halfway point from the ground
        float ballMidHeight = BallManager.Instance.gameObject.transform.position.y / 2;

        // Spike the ball
        GetComponent<BallInteract>().SpikeBall();

        // Wait for ball to get to halfway point
        Rigidbody ballRb = BallManager.Instance.GetComponent<Rigidbody>();
        while (BallManager.Instance.gameObject.transform.position.y > ballMidHeight)
        {
            // If the ball is blocked, can't finish ability
            if (GameManager.Instance.gameState == GameManager.GameState.Blocked) break;

            // If block touch, can't finish ability
            if (ballRb.linearVelocity.y > 0) break;

            // If the ball is now of course, cannot finish ability
            if (BallManager.Instance.offCourse) break;

            // If the ball was bumped before reaching the halfway point, can't finish ability
            if (GameManager.Instance.gameState == GameManager.GameState.Bumped) break;

            // Wait for next frame
            yield return null;
        }

        // If the ball actually reached the midpoint
        if (BallManager.Instance.gameObject.transform.position.y <= ballMidHeight)
        {
            // Ability happened
            shotHappened = true;

            // Ball should be at the halfway point, change its direction
            Vector3 changeTo;
            if (ballRb.linearVelocity.z > 0)
            {
                changeTo = transform.position.x < 0 ? new Vector3(8, 0, -4) : new Vector3(-8, 0, -4);
            }
            else
            {
                changeTo = transform.position.x < 0 ? new Vector3(8, 0, 4) : new Vector3(-8, 0, 4);
            }
            Vector3 unitVelocity = changeTo - BallManager.Instance.gameObject.transform.position;
            unitVelocity.Normalize();
            float ballSpeed = ballRb.linearVelocity.magnitude;
            ballRb.linearVelocity = unitVelocity * ballSpeed;

            int playerID = GetComponent<BallInteract>().playerID;
            HUDManager.Instance.TriggerOffensiveCooldown(playerID, _cooldownTime);
        }
    }
}
