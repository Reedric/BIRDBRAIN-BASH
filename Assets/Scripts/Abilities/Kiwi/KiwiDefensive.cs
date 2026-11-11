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
    [SerializeField] private float jumpOutForce = 3f;

    [Header("Burrow VFX")]
    [SerializeField] private GameObject burrowMarkerPrefab;
    [SerializeField] private Vector3 markerOffset = new Vector3(0f, 0.05f, 0f); // sits just above ground

    private bool isBurrowed = false;
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
        // If the point ends while the Kiwi is underground,
        // safely bring it back without going through normal activation.
        if (isBurrowed && !GameManager.PointInProgress())
        {
            JumpOut();

            // JumpOut shows the cooldown on the HUD, so actually start it as well
            BeginCooldown();
            return;
        }

        // While ability active, wait for either the full duration or a cancel request
        if (timeBurrowed < burrowDuration)
        {
            timeBurrowed += Time.deltaTime;

            // Keep the marker pinned to the kiwi's XZ position in case they move
            if (_activeBurrowMarker != null)
            {
                Vector3 markerPos = new Vector3(
                    transform.position.x,
                    transform.position.y + 3f + markerOffset.y,
                    transform.position.z
                );
                _activeBurrowMarker.transform.position = markerPos;
            }
        }
        else if (isBurrowed)
        {
            // Probably a better way to do this, but for now this works and shouldn't cause errors.
            // TryActivate is refused while the Kiwi is silenced (e.g. by Crow), which would keep it stuck
            // underground until the silence wears off, so surface it directly in that case.
            if (!TryActivate(AbilitySlot.Defensive))
            {
                JumpOut();
                BeginCooldown();
            }
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
        SetVisible(true);
        rb.useGravity = true;

        // Move back to the surface instead of translating 5 units upward.
        transform.position += Vector3.up * 3f;

        BirdStats.Remove(gameObject, this);

        // Clear any existing physics velocity before applying the small hop.
        rb.linearVelocity = Vector3.zero;

        rb.AddForce(Vector3.up * jumpOutForce, ForceMode.Impulse);

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);
    }

    // The renderers turned off while burrowed, so only those get turned back on
    private readonly System.Collections.Generic.List<Renderer> hiddenRenderers = new();

    // Hides the whole kiwi. The model is drawn by the renderers on its children, so hiding just the root's
    // MeshRenderer would leave the kiwi visible while it's meant to be underground.
    private void SetVisible(bool visible)
    {
        if (!visible)
        {
            hiddenRenderers.Clear();
            foreach (Renderer birdRenderer in GetComponentsInChildren<Renderer>())
            {
                if (!birdRenderer.enabled) continue;
                birdRenderer.enabled = false;
                hiddenRenderers.Add(birdRenderer);
            }
        }
        else
        {
            foreach (Renderer birdRenderer in hiddenRenderers)
            {
                if (birdRenderer != null) birdRenderer.enabled = true;
            }
            hiddenRenderers.Clear();
        }

        // Online, clients draw the kiwi themselves, so they need telling as well (the same as Hummingbird's dash)
        NetworkFx.SetVisible(gameObject, visible);
    }

    private void OnDisable()
    {
        // e.g. Phoenix turning it into a chicken while it's underground
        if (hiddenRenderers.Count > 0) SetVisible(true);
    }

    private void StealthBurrowing()
    {
        isBurrowed = true;

        // Burrow down
        SetVisible(false);
        rb.useGravity = false;

        // Store surface position before going underground
        Vector3 surfacePosition = transform.position;

        // World down, to match JumpOut moving it back up in world space. Translate is in the bird's own space,
        // which would put it back somewhere else if the model were ever tilted.
        transform.position += Vector3.down * 3f;
        // Taken away again in JumpOut. Adding and subtracting it directly could leave it behind or take it twice
        // whenever something else changed the kiwi's speed in between (a stun, a slow, the reset for a new point)
        BirdStats.Apply(gameObject, this, StatModifier.AirSpeed(speedBoost));

        // Spawn the dirt marker at the captured surface position
        if (burrowMarkerPrefab != null)
        {
            Vector3 markerPos = surfacePosition + markerOffset;
            _activeBurrowMarker = NetworkFx.Instantiate(burrowMarkerPrefab, markerPos, Quaternion.identity);
        }

        // Set time burrowed to 0
        timeBurrowed = 0;
    }
}