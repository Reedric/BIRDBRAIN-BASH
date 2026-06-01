using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class CrowOffensive : BirdAbility {
    public float timeEnemiesAreImpacted = 3f;
    public Animator animator; // Assign in inspector

    private BallInteract ballInteract;

    void Start()
    {
        ballInteract = GetComponent<BallInteract>();
    }

    protected override void Activate()
    {
        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, _cooldownTime);

        // Play animation
        if (animator != null)
            animator.SetTrigger("OffensiveAbility");

        // Play sound effect using AudioManager
        AudioManager.PlayBirdSound(BirdType.CROW, SoundType.OFFENSIVE, 1.0f);

        SilenceEnemies();
    }

    void SilenceEnemies()
    {
        // Determine which birds are on other team
        List<GameObject> opponents = new List<GameObject>();
        GameManager gameManager = GameManager.Instance;

        if (ballInteract.onLeft)
        {
            opponents.Add(gameManager.rightPlayer1);
            opponents.Add(gameManager.rightPlayer2);
        }
        else
        {
            opponents.Add(gameManager.leftPlayer1);
            opponents.Add(gameManager.leftPlayer2);
        }

        // Opponents are always on the other side of the crow
        bool opponentIsOnLeft = !ballInteract.onLeft;

        // Disable all the enemies abilities
        foreach (GameObject opponent in opponents)
        {
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
}
