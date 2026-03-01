using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SocialPlatforms.Impl;

public class NewMonoBehaviourScript : MonoBehaviour
{
    [Header("Mine Mine Mine Ability")]
    public GameManager gameManager;
    public float dashSpeed = 100f; //how fast the dash is
    public float cooldown = 15f; //cooldown in seconds
    public float shoveForce = 18f; //how much the seagull pushes others out of the way
    public float shoveRadius = 1.5f; //radius to shove objects around
    [HideInInspector] private bool isAbilityReady = true;
    private Rigidbody rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (gameManager == null)
        {
            Debug.Log("GameManager not set!");
        }
    }

    public void ActivateAbility()
    {
        if (!isAbilityReady)
        {
            return;
        }
        //Block ability if this player is the server (so you can't dash while you serve)
        if (gameManager.gameState == GameManager.GameState.PointStart && gameManager.server == gameObject)
        {
            return;
        }

        //Block ability if the ball has already been served by your side
        if (gameManager.gameState == GameManager.GameState.Served && gameManager.server == gameObject)
        {
            return;
        }
        StartCoroutine(DashToBall());
    }
    
    private IEnumerator DashToBall()
    {
        isAbilityReady = false;

        //Check if ball is on the player's side
        GameObject ball = gameManager.ball;
        if (ball == null)
        {
            isAbilityReady = true;
            yield break;
        }

        bool isBallOnSameSide = Mathf.Sign(ball.transform.position.x) == Mathf.Sign(transform.position.x);
        if (!isBallOnSameSide)
        {
            isAbilityReady = true; //ability remains ready
            yield break;
        }
        
        float fixedY = 0.5f;
        
        Rigidbody ballRb = ball.GetComponent<Rigidbody>();
        BallManager ballManager = ball.GetComponent<BallManager>();

        //Freeze Y so the dash stays level
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        //Always Dash to Ball until break
        while (true)
        {
            //Update landing position every frame (ball might move)
            Vector3 landingPos = ballManager.goingTo;
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

        //Ensure penguin/seagull is exactly on the landing spot at a fixed Y
        rb.MovePosition(new Vector3(ballManager.goingTo.x, fixedY, ballManager.goingTo.z));

        BallInteract ballInteract = GetComponent<BallInteract>();
        if (ballInteract != null)
        {
            ballInteract.BumpBall();
        }
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
                    Vector3 pushDirection = (hit.transform.position - transform.position);
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
        if (Keyboard.current.tKey.wasPressedThisFrame) //for testing can change later
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
