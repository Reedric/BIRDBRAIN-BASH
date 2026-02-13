using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ToucanDefensive : MonoBehaviour
{
    [SerializeField]
    public float cooldown; // Cooldown in seconds
    [SerializeField]
    public int buffAmount; // Amount the ability increases ally's stats
    [SerializeField]
    public int buffLength; // Amount of time in seconds the buff lasts
    public GameManager gameManager;
    private bool _onLeft;

    private bool onCooldown = false;

    void Update()
    {
        // If pressesd defensive ability button, activate ability
        if (InputSystem.actions.FindAction("Defensive Ability").WasPressedThisFrame())
        {
            TouCanDoIt();
        }
    }
    
    public void Start()
    {
        _onLeft = GetComponent<BallInteract>().onLeft;
    }

    public void TouCanDoIt()
    {
        // Only runs if not on cooldown
        if (!onCooldown)
        {
            Debug.Log("Tou-Can Do It!!! :D");

            GameObject teammate = null;

            // Finds the teammate to buff
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

            // Applies buff
            teammate.GetComponent<CharacterMovement>().BuffStats(buffAmount, buffLength);
            
            StartCoroutine(Cooldown());
        } else
        {
            Debug.Log("On Cooldown :C");
        }
    }

    // Cools down cooldown seconds
    public IEnumerator Cooldown()
    {
        onCooldown = true;
        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }

}
