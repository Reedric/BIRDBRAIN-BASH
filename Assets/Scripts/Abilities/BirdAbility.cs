using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class BirdAbility : MonoBehaviour
{
    private bool abilitiesDisabled = false;
    protected GameManager gameManager = GameManager.Instance;
    protected List<GameObject> opponents = new();
    protected bool _onLeft;
    private bool isStunned = false;

    void Start()
    {
        _onLeft = GetComponent<PlayerInput>().playerIndex < 2;
    }

    public void DisableAbilities(bool disabledOrNot)
    {
        abilitiesDisabled = disabledOrNot;
    }

    public bool CanUseAbilities()
    {
        return !abilitiesDisabled;
    }

    public bool PointInProgress()
    {
        // If the point has just started, cannot use ability
        if (GameManager.Instance.gameState == GameManager.GameState.PointStart) return false;

        // If the point has just ended, cannot use ability, else we are good to go
        return GameManager.Instance.gameState != GameManager.GameState.PointEnd;
    }
}