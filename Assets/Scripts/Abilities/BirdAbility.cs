using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BirdAbility : MonoBehaviour {
    private bool abilitiesDisabled = false;
    protected GameManager gameManager = GameManager.Instance; // ducky: GameManager instance for all abilities in case anyone needs it
    protected List<GameObject> opponents = new(); // ducky: opponents list for all abilities in case anyone needs it
    protected bool _onLeft; // ducky: for opponents if needed

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