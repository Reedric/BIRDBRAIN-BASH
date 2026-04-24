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

    private void Awake()
    {
        input = GetComponent<PlayerInput>();
        _onLeft = GetComponent<BallInteract>().onLeft;
    }

    override protected void Activate()
    {
        StartCoroutine(StunOpponents());
    }

    private IEnumerator StunOpponents()
    {
        GameManager gameManager = GameManager.Instance;

        opponents.Clear();

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, cooldownTime);

        // Play animation
        if (animator != null)
            animator.SetTrigger("OffensiveAbility"); // Make sure to have a trigger

        // Play sound effect using AudioManager
        // AudioManager.PlayBirdSound(BirdType.EAGLE, SoundType.OFFENSIVE, 1.0f);
       
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
                    ability.SetAbilitiesDisabled(true);
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
                    ability.SetAbilitiesDisabled(false);
                }
                opponent.GetComponent<CharacterMovement>().controlMovement(true, true);

            }
            else
            {
                opponent.GetComponent<AIBehavior>().enabled = true;
            }
        }
    }
}