using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(BallInteract))]
public class PelicanDefensive : BirdAbility
{
    public int holdLength; // Maximum amount of time in seconds the pelican can hold the ball in its mouth
    public BallInteract ballInteract;
    private bool isBallEaten = false;
    private Coroutine holdCoroutine;
    private GameManager.GameState stateWhenEaten;

    void Awake()
    {
        if (ballInteract == null) ballInteract = GetComponent<BallInteract>();
    }

    // If the pelican gets deactivated with the ball in its mouth (e.g. Phoenix turns it into a chicken), the coroutine
    // that would let it go stops, and the ball would stay hidden for the rest of the match with no way to score.
    void OnDisable()
    {
        if (!isBallEaten) return;
        isBallEaten = false;

        if (BallManager.Instance == null) return;

        GameObject ball = BallManager.Instance.gameObject;
        ball.transform.position = transform.position + new Vector3(0, 3f, 0);
        NetworkFx.SetActive(ball, true);

        // Just drop it, since the pelican isn't around to hit it anywhere
        if (ball.TryGetComponent(out Rigidbody ballRb))
        {
            ballRb.linearVelocity = Vector3.zero;
            ballRb.useGravity = true;
        }
        BallManager.Instance.offCourse = true;
    }

    override protected bool Activate()
    {
        // If ball is eaten, release it and start cooldown
        if (isBallEaten)
        {
            SpitBall();
            return true;
        }
        else
        {
            EatTheBall();
            return false; 
        }
    }

    void Update()
    {
        if (isBallEaten)
        {
            BallManager.Instance.gameObject.transform.position = transform.position + new Vector3(0, 3f, 0);

            // If serve, allow pelican to move along back line of court
            if (stateWhenEaten == GameManager.GameState.PointStart)
            {
                GetComponent<CharacterMovement>().controlMovement(true, false);

                if (transform.position.x < 0)
                {
                    transform.position = new Vector3(-10, transform.position.y, Mathf.Clamp(transform.position.z, -4f, 4f));
                }
                else
                {
                    transform.position = new Vector3(10, transform.position.y, Mathf.Clamp(transform.position.z, -4f, 4f));
                }
            }
        }
    }

    // Returns true when the pelican is within interaction range of the ball.
    // Delegates to BallInteract.IsPlayerNearBall() so the same contactPoint
    // and interactionRadius Inspector values are reused.
    private bool BallInEatRange()
    {
        return ballInteract.IsPlayerNearBall();
    }

    public void EatTheBall()
    {
        // Pelican must be close enough to the ball
        if (!BallInEatRange())
        {
            Debug.Log("[Pelican] Ball is too far away to eat.");
            return;
        }

        // If pelican last interacted with baLl, they cannot interact with it again
        if (GameManager.Instance.lastHit == gameObject)
        {
            Debug.Log("[Pelican] was the last hit, so they cannot eat the ball.");
            return;
        }

        // It has to be the pelican's turn to play the ball, the same as a normal hit. Being close enough isn't enough,
        // or it could eat a ball still on the other side of the net, or one the other team is about to play.
        if (!ballInteract.CanHit())
        {
            Debug.Log("[Pelican] can't play the ball right now, so it can't eat it.");
            return;
        }

        // Letting go of the ball plays the next hit, and there isn't one set up for a ball that has already been set
        // (it's waiting to be spiked). Eating it then would just drop it.
        if (GameManager.Instance.gameState == GameManager.GameState.Set)
            return;

        // Eating the ball on the serve releases it as a serve, which a normal serve has to wait for "GO" to do
        if (GameManager.Instance.gameState == GameManager.GameState.PointStart && !GameManager.IsCountdownComplete())
        {
            Debug.Log("[Pelican] can't eat the ball before the countdown finishes.");
            return;
        }

        // Store what state we ate in so release knows what hit to register
        stateWhenEaten = GameManager.Instance.gameState;

        // Play defensive sound
        AudioManager.PlayBirdSound(BirdType.PELICAN, SoundType.DEFENSIVE, 1.0f);

        // Trigger defensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
        {
            myBallInteract.animator.SetTriggerSynced("DefensiveAbility");
        }

        NetworkFx.SetActive(BallManager.Instance.gameObject, false);
        isBallEaten = true;

        holdCoroutine = StartCoroutine(HoldTime());
    }

    private void SpitBall()
    {
        // Use the stored reference. StopCoroutine(HoldTime()) would make a new instance and stop nothing
        if (holdCoroutine != null) StopCoroutine(holdCoroutine);
        ReleaseBall();
    }

    private void ReleaseBall()
    {
        if (!isBallEaten) return;
        isBallEaten = false;
        StartCoroutine(ReleaseBallCoroutine());

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);
    }

    private IEnumerator ReleaseBallCoroutine()
    {
        // Position and re-enable the ball first
        BallManager.Instance.gameObject.transform.position = transform.position + new Vector3(0, 3f, 0) + transform.forward;
        NetworkFx.SetActive(BallManager.Instance.gameObject, true);

        // Wait one frame so physics and state catch up before registering the hit
        yield return null;

        // Advance to the next hit based on what state the ball was eaten in
        switch (stateWhenEaten)
        {
            case GameManager.GameState.PointStart:
                ballInteract.ServeBall();  // eaten at beginning of point -> release as serve
                break;
            case GameManager.GameState.Spiked: case GameManager.GameState.Blocked: case GameManager.GameState.Served:
                ballInteract.BumpBall();   // eaten while ball incoming from enemy -> release as bump
                break;
            case GameManager.GameState.Bumped:
                // A set needs the ball to be falling, and it comes out of the pelican's mouth standing still
                if (BallManager.Instance.TryGetComponent(out Rigidbody setRb)) setRb.linearVelocity = Vector3.down * 0.5f;
                ballInteract.SetBall();    // eaten after ally bumped -> release as set
                break;
        }

        // However it went, the ball must not be left hanging in the air (a spiked ball has its gravity off)
        if (BallManager.Instance.TryGetComponent(out Rigidbody ballRb)) ballRb.useGravity = true;
    }

    public IEnumerator HoldTime()
    {
        yield return new WaitForSeconds(holdLength);
        ReleaseBall();

        // Letting go on its own skips Activate, which is what normally starts the cooldown the HUD shows
        BeginCooldown();
    }
}