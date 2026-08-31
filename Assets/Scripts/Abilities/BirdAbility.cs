using UnityEngine;

/// <summary>
/// Base class for bird abilities. Handles cooldown management and activation logic.
/// </summary>
public abstract class BirdAbility : MonoBehaviour 
{
    public AbilitySlot AbilitySlot;

    [SerializeField] protected float _cooldownTime;
    
    protected float _cooldownRemaining;
    private bool _abilitiesDisabled;
    protected bool _delayCooldownStart;
    private float _nextCooldownReduction;

    public bool IsReady => _cooldownRemaining <= 0 && !_abilitiesDisabled;

    public void TickCooldown(float deltaTime)
    {
        if (_cooldownRemaining > 0 && GameManager.PointInProgress()) _cooldownRemaining -= deltaTime;
    }

    protected void StartCooldown(float seconds)
    {
        _cooldownRemaining = seconds;
    }

    public bool TryActivate(AbilitySlot slot)
    {
        if (!IsReady)
        {
            Debug.Log($"[BirdAbility] {GetType().Name} not ready for activation ({_cooldownRemaining:F1}s remaining).");
            return false;
        }

        if (!BirdAbilityRuleService.Instance.CanUseAbility(gameObject, slot))
        {
            Debug.Log($"[BirdAbility] {GetType().Name} denied by rules for slot {slot}.");
            return false;
        }

        if (Activate())
        {
            if (!_delayCooldownStart)
            {
                _cooldownRemaining = _cooldownTime * (1f - _nextCooldownReduction);
                bool usedCooldownReduction = _nextCooldownReduction > 0f;
                _nextCooldownReduction = 0f;

                if (usedCooldownReduction)
                    UpdateCooldownUI();
            }

            Debug.Log($"[BirdAbility] {GetType().Name} activated successfully.");
            return true;
        }

        Debug.Log($"[BirdAbility] {GetType().Name} activation failed inside Activate().");
        return false;
    }

    // TODO: make this return bool, true means the cooldown will start, false means it won't (for abilities with multiple activations)
    protected abstract bool Activate();

    public void ApplyCooldownReduction(float reduction)
    {
        reduction = Mathf.Clamp01(reduction);

        if (_cooldownRemaining > 0f)
        {
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - _cooldownTime * reduction);
            UpdateCooldownUI();
            return;
        }

        _nextCooldownReduction = reduction;
    }

    private void UpdateCooldownUI()
    {
        if (HUDManager.Instance == null) return;

        int playerID = GetPlayerID();
        if (playerID < 0) return;

        if (_cooldownRemaining <= 0f)
        {
            if (AbilitySlot == AbilitySlot.Offensive)
                HUDManager.Instance.ResetOffensiveCooldown(playerID);
            else if (AbilitySlot == AbilitySlot.Defensive)
                HUDManager.Instance.ResetDefensiveCooldown(playerID);
        }
        else if (AbilitySlot == AbilitySlot.Offensive)
        {
            HUDManager.Instance.UpdateOffensiveCooldown(playerID, _cooldownRemaining, _cooldownTime);
        }
        else if (AbilitySlot == AbilitySlot.Defensive)
        {
            HUDManager.Instance.UpdateDefensiveCooldown(playerID, _cooldownRemaining, _cooldownTime);
        }
    }

    private int GetPlayerID()
    {
        BallInteract ballInteract = GetComponent<BallInteract>();
        if (ballInteract != null) return ballInteract.playerID;

        AIBehavior aiBehavior = GetComponent<AIBehavior>();
        return aiBehavior != null ? aiBehavior.playerID : -1;
    }

    public void SetAbilitiesDisabled(bool disabled) { _abilitiesDisabled = disabled; }
}