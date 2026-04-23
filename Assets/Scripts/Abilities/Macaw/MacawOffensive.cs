using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]

/// <summary>
/// Flip Flap - Squawk to make enemy controls flipped for a short period of time
/// </summary>
public class MacawOffensive : BirdAbility
{
    [SerializeField] private float cooldown = 20f;
    [SerializeField] private float flipDuration = 10f; 

    private List<PlayerInput> opponentControls = new();
    private bool onCooldown = false;
    private bool onLeft;

    void Start()
    {
        onLeft = transform.position.x < 0;
        if (onLeft)
        {
            opponentControls.Add(GameManager.Instance.rightPlayer1.GetComponent<PlayerInput>());
            opponentControls.Add(GameManager.Instance.rightPlayer2.GetComponent<PlayerInput>());
        }
        else
        {
            opponentControls.Add(GameManager.Instance.leftPlayer1.GetComponent<PlayerInput>());
            opponentControls.Add(GameManager.Instance.leftPlayer2.GetComponent<PlayerInput>());
        }
    }

    public void OnOffensiveAbility()
    {
        StartCoroutine(FlipFlap());
    }

    private IEnumerator FlipFlap()
    {
        if (onCooldown || !CanUseAbilities() || !PointInProgress()) yield break;
        onCooldown = true;

        FlipControls(true);
        yield return new WaitForSeconds(flipDuration);
        FlipControls(false);

        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }

    private void FlipControls(bool shouldFlip)
    {
        string InvertProcessor = shouldFlip ? "invertVector2(invertX=true, invertY=true)" : "";

        foreach (var opponent in opponentControls)
        {
            var movement = opponent.actions["Move"];
            for (int i = 0; i < movement.bindings.Count; i++)
                movement.ApplyBindingOverride(i, new InputBinding { overrideProcessors = InvertProcessor });
        }
    }
}
