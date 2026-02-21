using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CrowDefensiveAbility : MonoBehaviour
{
    public float cooldownTime; // Cooldown in seconds
    public float cooldownTimer; // Timer to track cooldown
    public int buffAmount; // Amount the ability increases ally's stats
    public int buffLength; // Amount of time in seconds the buff lasts
    private int coinCount = 0; // Counter for coins collected
    private int oldScore = 0; // Score of the last round
    public GameObject coin; // Coin item
    private List<GameObject> coins = new List<GameObject>(); // List to keep track of spawned coins
    public GameObject player; // Crow object
    private bool onCooldown = false; // If the ability is currently on cooldown
    private bool buffActive = false; // If the stat buff is currently active
    private Vector3 randomSpawnPosition1;
    private Vector3 randomSpawnPosition2;
    private Vector3 randomSpawnPosition3;
    public GameManager gameManager;

    void Update()
    {
        //Check if conditions are met to activate ability
        InputAction statBuff = InputSystem.actions.FindAction("Defensive Ability");
        if (!onCooldown && statBuff.WasPressedThisFrame())
        {
            CrowDefCall();
        } else if (onCooldown && statBuff.WasPressedThisFrame())
        {
            Debug.Log("Defensive ability on cooldown (" + cooldownTimer + " seconds remaining)");
        }
        // Check if coins exist and if score has changed since last round, if so reset coin count
        if (oldScore != (gameManager.scoreManager.side1Score + gameManager.scoreManager.side2Score))
        {
            oldScore = gameManager.scoreManager.side1Score + gameManager.scoreManager.side2Score;
            // Do not let the coins carry over into the next round (if they exist)
            ClearCurrCoins();
            // Do not let the buff carry over into the next round (if active)
            if (buffActive) 
            {
                this.GetComponent<CharacterMovement>().CancelBuffs();
                buffActive = false;
            }
        }
        // Cooldown timer countdown
        if (onCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0)
            {
                onCooldown = false;
                cooldownTimer = 0;
                Debug.Log("Defensive ability is ready to use");
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
    // Call ability
    void CrowDefCall() 
    {
        // Clear coins from the court if they exist from a previous ability use
        ClearCurrCoins();

        Debug.Log("Ability activated");
        // Check which side the player is on and spawn coins on that side of the court
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
        Cooldown();
    }
    // Call stat buff
    void CrowDefBuff()
    {
        Debug.Log("Buff activated");
        this.GetComponent<CharacterMovement>().BuffStats(buffAmount, buffLength);
        buffActive = true;
        Cooldown();
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
    // Set cooldown time
    void Cooldown()
    {
        cooldownTimer = cooldownTime;
        onCooldown = true;
    }
}
