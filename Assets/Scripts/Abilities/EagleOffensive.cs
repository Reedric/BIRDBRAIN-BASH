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

    private bool onCooldown = false;
    private PlayerInput input;

    private void Awake()
    {
        input = GetComponent<PlayerInput>();
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

            BirdAbility ability = opponent.GetComponent<BirdAbility>();
            if (ability != null)
            {
                ability.abilitiesDisabled(true);
            }
        }

        yield return new WaitForSeconds(stunDuration);

        
        foreach (GameObject opponent in opponents)
        {
            if (opponent == null) continue;

            BirdAbility ability = opponent.GetComponent<BirdAbility>();
            if (ability != null)
            {
                ability.abilitiesDisabled(false);
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