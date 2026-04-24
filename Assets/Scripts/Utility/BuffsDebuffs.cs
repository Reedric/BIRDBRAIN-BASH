using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuffsDebuffs : MonoBehaviour
{
    public static BuffsDebuffs Instance { get; private set; }

    public enum EffectType
    {
        Buff,
        Debuff,
        Silence,
        Stun
    }

    private Dictionary<GameObject, Dictionary<EffectType, GameObject>> activeVFX = new();

    [System.Serializable]
    public class EffectSet
    {
        public GameObject buff;
        public GameObject debuff;
        public GameObject silence;
        public GameObject stun;
    }

    [Header("Team 1 — Left Side")]
    public EffectSet team1Effects;

    [Header("Team 2 — Right Side")]
    public EffectSet team2Effects;

    private Dictionary<GameObject, Dictionary<EffectType, Coroutine>> activeEffects = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ApplyEffect(EffectType type, GameObject bird, float duration, bool onLeft)
    {
        if (bird == null) return;

        if (!activeEffects.ContainsKey(bird))
            activeEffects[bird] = new Dictionary<EffectType, Coroutine>();

        // Cancel existing effect of same type
        if (activeEffects[bird].ContainsKey(type))
        {
            StopCoroutine(activeEffects[bird][type]);
        }

        Coroutine co = StartCoroutine(RunEffect(type, bird, duration, onLeft));
        activeEffects[bird][type] = co;
    }

    private IEnumerator RunEffect(EffectType type, GameObject bird, float duration, bool onLeft)
    {
        GameObject vfx = SpawnEffect(type, bird, onLeft);

        // Play start sound
        if (type == EffectType.Buff)
            AudioManager.PlayBuffStartSound();
        else
            AudioManager.PlayDebuffStartSound();

        // ApplyGameplayEffect(type, bird, true);

        yield return new WaitForSeconds(duration);

        // ApplyGameplayEffect(type, bird, false);

        // Play end sound
        if (type == EffectType.Buff)
            AudioManager.PlayBuffEndSound();
        else
            AudioManager.PlayDebuffEndSound();

        if (activeVFX.ContainsKey(bird) && activeVFX[bird].ContainsKey(type))
        {
            GameObject vfxInstance = activeVFX[bird][type];

            if (vfxInstance != null)
            {
                ParticleSystem ps = vfxInstance.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    Destroy(vfxInstance, ps.main.duration + ps.main.startLifetime.constantMax);
                }
                else
                {
                    Destroy(vfxInstance);
                }
            }

            activeVFX[bird].Remove(type);
        }

        activeEffects[bird].Remove(type);
    }

    private GameObject SpawnEffect(EffectType type, GameObject bird, bool onLeft)
    {
        if (!activeVFX.ContainsKey(bird))
            activeVFX[bird] = new Dictionary<EffectType, GameObject>();

        // If VFX already exists, DON'T spawn another
        if (activeVFX[bird].ContainsKey(type))
        {
            return activeVFX[bird][type];
        }

        GameObject prefab = ResolvePrefab(type, onLeft);
        if (prefab == null) return null;

        GameObject vfx = Instantiate(prefab, bird.transform);
        vfx.transform.localPosition = Vector3.zero;

        activeVFX[bird][type] = vfx;

        return vfx;
    }

    private GameObject ResolvePrefab(EffectType type, bool onLeft)
    {
        EffectSet set = onLeft ? team1Effects : team2Effects;

        return type switch
        {
            EffectType.Buff => set.buff,
            EffectType.Debuff => set.debuff,
            EffectType.Silence => set.silence,
            EffectType.Stun => set.stun,
            _ => null
        };
    }

    private void ApplyGameplayEffect(EffectType type, GameObject bird, bool enable)
    {
        CharacterMovement movement = bird.GetComponent<CharacterMovement>();
        BirdAbility ability = bird.GetComponent<BirdAbility>();
        AIBehavior ai = bird.GetComponent<AIBehavior>();

        switch (type)
        {
            case EffectType.Buff:
                if (enable)
                {
                    if (movement != null)
                    {
                        movement.maxGroundSpeed *= 1.5f;
                        movement.maxAirSpeed *= 1.5f;
                    }
                    if (ai != null)
                    {
                        ai.maxGroundSpeed *= 1.5f;
                        ai.maxAirSpeed *= 1.5f;
                    }
                }
                else
                {
                    if (movement != null)
                    {
                        movement.maxGroundSpeed /= 1.5f;
                        movement.maxAirSpeed /= 1.5f;
                    }
                    // AI resets automatically via coroutine
                }
                break;

            case EffectType.Debuff:
                if (enable)
                {
                    if (movement != null)
                    {
                        movement.maxGroundSpeed *= 0.5f;
                        movement.maxAirSpeed *= 0.5f;
                    }

                    if (ai != null)
                    {
                        ai.maxGroundSpeed *= 0.5f;
                        ai.maxAirSpeed *= 0.5f;
                    }
                }
                else
                {
                    if (movement != null)
                    {
                        movement.maxGroundSpeed *= 2f;
                        movement.maxAirSpeed *= 2f;
                    }

                    if (ai != null)
                    {
                        ai.maxGroundSpeed *= 2f;
                        ai.maxAirSpeed *= 2f;
                    }
                }
                break;

            case EffectType.Silence:
                ability?.DisableAbilities(enable);
                ai?.DisableAbilities(enable);
                break;

            case EffectType.Stun:
                movement?.controlMovement(!enable, !enable);
                ability?.DisableAbilities(enable);

                ai?.DisableMovement(enable);
                ai?.DisableAbilities(enable);
                break;
        }
    }
}