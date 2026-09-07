using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(BallInteract))]
public class ToucanOffensive : BirdAbility
{
    // Toucan now uses the same armed functionality as Penguin/Phoenix.
    public override bool RequiresSpikeToActivate => true;

    protected override bool Activate()
    {
        // The normal spike has already happened.
        // The armed ability now makes that spike unblockable.
        AudioManager.PlayBirdSound(BirdType.TOUCAN, SoundType.OFFENSIVE, 1.0f);

        BallManager.Instance.unblockableOwner = gameObject;

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, _cooldownTime);

        return true;
    }

    // Activate the ability: next spike becomes unblockable
    public void TacoTocoToca()
    {
        // Play offensive sound
        AudioManager.PlayBirdSound(BirdType.TOUCAN, SoundType.OFFENSIVE, 1.0f);

        // Set the unblockable owner of the ball to this player
        BallManager.Instance.unblockableOwner = gameObject;

        // Spike the ball
        GetComponent<BallInteract>().SpikeBall();

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, _cooldownTime);
    }

    private bool CanSpike()
    {
        // If the toucan was the last one to hit, they cannot spike
        if (GameManager.Instance.lastHit == gameObject) return false;

        // If the ball is not on their side of the court, they cannot spike
        if (transform.position.x * BallManager.Instance.transform.position.x < 0) return false;

        // If the ball has just been set or bumped, then the toucan can hit the ball, otherwise, illegal state
        return GameManager.Instance.gameState == GameManager.GameState.Bumped
            || GameManager.Instance.gameState == GameManager.GameState.Set;
    }
}