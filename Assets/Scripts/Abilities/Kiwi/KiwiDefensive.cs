using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(CharacterMovement))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Transform))]

public class KiwiDefensive : BirdAbility
{
    [Header("Burrowing Settings")]
    [SerializeField] private float burrowDuration = 2f;
    [SerializeField] private float speedBoost = 2f;
    [SerializeField] private float jumpOutForce = 10f;

    [Header("Burrow VFX")]
    [SerializeField] private GameObject burrowMarkerPrefab;
    [SerializeField] private Vector3 markerOffset = new Vector3(0f, 0.05f, 0f); // sits just above ground

    private bool isBurrowed = false;
    private bool _cancelRequested = false;
    private float timeBurrowed = 2f;

    private GameObject _activeBurrowMarker;

    private MeshRenderer meshRenderer;
    private CharacterMovement characterMovement;
    private Rigidbody rb;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        characterMovement = GetComponent<CharacterMovement>();
        rb = GetComponent<Rigidbody>();
    }

    override protected bool Activate()
    {
        if (isBurrowed)
        {
            JumpOut();
            return true;
        }
        else
        {
            StealthBurrowing();
            return false;
        }
    }

    void Update()
    {
        // While ability active, wait for either the full duration or a cancel request
        if (timeBurrowed < burrowDuration)
        {
            timeBurrowed += Time.deltaTime;

            // Keep the marker pinned to the kiwi's XZ position in case they move
            if (_activeBurrowMarker != null)
            {
                Vector3 markerPos = new Vector3(
                    transform.position.x,
                    transform.position.y + 3f + markerOffset.y, // surface = kiwi Y + burrow depth
                    transform.position.z
                );
                _activeBurrowMarker.transform.position = markerPos;
            }
        }
        else if (isBurrowed)
        {
            // Probably a better way to do this, but for now this works and shouldn't cause errors
            TryActivate(AbilitySlot.Defensive);
        }
    }

    private void JumpOut()
    {
        // Destroy the marker before surfacing
        if (_activeBurrowMarker != null)
        {
            Destroy(_activeBurrowMarker);
            _activeBurrowMarker = null;
        }

        // Jump out
        isBurrowed = false;
        meshRenderer.enabled = true;
        rb.useGravity = true;
        transform.Translate(Vector3.up * 5f);
        characterMovement.maxAirSpeed -= speedBoost;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpOutForce, ForceMode.Impulse);

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);
    }

    private void StealthBurrowing()
    {
        isBurrowed = true;
        _cancelRequested = false;

        // Burrow down
        meshRenderer.enabled = false;
        rb.useGravity = false;

        // Store surface position before going underground
        Vector3 surfacePosition = transform.position;

        transform.Translate(Vector3.down * 3f);
        characterMovement.maxAirSpeed += speedBoost;

        // Spawn the dirt marker at the captured surface position
        if (burrowMarkerPrefab != null)
        {
            Vector3 markerPos = surfacePosition + markerOffset;
            _activeBurrowMarker = Instantiate(burrowMarkerPrefab, markerPos, Quaternion.identity);
        }

        // Set time burrowed to 0
        timeBurrowed = 0;
    }
}