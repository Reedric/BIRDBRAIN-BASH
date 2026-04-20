using System.Collections.Generic;
using UnityEngine;


// Behavior for the fish projectile spit out by the pelican's offensive ability (Slip Fish)
// The fish will cause the opponent to slip if they come into contact with it, and will disappear after 15 seconds
public class SlipFish : MonoBehaviour
{
    public GameObject pelican;
    [SerializeField]
    private float slipDuration = 3f;
    private readonly HashSet<GameObject> affectedPlayers = new(); // Keeps track of players already affected

    void OnCollisionEnter(Collision collision)
    {
        // Check if the collided object is the opponent (you can use tags or layers to identify the opponent)
        if (collision.gameObject.CompareTag("Player") && ValidSlipper(collision.gameObject)) // Ensure it's not the pelican itself
        {
            
            // If we decide to make a slip animation, play it here (TO BE IMPLEMENTED)
            StartCoroutine(SlipEffect(collision.gameObject));
        }
    }

    private bool ValidSlipper(GameObject other)
    {
        // If other is the pelican, return false
        if (other == pelican) return false;

        // Determine if the other is an enemy of the pelican
        GameManager gameManager = GameManager.Instance;
        if (pelican == gameManager.leftPlayer1 || pelican == gameManager.leftPlayer1)
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

        RagdollManager ragdollManager = opponent.GetComponent<RagdollManager>();
        CharacterMovement characterMovement = opponent.GetComponent<CharacterMovement>();

        if (ragdollManager != null) ragdollManager.ActivateRagdoll();    
        if (characterMovement != null) characterMovement.enabled = false;
        Debug.Log("Opponent slipped!");

        yield return new WaitForSeconds(slipDuration);

        if (ragdollManager != null) ragdollManager.DeactivateRagdoll();
        if (characterMovement != null) characterMovement.enabled = true;
        affectedPlayers.Remove(opponent);
        Debug.Log("Slip effect ended!");
    }
}
