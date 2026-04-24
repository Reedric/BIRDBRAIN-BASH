using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class CrowOffensive : BirdAbility {
    public float cooldown = 10f;
    public float timeEnemiesAreImpacted = 3f;
    public Animator animator; // Assign in inspector

    private bool onCooldown = false;
    private PlayerInput input; // Input for the player

    void Start()
    {
        input = GetComponent<PlayerInput>();
    }

    void Update()
    {
        if (!onCooldown && input.actions.FindAction("Offensive Ability").WasPressedThisFrame()
            && CanUseAbilities() && PointInProgress())
        {
            CrowAbility();
        }
    }

    public void CrowAbility() 
    {
        if (onCooldown)
        {
            Debug.Log("The crow is on cooldown and cannot activate its ability");
            return;
        }

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, cooldown);

        // Play animation
        if (animator != null)
            animator.SetTrigger("OffensiveAbility"); // Make sure to have a trigger

        // Play sound effect using AudioManager
        AudioManager.PlayBirdSound(BirdType.CROW, SoundType.OFFENSIVE, 1.0f);

        StartCoroutine(DisableEnemies());
        StartCoroutine(Cooldown());
    }

    IEnumerator DisableEnemies()
    {
        // Determine which birds are on other team
        List<BirdAbility> enemyAbilities = new List<BirdAbility>();
        GameManager gameManager = GameManager.Instance;
        if (gameManager.leftPlayer1 == gameObject || gameManager.leftPlayer2 == gameObject)
        {
            enemyAbilities.AddRange(gameManager.rightPlayer1.GetComponents<BirdAbility>());
            enemyAbilities.AddRange(gameManager.rightPlayer2.GetComponents<BirdAbility>());
        }
        else
        {
            enemyAbilities.AddRange(gameManager.leftPlayer1.GetComponents<BirdAbility>());
            enemyAbilities.AddRange(gameManager.leftPlayer2.GetComponents<BirdAbility>());
        }

        if (_onLeft)
        {
            opponents.Add(gameManager.rightPlayer1);
            opponents.Add(gameManager.rightPlayer2);
        } else
        {
            opponents.Add(gameManager.leftPlayer1);
            opponents.Add(gameManager.leftPlayer2);
        }

        Debug.Log(enemyAbilities.Count);

        // Disable all the enemies abilities
        for (int i = 0; i < enemyAbilities.Count; i++)
        {
            // ducky: what the hell did I just make for this ostrich check
            BallInteract birdPlayer = opponents[i].GetComponent<BallInteract>();
            BirdType birdType;
            if (birdPlayer == null)
            {
                birdType = opponents[i].GetComponent<AIBehavior>().GetBirdType();
            }
            else
            {
                birdType = opponents[i].GetComponent<BallInteract>().GetBirdType();
            }
            if (birdType != BirdType.OSTRICH)
            {
                enemyAbilities[i].DisableAbilities(true);
                Debug.Log(enemyAbilities[i]);
            }
        }

        // Wait for ability to end
        yield return new WaitForSeconds(timeEnemiesAreImpacted);

        // Enable all the enemies abilities
        foreach (BirdAbility enemy in enemyAbilities) 
        {
            enemy.DisableAbilities(false);
        }
    }

    private IEnumerator Cooldown() 
    {
        onCooldown = true;
        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }
}
