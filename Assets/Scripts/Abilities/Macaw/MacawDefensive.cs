using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Repeat After You - Will randomly mimic any ability from one of the birds on the court;
///  the defense ability icon will change to the icon of the mimicked bird
/// </summary>
public class MacawDefensive : BirdAbility
{
    [SerializeField] private float cooldown = 20f;
    [SerializeField] private float mimicDuration = 15f;

    private bool onCooldown = false;
    private List<BirdAbility> playerAbilities = new();
    private const int playerCount = 4;
    private BirdAbility currentAbility;

    void Start()
    {
        // get all the player abilities on the court (except for Macaw's) and add them to the list of abilities to mimic
        for (int i = 0; i < playerCount; i++)
        {
            GameObject player = i switch
            {
                0 => GameManager.Instance.leftPlayer1,
                1 => GameManager.Instance.leftPlayer2,
                2 => GameManager.Instance.rightPlayer1,
                3 => GameManager.Instance.rightPlayer2,
                _ => null
            };

            if (player != null && player != this.gameObject)
                playerAbilities.AddRange(player.GetComponents<BirdAbility>());
        }

        currentAbility = PrimeRandomAbility();
    }

    public void OnDefensiveAbility()
    {
        
    }

    // called every mimicDuration so long as the abilty hasnt been used
    private BirdAbility PrimeRandomAbility()
    {
        return playerAbilities[Random.Range(0, playerAbilities.Count)];
    }
}
