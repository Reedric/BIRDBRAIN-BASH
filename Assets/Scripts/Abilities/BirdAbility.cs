using UnityEngine;

/// <summary>
/// Base class for bird abilities. Handles cooldown management and activation logic.
/// </summary>
public abstract class BirdAbility : MonoBehaviour 
{
    public AbilitySlot AbilitySlot;

    [SerializeField] protected float _cooldownTime;
    
    private float _cooldownRemaining;
    private bool _abilitiesDisabled;

    public bool IsReady => _cooldownRemaining <= 0 && !_abilitiesDisabled;

    public void TickCooldown(float deltaTime)
    {
        if (_cooldownRemaining > 0) _cooldownRemaining -= deltaTime;
    }

    public bool TryActivate()
    {
        if (!IsReady) return false;
        if (!BirdAbilityRuleService.Instance.CanUseAbility(gameObject)) return false;

        Activate();
        _cooldownRemaining = _cooldownTime;
        return true;
    }

    protected abstract void Activate();

    public void SetAbilitiesDisabled(bool disabled) { _abilitiesDisabled = disabled; }
}