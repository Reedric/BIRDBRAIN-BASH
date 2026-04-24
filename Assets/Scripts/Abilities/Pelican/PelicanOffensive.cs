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
    [SerializeField] private float cooldown = 30f; // Cooldown in seconds (30 because the fish disappears after 15s, so 15s cooldown after that)

    [SerializeField] private float slipFishSpeed = 15f; // Speed at which the fish is spit out
    [SerializeField] private float fishLifetime = 15f;
    [SerializeField] private GameObject fishPrefab; // assign in inspector until there's a permanent spot for it
    private bool onCooldown = false;
    private PlayerInput playerInput;
    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        _onLeft = GetComponent<BallInteract>().onLeft;
    }

    private void Update()
    {
        // If pressed offensive ability button, activate ability
        if (playerInput.actions.FindAction("Offensive Ability").WasPressedThisFrame() && !onCooldown
            && CanUseAbilities() && PointInProgress())
        {
            SlipFish();
        }
    }

    private void SlipFish()
    {
        if (onCooldown) return;

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, cooldown);

        // Play offensive sound
        AudioManager.PlayBirdSound(BirdType.PELICAN, SoundType.OFFENSIVE, 1.0f);

        // Trigger offensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
            myBallInteract.animator.SetTrigger("OffensiveAbility");

        // Instantiate fish at the pelican's mouth position
        Vector3 mouthPos = transform.position + transform.forward * mouthForwardOffset + transform.up * mouthUpOffset;
        GameObject fish = Instantiate(fishPrefab, mouthPos, transform.rotation);

        // Let the fish know who the pelican is (to ignore self-collision)
        // and which side the pelican is on (so SlipFish can pass the correct
        // opponentIsOnLeft value to BuffsDebuffs.ApplyEffect when it stuns on collision)
        SlipFish slipFish = fish.GetComponent<SlipFish>();
        slipFish.pelican = gameObject;
        slipFish.pelicanIsOnLeft = _onLeft;

        // Account for rotation offset
        Vector3 forward = Quaternion.Euler(-GetComponent<CharacterMovement>().rotationOffsetEuler) * transform.forward;

        // Add arc velocity so the fish clears the net
        if (fish.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = forward * slipFishSpeed + Vector3.up * (slipFishSpeed / 4);
        }

        Destroy(fish, fishLifetime);

        onCooldown = true;
        StartCoroutine(Cooldown());
    }

    private IEnumerator Cooldown()
    {
        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }
}