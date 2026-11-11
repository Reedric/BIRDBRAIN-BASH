using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BallInteract))]

public class LovebirdDefensive : BirdAbility
{
    [Header("Romantic Rush")]
    public float dashSpeed = 18.0f;
    public float dashToDistance = 2.0f; // How close Loverbird dashes to Ally
    [Tooltip("Longest the dash can go on for before giving up on reaching the ally.")]
    [SerializeField] private float maxDashDuration = 1.5f;
    private Rigidbody rb;

    // Checks for Game Manager on Start
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    override protected bool Activate()
    {
        StartCoroutine(RomanticRush());
        return true;
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

        // Play defensive sound
        AudioManager.PlayBirdSound(BirdType.LOVEBIRD, SoundType.DEFENSIVE, 1.0f);

        // Trigger defensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
        {
            myBallInteract.animator.SetTriggerSynced("DefensiveAbility");
        }

        Debug.Log("Dashing towards ally...");
        // Continues moving as long as the distance is within 1 unit.
        // The dash holds the lovebird in place until it arrives, so it gives up if the ally can't be reached,
        // like a Kiwi burrowed underground or a bird turned into a chicken, instead of trapping it forever.
        float dashTime = 0f;
        while (ally != null && Vector3.Distance(transform.position, ally.transform.position) > dashToDistance)
        {
            if (!ally.activeInHierarchy || !GameManager.PointInProgress() || dashTime >= maxDashDuration)
                break;

            Vector3 direction = (ally.transform.position - transform.position).normalized;
            rb.MovePosition(transform.position + dashSpeed * Time.deltaTime * direction);

            dashTime += Time.deltaTime;
            yield return null;
        }

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);
    }
}
