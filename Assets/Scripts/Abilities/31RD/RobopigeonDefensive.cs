using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BallInteract))]
public class RobopigeonDefensive : BirdAbility
{
    [Header("Hardware Hacking Settings")]
    [SerializeField] private GameObject mini31RDPrefab;
    [SerializeField] private GameObject spawnVFXPrefab;
    [SerializeField] private GameObject despawnVFXPrefab;
    [SerializeField] private float miniScale = 0.2f;
    [SerializeField] private float duration = 10f;
    [SerializeField] private float cooldownAfterDuration = 40f;
    [SerializeField] private float miniGroundSpeed = 4f;
    [SerializeField] private float miniJumpForce = 4f;
    [SerializeField] private float miniStrength = 4f;

    private BallInteract ballInteract;
    private GameObject activeMinion;
    private Coroutine lifetimeCoroutine;

    private void Awake()
    {
        ballInteract = GetComponent<BallInteract>();
        AbilitySlot = AbilitySlot.Defensive;
        _cooldownTime = 0f; // Start cooldown manually after the minion expires.
    }

    protected override bool Activate()
    {
        Debug.Log("[RobopigeonDefensive] Activate called.");

        if (activeMinion != null)
            return false;

        if (mini31RDPrefab == null)
        {
            Debug.LogWarning("31RD defensive ability requires a mini 31RD prefab.");
            return false;
        }

        if (ballInteract == null)
        {
            ballInteract = GetComponent<BallInteract>();
            if (ballInteract == null)
                return false;
        }

        Vector3 spawnPosition = transform.position + Vector3.up * 10f;
        activeMinion = Instantiate(mini31RDPrefab, spawnPosition, transform.rotation);
        activeMinion.transform.localScale = Vector3.one * miniScale;

        Debug.Log($"[RobopigeonDefensive] Spawned minion at {spawnPosition} for player {ballInteract.playerID}.");
        ConfigureMinion(activeMinion);
        PlaySpawnEffects(spawnPosition);

        lifetimeCoroutine = StartCoroutine(MinionLifetime());
        return true;
    }

    private void ConfigureMinion(GameObject minion)
    {
        BallInteract minionBall = minion.GetComponent<BallInteract>();
        AIBehavior minionAI = minion.GetComponent<AIBehavior>();
        Rigidbody minionRb = minion.GetComponent<Rigidbody>();

        if (minionBall != null)
        {
            minionBall.onLeft = ballInteract.onLeft;
            minionBall.playerID = ballInteract.playerID;
            minionBall.spikeStat = miniStrength;
            minionBall.SetBirdType(BirdType.ROBOPIGEON);
        }

        if (minionAI != null)
        {
            minionAI.onLeft = ballInteract.onLeft;
            minionAI.playerID = ballInteract.playerID;
            minionAI.maxGroundSpeed = miniGroundSpeed;
            minionAI.maxAirSpeed = miniGroundSpeed;
            minionAI.jumpForce = miniJumpForce;
            minionAI.SetAIDifficulty(AIBehavior.AIDifficulty.Hard);
            minionAI.easyBumpChance = 1f;
            minionAI.mediumBumpChance = 1f;
            minionAI.hardBumpChance = 1f;
            minionAI.easySetChance = 1f;
            minionAI.mediumSetChance = 1f;
            minionAI.hardSetChance = 1f;
            minionAI.easySpikeChance = 1f;
            minionAI.mediumSpikeChance = 1f;
            minionAI.hardSpikeChance = 1f;
        }

        if (minionBall == null && minionAI == null)
        {
            Debug.LogWarning("RobopigeonDefensive: spawned minion prefab has neither BallInteract nor AIBehavior.");
        }

        if (minionRb != null)
        {
            minionRb.linearVelocity = Vector3.zero;
            minionRb.angularVelocity = Vector3.zero;
            minionRb.useGravity = true;
        }
        else
        {
            Debug.LogWarning("RobopigeonDefensive: spawned minion prefab is missing Rigidbody.");
        }
    }

    private IEnumerator MinionLifetime()
    {
        float remaining = duration;
        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            yield return null;
        }

        EndAbility();
    }

    private void PlaySpawnEffects(Vector3 position)
    {
        if (spawnVFXPrefab != null)
        {
            GameObject vfx = Instantiate(spawnVFXPrefab, position, Quaternion.identity);
            Destroy(vfx, 5f);
        }
    }

    private void PlayDespawnEffects(Vector3 position)
    {
        if (despawnVFXPrefab != null)
        {
            GameObject vfx = Instantiate(despawnVFXPrefab, position, Quaternion.identity);
            Destroy(vfx, 5f);
        }
    }

    private void EndAbility()
    {
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }

        if (activeMinion != null)
        {
            PlayDespawnEffects(activeMinion.transform.position);
            Destroy(activeMinion);
            activeMinion = null;
        }

        if (ballInteract != null)
        {
            int playerID = ballInteract.playerID;
            StartCooldown(cooldownAfterDuration);

            if (HUDManager.Instance == null)
            {
                Debug.LogWarning("[RobopigeonDefensive] HUDManager.Instance is null when trying to trigger cooldown.");
            }
            else if (playerID < 0)
            {
                Debug.LogWarning("[RobopigeonDefensive] Invalid playerID when triggering HUD cooldown.");
            }
            else
            {
                Debug.Log($"[RobopigeonDefensive] Triggering defensive cooldown for player {playerID} duration {cooldownAfterDuration}s.");
                HUDManager.Instance.TriggerDefensiveCooldown(playerID, cooldownAfterDuration);
            }
        }
    }
}
