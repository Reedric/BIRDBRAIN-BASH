using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class CrowDefensive : BirdAbility
{
    public int buffAmount; // Amount the ability increases ally's stats
    public int buffLength; // Amount of time in seconds the buff lasts
    private int coinCount = 0; // Counter for coins collected
    private int oldScore = 0; // Score of the last round
    public GameObject coin; // Coin item
    private List<GameObject> coins = new List<GameObject>(); // List to keep track of spawned coins
    private Vector3 randomSpawnPosition1;
    private Vector3 randomSpawnPosition2;
    private Vector3 randomSpawnPosition3;
    
    void Update()
    {
        if (ScoreManager.Instance == null || GameManager.Instance == null) return;

        // Check if coins exist and if score has changed since last round, if so reset coin count
        if (oldScore != (ScoreManager.Instance.side1Score + ScoreManager.Instance.side2Score))
        {
            oldScore = ScoreManager.Instance.side1Score + ScoreManager.Instance.side2Score;
            // Do not let the coins carry over into the next round (if they exist)
            ClearCurrCoins();
        }
        // Coins needed to activate stat buff
        if (coinCount == 3) 
        {
            TryActivate(AbilitySlot.Defensive);
        }
    }
    protected override bool Activate()
    {
        // If collected all coins, activate ability
        if (coinCount == 3)
        {
            // Also forgets the collected coins. Left in the list, they'd make it look like
            // coins were still out, and the ability couldn't be used again until somebody scored.
            ClearCurrCoins();
            CrowDefBuff();
            return true;
        }

        // If coins already on the court, do not allow respawning of coins
        coins.RemoveAll(c => c == null);
        if (coins.Count > 0) return false;

        // Clear coins from the court if they exist from a previous ability use
        ClearCurrCoins();

        // Check which side the player is on and spawn coins on that side of the court
        GameManager gameManager = GameManager.Instance;
        if (gameObject == gameManager.leftPlayer1 || gameObject == gameManager.leftPlayer2) 
        {
            randomSpawnPosition1 = new Vector3(Random.Range(-8, -0.5f), .5f, Random.Range(-4, 4));
            randomSpawnPosition2 = new Vector3(Random.Range(-8, -0.5f), .5f, Random.Range(-4, 4));
            randomSpawnPosition3 = new Vector3(Random.Range(-8, -0.5f), .5f, Random.Range(-4, 4));
        } 
        else if (gameObject == gameManager.rightPlayer1 || gameObject == gameManager.rightPlayer2)
        {
            randomSpawnPosition1 = new Vector3(Random.Range(.5f, 8), .5f, Random.Range(-4, 4));
            randomSpawnPosition2 = new Vector3(Random.Range(.5f, 8), .5f, Random.Range(-4, 4));
            randomSpawnPosition3 = new Vector3(Random.Range(.5f, 8), .5f, Random.Range(-4, 4));
        }

        // Spawn three coins randomly on the court
        GameObject coin1 = NetworkFx.Instantiate(coin, randomSpawnPosition1, Quaternion.identity);
        GameObject coin2 = NetworkFx.Instantiate(coin, randomSpawnPosition2, Quaternion.identity);
        GameObject coin3 = NetworkFx.Instantiate(coin, randomSpawnPosition3, Quaternion.identity);
        coins.Add(coin1);
        coins.Add(coin2);
        coins.Add(coin3);

        // Return that the ability has not officially been used
        return false;
    }

    // Call stat buff
    void CrowDefBuff()
    {
        Debug.Log("Buff activated");
        BuffsDebuffs.Instance.ApplyEffect(BuffsDebuffs.EffectType.Buff, gameObject, buffLength,
            gameObject == GameManager.Instance.leftPlayer1 || gameObject == GameManager.Instance.leftPlayer2);
        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);
    }

    // Clear current coins on the field
    void ClearCurrCoins() 
    {
        foreach (GameObject c in coins)
            {
                if (c != null) 
                {
                    Destroy(c);
                }
            }
        coins.Clear();
        coinCount = 0;
    }

    // Coin collision detection
    void OnTriggerEnter(Collider other)
    {
        Debug.Log(coinCount);
        if (other.gameObject.tag == "Coin")
        {
            Destroy(other.gameObject);
            coinCount++;

            // Play sound effect using AudioManager (only for coins, not every trigger the crow touches)
            AudioManager.PlayBirdSound(BirdType.CROW, SoundType.DEFENSIVE, 1.0f);
        }
    }
}
