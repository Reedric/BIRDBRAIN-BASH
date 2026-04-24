using UnityEngine;

public class BirdAbilityRuleService : MonoBehaviour
{
    public static BirdAbilityRuleService Instance { get; private set; }

    [SerializeField] private GameManager gameManager;

    private bool globalAbilitiesDisabled;

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
        globalAbilitiesDisabled = disabled;
    }

    public bool CanUseAbility(GameObject user)
    {
        if (globalAbilitiesDisabled) return false;

        if (gameManager == null) return false;
        if (gameManager.gameState == GameManager.GameState.PointStart) return false;
        if (gameManager.gameState == GameManager.GameState.PointEnd) return false;
        
        return true;
    }
}
