using System.Collections;
using UnityEngine;

public class ToucanOffensive : MonoBehaviour
{
    [SerializeField]
    public float cooldown = 10f; // Cooldown in seconds (TBA)
    
    // When true the next spike by this character will be unblockable
    [HideInInspector]
    public bool abilityActive = false;
    private bool onCooldown = false;

    public BallManager ballManager;

    // Activate the ability: next spike becomes unblockable
    public void TouCanDoIt()
    {
        if (onCooldown)
        {
            Debug.Log("Toucan Offense on cooldown");
            return;
        }

        if (ballManager == null)
        {
            Debug.LogWarning("ToucanOffensive: BallManager reference not set");
        }

        abilityActive = true;
        Debug.Log("Toucan offensive ability activated: next spike is unblockable");

        StartCoroutine(Cooldown());
    }

    private IEnumerator Cooldown()
    {
        onCooldown = true;
        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }
}
