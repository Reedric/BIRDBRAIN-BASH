using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Repeat After You - Will randomly mimic any ability from one of the birds on the court;
///  the defense ability icon will change to the icon of the mimicked bird
/// </summary>
public class MacawDefensive : BirdAbility
{
    [SerializeField] private float mimicDuration = 15f;

    private List<BirdAbility> playerAbilities = new();
    private const int playerCount = 4;
    private BirdAbility currentAbility;
    private float mimicTimer;

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

        PrimeRandomAbility();
    }

    override protected void Activate()
    {
        if (currentAbility == null) return;

        currentAbility.TryActivate();
    }

    // TODO: find better way to do this to not clog update
    void Update()
    {
        if (currentAbility == null) return;

        mimicTimer += Time.deltaTime;
        if (mimicTimer >= mimicDuration)
        {
            PrimeRandomAbility();
            mimicTimer = 0f;
        }
    }

    // called every mimicDuration so long as the abilty hasnt been used
    private void PrimeRandomAbility()
    {
        currentAbility = playerAbilities[Random.Range(0, playerAbilities.Count)];
    }
}
