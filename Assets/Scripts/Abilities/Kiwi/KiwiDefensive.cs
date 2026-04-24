using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(CharacterMovement))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Transform))]

/// <summary>
/// Stealth Burrowing - Kiwi burrows underground, becoming faster while also ignoring field effects.
/// When the ability ends, Kiwi jumps out of the ground 
/// </summary>
public class KiwiDefensive : BirdAbility
{
    [Header("Burrowing Settings")]
    [SerializeField] private float burrowDuration = 2f;
    [SerializeField] private float speedBoost = 2f;

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
        StartCoroutine(StealthBurrowing());
    }

    private IEnumerator StealthBurrowing()
    {
        // Need some type of animation or visual for burrowing but for now the bird will just go invisible
        meshRenderer.enabled = false; // makes bird invisible
        rb.useGravity = false;
        transform.Translate(Vector3.down * 3f); // moves bird under play area to avoid field obstacles
        characterMovement.maxAirSpeed += speedBoost;

        yield return new WaitForSeconds(burrowDuration);

        meshRenderer.enabled = true;
        rb.useGravity = true;
        transform.Translate(Vector3.up * 5f);
        characterMovement.maxAirSpeed -= speedBoost;
    }
}
