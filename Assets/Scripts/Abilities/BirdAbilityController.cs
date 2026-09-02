using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Controller for managing bird abilities. It handles the activation of offensive
/// and defensive abilities and updates their cooldowns each frame.
/// </summary>
public class BirdAbilityController : MonoBehaviour
{
    private Dictionary<AbilitySlot, BirdAbility> abilities = new();

    void Awake()
    {
        InitializeAbilities();
    }

    void Start()
    {
        // Rebuild the ability lookup after all child Awake methods have run.
        InitializeAbilities();
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;
        foreach (var ability in abilities.Values) ability.TickCooldown(deltaTime);
    }

    private void InitializeAbilities()
    {
        abilities.Clear();
        foreach (var ability in GetComponentsInChildren<BirdAbility>())
            abilities[ability.AbilitySlot] = ability;
    }

    public void UseAbility(AbilitySlot slot)
    {
        if (!abilities.TryGetValue(slot, out var ability))
        {
            Debug.LogWarning($"[BirdAbilityController] No ability found for slot {slot} on {gameObject.name}.");
            return;
        }

        if (ability.RequiresSpikeToActivate)
        {
            if (ability.IsArmed)
            {
                Debug.Log($"[BirdAbilityController] Disarming {slot} on {gameObject.name} with {ability.GetType().Name}.");
                ability.Disarm();
            }
            else
            {
                Debug.Log($"[BirdAbilityController] Arming {slot} on {gameObject.name} with {ability.GetType().Name}.");
                ability.TryArm();
            }
            return;
        }

        Debug.Log($"[BirdAbilityController] UseAbility {slot} on {gameObject.name} with {ability.GetType().Name}.");
        ability.TryActivate(slot);
    }

    /// Called by BallInteract when the player performs a normal spike, so any armed ability for the slot can fire.
    public void TryTriggerArmedAbility(AbilitySlot slot)
    {
        if (abilities.TryGetValue(slot, out var ability) && ability.RequiresSpikeToActivate)
            ability.TriggerArmed();
    }

    public void SetAbility(AbilitySlot slot, BirdAbility ability)
    {
        abilities[slot] = ability;
    }
}
