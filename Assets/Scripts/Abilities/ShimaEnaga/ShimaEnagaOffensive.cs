using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shima Enaga Offensive Ability — Cottonball Bounce
///
/// Spawns three cottonballs at the Shima Enaga and throws them
/// toward random positions on the opponent's side of the court.
/// Once a cottonball reaches its destination, its Rigidbody is
/// enabled and it becomes a bouncy obstacle.
///
/// Cottonballs last for 8 seconds and are cleared when the point ends.
/// If the point ends before the cottonballs finish deploying, the
/// ability cooldown is refunded.
/// </summary>
public class ShimaEnagaOffensive : BirdAbility
{
    [Header("Cottonball Bounce")]
    [SerializeField] private GameObject cottonballPrefab;
    [SerializeField] private int cottonballCount = 3;
    [SerializeField] private float cottonballLifetime = 8f;

    [Header("Throwing")]
    [SerializeField] private float throwDuration = 0.65f;
    [SerializeField] private float throwArcHeight = 2f;
    [SerializeField] private float spawnHeightOffset = 0.5f;

    [Header("Landing Positions")]
    [SerializeField] private float courtSideMinX = 0.5f;
    [SerializeField] private float courtSideMaxX = 7f;
    [SerializeField] private float courtMinZ = -4f;
    [SerializeField] private float courtMaxZ = 4f;
    [SerializeField] private float minimumLandingSpacing = 2f;
    [SerializeField] private float landingHeight = 0.5f;

    private readonly List<GameObject> activeCottonballs = new();

    private bool isDeploying;
    private bool deploymentCompleted;

    private void Awake()
    {
        if (_cooldownTime <= 0f)
            _cooldownTime = 30f;
    }

    private void Update()
    {
        if (activeCottonballs.Count > 0 &&
            GameManager.Instance.gameState == GameManager.GameState.PointEnd)
        {
            bool wasDeploying = isDeploying;

            ClearCottonballs();

            // If the point ended before deployment finished,
            // refund the ability cooldown.
            if (wasDeploying && !deploymentCompleted)
                RefundCooldown();
        }
    }

    protected override bool Activate()
    {
        if (cottonballPrefab == null)
        {
            Debug.LogWarning(
                "ShimaEnagaOffensive: Assign a cottonball prefab before using the ability."
            );

            return false;
        }

        ClearCottonballs();

        int playerID = GetPlayerID(gameObject);

        if (playerID < 0)
        {
            Debug.LogWarning(
                "ShimaEnagaOffensive: Could not find BallInteract or AIBehavior on the Shima Enaga."
            );

            return false;
        }

        // Start deployment.
        isDeploying = true;
        deploymentCompleted = false;

        AudioManager.PlayBirdSound(
            BirdType.SHIMAENAGA,
            SoundType.OFFENSIVE,
            1.0f
        );

        // HUD cooldown starts through the normal BirdAbility system,
        // but we also trigger the visual HUD cooldown immediately.
        HUDManager.Instance.TriggerOffensiveCooldown(
            playerID,
            _cooldownTime
        );

        StartCoroutine(DeployCottonballs());

        return true;
    }

    private IEnumerator DeployCottonballs()
    {
        bool isLeftPlayer =
            gameObject == GameManager.Instance.leftPlayer1 ||
            gameObject == GameManager.Instance.leftPlayer2;

        bool isRightPlayer =
            gameObject == GameManager.Instance.rightPlayer1 ||
            gameObject == GameManager.Instance.rightPlayer2;

        if (!isLeftPlayer && !isRightPlayer)
        {
            Debug.LogWarning(
                "ShimaEnagaOffensive: Shima Enaga is not assigned to a GameManager player slot."
            );

            isDeploying = false;
            RefundCooldown();
            yield break;
        }

        // The opponent's side is the opposite side of the Shima Enaga.
        float sideMinX;
        float sideMaxX;

        if (isLeftPlayer)
        {
            sideMinX = 0.5f;
            sideMaxX = 7f;
        }
        else
        {
            sideMinX = -7f;
            sideMaxX = -0.5f;
        }

        List<Vector3> landingPositions = FindLandingPositions(
            sideMinX,
            sideMaxX
        );

        if (landingPositions.Count == 0)
        {
            isDeploying = false;
            RefundCooldown();
            yield break;
        }

        Vector3 spawnPosition =
            transform.position + Vector3.up * spawnHeightOffset;

        int ballsToSpawn = Mathf.Min(
            cottonballCount,
            landingPositions.Count
        );

        List<Coroutine> throwCoroutines = new();

        for (int i = 0; i < ballsToSpawn; i++)
        {
            if (!GameManager.PointInProgress())
            {
                isDeploying = false;
                RefundCooldown();
                yield break;
            }

            GameObject cottonball = Instantiate(
                cottonballPrefab,
                spawnPosition,
                Quaternion.identity
            );

            activeCottonballs.Add(cottonball);

            ShimaEnagaCottonball cottonballScript =
                cottonball.GetComponent<ShimaEnagaCottonball>();

            if (cottonballScript == null)
            {
                cottonballScript =
                    cottonball.AddComponent<ShimaEnagaCottonball>();
            }

            cottonballScript.Initialize(
                this,
                cottonball,
                cottonballLifetime,
                landingPositions[i]
            );

            throwCoroutines.Add(
                StartCoroutine(
                    ThrowCottonball(
                        cottonball,
                        cottonballScript,
                        landingPositions[i]
                    )
                )
            );

            // Tiny stagger so the three balls don't appear as one blob.
            yield return new WaitForSeconds(0.08f);
        }

        // Wait until every cottonball has finished its throw.
        // We intentionally use a point-progress check here because
        // the point may end while the balls are airborne.
        float elapsed = 0f;

        while (elapsed < throwDuration + 0.2f)
        {
            if (!GameManager.PointInProgress())
            {
                isDeploying = false;
                RefundCooldown();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!GameManager.PointInProgress())
        {
            isDeploying = false;
            RefundCooldown();
            yield break;
        }

        deploymentCompleted = true;
        isDeploying = false;
    }

    private IEnumerator ThrowCottonball(
        GameObject cottonball,
        ShimaEnagaCottonball cottonballScript,
        Vector3 target
    )
    {
        if (cottonball == null || cottonballScript == null)
            yield break;

        Vector3 start = cottonball.transform.position;

        float elapsed = 0f;

        while (elapsed < throwDuration)
        {
            if (cottonball == null)
                yield break;

            // If the point ends while the ball is flying,
            // the ball should disappear immediately.
            if (!GameManager.PointInProgress())
            {
                if (cottonball != null)
                    Destroy(cottonball);

                yield break;
            }

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / throwDuration);

            // Smooth horizontal travel.
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 position = Vector3.Lerp(
                start,
                target,
                smoothT
            );

            // Arc the cottonball upward during its throw.
            float arc = Mathf.Sin(t * Mathf.PI) * throwArcHeight;
            position.y += arc;

            cottonball.transform.position = position;

            yield return null;
        }

        if (cottonball == null)
            yield break;

        cottonball.transform.position = target;

        cottonballScript.Land();
    }

    private List<Vector3> FindLandingPositions(
        float sideMinX,
        float sideMaxX
    )
    {
        List<Vector3> positions = new();

        for (
            int attempt = 0;
            attempt < 100 &&
            positions.Count < cottonballCount;
            attempt++
        )
        {
            Vector3 candidate = new(
                Random.Range(sideMinX, sideMaxX),
                landingHeight,
                Random.Range(courtMinZ, courtMaxZ)
            );

            bool spaced = true;

            foreach (Vector3 position in positions)
            {
                float distance = Vector2.Distance(
                    new Vector2(candidate.x, candidate.z),
                    new Vector2(position.x, position.z)
                );

                if (distance < minimumLandingSpacing)
                {
                    spaced = false;
                    break;
                }
            }

            if (spaced)
                positions.Add(candidate);
        }

        return positions;
    }
    

    public void RemoveCottonball(GameObject cottonball)
    {
        if (cottonball == null)
            return;

        activeCottonballs.Remove(cottonball);
    }

    private void ClearCottonballs()
    {
        StopAllCoroutines();

        foreach (GameObject cottonball in activeCottonballs)
        {
            if (cottonball != null)
                Destroy(cottonball);
        }

        activeCottonballs.Clear();

        isDeploying = false;
        deploymentCompleted = false;
    }

    private void RefundCooldown()
    {
        int playerID = GetPlayerID(gameObject);

        if (playerID >= 0 && HUDManager.Instance != null)
        {
            HUDManager.Instance.ResetOffensiveCooldown(playerID);
        }

        _cooldownRemaining = 0f;
        isDeploying = false;
        deploymentCompleted = false;
    }

    // Returns the player slot ID from whichever hit-interaction
    // component is present. BallInteract for player-controlled birds,
    // AIBehavior for AI-controlled ones, so this ability works
    // identically for both.
    private int GetPlayerID(GameObject bird)
    {
        BallInteract ballInteract = bird.GetComponent<BallInteract>();

        if (ballInteract != null)
            return ballInteract.playerID;

        AIBehavior aiBehavior = bird.GetComponent<AIBehavior>();

        if (aiBehavior != null)
            return aiBehavior.playerID;

        return -1;
    }

    private void OnDestroy()
    {
        foreach (GameObject cottonball in activeCottonballs)
        {
            if (cottonball != null)
                Destroy(cottonball);
        }

        activeCottonballs.Clear();
    }
}