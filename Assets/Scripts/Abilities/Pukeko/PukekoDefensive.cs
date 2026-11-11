using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CharacterMovement))]

/// <summary>
/// Playing Dirty - Dash towards the net and instantly block the ball if you collide.
/// </summary>
public class PukekoDefensive : BirdAbility
{
    [Header("Pukeko Defensive Settings")]
    [SerializeField] private float dashSpeed = 10f;

    private bool isDashing = false;
    private Rigidbody rb;
    private CharacterMovement characterMovement;
    private BallInteract ballInteract;
    private readonly WaitForSeconds dashDuration = new(0.5f); // arbitrarily chosen value to turn off movement for during dash

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        characterMovement = GetComponent<CharacterMovement>();
        ballInteract = GetComponent<BallInteract>();
    }

    override protected bool Activate()
    {
        StartCoroutine(PlayingDirty());

        // Ability successfully activated
        return true;
    }

    private IEnumerator PlayingDirty()
    {
        isDashing = true;
        characterMovement.controlMovement(false, false);

        // Dash towards the net
        if (transform.position.x > 0) rb.AddForce(new Vector3(-1, 0, 0) * dashSpeed, ForceMode.Impulse);
        else rb.AddForce(new Vector3(1, 0, 0) * dashSpeed, ForceMode.Impulse);
        yield return dashDuration; // Short dash duration

        isDashing = false;
        characterMovement.controlMovement(true, true);

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);
    }

    // If the pukeko gets switched off mid-dash (Phoenix turning it into a chicken), the dash never finishes, so from
    // then on it would block the ball every time it touches it
    private void OnDisable()
    {
        if (!isDashing) return;

        isDashing = false;
        if (characterMovement != null) characterMovement.controlMovement(true, true);
    }

    // Detect collision with the ball during the dash
    private void OnCollisionEnter(Collision collision)
    {
        if (!isDashing || BallManager.Instance == null || collision.gameObject != BallManager.Instance.gameObject)
            return;

        // Only an incoming attack can be blocked, and only when it's this team's turn, the same as a normal block.
        // Otherwise running into the ball would block anything, like a teammate's set or the ball on the other side.
        GameManager.GameState state = GameManager.Instance.gameState;
        if ((state == GameManager.GameState.Spiked || state == GameManager.GameState.Blocked) && ballInteract.CanHit())
            ballInteract.BlockBall();
    }
}
