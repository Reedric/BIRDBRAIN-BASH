using UnityEngine;
using UnityEngine.InputSystem;

public class DodoScript : MonoBehaviour
{
    public Animator animator; // Assign in inspector
    public string offensiveAbilityAction = "Offensive Ability"; // Input action name
    private PlayerInput playerInput;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerInput = GetComponent<PlayerInput>();
        if (animator == null) animator = GetComponent<Animator>();
    }

    // Update is called once per frame
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
        AudioManager.PlayBirdSound(BirdType.DODO, SoundType.OFFENSIVE, 1.0f);
    }
}
