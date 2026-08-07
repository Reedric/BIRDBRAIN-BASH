using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BallInteract))]
[RequireComponent(typeof(PlayerInput))]
public class PhoenixDefensive : BirdAbility
{
    [Header("Revive Settings")]
    public float reviveHeightOffset = 0.4f; // How far above the contact point the ball is placed before relaunch, so it doesn't immediately re-trigger the ground collision
    public float reviveUpwardForce = 6.0f; // Upward speed given to the ball when it's popped back into play
    public Vector3 reviveHorizontalNudge = Vector3.zero; // Optional small horizontal push (e.g. toward teammates) - leave at zero for a straight pop-up

    [Header("Revive Effects")]
    public GameObject ashesEffect; // "rising from the ashes" VFX prefab played at the revive spot

    private bool _onLeft;

    void Update()
    {
        TickCooldown(Time.deltaTime);
    }

    void Start()
    {
        _onLeft = GetComponent<BallInteract>().onLeft;
    }

    void OnEnable()
    {
        // Subscribe to ScoreManager's pre-score hook so we get a shot at saving the point
        ScoreManager.InterceptPoint += TryRevive;
    }

    void OnDisable()
    {
        ScoreManager.InterceptPoint -= TryRevive;
    }

    // Phoenix's revive is fully passive - it isn't triggered by the ability button, so this is
    // never called by BirdAbility's input handling. Left as a required no-op override.
    override protected bool Activate()
    {
        return false;
    }

    // Called by ScoreManager right before it registers a ground-touch point against this bird's side.
    // Returning true consumes/saves the point; ScoreManager then skips the score increment entirely.
    private bool TryRevive(bool leftConceding, Rigidbody ballRb, Vector3 contactPoint)
    {
        Debug.Log(
            $"PHOENIX INTERCEPT: leftConceding={leftConceding}, " +
            $"phoenixOnLeft={_onLeft}, cooldown={_cooldownRemaining}");

        // Only step in when the miss is against MY side, not the opponent's
        if (leftConceding != _onLeft)
            return false;

        // Still cooling down from the last revive
        if (_cooldownRemaining > 0f)
            return false;

        Rigidbody actualBallRb = ballRb;
        if (actualBallRb == null && BallManager.Instance != null)
            actualBallRb = BallManager.Instance.gameObject.GetComponent<Rigidbody>();

        if (actualBallRb == null)
        {
            Debug.LogWarning("PhoenixDefensive: could not find the ball Rigidbody to revive.");
            return false;
        }

        BallInteract myBallInteract = GetComponent<BallInteract>();
        int playerID = myBallInteract.playerID;

        // Play defensive sound and animation
        AudioManager.PlayBirdSound(BirdType.PHOENIX, SoundType.DEFENSIVE, 1.0f);

        if (myBallInteract.animator != null)
            myBallInteract.animator.SetTrigger("DefensiveAbility");

        // Rise-from-the-ashes VFX at the spot the ball landed
        if (ashesEffect != null)
        {
            GameObject ashesInstance = Instantiate(ashesEffect, contactPoint, Quaternion.identity);
            ParticleSystem ashes = ashesInstance.GetComponent<ParticleSystem>();

            if (ashes != null)
            {
                ashes.Play();
                Destroy(ashesInstance, ashes.main.duration + ashes.main.startLifetime.constantMax);
            }
            else
            {
                Destroy(ashesInstance, 5f);
            }
        }

        // Reposition the ball straight up from where it landed - NOT sideways. Any meaningful horizontal
        // push risks carrying the ball across the net onto the opponent's court, which registers as a
        // brand new (and unintended) real point for Phoenix's own team once it lands over there.
        Vector3 revivePosition = contactPoint + Vector3.up * reviveHeightOffset;

        actualBallRb.position = revivePosition;
        actualBallRb.velocity = Vector3.zero;
        actualBallRb.angularVelocity = Vector3.zero;
        actualBallRb.useGravity = true;

        Vector3 bounceDirection = Vector3.up * reviveUpwardForce + reviveHorizontalNudge;
        actualBallRb.AddForce(bounceDirection, ForceMode.VelocityChange);

        StartCooldown(_cooldownTime);
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);

        Debug.Log("PHOENIX REVIVE: Point saved. No score awarded.");

        return true;
    }
}