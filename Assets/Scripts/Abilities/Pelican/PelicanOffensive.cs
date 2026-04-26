using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(PlayerInput))]

// Slip Fish - Spit a fish into the opponent's court (wherever the bird is facing) and watch them slip 
// (fish will disappear after 15s) (15s cooldown after fish disappears)
public class PelicanOffensive : BirdAbility
{
    [Header("Mouth Offset")]
    [SerializeField] private float mouthForwardOffset = 1.0f;
    [SerializeField] private float mouthUpOffset = 1.5f;

    [SerializeField] private float slipFishSpeed = 15f; // Speed at which the fish is spit out
    [SerializeField] private float fishLifetime = 15f;
    [SerializeField] private GameObject fishPrefab; // assign in inspector until there's a permanent spot for it

    override protected void Activate()
    {
        SlipFish();
    }

    private void SlipFish()
    {
        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, _cooldownTime);

        // Play offensive sound
        AudioManager.PlayBirdSound(BirdType.PELICAN, SoundType.OFFENSIVE, 1.0f);

        // Trigger offensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
        {
            myBallInteract.animator.SetTrigger("OffensiveAbility");
        }

        // Instantiate at the pelican's mouth position and rotation
        Vector3 mouthPos = transform.position + transform.forward * mouthForwardOffset + transform.up * mouthUpOffset;
        GameObject fish = Instantiate(fishPrefab, mouthPos, transform.rotation);

        // Let the fish know which game object is the pelican to prevent collisions with it
        fish.GetComponent<SlipFish>().pelican = gameObject;
        
        // Account for rotation offset
        Vector3 forward = Quaternion.Euler(-GetComponent<CharacterMovement>().rotationOffsetEuler) * transform.forward;
        
        // Add velocity to the fish to make it move forward at an arc so it goes over the net
        if (fish.TryGetComponent<Rigidbody>(out var rb))
        {
            // Reduce the upward velocity so the fish falls faster
            rb.linearVelocity = forward * slipFishSpeed + Vector3.up * (slipFishSpeed / 4);
        }

        // Destroy the fish after its lifetime expires
        Destroy(fish, fishLifetime);
    }
}
