using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(BallInteract))]
[RequireComponent(typeof(CharacterMovement))]
public class ToucanDefensive : BirdAbility
{
    public int buffAmount; // Amount the ability increases ally's stats
    public int buffLength; // Amount of time in seconds the buff lasts
    private bool _onLeft;

    override protected void Activate()
    {
        TouCanDoIt();
    }
    
    public void Start()
    {
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
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, cooldownTime);

        // Play defensive sound
        AudioManager.PlayBirdSound(BirdType.TOUCAN, SoundType.HAPPY, 1.0f);

        // Trigger defensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
        {
            myBallInteract.animator.SetTrigger("DefensiveAbility");
        }
    }
}
