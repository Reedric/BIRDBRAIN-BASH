using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class CrowOffensive : BirdAbility
{
    public float cooldown = 10f;
    public float timeEnemiesAreImpacted = 3f;
    public Animator animator; // Assign in inspector

    private bool onCooldown = false;
    private PlayerInput input;

    void Start()
    {
        input = GetComponent<PlayerInput>();
        _onLeft = GetComponent<BallInteract>().onLeft;
    }

    void Update()
    {
        if (!onCooldown && input.actions.FindAction("Offensive Ability").WasPressedThisFrame()
            && CanUseAbilities() && PointInProgress())
        {
            CrowAbility();
        }
    }

    public void CrowAbility()
    {
        if (onCooldown)
        {
            Debug.Log("The crow is on cooldown and cannot activate its ability");
            return;
        }

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, cooldown);

        // Play animation
        if (animator != null)
            animator.SetTrigger("OffensiveAbility");

        // Play sound effect using AudioManager
        AudioManager.PlayBirdSound(BirdType.CROW, SoundType.OFFENSIVE, 1.0f);

        SilenceEnemies();
        StartCoroutine(Cooldown());
    }

    private void SilenceEnemies()
    {
        opponents.Clear();

        GameManager gameManager = GameManager.Instance;

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

            // Ostrich is immune to silence
            BallInteract birdPlayer = opponent.GetComponent<BallInteract>();
            BirdType birdType = birdPlayer != null
                ? birdPlayer.GetBirdType()
                : opponent.GetComponent<AIBehavior>().GetBirdType();

            if (birdType == BirdType.OSTRICH) continue;

            // BuffsDebuffs handles everything: VFX, audio, disabling abilities
            // (for both player and AI), and re-enabling after timeEnemiesAreImpacted.
            BuffsDebuffs.Instance.ApplyEffect(
                BuffsDebuffs.EffectType.Silence,
                opponent,
                timeEnemiesAreImpacted,
                opponentIsOnLeft
            );
        }
    }

    private IEnumerator Cooldown()
    {
        onCooldown = true;
        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }
}