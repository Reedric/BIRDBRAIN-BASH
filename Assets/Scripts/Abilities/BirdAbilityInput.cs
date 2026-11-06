using UnityEngine;

/// <summary>
/// Handles player input for activating bird abilities.
/// It listens for input events and calls the ability.
/// </summary>
public class BirdAbilityInput : MonoBehaviour
{
    [SerializeField] private BirdAbilityController abilityController;

    // PlayerInput messages, which are for local matches. Online input comes through PlayerNetworkController instead.
    public void OnOffensiveAbility() { if (!NetworkSession.IsOnline) ActivateOffensive(); }
    public void OnDefensiveAbility() { if (!NetworkSession.IsOnline) ActivateDefensive(); }

    public void ActivateOffensive() { abilityController.UseAbility(AbilitySlot.Offensive); }
    public void ActivateDefensive() { abilityController.UseAbility(AbilitySlot.Defensive); }
}
