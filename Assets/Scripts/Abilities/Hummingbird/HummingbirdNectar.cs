using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HummingbirdNectar : MonoBehaviour
{
    // The slow itself is a stat modifier on the bird (see BirdStats), keyed to this nectar. Saving the bird's stats
    // and putting them back would undo (or bring back) whatever else changed them in the meantime, like a stun.
    private sealed class AffectedBird
    {
        public GameObject vfx;
        public Coroutine lingerCoroutine;
    }

    private readonly Dictionary<GameObject, AffectedBird> affectedBirds = new();
    private readonly List<Renderer> nectarRenderers = new();
    private readonly List<Material> nectarMaterials = new();
    private readonly List<Color> originalColors = new();
    private GameObject birdNectarVfxPrefab;
    private float slowAmount;
    private float jumpReduction;
    private float lingerDuration;
    private float lifetime;
    private float fadeDuration;
    private Coroutine lifetimeCoroutine;
    private bool fading;

    private void Update()
    {
        if (!fading && GameManager.Instance.gameState == GameManager.GameState.PointEnd)
            StartCoroutine(FadeOutAndDestroy());
    }

    public void Initialize(
        float speedReduction,
        float jumpForceReduction,
        float effectLingerDuration,
        float nectarDuration,
        float nectarFadeDuration,
        GameObject birdVfxPrefab)
    {
        slowAmount = speedReduction;
        jumpReduction = jumpForceReduction;
        lingerDuration = effectLingerDuration;
        lifetime = nectarDuration;
        fadeDuration = nectarFadeDuration;
        birdNectarVfxPrefab = birdVfxPrefab;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            nectarRenderers.Add(renderer);
            foreach (Material material in renderer.materials)
            {
                nectarMaterials.Add(material);
                originalColors.Add(GetMaterialColor(material));
            }
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
            colliders = new[] { gameObject.AddComponent<SphereCollider>() };
        foreach (Collider collider in colliders)
        {
            collider.isTrigger = true;
            if (collider.gameObject != gameObject)
            {
                HummingbirdNectarCollider proxy = collider.gameObject.AddComponent<HummingbirdNectarCollider>();
                proxy.Initialize(this);
            }
        }

        lifetimeCoroutine = StartCoroutine(LifetimeRoutine());
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleTriggerEnter(other);
    }

    public void HandleTriggerEnter(Collider other)
    {
        GameObject bird = FindBird(other);
        if (bird != null)
            ApplyToBird(bird);
    }

    private void OnTriggerExit(Collider other)
    {
        HandleTriggerExit(other);
    }

    public void HandleTriggerExit(Collider other)
    {
        GameObject bird = FindBird(other);
        if (bird != null && affectedBirds.TryGetValue(bird, out AffectedBird affected))
        {
            if (affected.lingerCoroutine != null)
                StopCoroutine(affected.lingerCoroutine);
            affected.lingerCoroutine = StartCoroutine(RemoveAfterLinger(bird, affected));
        }
    }

    private GameObject FindBird(Collider collider)
    {
        BallInteract player = collider.GetComponentInParent<BallInteract>();
        if (player != null)
            return player.gameObject;

        AIBehavior ai = collider.GetComponentInParent<AIBehavior>();
        return ai != null ? ai.gameObject : null;
    }

    private void ApplyToBird(GameObject bird)
    {
        if (!affectedBirds.TryGetValue(bird, out AffectedBird affected))
        {
            if (!bird.TryGetComponent(out CharacterMovement _) && !bird.TryGetComponent(out AIBehavior _))
                return;

            affected = new AffectedBird();
            affectedBirds[bird] = affected;
            BirdStats.Apply(bird, this, new StatModifier { speedAdd = -slowAmount, jumpAdd = -jumpReduction });
            affected.vfx = SpawnBirdVfx(bird);
        }

        if (affected.lingerCoroutine != null)
        {
            StopCoroutine(affected.lingerCoroutine);
            affected.lingerCoroutine = null;
        }
    }

    private IEnumerator RemoveAfterLinger(GameObject bird, AffectedBird affected)
    {
        yield return new WaitForSeconds(lingerDuration);
        RestoreBird(bird, affected);
    }

    private void RestoreBird(GameObject bird, AffectedBird affected)
    {
        BirdStats.Remove(bird, this);

        if (affected.vfx != null)
            Destroy(affected.vfx);
        affectedBirds.Remove(bird);
    }

    private GameObject SpawnBirdVfx(GameObject bird)
    {
        if (birdNectarVfxPrefab == null)
            return null;

        return NetworkFx.Instantiate(birdNectarVfxPrefab, bird.transform);
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetime);
        yield return FadeOutAndDestroy();
    }

    private IEnumerator FadeOutAndDestroy()
    {
        if (fading)
            yield break;
        fading = true;

        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        foreach (KeyValuePair<GameObject, AffectedBird> entry in affectedBirds)
        {
            if (entry.Value.lingerCoroutine != null)
                StopCoroutine(entry.Value.lingerCoroutine);
            BirdStats.Remove(entry.Key, this);
            if (entry.Value.vfx != null)
                Destroy(entry.Value.vfx);
        }
        affectedBirds.Clear();

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            for (int i = 0; i < nectarMaterials.Count; i++)
            {
                Color color = originalColors[i];
                color.a *= alpha;
                SetMaterialColor(nectarMaterials[i], color);
            }
            yield return null;
        }

        Destroy(gameObject);
    }

    private static Color GetMaterialColor(Material material)
    {
        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color"))
            return material.GetColor("_Color");
        return Color.white;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private void OnDestroy()
    {
        if (lifetimeCoroutine != null)
            StopCoroutine(lifetimeCoroutine);

        foreach (KeyValuePair<GameObject, AffectedBird> entry in affectedBirds)
        {
            BirdStats.Remove(entry.Key, this);
            if (entry.Value.vfx != null)
                Destroy(entry.Value.vfx);
        }
        affectedBirds.Clear();
    }
}

public class HummingbirdNectarCollider : MonoBehaviour
{
    private HummingbirdNectar nectar;

    public void Initialize(HummingbirdNectar nectarZone)
    {
        nectar = nectarZone;
    }

    private void OnTriggerEnter(Collider other)
    {
        nectar?.HandleTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        nectar?.HandleTriggerExit(other);
    }
}