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

    override protected void Activate()
    {
        // If pressed defensive ability button, activate ability
        if (isBallEaten)
            SpitBall();
        else if (IsReady)
            EatTheBall();

        if (isBallEaten)
        {
            BallManager.Instance.gameObject.transform.position = transform.position + new Vector3(0, 1f, 0);
        }
    }

    // Returns true when the pelican is within interaction range of the ball.
    // Delegates to BallInteract.IsPlayerNearBall() so the same contactPoint
    // and interactionRadius Inspector values are reused — no separate tuning needed.
    private bool BallInEatRange()
    {
        return ballInteract.IsPlayerNearBall();
    }

    public void EatTheBall()
    {

        // Valid state handled in BirdAbilityRuleService, this statement no longer valid                    
        // if (!isValidState)
        // {
        //     Debug.Log($"[Pelican] Tried to eat in invalid state: {gameManager.gameState}");
        //     return;
        // }

        // Pelican must be on their own side of the court -> NOW handled in BirdAbilityRuleService
        // if (!IsOnOwnSide())
        // {
        //     Debug.Log("[Pelican] Tried to eat from enemy side of court.");
        //     return;
        // }

        // Pelican must be close enough to the ball
        if (!BallInEatRange())
        {
            Debug.Log("[Pelican] Ball is too far away to eat.");
            return;
        }

        // Store what state we ate in so release knows what hit to register
        stateWhenEaten = GameManager.Instance.gameState;

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);

        // Play defensive sound
        AudioManager.PlayBirdSound(BirdType.PELICAN, SoundType.DEFENSIVE, 1.0f);

        // Trigger defensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
        {
            myBallInteract.animator.SetTrigger("DefensiveAbility");
        }

        BallManager.Instance.gameObject.SetActive(false);
        isBallEaten = true;

        holdCoroutine = StartCoroutine(HoldTime());
    }

    private void SpitBall()
    {
        // Use the stored reference — NOT StopCoroutine(HoldTime()) which creates a new instance
        if (holdCoroutine != null) StopCoroutine(holdCoroutine);
        ReleaseBall();
    }

    private void ReleaseBall()
    {
        if (!isBallEaten) return;
        isBallEaten = false;
        StartCoroutine(ReleaseBallCoroutine());
    }

    private IEnumerator ReleaseBallCoroutine()
    {
        // Position and re-enable the ball first
        BallManager.Instance.gameObject.transform.position = transform.position + new Vector3(0, 1f, 0);
        BallManager.Instance.gameObject.SetActive(true);

        // Wait one frame so physics and state catch up before registering the hit
        yield return null;

        // Advance to the next hit based on what state the ball was eaten in
        switch (stateWhenEaten)
        {
            case GameManager.GameState.Served:
                ballInteract.ServeBall();  // eaten at beginning of point -> release as serve
                break;
            case GameManager.GameState.Spiked:
                ballInteract.BumpBall();   // eaten while ball incoming from enemy -> release as bump
                break;
            case GameManager.GameState.Bumped:
                ballInteract.SetBall();    // eaten after ally bumped -> release as set
                break;
        }
    }

    public IEnumerator HoldTime()
    {
        yield return new WaitForSeconds(holdLength);
        ReleaseBall();
    }
}