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
    [SerializeField] private float cooldown = 20f;
    [SerializeField] private float dashSpeed = 10f;

    private bool onCooldown = false;
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

    public void OnDefensiveAbility()
    {
        if (!onCooldown)
        {
            // Debug.Log("Pukeko Defensive Ability Activated: Playing Dirty");
            onCooldown = true;
            StartCoroutine(PlayingDirty());
        }
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

        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }

    // Detect collision with the ball during the dash
    private void OnCollisionEnter(Collision collision)
    {
        if (isDashing && collision.gameObject == BallManager.Instance.gameObject)
            ballInteract.BlockBall();
    }
}
