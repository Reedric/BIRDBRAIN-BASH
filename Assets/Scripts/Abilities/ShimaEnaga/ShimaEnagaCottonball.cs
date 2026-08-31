using System.Collections;
using UnityEngine;

/// <summary>
/// Handles the physical behavior of a Shima Enaga cottonball.
///
/// The Rigidbody remains disabled while the cottonball is being thrown.
/// Once it reaches the opponent's side, the Rigidbody is enabled and
/// the cottonball becomes a physical bounce obstacle.
/// </summary>
public class ShimaEnagaCottonball : MonoBehaviour
{
    [Header("Bounce")]
    [SerializeField] private float bounceForce = 7f;
    [SerializeField] private float minimumUpwardForce = 3f;
    [SerializeField] private float randomHorizontalForce = 5f;

    [Header("Squash & Stretch")]
    [SerializeField] private float squashAmount = 0.75f;
    [SerializeField] private float stretchAmount = 1.25f;
    [SerializeField] private float squashDuration = 0.12f;

    [Header("Sound")]
    [SerializeField] private SoundType touchSound = SoundType.DEFENSIVE;

    [Header("Destruction VFX")]
    [SerializeField] private GameObject poofVFXPrefab;
    [SerializeField] private float poofVFXLifetime = 2f;

    private ShimaEnagaOffensive owner;
    private GameObject spawnedCottonball;
    private Rigidbody rb;

    private float lifetime;
    private Vector3 landingPosition;

    private Vector3 originalScale;

    private bool hasLanded;
    private bool hasBeenTouched;

    // Tracks the in-progress squash/stretch animation so a rapid
    // re-touch doesn't start a second coroutine on top of it.
    private Coroutine squashStretchRoutine;

    public void Initialize(
        ShimaEnagaOffensive abilityOwner,
        GameObject spawnedObject,
        float lifetimeSeconds,
        Vector3 targetPosition
    )
    {
        owner = abilityOwner;
        spawnedCottonball = spawnedObject;
        lifetime = lifetimeSeconds;
        landingPosition = targetPosition;

        rb = GetComponent<Rigidbody>();

        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        // IMPORTANT:
        // Rigidbody is completely disabled during the throw.
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.detectCollisions = false;

        originalScale = transform.localScale;

        hasLanded = false;
        hasBeenTouched = false;
    }

    public void Land()
    {
        if (hasLanded)
            return;

        hasLanded = true;

        transform.position = landingPosition;

        if (rb == null)
            return;

        // Keep the cottonball kinematic even after landing so it acts
        // as a fixed obstacle — birds can still collide with it (and
        // get bounced via AddForce in BounceBird), but can't
        // physically shove it out of position.
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = true;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Snap it exactly onto its landing spot.
        rb.position = landingPosition;
        transform.position = landingPosition;

        StartCoroutine(LifetimeRoutine());
    }

    private IEnumerator LifetimeRoutine()
    {
        float elapsed = 0f;

        while (elapsed < lifetime)
        {
            if (!GameManager.PointInProgress())
            {
                DestroyCottonball();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        DestroyCottonball();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!hasLanded)
            return;

        HandleTouch(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!hasLanded)
            return;

        HandleTouch(other.gameObject);
    }

    private void HandleTouch(GameObject touchedObject)
    {
        if (touchedObject == null)
            return;

        GameObject bird = FindBirdFromObject(touchedObject);

        if (bird == null)
            return;

        if (!IsEnemyBird(bird))
            return;

        // Prevent a single physics collision from triggering
        // the bounce repeatedly.
        if (hasBeenTouched)
            return;

        hasBeenTouched = true;

        AudioManager.PlayBirdSound(
            BirdType.SHIMAENAGA,
            touchSound,
            1.0f
        );

        BounceBird(bird);

        // Stop any squash/stretch still in progress so back-to-back
        // touches don't leave two coroutines fighting over the
        // cottonball's scale.
        if (squashStretchRoutine != null)
        {
            StopCoroutine(squashStretchRoutine);
            squashStretchRoutine = null;
        }

        transform.localScale = originalScale;
        squashStretchRoutine = StartCoroutine(SquashAndStretch());

        // Allow another bird collision after the animation has
        // started. This means the cottonball remains usable.
        StartCoroutine(ResetTouchLock());
    }

    private GameObject FindBirdFromObject(GameObject objectHit)
    {
        // Match against the GameManager's four player slots instead of
        // requiring a specific component. This makes bird detection
        // work for both player-controlled birds (BallInteract) and
        // AI-controlled birds (AIBehavior), since both are registered
        // in these slots regardless of which script they carry.
        GameManager gameManager = GameManager.Instance;

        if (gameManager == null)
            return null;

        Transform current = objectHit.transform;

        while (current != null)
        {
            GameObject candidate = current.gameObject;

            if (candidate == gameManager.leftPlayer1 ||
                candidate == gameManager.leftPlayer2 ||
                candidate == gameManager.rightPlayer1 ||
                candidate == gameManager.rightPlayer2)
            {
                return candidate;
            }

            current = current.parent;
        }

        return null;
    }

    private bool IsEnemyBird(GameObject bird)
    {
        if (owner == null || bird == null)
            return false;

        GameManager gameManager = GameManager.Instance;

        bool ownerIsLeft =
            owner.gameObject == gameManager.leftPlayer1 ||
            owner.gameObject == gameManager.leftPlayer2;

        bool ownerIsRight =
            owner.gameObject == gameManager.rightPlayer1 ||
            owner.gameObject == gameManager.rightPlayer2;

        if (!ownerIsLeft && !ownerIsRight)
            return false;

        bool birdIsLeft =
            bird == gameManager.leftPlayer1 ||
            bird == gameManager.leftPlayer2;

        bool birdIsRight =
            bird == gameManager.rightPlayer1 ||
            bird == gameManager.rightPlayer2;

        if (ownerIsLeft)
            return birdIsRight;

        return birdIsLeft;
    }

    private void BounceBird(GameObject bird)
    {
        Rigidbody birdRb = bird.GetComponent<Rigidbody>();

        if (birdRb == null)
            return;

        // Push the bird opposite its current direction of travel so
        // the knockback reads as a left/right shove instead of a
        // coin-flip. If the bird is basically stationary, push it
        // away from the cottonball itself instead. Random is only a
        // last-resort fallback for the degenerate case where both of
        // those vectors are zero.
        Vector2 currentHorizontal = new Vector2(
            birdRb.linearVelocity.x,
            birdRb.linearVelocity.z
        );

        Vector2 horizontalDirection;

        if (currentHorizontal.sqrMagnitude > 0.01f)
        {
            horizontalDirection = -currentHorizontal.normalized;
        }
        else
        {
            Vector3 awayFromBall = bird.transform.position - transform.position;
            Vector2 away = new Vector2(awayFromBall.x, awayFromBall.z);

            horizontalDirection = away.sqrMagnitude > 0.01f
                ? away.normalized
                : Random.insideUnitCircle.normalized;
        }

        Vector3 forceDirection = new Vector3(
            horizontalDirection.x,
            0f,
            horizontalDirection.y
        );

        // Make sure the bird gets a meaningful horizontal push.
        forceDirection *= randomHorizontalForce;

        // Always give the bird some upward launch.
        forceDirection.y = Mathf.Max(
            minimumUpwardForce,
            bounceForce
        );

        birdRb.linearVelocity = Vector3.zero;
        birdRb.AddForce(
            forceDirection,
            ForceMode.VelocityChange
        );
    }

    private IEnumerator SquashAndStretch()
    {
        transform.localScale = originalScale;

        Vector3 squashScale = new Vector3(
            originalScale.x * stretchAmount,
            originalScale.y * squashAmount,
            originalScale.z * stretchAmount
        );

        Vector3 stretchScale = new Vector3(
            originalScale.x * squashAmount,
            originalScale.y * stretchAmount,
            originalScale.z * squashAmount
        );

        // Animate through each pose instead of snapping to it, so the
        // squash/stretch actually reads as motion.
        yield return LerpScale(originalScale, squashScale, squashDuration);
        yield return LerpScale(squashScale, stretchScale, squashDuration);
        yield return LerpScale(stretchScale, originalScale, squashDuration);

        squashStretchRoutine = null;
    }

    private IEnumerator LerpScale(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transform.localScale = Vector3.LerpUnclamped(from, to, smoothT);

            yield return null;
        }

        transform.localScale = to;
    }

    private IEnumerator ResetTouchLock()
    {
        yield return new WaitForSeconds(0.15f);

        hasBeenTouched = false;
    }

    private void DestroyCottonball()
    {
        if (owner != null)
            owner.RemoveCottonball(gameObject);

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        StopAllCoroutines();

        if (owner != null)
            owner.RemoveCottonball(gameObject);

        SpawnPoofVFX();
    }

    private void SpawnPoofVFX()
    {
        if (poofVFXPrefab == null)
            return;

        // Guard against instantiating during app quit/scene teardown.
        if (!Application.isPlaying)
            return;

        GameObject poof = Instantiate(
            poofVFXPrefab,
            transform.position,
            Quaternion.identity
        );

        Destroy(poof, poofVFXLifetime);
    }
}