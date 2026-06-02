using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(BallInteract))]
public class EagleOffensive : BirdAbility
{
    [Header("Ability Settings")]
    public float stunDuration = 2f;
    public Animator animator; // Assign in inspector

    private PlayerInput input;
    private bool _onLeft;
    private List<GameObject> opponents = new();

    private void Start() // Changed this to Start from Awake as setting _onLeft in Awake caused a race condition
    {
        input = GetComponent<PlayerInput>();
        _onLeft = GetComponent<BallInteract>().onLeft;
    }

    override protected void Activate()
    {
        StunOpponents();
    }

    private void StunOpponents()
    {
        GameManager gameManager = GameManager.Instance;

        opponents.Clear();

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, _cooldownTime);

        // Trigger offensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
        {
            myBallInteract.animator.SetTrigger("OffensiveAbility");
        }

        // Play sound effect using AudioManager
        AudioManager.PlayBirdSound(BirdType.EAGLE, SoundType.OFFENSIVE, 1.0f);
        Debug.LogFormat("On Left: {0}", _onLeft);
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

            // ostrich is immune to stun!
            BallInteract birdPlayer = opponent.GetComponent<BallInteract>();
            BirdType birdType = birdPlayer != null
                ? birdPlayer.GetBirdType()
                : opponent.GetComponent<AIBehavior>().GetBirdType();

            if (birdType == BirdType.OSTRICH) continue;

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
}