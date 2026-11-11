using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]

/// <summary>
/// Flip Flap - Squawk to make enemy controls flipped for a short period of time
/// </summary>
public class MacawOffensive : BirdAbility
{
    [SerializeField] private GameObject flipVfxPrefab;
    private const float FlipDuration = 10f;

    private List<PlayerInput> opponentControls = new();
    private readonly List<GameObject> activeVfx = new();
    private Coroutine flipCoroutine;

    override protected bool Activate()
    {
        if (!GameManager.PointInProgress())
            return false;

        CacheOpponentControls();
        if (opponentControls.Count == 0)
            return false;

        // Play sound effect using AudioManager
        AudioManager.PlayBirdSound(BirdType.MACAW, SoundType.OFFENSIVE, 1.0f);

        flipCoroutine = StartCoroutine(FlipFlap());

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, _cooldownTime);

        // Ability successfully activated
        return true;
    }

    private IEnumerator FlipFlap()
    {
        FlipControls(true);
        SpawnFlipVfx();

        float elapsed = 0f;
        while (elapsed < FlipDuration && GameManager.PointInProgress())
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        FlipControls(false);
        ClearFlipVfx();
        flipCoroutine = null;
    }

    private void FlipControls(bool shouldFlip)
    {
        foreach (var opponent in opponentControls)
        {
            // Can already be gone when this runs from OnDestroy while the scene is unloading
            if (opponent == null) continue;

            // CharacterMovement flips the input itself, for local and online players alike. Overriding the Move action's
            // bindings doesn't work for this: they include the keyboard's WASD parts (single
            // keys, not Vector2s, so the invert processor doesn't fit them), and an override doesn't follow a player who switches
            // between keyboard and controller partway through.
            if (opponent.TryGetComponent(out CharacterMovement characterMovement))
                characterMovement.SetInputInverted(shouldFlip);
        }
    }

    private void CacheOpponentControls()
    {
        opponentControls.Clear();

        BallInteract ballInteract = GetComponent<BallInteract>();
        GameManager gameManager = GameManager.Instance;
        if (ballInteract == null || gameManager == null)
            return;

        GameObject[] opponents = ballInteract.onLeft
            ? new[] { gameManager.rightPlayer1, gameManager.rightPlayer2 }
            : new[] { gameManager.leftPlayer1, gameManager.leftPlayer2 };

        foreach (GameObject opponent in opponents)
        {
            PlayerInput input = opponent != null ? opponent.GetComponent<PlayerInput>() : null;
            if (input != null)
                opponentControls.Add(input);
        }
    }

    private void SpawnFlipVfx()
    {
        if (flipVfxPrefab == null)
            return;

        foreach (PlayerInput opponent in opponentControls)
        {
            GameObject vfx = NetworkFx.Instantiate(flipVfxPrefab, opponent.transform);
            vfx.transform.localPosition = Vector3.zero;
            activeVfx.Add(vfx);
        }
    }

    private void ClearFlipVfx()
    {
        foreach (GameObject vfx in activeVfx)
        {
            if (vfx != null)
                Destroy(vfx);
        }

        activeVfx.Clear();
    }

    // If the macaw gets switched off mid-flip (Phoenix turning it into a chicken), the timer stops with it, and the
    // opponents' controls would stay flipped until the macaw next used the ability.
    private void OnDisable()
    {
        if (flipCoroutine == null) return;

        StopCoroutine(flipCoroutine);
        flipCoroutine = null;
        FlipControls(false);
        ClearFlipVfx();
    }

    private void OnDestroy()
    {
        if (flipCoroutine != null)
            StopCoroutine(flipCoroutine);

        FlipControls(false);
        ClearFlipVfx();
    }
}
