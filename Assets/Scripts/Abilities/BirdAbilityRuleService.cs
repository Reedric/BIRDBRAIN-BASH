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

    public bool CanUseAbility(GameObject user, AbilitySlot slot)
    {
        if (_globalAbilitiesDisabled)
        {
            Debug.Log("[BirdAbilityRuleService] Global abilities disabled.");
            return false;
        }

        if (gameManager == null)
        {
            Debug.LogWarning("[BirdAbilityRuleService] GameManager reference is null.");
            return false;
        }

        BallInteract ballInteract = user.GetComponent<BallInteract>();
        if (ballInteract == null)
        {
            Debug.LogWarning("[BirdAbilityRuleService] CanUseAbility called on object without BallInteract.");
            return false;
        }

        bool canUse = false;
        if (OnDefense(ballInteract) && slot == AbilitySlot.Defensive) canUse = true;
        if (OnOffense(ballInteract) && slot == AbilitySlot.Offensive) canUse = true;

        Debug.Log($"[BirdAbilityRuleService] CanUseAbility for {ballInteract.GetBirdType()} slot {slot}: {canUse} (state={GameManager.Instance?.gameState}).");
        return canUse;
    }

    private bool OnDefense(BallInteract ballInteract)
    {
        if (ballInteract == null) return false;

        // Birds whose abilities do not rely on game state or have their own special rules
        if (ballInteract.GetBirdType() == BirdType.PENGUIN && GameManager.PointInProgress()) return true;
        if (ballInteract.GetBirdType() == BirdType.CROW && GameManager.PointInProgress()) return true;
        if (ballInteract.GetBirdType() == BirdType.PELICAN && GameManager.Instance.gameState == GameManager.GameState.PointStart
            && ballInteract.Equals(GameManager.Instance.server.GetComponent<BallInteract>())) return true;
        if (ballInteract.GetBirdType() == BirdType.TOUCAN && GameManager.PointInProgress()) return true;
        if (ballInteract.GetBirdType() == BirdType.ROBOPIGEON) return true;
        if (ballInteract.GetBirdType() == BirdType.KIWI && GameManager.PointInProgress()) return true;

        // General defensive abilities can be used by either side while the point is in progress
        if (GameManager.PointInProgress()) return true;

        // If none above statements hit, then must not be on defense
        return false;
    }

    private bool OnOffense(BallInteract ballInteract)
    {
        if (ballInteract == null) return false;

        // Birds whose abilities do not rely on game state or have their own special rules
        if (ballInteract.GetBirdType() == BirdType.OWL) return true;
        if (ballInteract.GetBirdType() == BirdType.EAGLE && GameManager.PointInProgress()) return true;
        if (ballInteract.GetBirdType() == BirdType.CHICKEN) return true;
        if (ballInteract.GetBirdType() == BirdType.CROW && GameManager.PointInProgress()) return true;
        if (ballInteract.GetBirdType() == BirdType.KIWI && GameManager.PointInProgress()) return true;
        if (ballInteract.GetBirdType() == BirdType.MACAW) return true;
        if (ballInteract.GetBirdType() == BirdType.LOVEBIRD) return true;
        if (ballInteract.GetBirdType() == BirdType.PELICAN) return true;
        if (ballInteract.GetBirdType() == BirdType.DODO) return true;
        if (ballInteract.GetBirdType() == BirdType.PUKEKO && GameManager.PointInProgress()) return true;
        if (ballInteract.GetBirdType() == BirdType.SEAGULL && GameManager.Instance.gameState == GameManager.GameState.PointStart
            && ScoreManager.Instance.side1ServeIndicator.activeInHierarchy == (ballInteract.transform.position.x < 0)) return true;

        // General offensive abilities can be used by either side while the point is in progress
        if (GameManager.PointInProgress()) return true;

        // If none above statements hit, then must not be on offense
        return false;
    }
}