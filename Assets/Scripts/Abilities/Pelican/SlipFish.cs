using System.Collections.Generic;
using UnityEngine;

// Behavior for the fish projectile spit out by the pelican's offensive ability (Slip Fish)
// The fish will cause the opponent to slip if they come into contact with it, and will disappear after 15 seconds
public class SlipFish : MonoBehaviour
{
    public GameObject pelican;
    // Set by PelicanOffensive so we know which side the pelican is on,
    // letting us pass the correct opponentIsOnLeft value to BuffsDebuffs.
    public bool pelicanIsOnLeft;

    [SerializeField]
    private float slipDuration = 3f;
    private readonly HashSet<GameObject> affectedPlayers = new();

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") && ValidSlipper(collision.gameObject))
        {
            // Run on the persistent singleton so fish destruction can't kill the coroutine
            BuffsDebuffs.Instance.StartCoroutine(SlipEffect(collision.gameObject));
        }
    }

    private bool ValidSlipper(GameObject other)
    {
        // If other is the pelican, return false
        if (other == pelican) return false;

        // Determine if the other is an enemy of the pelican
        GameManager gameManager = GameManager.Instance;

        // Fixed: original checked leftPlayer1 twice instead of leftPlayer1 || leftPlayer2
        if (pelican == gameManager.leftPlayer1 || pelican == gameManager.leftPlayer2)
        {
            return other == gameManager.rightPlayer1 || other == gameManager.rightPlayer2;
        }
        else
        {
            return other == gameManager.leftPlayer1 || other == gameManager.leftPlayer2;
        }
    }

    private System.Collections.IEnumerator SlipEffect(GameObject opponent)
    {
        // Check if the opponent has already been affected by this fish to prevent multiple slips
        if (affectedPlayers.Contains(opponent)) yield break;
        affectedPlayers.Add(opponent);

        // Apply stun VFX and audio via BuffsDebuffs.
        BuffsDebuffs.Instance.ApplyEffect(
            BuffsDebuffs.EffectType.Stun,
            opponent,
            slipDuration,
            !pelicanIsOnLeft
        );

        yield return new WaitForSeconds(slipDuration);

        affectedPlayers.Remove(opponent);
    }
}