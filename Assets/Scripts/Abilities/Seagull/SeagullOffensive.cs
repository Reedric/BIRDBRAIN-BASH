using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BallInteract))]
[RequireComponent(typeof(CharacterMovement))]
[RequireComponent(typeof(PlayerInput))]
public class SeagullOffensive : BirdAbility
{
    public int debuffLength = 5;          // Length of debuff in seconds
    public int debuffAmount = 1;          // Amount the debuff will DECREASE stats

    private bool _onLeft;

    void Start()
    {
        _onLeft = GetComponent<BallInteract>().onLeft;
        GetComponent<CharacterMovement>().controlMovement(true, true);
    }

    override protected bool Activate()
    {
        DebuffEnemy();

        // Ability successfully activated
        return true;
    }

    public void DebuffEnemy()
    {
        List<GameObject> opponents = new();
        if (_onLeft)
        {
            opponents.Add(GameManager.Instance.rightPlayer1);
            opponents.Add(GameManager.Instance.rightPlayer2);
        }
        else
        {
            opponents.Add(GameManager.Instance.leftPlayer1);
            opponents.Add(GameManager.Instance.leftPlayer2);
        }

        // Opponents are always on the opposite side of the caster
        bool opponentIsOnLeft = !_onLeft;

        foreach (GameObject opponent in opponents)
        {
            if (opponent == null) continue;

            // Ostrich is immune to debuffs
            BallInteract birdPlayer = opponent.GetComponent<BallInteract>();
            BirdType birdType = birdPlayer != null
                ? birdPlayer.GetBirdType()
                : opponent.GetComponent<AIBehavior>().GetBirdType();

            if (birdType == BirdType.OSTRICH) continue;

            // BuffsDebuffs handles VFX, audio, stat changes, and cleanup
            BuffsDebuffs.Instance.ApplyEffect(
                BuffsDebuffs.EffectType.Debuff,
                opponent,
                debuffLength,
                opponentIsOnLeft
            );

            // Trigger debuff animation on the opponent
            CharacterMovement cm = opponent.GetComponent<CharacterMovement>();
            AIBehavior ai = opponent.GetComponent<AIBehavior>();
            Animator opponentAnimator = cm?.animator ?? ai?.animator;
            if (opponentAnimator != null)
                opponentAnimator.SetTriggerSynced("OffensiveAbility");
        }

        // Trigger animation on this player
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
            myBallInteract.animator.SetTriggerSynced("OffensiveAbility");

        // Play offensive sound
        AudioManager.PlayBirdSound(BirdType.SEAGULL, SoundType.OFFENSIVE, 1.0f);

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, _cooldownTime);
    }
}