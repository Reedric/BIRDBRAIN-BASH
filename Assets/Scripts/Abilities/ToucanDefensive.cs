using System.Collections;
using UnityEngine;

public class ToucanDefensive : MonoBehaviour
{

    [SerializeField]
    public float cooldown; // Cooldown in seconds
    [SerializeField]
    public int buffAmount; // Amount the ability increases ally's stats
    [SerializeField]
    public int buffLength; // Amount of time in seconds the buff lasts
    public GameManager gameManager;

    private bool onCooldown = false;

    public void TouCanDoIt()
    {
        if (!onCooldown)
        {
            Debug.Log("Tou-Can Do It!!! :D");

            GameObject teammate = null;
            GameObject rightPlayer1 = gameManager.rightPlayer1;
            GameObject rightPlayer2 = gameManager.rightPlayer2;

            if (rightPlayer1 != this)
            {
                teammate = rightPlayer1;
            } else
            {
                teammate = rightPlayer2;
            }

            teammate.GetComponent<CharacterMovement>().BuffStats(buffAmount, buffLength);
            
            StartCoroutine(Cooldown());
        } else
        {
            Debug.Log("On Cooldown :C");
        }
    }

    public IEnumerator Cooldown()
    {
        onCooldown = true;
        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }

}
