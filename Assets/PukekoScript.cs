using UnityEngine;

using UnityEngine.InputSystem;

public class PukekoScript : MonoBehaviour
{
    public Animator animator; // Assign in inspector
    public string offensiveAbilityAction = "Offensive Ability"; // Input action name
    private PlayerInput playerInput;

    void Start()
    {
        playerInput = GetComponent<PlayerInput>();
        if (animator == null) animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (playerInput != null && playerInput.actions.FindAction(offensiveAbilityAction).WasPressedThisFrame())
        {
            TriggerOffensiveAbility();
        }
    }

    private void TriggerOffensiveAbility()
    {
        // Play animation
        if (animator != null)
            animator.SetTrigger("OffensiveAbility"); // Make sure you have a trigger called "OffensiveAbility" in Animator

        // Play sound effect using AudioManager
        AudioManager.PlayBirdSound(BirdType.PUKEKO, SoundType.OFFENSIVE, 1.0f);
    }
}
