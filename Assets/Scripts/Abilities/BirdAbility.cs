using UnityEngine;

/// <summary>
/// Base class for bird abilities. Handles cooldown management and activation logic.
/// </summary>
public abstract class BirdAbility : MonoBehaviour 
{
    [SerializeField] private AbilitySlot abilitySlot;
    public AbilitySlot Slot => abilitySlot;

    [SerializeField] protected float cooldownTime;
    
    private float cooldownRemaining;
    private bool abilitiesDisabled;

    public bool IsReady => cooldownRemaining <= 0 && !abilitiesDisabled;

    public void TickCooldown(float deltaTime)
    {
        if (cooldownRemaining > 0) cooldownRemaining -= deltaTime;
    }

    public bool CanActivate()
    {
        return IsReady && BirdAbilityRuleService.Instance.CanUseAbility(gameObject);
    }

    public bool TryActivate()
    {
        if (!CanActivate()) return false;

        Activate();
        cooldownRemaining = cooldownTime;
        return true;
    }

    protected abstract void Activate();

    public void SetAbilitiesDisabled(bool disabled) { abilitiesDisabled = disabled; }
}