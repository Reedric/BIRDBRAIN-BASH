using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(CharacterMovement))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Transform))]

/// <summary>
/// Stealth Burrowing - Kiwi burrows underground, becoming faster while also ignoring field effects.
/// When the ability ends (or jump is pressed), Kiwi jumps out of the ground.
/// </summary>
public class KiwiDefensive : BirdAbility
{
    [Header("Burrowing Settings")]
    [SerializeField] private float burrowDuration = 2f;
    [SerializeField] private float speedBoost = 2f;
    [SerializeField] private float jumpOutForce = 10f;

    [SerializeField] private float cooldown = 12f;
    private bool onCooldown = false;
    private bool isBurrowed = false;
    private bool _jumpRequested = false;

    private MeshRenderer meshRenderer;
    private CharacterMovement characterMovement;
    private Rigidbody rb;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        characterMovement = GetComponent<CharacterMovement>();
        rb = GetComponent<Rigidbody>();
    }

    public void OnDefensiveAbility(InputValue value)
    {
        StartCoroutine(StealthBurrowing());
    }

    // Called by Unity's Input System when the jump action fires
    public void OnJump(InputValue value)
    {
        if (isBurrowed)
            _jumpRequested = true;
    }

    private IEnumerator StealthBurrowing()
    {
        if (onCooldown || !CanUseAbilities() || !PointInProgress()) yield break;
        onCooldown = true;
        isBurrowed = true;
        _jumpRequested = false;

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, cooldown);

        // Burrow down
        meshRenderer.enabled = false;
        rb.useGravity = false;
        transform.Translate(Vector3.down * 3f);
        characterMovement.maxAirSpeed += speedBoost;

        // Wait for either the full duration or an early jump-out request
        float elapsed = 0f;
        while (elapsed < burrowDuration && !_jumpRequested)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Jump out
        isBurrowed = false;
        meshRenderer.enabled = true;
        rb.useGravity = true;
        transform.Translate(Vector3.up * 5f);
        characterMovement.maxAirSpeed -= speedBoost;

        // Apply upward force so it feels like a proper jump-out
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpOutForce, ForceMode.Impulse);

        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }
}