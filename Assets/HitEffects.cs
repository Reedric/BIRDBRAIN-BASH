using UnityEngine;

// Manages VFX particle effects for all ball hit types.
// TEAM MAPPING:
//  Team 1 (Blue) = onLeft is TRUE  (left side of court)
//  Team 2 (Pink) = onLeft is FALSE (right side of court)

public class HitEffects : MonoBehaviour
{
    public static HitEffects Instance { get; private set; }

    // Hit type enum

    public enum HitType
    {
        BumpSetServe,   // Shared effect for bumps, sets, and serves
        Spike,          // Spike-specific effect
        Block           // Block-specific effect
    }

    // prefabs

    [Header("Team 1 — Blue (onLeft = true)")]
    [Tooltip("Shared prefab used for bumps, sets, and serves — Team 1")]
    public GameObject team1BumpSetServePrefab;

    [Tooltip("Spike effect prefab — Team 1")]
    public GameObject team1SpikePrefab;

    [Tooltip("Block effect prefab — Team 1")]
    public GameObject team1BlockPrefab;

    [Header("Team 2 — Pink (onLeft = false)")]
    [Tooltip("Shared prefab used for bumps, sets, and serves — Team 2")]
    public GameObject team2BumpSetServePrefab;

    [Tooltip("Spike effect prefab — Team 2")]
    public GameObject team2SpikePrefab;

    [Tooltip("Block effect prefab — Team 2")]
    public GameObject team2BlockPrefab;

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

    /// <summary>
    /// Spawns the appropriate particle effect at the ball's current position.
    /// </summary>
    /// <param name="hitType">The type of hit that was performed.</param>
    /// <param name="onLeft">Pass the hitting object's onLeft field to determine team.</param>
    public void PlayEffect(HitType hitType, bool onLeft)
    {
        GameObject prefab = ResolvePrefab(hitType, onLeft);

        if (prefab == null)
        {
            Debug.LogWarningFormat(
                "HitEffects: No prefab assigned for HitType={0}, Team={1}.",
                hitType, onLeft ? "1 (Blue)" : "2 (Pink)");
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

    private GameObject ResolvePrefab(HitType hitType, bool onLeft)
    {
        if (onLeft) // Team 1 — Blue
        {
            return hitType switch
            {
                HitType.BumpSetServe => team1BumpSetServePrefab,
                HitType.Spike        => team1SpikePrefab,
                HitType.Block        => team1BlockPrefab,
                _                    => null
            };
        }
        else // Team 2 — Pink
        {
            return hitType switch
            {
                HitType.BumpSetServe => team2BumpSetServePrefab,
                HitType.Spike        => team2SpikePrefab,
                HitType.Block        => team2BlockPrefab,
                _                    => null
            };
        }
    }
}