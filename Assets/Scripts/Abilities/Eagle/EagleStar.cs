using System.Collections;
using UnityEngine;

public class EagleStar : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 45f;
    [SerializeField] private float scaleTransitionDuration = 0.35f;

    private EagleDefensive owner;
    private GameObject spawnedStar;
    private bool animateVisuals;
    private bool isExiting;
    private float rotationDirection;
    private Vector3 targetScale;
    private Coroutine lifetimeCoroutine;
    private Coroutine exitCoroutine;

    public void Initialize(EagleDefensive defensiveAbility, GameObject rootStar, float lifetime, bool animateRoot)
    {
        owner = defensiveAbility;
        spawnedStar = rootStar;
        animateVisuals = animateRoot;

        if (animateVisuals)
        {
            targetScale = transform.localScale;
            rotationDirection = Random.value < 0.5f ? -1f : 1f;
            transform.localScale = Vector3.zero;
            lifetimeCoroutine = StartCoroutine(AnimateLifetime(lifetime));
        }
    }

    private void Update()
    {
        if (animateVisuals)
        {
            transform.Rotate(Vector3.up, rotationDirection * rotationSpeed * Time.deltaTime, Space.World);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryBounce(collision.collider.GetComponentInParent<BallManager>());
    }

    private void OnTriggerEnter(Collider other)
    {
        TryBounce(other.GetComponentInParent<BallManager>());
    }

    private void TryBounce(BallManager ball)
    {
        if (!isExiting && owner != null && ball != null)
            owner.HandleStarHit(this);
    }

    public GameObject SpawnedStar => spawnedStar;

    public void BeginExit()
    {
        if (!animateVisuals || isExiting || spawnedStar == null)
            return;

        isExiting = true;
        owner = null;
        foreach (Collider collider in spawnedStar.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        if (lifetimeCoroutine != null)
            StopCoroutine(lifetimeCoroutine);

        exitCoroutine = StartCoroutine(AnimateExit());
    }

    private IEnumerator AnimateLifetime(float lifetime)
    {
        float transitionDuration = Mathf.Min(scaleTransitionDuration, lifetime * 0.5f);
        yield return ScaleTo(targetScale, transitionDuration);

        yield return new WaitForSeconds(Mathf.Max(0f, lifetime - transitionDuration * 2f));
        yield return ScaleTo(Vector3.zero, transitionDuration);

        if (!isExiting)
            BeginExit();
    }

    private IEnumerator AnimateExit()
    {
        yield return ScaleTo(Vector3.zero, scaleTransitionDuration);

        if (spawnedStar != null)
            Destroy(spawnedStar);
    }

    private IEnumerator ScaleTo(Vector3 destination, float duration)
    {
        Vector3 startingScale = transform.localScale;
        if (duration <= 0f)
        {
            transform.localScale = destination;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startingScale, destination, elapsed / duration);
            yield return null;
        }

        transform.localScale = destination;
    }
}