using UnityEngine;

// Manages VFX particle effects for all ball hit types.
// PLAYER MAPPING:
//  0 — Blue   (Team 1, first player,  onLeft = true)
//  1 — Green  (Team 1, second player, onLeft = true)
//  2 — Pink   (Team 2, first player,  onLeft = false)
//  3 — Yellow (Team 2, second player, onLeft = false)

public class HitEffects : MonoBehaviour
{
    public static HitEffects Instance { get; private set; }

    // ── Hit type ─────────────────────────────────────────────────────────────

    public enum HitType
    {
        BumpSetServe,   // Shared effect for bumps, sets, and serves
        Spike,          // Spike-specific effect
        Block           // Block-specific effect
    }

    // ── Prefabs ───────────────────────────────────────────────────────────────

    [Header("Player 1 — Blue (Team 1)")]
    public GameObject p1BumpSetServePrefab;
    public GameObject p1SpikePrefab;
    public GameObject p1BlockPrefab;

    [Header("Player 2 — Green (Team 1)")]
    public GameObject p2BumpSetServePrefab;
    public GameObject p2SpikePrefab;
    public GameObject p2BlockPrefab;

    [Header("Player 3 — Pink (Team 2)")]
    public GameObject p3BumpSetServePrefab;
    public GameObject p3SpikePrefab;
    public GameObject p3BlockPrefab;

    [Header("Player 4 — Yellow (Team 2)")]
    public GameObject p4BumpSetServePrefab;
    public GameObject p4SpikePrefab;
    public GameObject p4BlockPrefab;

    // ── Singleton ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("HitEffects: duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns the appropriate particle effect at the ball's current position.
    /// </summary>
    /// <param name="hitType">The type of hit that was performed.</param>
    /// <param name="playerIndex">
    ///   0 = Blue, 1 = Green, 2 = Pink, 3 = Yellow.
    ///   Previously this was bool onLeft; Team 1 maps to 0/1, Team 2 maps to 2/3.
    /// </param>
    public void PlayEffect(HitType hitType, int playerIndex)
    {
        GameObject prefab = ResolvePrefab(hitType, playerIndex);

        if (prefab == null)
        {
            Debug.LogWarningFormat(
                "HitEffects: No prefab assigned for HitType={0}, playerIndex={1}.",
                hitType, playerIndex);
            return;
        }

        // Spawn at the ball's world position
        Vector3 spawnPosition = BallManager.Instance.gameObject.transform.position;
        GameObject vfxInstance = Instantiate(prefab, spawnPosition, Quaternion.identity);

        // Auto-destroy once the particle system finishes
        ParticleSystem ps = vfxInstance.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            float lifetime = ps.main.duration + ps.main.startLifetime.constantMax;
            Destroy(vfxInstance, lifetime);
        }
        else
        {
            // Fallback destroy if prefab has no ParticleSystem at the root
            Debug.LogWarningFormat(
                "HitEffects: Prefab '{0}' has no ParticleSystem at root. Destroying after 3s.", prefab.name);
            Destroy(vfxInstance, 3f);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private GameObject ResolvePrefab(HitType hitType, int playerIndex)
    {
        return playerIndex switch
        {
            0 => hitType switch  // Blue
            {
                HitType.BumpSetServe => p1BumpSetServePrefab,
                HitType.Spike        => p1SpikePrefab,
                HitType.Block        => p1BlockPrefab,
                _                    => null
            },
            1 => hitType switch  // Green
            {
                HitType.BumpSetServe => p2BumpSetServePrefab,
                HitType.Spike        => p2SpikePrefab,
                HitType.Block        => p2BlockPrefab,
                _                    => null
            },
            2 => hitType switch  // Pink
            {
                HitType.BumpSetServe => p3BumpSetServePrefab,
                HitType.Spike        => p3SpikePrefab,
                HitType.Block        => p3BlockPrefab,
                _                    => null
            },
            3 => hitType switch  // Yellow
            {
                HitType.BumpSetServe => p4BumpSetServePrefab,
                HitType.Spike        => p4SpikePrefab,
                HitType.Block        => p4BlockPrefab,
                _                    => null
            },
            _ => null
        };
    }
}