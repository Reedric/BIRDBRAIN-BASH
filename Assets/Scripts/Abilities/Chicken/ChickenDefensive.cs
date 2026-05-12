using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CharacterMovement))]

/// <summary>
/// Slow Falling - anytime Chicken jumps, instead of falling he glides down to the ground
/// (similar to Minecraft chickens) (passive)
/// </summary>
public class ChickenDefensive : BirdAbility
{
    [Header("Chicken Defensive Settings")]
    [SerializeField] private float slowFallMultiplier = 0.5f; // Adjust this for how much you want to slow the fall

    private Rigidbody rb;

    private void Awake() { rb = GetComponent<Rigidbody>(); }

    private void FixedUpdate() 
    {
        if (rb.linearVelocity.y < 0) // If falling
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y * slowFallMultiplier, rb.linearVelocity.z);
    }
}
