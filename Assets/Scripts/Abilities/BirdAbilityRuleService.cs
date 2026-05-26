using UnityEngine;

public class BirdAbilityRuleService : MonoBehaviour
{
    public static BirdAbilityRuleService Instance { get; private set; }

    [SerializeField] private GameManager gameManager;

    private bool _globalAbilitiesDisabled;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SetGlobalAbilitiesDisabled(bool disabled)
    {
        _globalAbilitiesDisabled = disabled;
    }

    public bool CanUseAbility(GameObject user)
    {
        if (_globalAbilitiesDisabled) return false;

        if (gameManager == null) return false;
        if (gameManager.gameState == GameManager.GameState.PointStart) return false;
        if (gameManager.gameState == GameManager.GameState.PointEnd) return false;

        // TODO:
        // if on defense, only allow defensive abilities
        // if on offense, only allow offensive abilities
        
        return true;
    }
}
