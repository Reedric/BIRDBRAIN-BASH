using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(BallInteract))]
[RequireComponent(typeof(CharacterMovement))]
public class ToucanDefensive : BirdAbility
{
    public float cooldown; // Cooldown in seconds
    public int buffAmount; // Amount the ability increases ally's stats
    public int buffLength; // Amount of time in seconds the buff lasts

    private bool onCooldown = false;
    private PlayerInput playerInput; // Input for this specific player

    void Update()
    {
        // If pressesd defensive ability button, activate ability
        if (!onCooldown && playerInput.actions.FindAction("Defensive Ability").WasPressedThisFrame()
            && CanUseAbilities() && PointInProgress())
        {
            TouCanDoIt();
        }
    }
    
    public void Start()
    {
        playerInput = GetComponent<PlayerInput>();
        _onLeft = GetComponent<BallInteract>().onLeft;
    }

    public void TouCanDoIt()
    {
        GameObject teammate;

        // Finds the teammate to buff
        GameManager gameManager = GameManager.Instance;
        if (_onLeft)
        {
            GameObject leftPlayer1 = gameManager.leftPlayer1;
            GameObject leftPlayer2 = gameManager.leftPlayer2;
            if (leftPlayer1 != this)
            {
                teammate = leftPlayer1;
            } else
            {
                teammate = leftPlayer2;
            }
        } else
        {
            GameObject rightPlayer1 = gameManager.rightPlayer1;
            GameObject rightPlayer2 = gameManager.rightPlayer2;
            if (rightPlayer1 != this)
            {
                teammate = rightPlayer1;
            } else
            {
                teammate = rightPlayer2;
            }
        }

        // Applies buff to player and teammate
        GetComponent<CharacterMovement>().BuffStats(buffAmount, buffLength);
        try // Human teammate
        {
            teammate.GetComponent<CharacterMovement>().BuffStats(buffAmount, buffLength);
        }
        catch (NullReferenceException) // AI teammate
        {
            teammate.GetComponent<AIBehavior>().BuffStats(buffAmount, buffLength);
        }
        catch (Exception) // Idk how you get here, ggs ig
        {
            Debug.LogError("Something went wrong when buffing teammate stats for Toucan Defensive...");
        }

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, cooldown);

        // Play defensive sound
        AudioManager.PlayBirdSound(BirdType.TOUCAN, SoundType.DEFENSIVE, 1.0f);
        
        StartCoroutine(Cooldown());
    }

    // Cools down cooldown seconds
    public IEnumerator Cooldown()
    {
        onCooldown = true;
        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }

}
