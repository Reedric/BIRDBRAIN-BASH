using UnityEngine;

/// <summary>
/// Handles player input for activating bird abilities. 
/// It listens for input events and calls the ability.
/// </summary>
public class BirdAbilityInput : MonoBehaviour
{
    [SerializeField] private BirdAbilityController abilityController;

    public void OnOffensiveAbilityInput() { abilityController.UseAbility(AbilitySlot.Offensive); }
    public void OnDefensiveAbilityInput() { abilityController.UseAbility(AbilitySlot.Defensive); }
}
