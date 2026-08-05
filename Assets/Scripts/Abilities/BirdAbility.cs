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
            _cooldownRemaining = _cooldownTime;
            Debug.Log($"[BirdAbility] {GetType().Name} activated successfully.");
            return true;
        }

        Debug.Log($"[BirdAbility] {GetType().Name} activation failed inside Activate().");
        return false;
    }

    // TODO: make this return bool, true means the cooldown will start, false means it won't (for abilities with multiple activations)
    protected abstract bool Activate();

    public void SetAbilitiesDisabled(bool disabled) { _abilitiesDisabled = disabled; }
}