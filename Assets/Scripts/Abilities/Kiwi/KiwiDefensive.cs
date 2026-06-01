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

    private bool onCooldown = false;
    private bool isBurrowed = false;
    private bool _cancelRequested = false;

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

    override protected void Activate()
    {
        if (isBurrowed)
            _cancelRequested = true;
        else
            StartCoroutine(StealthBurrowing());
    }

    private IEnumerator StealthBurrowing()
    {
        isBurrowed = true;
        _cancelRequested = false;

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);

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

        // Wait for either the full duration or a cancel request
        float elapsed = 0f;
        while (elapsed < burrowDuration && !_cancelRequested)
        {
            elapsed += Time.deltaTime;

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

            yield return null;
        }

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
    }
}