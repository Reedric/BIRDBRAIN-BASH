using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(BallInteract))]
public class ToucanOffensive : BirdAbility
{
    public float cooldown = 10f; // Cooldown in seconds (TBA)
    
    private bool onCooldown = false;
    private PlayerInput playerInput; // Input for this player
    void Start()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    void Update()
    {
        // Offensive ability activation (Toucan): allow activation regardless of CanHit()
        if (!onCooldown && playerInput.actions.FindAction("Offensive Ability").WasPressedThisFrame()
            && CanUseAbilities() && GameManager.Instance.gameState == GameManager.GameState.Set
            && GetComponent<BallInteract>().IsPlayerNearBall())
        {
            TacoTocoToca();
        }
    }
    // Activate the ability: next spike becomes unblockable
    public void TacoTocoToca()
    {

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, cooldown);

        // Play defensive sound
        AudioManager.PlayBirdSound(BirdType.TOUCAN, SoundType.OFFENSIVE, 1.0f);

        // Set the unblockable owner of the ball to this player
        BallManager.Instance.unblockableOwner = gameObject;

        // Spike the ball
        GetComponent<BallInteract>().SpikeBall();

        StartCoroutine(Cooldown());
    }

    private IEnumerator Cooldown()
    {
        onCooldown = true;
        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }
}
