using System;
using System.Collections;
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
    public int debuffWindowLength = 20;   // Seconds after a score the player can trigger the debuff

    private bool _debuffWindow = false;
    private PlayerInput playerInput;

    void Start()
    {
        playerInput = GetComponent<PlayerInput>();
        _onLeft = GetComponent<BallInteract>().onLeft;
        EventManager.SubscribeScore(OnScore);
        GetComponent<CharacterMovement>().controlMovement(true, true);
    }

    void Update()
    {
        if (_debuffWindow && playerInput.actions.FindAction("Offensive Ability").WasPressedThisFrame() && CanMock())
        {
            DebuffEnemy();
        }
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
                opponentAnimator.SetTrigger("OffensiveAbility");
        }

        // Trigger animation on this player
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
            myBallInteract.animator.SetTrigger("OffensiveAbility");

        _debuffWindow = false;

        // Play offensive sound
        AudioManager.PlayBirdSound(BirdType.SEAGULL, SoundType.OFFENSIVE, 1.0f);

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, 5);
    }

    public bool OnScore(bool leftScored)
    {
        if ((leftScored && _onLeft) || (!leftScored && !_onLeft))
        {
            StartCoroutine(WindowTimer());
            return true;
        }

        return false;
    }

    private IEnumerator WindowTimer()
    {
        _debuffWindow = true;
        yield return new WaitForSeconds(debuffWindowLength);
        _debuffWindow = false;
    }

    private bool CanMock()
    {
        // If abilities are disabled for the seagull, cannot mock
        if (!CanUseAbilities()) return false;

        // If the point hasn't just ended or point not about to start return false
        if (!gameManager.gameState.Equals(GameManager.GameState.PointStart) && gameManager.gameState.Equals(GameManager.GameState.PointEnd))
        {
            return false;
        }

        // Get which side just scored the point
        bool leftJustScored = ScoreManager.Instance.side1ServeIndicator.activeInHierarchy;

        // Return true if they equal
        return _onLeft == leftJustScored;
    }
}