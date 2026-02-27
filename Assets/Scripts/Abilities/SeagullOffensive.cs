using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BallInteract))]
public class SeagullOffensive : MonoBehaviour
{
    public int debuffLength; // Length of debuff in seconds
    public int debuffAmount; // Amount the debuff will DECREASE stats
    public int debuffWindowLength; // Amount of time in seconds after a score the player can trigger the debuff

    public GameManager gameManager;
    private bool _debuffWindow = false;
    private bool _onLeft;

    void Start()
    {
        _onLeft = GetComponent<BallInteract>().onLeft;
        EventManager.SubscribeScore(OnScore);
    }

    void Update()
    {
        if (_debuffWindow && InputSystem.actions.FindAction("Offensive Ability").WasPressedThisFrame())
        {
            DebuffEnemy();
        }
    }

    public void DebuffEnemy()
    {
        Debug.Log("Debuffing enemies...");
        List<GameObject> opponents = new();
        if (_onLeft)
        {
            opponents.Add(gameManager.rightPlayer1);
            opponents.Add(gameManager.rightPlayer2);
        } else
        {
            opponents.Add(gameManager.leftPlayer1);
            opponents.Add(gameManager.leftPlayer2);
        }

        foreach (GameObject opponent in opponents)
        {
            // Try to debuff player opponent
            try
            {
                opponent.GetComponent<CharacterMovement>().BuffStats(-debuffAmount, debuffLength);
            }
            catch (NullReferenceException)
            {
                // Must be an AI opponent
                opponent.GetComponent<AIBehavior>().BuffStats(-debuffAmount, debuffLength);
            }
            catch (Exception)
            {
                Debug.LogError("Something weird happened in DebuffEnemy for SeagullOffensive...");
            }
        }

    
        _debuffWindow = false;

        // Play laugh sound
        AudioManager.PlayBirdSound(BirdType.PENGUIN, SoundType.DEFENSIVE, 1.0f);
    }

    public bool OnScore(bool leftScored)
    {
        if ((leftScored && _onLeft) || (!leftScored && !_onLeft))
        {
            StartCoroutine(WindowTimer());
            return true;
        }

        return false;
    }

    private IEnumerator WindowTimer()
    {
        _debuffWindow = true;
        yield return new WaitForSeconds(debuffWindowLength);
        _debuffWindow = false;
    }
}