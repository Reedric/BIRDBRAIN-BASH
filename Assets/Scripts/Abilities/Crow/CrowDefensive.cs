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
    private bool buffActive = false; // If the stat buff is currently active
    private Vector3 randomSpawnPosition1;
    private Vector3 randomSpawnPosition2;
    private Vector3 randomSpawnPosition3;
    
    void Update()
    {
        // Check if coins exist and if score has changed since last round, if so reset coin count
        if (oldScore != (ScoreManager.Instance.side1Score + ScoreManager.Instance.side2Score))
        {
            oldScore = ScoreManager.Instance.side1Score + ScoreManager.Instance.side2Score;
            // Do not let the coins carry over into the next round (if they exist)
            ClearCurrCoins();
            // Do not let the buff carry over into the next round (if active)
            if (buffActive) 
            {
                GetComponent<CharacterMovement>().CancelBuffs();
                buffActive = false;
            }
        }
        // Coins needed to activate stat buff
        if (coinCount == 3) 
        {
            coinCount = 0;
            CrowDefBuff();
        }
    }
    protected override void Activate()
    {
        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);

        // Clear coins from the court if they exist from a previous ability use
        ClearCurrCoins();

        // Check which side the player is on and spawn coins on that side of the court
        GameManager gameManager = GameManager.Instance;
        if (gameObject == gameManager.leftPlayer1 || gameObject == gameManager.leftPlayer2) 
        {
            randomSpawnPosition1 = new Vector3(Random.Range(-.5f, 8), .5f, Random.Range(-4, 4));
            randomSpawnPosition2 = new Vector3(Random.Range(-.5f, 8), .5f, Random.Range(-4, 4));
            randomSpawnPosition3 = new Vector3(Random.Range(-.5f, 8), .5f, Random.Range(-4, 4));
        } 
        else if (gameObject == gameManager.rightPlayer1 || gameObject == gameManager.rightPlayer2)
        {
            randomSpawnPosition1 = new Vector3(Random.Range(.5f, 8), .5f, Random.Range(-4, 4));
            randomSpawnPosition2 = new Vector3(Random.Range(.5f, 8), .5f, Random.Range(-4, 4));
            randomSpawnPosition3 = new Vector3(Random.Range(.5f, 8), .5f, Random.Range(-4, 4));
        }
        // Spawn three coins randomly on the court
        GameObject coin1 = Instantiate(coin, randomSpawnPosition1, Quaternion.identity);
        GameObject coin2 = Instantiate(coin, randomSpawnPosition2, Quaternion.identity);
        GameObject coin3 = Instantiate(coin, randomSpawnPosition3, Quaternion.identity);
        coins.Add(coin1);
        coins.Add(coin2);
        coins.Add(coin3);
    }
    // Call stat buff
    void CrowDefBuff()
    {
        Debug.Log("Buff activated");
        GetComponent<CharacterMovement>().BuffStats(buffAmount, buffLength);
        buffActive = true;
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
        }
    }

}
