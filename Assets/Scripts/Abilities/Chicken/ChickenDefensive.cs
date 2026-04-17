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

    private Rigidbody rigidbody;

    private void Awake() { rigidbody = GetComponent<Rigidbody>(); }

    private void FixedUpdate() 
    {
        if (rigidbody.linearVelocity.y < 0) // If falling
            rigidbody.linearVelocity = new Vector3(rigidbody.linearVelocity.x, rigidbody.linearVelocity.y * slowFallMultiplier, rigidbody.linearVelocity.z);
    }
}
