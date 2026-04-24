using UnityEngine;

public class DodoOffensive : BirdAbility
{
    public Animator animator; // Assign in inspector

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    protected override void Activate()
    {
        // Play animation
        if (animator != null)
            animator.SetTrigger("OffensiveAbility"); // Make sure you have a trigger called "OffensiveAbility" in Animator

        // Play sound effect using AudioManager
        AudioManager.PlayBirdSound(BirdType.DODO, SoundType.OFFENSIVE, 1.0f);
    }
}
