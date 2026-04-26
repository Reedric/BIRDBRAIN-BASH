using System.Collections.Generic;
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
        foreach (var ability in GetComponentsInChildren<BirdAbility>())
            abilities[ability.AbilitySlot] = ability;
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;
        foreach (var ability in abilities.Values) ability.TickCooldown(deltaTime);
    }

    public void UseAbility(AbilitySlot slot)
    {
        if (!abilities.TryGetValue(slot, out var ability)) return;
        ability.TryActivate();
    }

    public void SetAbility(AbilitySlot slot, BirdAbility ability)
    {
        abilities[slot] = ability;
    }
}
