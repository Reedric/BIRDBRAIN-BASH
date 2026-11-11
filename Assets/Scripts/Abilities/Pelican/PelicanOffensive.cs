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

    override protected bool Activate()
    {
        if (!GameManager.PointInProgress() || fishPrefab == null)
            return false;

        StartCoroutine(SlipFish());

        // Ability successfully activated
        return true;
    }

    private IEnumerator SlipFish()
    {
        // Play offensive sound
        AudioManager.PlayBirdSound(BirdType.PELICAN, SoundType.OFFENSIVE, 1.0f);

        // Trigger offensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
            myBallInteract.animator.SetTriggerSynced("OffensiveAbility");

        // Instantiate fish at the pelican's mouth position
        Vector3 mouthPos = transform.position + transform.forward * mouthForwardOffset + transform.up * mouthUpOffset;
        GameObject fish = NetworkFx.Instantiate(fishPrefab, mouthPos, transform.rotation);

        // Also on a timer of its own (and SlipFish removes itself when the point ends). If the pelican gets switched
        // off (Phoenix turning it into a chicken) this coroutine stops, and the fish would stay for the whole match.
        Destroy(fish, fishLifetime);

        // Let the fish know who the pelican is (to ignore self-collision)
        // and which side the pelican is on (so SlipFish can pass the correct
        // opponentIsOnLeft value to BuffsDebuffs.ApplyEffect when it stuns on collision)
        SlipFish slipFish = fish.GetComponent<SlipFish>();
        slipFish.pelican = gameObject;
        slipFish.pelicanIsOnLeft = myBallInteract != null && myBallInteract.onLeft; // Without this every slip shows the left team's effect

        // Account for rotation offset
        Vector3 forward = Quaternion.Euler(-GetComponent<CharacterMovement>().rotationOffsetEuler) * transform.forward;

        // Add arc velocity so the fish clears the net
        if (fish.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = forward * (slipFishSpeed * 0.85f) + Vector3.up * (slipFishSpeed / 4);
        }

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, _cooldownTime);

        // Wait for either the point to end or fish's lifetime to expire
        float timeElapsed = 0f;
        while (timeElapsed < fishLifetime && GameManager.Instance.gameState != GameManager.GameState.PointEnd)
        {
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(fish);
    }
}