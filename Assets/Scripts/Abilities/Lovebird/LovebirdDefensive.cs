using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(Rigidbody))]
public class LovebirdDefensive : BirdAbility
{
    [Header("Romantic Rush")]
    public float cooldown = 6.0f;
    public float dashSpeed = 18.0f;
    public float dashToDistance = 2.0f; // How close Loverbird dashes to Ally
    private bool abilityReady = true;
    private Rigidbody rb;
    private PlayerInput playerInput;

    // Checks for Game Manager on Start
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();
    }

    // Finds which GameObject is the Ally to player
    private GameObject GetAlly()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameObject == gameManager.leftPlayer1)
        {
            return gameManager.leftPlayer2;
        }
        else if (gameObject == gameManager.leftPlayer2)
        {
            return gameManager.leftPlayer1;
        }
        else if (gameObject == gameManager.rightPlayer1)
        {
            return gameManager.rightPlayer2;
        }
        else if (gameObject == gameManager.rightPlayer2)
        {
            return gameManager.rightPlayer1;
        }
        else
        {
            Debug.Log("Player not found!");
            return null;
        }
    }

    public IEnumerator RomanticRush()
    {
        GameObject ally = GetAlly();
        if (ally == null)
        {
            yield break;
        }

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, cooldown);

        // Play defensive sound
        AudioManager.PlayBirdSound(BirdType.LOVEBIRD, SoundType.DEFENSIVE, 1.0f);

        // Trigger defensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
        {
            myBallInteract.animator.SetTrigger("DefensiveAbility");
        }

        Debug.Log("Dashing towards ally...");
        // Continues moving as long as the distance is within 1 unit
        while (Vector3.Distance(transform.position, ally.transform.position) > dashToDistance)
        {
            Vector3 direction = (ally.transform.position - transform.position).normalized;
            rb.MovePosition(transform.position + dashSpeed * Time.deltaTime * direction);
            yield return null;
        }
        StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        yield return new WaitForSeconds(cooldown);
        abilityReady = true;
    }

    void Update()
    {
        if (playerInput.actions.FindAction("Defensive Ability").WasPressedThisFrame() && abilityReady 
            && PointInProgress() && CanUseAbilities())
        {
            StartCoroutine(RomanticRush());
            abilityReady = false;
        }
    }
}
