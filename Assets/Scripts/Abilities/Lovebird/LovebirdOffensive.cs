using System;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BallInteract))]
[RequireComponent(typeof(PlayerInput))]
public class LovebirdOffensive : BirdAbility
{
    public float DebuffLength = 4.0f; // Time in seconds the debuff lasts
    public int cooldown = 20; // Time in seconds the cooldown lasts
    public float walkSpeed = 2.0f; // How fast the opponents walk towards you
    public ParticleSystem hearts; // Hearts effect for opponents
    public float heartsOffset = 1.15f; // How much the hearts will be offset above the opponent
    private bool _onCooldown = false;
    private bool _debuffActive = false;
    private List<ParticleSystem> _hearts = new();
    private PlayerInput playerInput;

    void Start()
    {
        _onLeft = GetComponent<BallInteract>().onLeft;
        playerInput = GetComponent<PlayerInput>();
    }

    void Update()
    {
        // If offensive ability pressed, debuff enemy
        if (!_onCooldown && PointInProgress() && CanUseAbilities()
            && playerInput.actions.FindAction("Offensive Ability").WasPressedThisFrame())
        {
            DebuffEnemy();
        }

        // If the debuff is active, moves the opponents towards the net
        if (_debuffActive)
        {
            foreach (GameObject opponent in opponents)
            {
                // ducky: copied over from SeagullOffensive, again what in the hell
                BallInteract birdPlayer = opponent.GetComponent<BallInteract>();
                BirdType birdType;
                if (birdPlayer == null)
                {
                    birdType = opponent.GetComponent<AIBehavior>().GetBirdType();
                }
                else
                {
                    birdType = opponent.GetComponent<BallInteract>().GetBirdType();
                }
                if (birdType != BirdType.OSTRICH)
                {
                    // Gets a normalized direction vector from the opponent to the Lovebird
                    Vector3 dir = this.transform.position - opponent.transform.position;
                    dir.Normalize();

                    // Moves opponent towards the Lovebird
                    opponent.transform.position += dir * walkSpeed / 300;
                }
            }
        }
    }

    public void DebuffEnemy()
    {
        _onCooldown = true;
        StartCoroutine(Cooldown());

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, cooldown);

        // Play offensive sound
        AudioManager.PlayBirdSound(BirdType.LOVEBIRD, SoundType.OFFENSIVE, 1.0f);

        // Trigger offensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
        {
            myBallInteract.animator.SetTrigger("OffensiveAbility");
        }

        // Gets opponents
        GameManager gameManager = GameManager.Instance;
        if (_onLeft)
        {
            opponents.Add(gameManager.rightPlayer1);
            opponents.Add(gameManager.rightPlayer2);
        } else
        {
            opponents.Add(gameManager.leftPlayer1);
            opponents.Add(gameManager.leftPlayer2);
        }

        // Disables manual movement for AI and Players
        foreach (GameObject opponent in opponents)
        {
            try
            {
                if (opponent.GetComponent<BallInteract>().GetBirdType() != BirdType.OSTRICH) 
                {
                    opponent.GetComponent<CharacterMovement>().enabled = false;
                    ParticleSystem heart = Instantiate(hearts, opponent.transform);
                    heart.transform.position += new Vector3(0f, heartsOffset, 0f);
                    heart.Play();
                    _hearts.Add(heart);
                }
            }
            catch (NullReferenceException)
            {
                if (opponent.GetComponent<AIBehavior>().GetBirdType() != BirdType.OSTRICH)
                {
                    opponent.GetComponent<AIBehavior>().enabled = false;
                    ParticleSystem heart = Instantiate(hearts, opponent.transform);
                    heart.transform.position += new Vector3(0f, heartsOffset, 0f);
                    heart.Play();
                    _hearts.Add(heart);
                }
            }
        }
        _debuffActive = true;

        StartCoroutine(DebuffTimer());
    }

    private IEnumerator DebuffTimer()
    {
        yield return new WaitForSeconds(DebuffLength);
        _debuffActive = false;

        //Re-enables manual movement for AI and Players
        foreach (GameObject opponent in opponents)
        {
            if (opponent.GetComponent<CharacterMovement>())
            {
                opponent.GetComponent<CharacterMovement>().enabled = true;
            }
            if (opponent.GetComponent<AIBehavior>())
            {
                opponent.GetComponent<AIBehavior>().enabled = true;
            }
        }

        foreach (ParticleSystem heart in _hearts)
        {
            Destroy(heart);
        }
    }

    private IEnumerator Cooldown()
    {
        yield return new WaitForSeconds(cooldown);
        _onCooldown = false;
    }
}

    
