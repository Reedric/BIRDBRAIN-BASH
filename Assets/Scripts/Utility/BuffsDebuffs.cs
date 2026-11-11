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
    private Dictionary<GameObject, Dictionary<EffectType, Coroutine>> activeEffects = new();
    private HashSet<RagdollManager> activeRagdolls = new();
    private Dictionary<GameObject, HashSet<EffectType>> preservedEffects = new();

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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // A new match is starting. Everything tracked so far belongs to birds from the last one, which are gone,
            // and ClearAllEffects (at the start of every point) would throw on them and stop the point from resetting.
            Instance.ResetForNewMatch();
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void ResetForNewMatch()
    {
        StopAllCoroutines();

        foreach (Dictionary<EffectType, GameObject> vfxByType in activeVFX.Values)
        {
            foreach (GameObject vfx in vfxByType.Values)
            {
                if (vfx != null) Destroy(vfx);
            }
        }

        activeVFX.Clear();
        activeEffects.Clear();
        activeRagdolls.Clear();
        preservedEffects.Clear();
    }

    public void ApplyEffect(EffectType type, GameObject bird, float duration, bool onLeft)
    {
        if (bird == null) return;

        // Reject any effect that tries to start outside of an active point
        // if (!GameManager.PointInProgress()) return;

        if (!activeEffects.ContainsKey(bird))
            activeEffects[bird] = new Dictionary<EffectType, Coroutine>();

        if (activeEffects[bird].ContainsKey(type))
        {
            StopCoroutine(activeEffects[bird][type]);
            ApplyGameplayEffect(type, bird, false);
        }

        Coroutine co = StartCoroutine(RunEffect(type, bird, duration, onLeft));
        activeEffects[bird][type] = co;
    }

    public void PreserveEffect(GameObject bird, EffectType type)
    {
        if (bird == null) return;
        if (!preservedEffects.ContainsKey(bird))
            preservedEffects[bird] = new HashSet<EffectType>();
        preservedEffects[bird].Add(type);
    }

    public void ReleaseEffect(GameObject bird, EffectType type)
    {
        if (bird == null) return;
        if (!preservedEffects.ContainsKey(bird)) return;
        preservedEffects[bird].Remove(type);
        if (preservedEffects[bird].Count == 0)
            preservedEffects.Remove(bird);
    }

    public bool HasActiveEffects(GameObject bird)
    {
        return bird != null
            && activeEffects.TryGetValue(bird, out Dictionary<EffectType, Coroutine> effects)
            && effects.Count > 0;
    }

    public bool IsEffectActive(EffectType type, GameObject bird)
    {
        return bird != null
            && activeEffects.ContainsKey(bird)
            && activeEffects[bird].ContainsKey(type);
    }

    private bool IsEffectPreserved(GameObject bird, EffectType type)
    {
        return bird != null
            && preservedEffects.ContainsKey(bird)
            && preservedEffects[bird].Contains(type);
    }

    private IEnumerator RunEffect(EffectType type, GameObject bird, float duration, bool onLeft)
    {
        SpawnEffect(type, bird, onLeft);

        // Play start sound
        if (type == EffectType.Buff)
            AudioManager.PlayBuffStartSound();
        else
            AudioManager.PlayDebuffStartSound();

        // Apply the gameplay effect (silence, stun, speed changes, etc.)
        ApplyGameplayEffect(type, bird, true);

        yield return new WaitForSeconds(duration);

        // The bird can be gone by now, e.g. if the match ended and this object carried on into the next scene
        if (bird == null)
        {
            ForgetBird(bird);
            yield break;
        }

        // Remove the gameplay effect
        ApplyGameplayEffect(type, bird, false);

        // Play end sound
        if (type == EffectType.Buff)
            AudioManager.PlayBuffEndSound();
        else
            AudioManager.PlayDebuffEndSound();

        // Tear down VFX
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

        // If VFX already exists, don't spawn another
        if (activeVFX[bird].ContainsKey(type))
        {
            return activeVFX[bird][type];
        }

        GameObject prefab = ResolvePrefab(type, onLeft);
        if (prefab == null) return null;

        GameObject vfx = NetworkFx.Instantiate(prefab, bird.transform);
        vfx.transform.localPosition = Vector3.zero;

        activeVFX[bird][type] = vfx;

        return vfx;
    }

    private GameObject ResolvePrefab(EffectType type, bool onLeft)
    {
        EffectSet set = onLeft ? team1Effects : team2Effects;

        return type switch
        {
            EffectType.Buff    => set.buff,
            EffectType.Debuff  => set.debuff,
            EffectType.Silence => set.silence,
            EffectType.Stun    => set.stun,
            _                  => null
        };
    }

    public void TrackRagdoll(RagdollManager rm, bool active)
    {
        if (active) activeRagdolls.Add(rm);
        else activeRagdolls.Remove(rm);
    }

    // Drops everything tracked for a bird that has been destroyed. (Dictionary lookups still work on a destroyed
    // GameObject, since it's the same C# object, even though Unity treats it as null.)
    private void ForgetBird(GameObject bird)
    {
        activeEffects.Remove(bird);
        activeVFX.Remove(bird);
        preservedEffects.Remove(bird);
    }

    // Each effect type is one entry in the bird's stat modifiers (see BirdStats). Multiplying and dividing
    // the stats, or saving and restoring them for a stun, goes wrong whenever two effects overlap: a buff ending
    // during a stun undoes the stun, and a stun ending puts back the speed from before a buff that's already gone.
    private static readonly object buffStatKey = new object();
    private static readonly object debuffStatKey = new object();
    private static readonly object stunStatKey = new object();

    private void ApplyGameplayEffect(EffectType type, GameObject bird, bool enable)
    {
        if (bird == null) return;

        switch (type)
        {
            case EffectType.Buff:
                if (enable) BirdStats.Apply(bird, buffStatKey, StatModifier.Multiply(1.5f));
                else BirdStats.Remove(bird, buffStatKey);
                break;

            case EffectType.Debuff:
                if (enable) BirdStats.Apply(bird, debuffStatKey, StatModifier.Multiply(0.5f));
                else BirdStats.Remove(bird, debuffStatKey);
                break;

            case EffectType.Silence:
                // Every ability on the bird, not just whichever one GetComponent finds first
                foreach (BirdAbility ability in bird.GetComponentsInChildren<BirdAbility>(true))
                    ability.SetSilenced(enable);
                if (bird.TryGetComponent(out AIBehavior ai)) ai.SilenceAbilities(enable);
                break;

            case EffectType.Stun:
                if (enable)
                {
                    BirdStats.Apply(bird, stunStatKey, StatModifier.Stunned);
                    AudioManager.PlayStunLoop(bird);
                }
                else
                {
                    BirdStats.Remove(bird, stunStatKey);
                    AudioManager.StopStunLoop(bird);
                }
                break;
        }
    }

    // Puts the stat changes from effects that are still running back on a bird after its stats were reset for a
    // new point. Only effects that last across points (Ostrich's buff) are still running by then.
    public void ReapplyActiveEffects(GameObject bird)
    {
        if (bird == null || !activeEffects.TryGetValue(bird, out Dictionary<EffectType, Coroutine> effects)) return;

        foreach (EffectType type in effects.Keys)
        {
            if (type == EffectType.Buff || type == EffectType.Debuff)
                ApplyGameplayEffect(type, bird, true);
        }
    }

    // cleanup effects after point is over
    public void ClearAllEffects()
    {
        var activeEffectsCopy = new Dictionary<GameObject, Dictionary<EffectType, Coroutine>>(activeEffects);

        foreach (var bird in activeEffectsCopy.Keys)
        {
            if (bird == null)
            {
                foreach (Coroutine coroutine in activeEffectsCopy[bird].Values)
                {
                    if (coroutine != null) StopCoroutine(coroutine);
                }

                ForgetBird(bird);
                continue;
            }

            foreach (var kvp in new Dictionary<EffectType, Coroutine>(activeEffectsCopy[bird]))
            {
                if (IsEffectPreserved(bird, kvp.Key))
                    continue;

                StopCoroutine(kvp.Value);
                ApplyGameplayEffect(kvp.Key, bird, false);
                activeEffects[bird].Remove(kvp.Key);
            }

            if (activeEffects.ContainsKey(bird) && activeEffects[bird].Count == 0)
                activeEffects.Remove(bird);
        }

        // Destroy the spawned VFX too. Just clearing the dict
        // leaves the GameObjects alive, and the particles replay forever
        var vfxCopy = new Dictionary<GameObject, Dictionary<EffectType, GameObject>>(activeVFX);
        foreach (var bird in vfxCopy.Keys)
        {
            foreach (var kvp in new Dictionary<EffectType, GameObject>(vfxCopy[bird]))
            {
                if (IsEffectPreserved(bird, kvp.Key))
                    continue;

                if (kvp.Value != null) Destroy(kvp.Value);
                activeVFX[bird].Remove(kvp.Key);
            }

            if (activeVFX.ContainsKey(bird) && activeVFX[bird].Count == 0)
                activeVFX.Remove(bird);
        }
    }
}