using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(BallInteract))]
public class PelicanDefensive : BirdAbility
{
    public float cooldown; // Cooldown in seconds
    public int holdLength; // Maximum amount of time in seconds the pelican can hold the ball in its mouth
    public BallInteract ballInteract;
    private bool onCooldown = false;
    private bool isBallEaten = false;
    private PlayerInput playerInput;

    public void Start()
    {
        ballInteract = GetComponent<BallInteract>();
        playerInput = GetComponent<PlayerInput>();
    }

    void Update()
    {
        if (playerInput == null) return;

        // If pressed defensive ability button, activate ability
        if (!onCooldown && playerInput.actions.FindAction("Defensive Ability").WasPressedThisFrame() && CanUseAbilities())
        {
            EatTheBall();
        }

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
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, cooldown);
        
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

            StartCoroutine(Cooldown());
            StartCoroutine(HoldTime());
        }
    }

    

    // Cools down cooldown seconds
    public IEnumerator Cooldown()
    {
        onCooldown = true;
        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }

    public IEnumerator HoldTime()
    {
        yield return new WaitForSeconds(holdLength);
        BallManager.Instance.gameObject.SetActive(true);
        isBallEaten = false;
        ballInteract.ServeBall();
    }
}