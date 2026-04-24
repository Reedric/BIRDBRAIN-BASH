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
            StartCoroutine(StunOpponents());
            StartCoroutine(CooldownRoutine());
        }
    }

    private IEnumerator StunOpponents()
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

        
        foreach (GameObject opponent in opponents)
        {
            if (opponent == null) continue;

            BirdAbility[] abilities = opponent.GetComponents<BirdAbility>();
            if (abilities.Length > 0)
            {
                foreach (BirdAbility ability in abilities)
                {
                    ability.DisableAbilities(true);
                }
                opponent.GetComponent<CharacterMovement>().controlMovement(false, false);

            }
            else
            {
                opponent.GetComponent<AIBehavior>().enabled = false;
            }
        }

        yield return new WaitForSeconds(stunDuration);

        
        foreach (GameObject opponent in opponents)
        {
            if (opponent == null) continue;

            BirdAbility[] abilities = opponent.GetComponents<BirdAbility>();
            if (abilities.Length > 0)
            {
                foreach (BirdAbility ability in abilities)
                {
                    ability.DisableAbilities(false);
                }
                opponent.GetComponent<CharacterMovement>().controlMovement(true, true);

            }
            else
            {
                opponent.GetComponent<AIBehavior>().enabled = true;
            }
        }
    }

    private IEnumerator CooldownRoutine()
    {
        onCooldown = true;
        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }
}