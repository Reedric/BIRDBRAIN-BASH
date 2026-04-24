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
    [SerializeField] private float flipDuration = 10f; 

    private List<PlayerInput> opponentControls = new();
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

        //foreach (var player in opponentControls)
        //{
        //    Debug.Log(player.actions.GetInstanceID());
        //}
    }

    override protected void Activate()
    {
        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, cooldownTime);

        // Play sound effect using AudioManager
        AudioManager.PlayBirdSound(BirdType.MACAW, SoundType.OFFENSIVE, 1.0f);

        StartCoroutine(FlipFlap());
    }

    private IEnumerator FlipFlap()
    {
        FlipControls(true);
        yield return new WaitForSeconds(flipDuration);
        FlipControls(false);
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
