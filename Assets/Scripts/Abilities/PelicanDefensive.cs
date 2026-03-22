using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PelicanDefensive : BirdAbility
{
    public float cooldown; // Cooldown in seconds
    public int holdLength; // Amount the ability increases ally's stats
    public BallInteract ballInteract;
    public GameObject ball;
    public GameManager gameManager;
    private bool onCooldown = false;
    private bool isBallEaten = false;
    private PlayerInput playerInput;

    void Update()
    {
        if (playerInput == null) return;
        // If pressed defensive ability button, activate ability
        if (!onCooldown && playerInput.actions.FindAction("Defensive Ability").WasPressedThisFrame() && canUseAbilities())
        {
            EatTheBall();
        }
        if (isBallEaten && playerInput.actions.FindAction("Serve").WasPressedThisFrame())
        {
            Debug.Log("Pelican released the ball manually.");
            if (ball != null)
            {
                ball.SetActive(true);
                isBallEaten = false;
            }
        }
        if (isBallEaten)
        {
            ball.transform.position = transform.position + new Vector3(0, 1f, 0);
        }
        
    }
    
    public void Start()
    {
        ballInteract = GetComponent<BallInteract>();
        ball = GameObject.FindGameObjectWithTag("Ball");
        playerInput = GetComponent<PlayerInput>();
    }

    public void EatTheBall()
    {
        // Only runs if not on cooldown
        if (!onCooldown)
        {
            bool validState = gameManager.gameState == GameManager.GameState.PointStart
                || gameManager.gameState == GameManager.GameState.Served
                || gameManager.gameState == GameManager.GameState.Set;
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

                Debug.Log($"Pelican has eaten the ball. State: {gameManager.gameState}");
                if (ball != null)
                {
                    ballInteract.ServeBall();
                    ball.SetActive(false);
                    isBallEaten = true;
                }
                StartCoroutine(Cooldown());
                StartCoroutine(HoldTime());
            }
            else
            {
                Debug.Log($"Pelican cannot eat the ball in state: {gameManager.gameState} or not server.");
            }
        }
        else
        {
            Debug.Log("Pelican Defensive is on cooldown!");
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
        ball.SetActive(true);
        isBallEaten = false;
        ballInteract.ServeBall();
        Debug.Log("Pelican hold length is up!");
    }
}