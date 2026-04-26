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
        if (GameManager.Instance.gameState == GameManager.GameState.Set)
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
}
