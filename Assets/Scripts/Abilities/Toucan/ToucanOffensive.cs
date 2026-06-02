using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(BallInteract))]
public class ToucanOffensive : BirdAbility
{
    override protected void Activate()
    {
        // Offensive ability activation (Toucan): allow activation regardless of CanHit()
        if (CanSpike())
        {
            TacoTocoToca();
        }
    }
    // Activate the ability: next spike becomes unblockable
    public void TacoTocoToca()
    {

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, _cooldownTime);

        // Play defensive sound
        AudioManager.PlayBirdSound(BirdType.TOUCAN, SoundType.OFFENSIVE, 1.0f);

        // Set the unblockable owner of the ball to this player
        BallManager.Instance.unblockableOwner = gameObject;

        // Spike the ball
        GetComponent<BallInteract>().SpikeBall();
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
