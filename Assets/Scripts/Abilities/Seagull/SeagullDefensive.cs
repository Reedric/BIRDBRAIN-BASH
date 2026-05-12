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
    public float cooldown = 15f; //cooldown in seconds
    public float shoveForce = 18f; //how much the seagull pushes others out of the way
    public float shoveRadius = 1.5f; //radius to shove objects around
    [HideInInspector] private bool isAbilityReady = true;
    private Rigidbody rb;
    private PlayerInput playerInput; // Input for this specific player

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerInput = GetComponent<PlayerInput>();
        rb = GetComponent<Rigidbody>();
    }

    public void ActivateAbility()
    {
        if (!isAbilityReady)
        {
            return;
        }
        if (gameManager.gameState == GameManager.GameState.PointStart && gameManager.server == gameObject)
        {
            return;
        }
        if (gameManager.gameState == GameManager.GameState.Served && gameManager.server == gameObject)
        {
            return;
        }
        if (!CanUseAbilities())
        {
            return;
        }
        if (CanDashToBall())
        {
            StartCoroutine(DashToBall());
        }
    }

    private bool CanDashToBall()
    {
        // Check if ball is on the player's side
        bool onLeft = GetComponent<BallInteract>().onLeft;
        float ballX = BallManager.Instance.gameObject.transform.position.x;
        bool ballOnMySide = (onLeft && ballX < 0) || (!onLeft && ballX > 0);

        if (!ballOnMySide)
        {
            return false;
        }

        // Allow dashing during any active play state where the ball is in motion
        GameManager.GameState state = GameManager.Instance.gameState;
        bool validState = state == GameManager.GameState.Spiked ||
                        state == GameManager.GameState.Blocked ||
                        state == GameManager.GameState.Bumped ||
                        state == GameManager.GameState.Set;

        return validState;
    }
    
    private IEnumerator DashToBall()
    {
        isAbilityReady = false;

        // Play defensive sound
        AudioManager.PlayBirdSound(BirdType.SEAGULL, SoundType.DEFENSIVE, 1.0f);

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, cooldown);

        // Trigger defensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract.animator != null)
        {
            myBallInteract.animator.SetTrigger("DefensiveAbility");
        }

        //Check if ball is on the player's side
        bool isBallOnSameSide = Mathf.Sign(BallManager.Instance.gameObject.transform.position.x) == Mathf.Sign(transform.position.x);
        if (!isBallOnSameSide)
        {
            isAbilityReady = true; //ability remains ready
            yield break;
        }
        
        float fixedY = 0.5f;
        
        //Freeze Y so the dash stays level
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

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
            if (distance <= step)
            {
                //Snap to landing position with a slight offset for realistic contact
                float offset = 0.1f;
                rb.MovePosition(landingPos - direction * offset);
                break; //reached the ball
            }
            else
            {
                rb.MovePosition(transform.position + direction * step);
            }

            //Push nearby objects
            ShoveNearbyObjects();

            yield return null;
        }

        //Ensure seagull is exactly on the landing spot at a fixed Y
        rb.MovePosition(new Vector3(BallManager.Instance.goingTo.x, fixedY, BallManager.Instance.goingTo.z));

        BallInteract ballInteract = GetComponent<BallInteract>();
        ballInteract.BumpBall();

        //Unfreeze Y movments
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        //Start cooldown
        StartCoroutine(CooldownRoutine());
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

    //Update is called once per frame
    void Update()
    {
        if (playerInput.actions.FindAction("Defensive Ability").WasPressedThisFrame()) //for testing can change later
        {
            ActivateAbility();
        }
    }

    private IEnumerator CooldownRoutine()
    {
        // Wait for the cooldown time
        yield return new WaitForSeconds(cooldown);

        // After waiting, the ability is ready again
        isAbilityReady = true;
    }
}
