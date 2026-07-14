using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BallInteract))]
public class SeagullDefensive : BirdAbility
{
    [Header("Mine Mine Mine Ability")]
    public float dashSpeed = 100f; //how fast the dash is
    public float shoveForce = 18f; //how much the seagull pushes others out of the way
    public float shoveRadius = 1.5f; //radius to shove objects around
    private Rigidbody rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
    
    private IEnumerator DashToBall()
    {
        // Play defensive sound
        AudioManager.PlayBirdSound(BirdType.SEAGULL, SoundType.DEFENSIVE, 1.0f);

        // Trigger defensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        
        float fixedY = 0.5f;
        
        //Freeze Y so the dash stays level
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        // Save current game state to check if it touched the ground or if teammate got it
        GameManager.GameState dashState = GameManager.Instance.gameState;

        //Always Dash to Ball until break
        while (true)
        {
            //Update landing position every frame (ball might move)
            Vector3 landingPos = BallManager.Instance.goingTo;
            landingPos.y = fixedY;

            //Direction toward the ball
            Vector3 direction = (landingPos - transform.position).normalized;

            //Step distance based on dashSpeed and deltaTime
            float step = dashSpeed * Time.deltaTime;

            //Distance to target
            float distance = Vector3.Distance(transform.position, landingPos);

            //Once we reached the ball
            Debug.Log(distance - step);
            if (distance <= step)
            {
                //Snap to landing position with a slight offset for realistic contact
                float offset = 0.1f;
                rb.MovePosition(landingPos - direction * offset);
                break; //reached the ball
            }
            // If the game state changed, stop dashing
            else if (dashState != GameManager.Instance.gameState)
            {
                break;
            }
            else
            {
                rb.MovePosition(transform.position + direction * step);
            }

            //Push nearby objects
            ShoveNearbyObjects();

            yield return null;
        }

        // If the game state did not change, then successfully dashed to ball
        if (dashState == GameManager.Instance.gameState)
        {
            //Ensure seagull is exactly on the landing spot at a fixed Y
            rb.MovePosition(new Vector3(BallManager.Instance.goingTo.x, fixedY, BallManager.Instance.goingTo.z));

            // while (Vector3.Distance(BallManager.Instance.transform.position, transform.position) > myBallInteract.interactionRadius) yield return null;
            while (Vector3.Distance(BallManager.Instance.transform.position, transform.position) > myBallInteract.interactionRadius && dashState == GameManager.Instance.gameState)
            {
                Debug.Log(Vector3.Distance(BallManager.Instance.transform.position, transform.position));
                yield return null;
            }
            if (GameManager.Instance.gameState == GameManager.GameState.Bumped) myBallInteract.SetBall();
            else myBallInteract.BumpBall();

            int playerID = GetComponent<BallInteract>().playerID;
            HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);
        }

        //Unfreeze Y movments
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void ShoveNearbyObjects()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, shoveRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];

            //Won't shove itself
            if (hit.gameObject != gameObject)
            {   
                Rigidbody otherRb = hit.GetComponent<Rigidbody>();
                //pushes shoveable objects only (things with rigidbodies)
                if (otherRb != null && !otherRb.isKinematic)
                {
                    //gets the direction
                    Vector3 pushDirection = hit.transform.position - transform.position;
                    pushDirection.y = 0; // ignore vertical
                    pushDirection.Normalize();

                    otherRb.AddForce(pushDirection * shoveForce * 0.1f, ForceMode.VelocityChange);
                }
            }
        }
    }

    override protected bool Activate()
    {
        StartCoroutine(DashToBall());

        // If seagull was last one to hit, successfully activated ability
        if (GameManager.Instance.lastHit == gameObject) return true;
        else return false;
    }
}
