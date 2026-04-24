using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class EagleOffensive : BirdAbility
{
    [Header("Ability Settings")]
    public float stunDuration = 2f;
    public float cooldown = 10f;
    public Animator animator; // Assign in inspector

    private bool onCooldown = false;
    private PlayerInput input;

    private void Awake()
    {
        input = GetComponent<PlayerInput>();
        _onLeft = GetComponent<BallInteract>().onLeft;
    }

    private void Update()
    {
        if (onCooldown) return;
        if (!CanUseAbilities()) return;
        if (!PointInProgress()) return;

        if (input.actions.FindAction("Offensive Ability").WasPressedThisFrame())
        {
            StunOpponents();
            StartCoroutine(CooldownRoutine());
        }
    }

    private void StunOpponents()
    {
        GameManager gameManager = GameManager.Instance;

        opponents.Clear();

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, cooldown);

        // Trigger offensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
        {
            myBallInteract.animator.SetTrigger("OffensiveAbility");
        }

        // Play sound effect using AudioManager
        AudioManager.PlayBirdSound(BirdType.EAGLE, SoundType.OFFENSIVE, 1.0f);

        if (_onLeft)
        {
            opponents.Add(gameManager.rightPlayer1);
            opponents.Add(gameManager.rightPlayer2);
        }
        else
        {
            opponents.Add(gameManager.leftPlayer1);
            opponents.Add(gameManager.leftPlayer2);
        }

        // Opponents are always on the opposite side of the caster
        bool opponentIsOnLeft = !_onLeft;

        foreach (GameObject opponent in opponents)
        {
            if (opponent == null) continue;

            // BuffsDebuffs handles everything: VFX, audio, disabling movement +
            // abilities (for both player and AI), and re-enabling after stunDuration.
            BuffsDebuffs.Instance.ApplyEffect(
                BuffsDebuffs.EffectType.Stun,
                opponent,
                stunDuration,
                opponentIsOnLeft
            );
        }
    }

    private IEnumerator CooldownRoutine()
    {
        onCooldown = true;
        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }
}