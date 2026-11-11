using UnityEngine;

/// <summary>
/// Handles player input for activating bird abilities.
/// It listens for input events and calls the ability.
/// </summary>
public class BirdAbilityInput : MonoBehaviour
{
    [SerializeField] private BirdAbilityController abilityController;

    // PlayerInput messages, which are for local matches. Online input comes through PlayerNetworkController instead.
    // The player who paused keeps their controls so they can use the menu, which shouldn't also fire abilities.
    public void OnOffensiveAbility() { if (!NetworkSession.IsOnline && !LocalMatchPaused) ActivateOffensive(); }
    public void OnDefensiveAbility() { if (!NetworkSession.IsOnline && !LocalMatchPaused) ActivateDefensive(); }

    private static bool LocalMatchPaused => PauseMenu.Instance != null && PauseMenu.Instance.GameIsPaused;

    public void ActivateOffensive() { abilityController.UseAbility(AbilitySlot.Offensive); }
    public void ActivateDefensive() { abilityController.UseAbility(AbilitySlot.Defensive); }
}
