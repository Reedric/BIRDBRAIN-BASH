using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BallInteract))]
public class SeagullOffensive : MonoBehaviour
{
    public int debuffLength; // Length of debuff in seconds
    public int debuffAmount; // Amount the debuff will DECREASE stats
    public int debuffWindowLength; // Amount of time in seconds after a score the player can trigger the debuff
    public AudioClip laughSound; // Laughing audio clip
    public AudioSource laughPlayer; // Source to play the laugh sound effect

    public GameManager gameManager;
    private bool _debuffWindow = false;
    private bool _onLeft;

    void Start()
    {
        _onLeft = GetComponent<BallInteract>().onLeft;
        EventManager.SubscribeScore(OnScore);
    }

    public void DebuffEnemy()
    {
        if (_debuffWindow)
        {
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
                opponent.GetComponent<CharacterMovement>().BuffStats(-debuffAmount, debuffLength);
            }

            if (laughPlayer != null && laughSound != null)
            {
                laughPlayer.PlayOneShot(laughSound);
            }
            _debuffWindow = false;
        }
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