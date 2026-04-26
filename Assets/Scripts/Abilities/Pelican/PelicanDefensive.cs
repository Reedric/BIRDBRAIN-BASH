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
    private PlayerInput playerInput;

    public void Start()
    {
        ballInteract = GetComponent<BallInteract>();
        playerInput = GetComponent<PlayerInput>();
    }

    override protected void Activate()
    {
        EatTheBall();

        if (isBallEaten && playerInput.actions.FindAction("Serve").WasPressedThisFrame())
        {
            BallManager.Instance.gameObject.SetActive(true);
            isBallEaten = false;
        }

        if (isBallEaten)
        {
            BallManager.Instance.gameObject.transform.position = transform.position + new Vector3(0, 1f, 0);
        }
    }

    public void EatTheBall()
    {
        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);
        
        GameManager gameManager = GameManager.Instance;
        bool validState = gameManager.gameState == GameManager.GameState.PointStart;
        if (validState && gameManager.server == gameObject)
        {
            // Play defensive sound
            AudioManager.PlayBirdSound(BirdType.PELICAN, SoundType.DEFENSIVE, 1.0f);

            // Trigger defensive ability animation if animator exists
            var myBallInteract = GetComponent<BallInteract>();
            if (myBallInteract != null && myBallInteract.animator != null)
            {
                myBallInteract.animator.SetTrigger("DefensiveAbility");
            }

            ballInteract.ServeBall();
            BallManager.Instance.gameObject.SetActive(false);
            isBallEaten = true;

            StartCoroutine(HoldTime());
        }
    }

    public IEnumerator HoldTime()
    {
        yield return new WaitForSeconds(holdLength);
        BallManager.Instance.gameObject.SetActive(true);
        isBallEaten = false;
        ballInteract.ServeBall();
    }
}