using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhoenixOffensive : BirdAbility
{
    [Header("Rotisserie Chicken")]
    [SerializeField] private GameObject rotisserieChickenPrefab;
    [SerializeField] private float transformationRadius = 3f;
    [SerializeField] private float rotisserieDuration = 8f;

    [Header("Transformation VFX")]
    [SerializeField] private GameObject transformEffectPrefab;
    [SerializeField] private GameObject restoreEffectPrefab;

    private BallInteract ballInteraction;

    private readonly List<ChickenTarget> activeChickenTargets = new List<ChickenTarget>();

    private Coroutine transformationCoroutine;
    private Coroutine waitForEnemyHitCoroutine;

    private bool transformationActive = false;
    private bool waitingForEnemyHit = false;
    private bool cooldownPending = false;

    private class ChickenTarget
    {
        public GameObject original;
        public GameObject chicken;

        public CharacterMovement characterMovement;
        public AIBehavior aiBehavior;

        public bool wasAI;
        public bool wasActive;

        public bool originalCanMove;
        public bool originalCanJump;

        public Vector3 originalVelocity;
        public Quaternion originalRotation;

        public List<FollowObject> followers = new List<FollowObject>();
    }

    void Start()
    {
        ballInteraction = GetComponent<BallInteract>();

        // The cooldown should only begin after the rotisserie effect has finished.
        _delayCooldownStart = true;
    }

    protected override bool Activate()
    {
        if (transformationActive || waitingForEnemyHit)
            return false;

        if (ballInteraction == null)
            ballInteraction = GetComponent<BallInteract>();

        if (ballInteraction == null || BallManager.Instance == null || GameManager.Instance == null)
            return false;

        GameManager gameManager = GameManager.Instance;

        // Phoenix can only use the offensive after a teammate has bumped or set the ball.
        if (gameManager.gameState != GameManager.GameState.Bumped &&
            gameManager.gameState != GameManager.GameState.Set)
        {
            Debug.Log("[PhoenixOffensive] Cannot activate: ball is not ready to be spiked.");
            return false;
        }

        // Make sure the previous hit was made by Phoenix's team.
        if (gameManager.leftAttack != ballInteraction.onLeft)
        {
            Debug.Log("[PhoenixOffensive] Cannot activate: it is the opposing team's turn.");
            return false;
        }

        // Make sure the ball is physically on Phoenix's side of the court.
        if (BallManager.Instance.transform.position.x * transform.position.x < 0)
        {
            Debug.Log("[PhoenixOffensive] Cannot activate: ball is on the opposing side.");
            return false;
        }

        StartRotisserie();

        return true;
    }

    private void StartRotisserie()
    {
        waitingForEnemyHit = true;
        cooldownPending = false;

        // Spike the ball and arm the rotisserie effect.
        ballInteraction.SpikeBall();

        waitForEnemyHitCoroutine = StartCoroutine(WaitForEnemyHit());

        Debug.Log("[PhoenixOffensive] Ball is armed with Burn Ball.");
    }

    private IEnumerator WaitForEnemyHit()
    {
        GameManager gameManager = GameManager.Instance;

        while (waitingForEnemyHit)
        {
            if (gameManager == null || BallManager.Instance == null)
            {
                CancelBurnBall();
                yield break;
            }

            // If the point ends before the enemy touches the ball,
            // cancel the ability without creating chickens.
            if (gameManager.gameState == GameManager.GameState.PointEnd)
            {
                Debug.Log("[PhoenixOffensive] Point ended before enemy touched the ball. Burn Ball canceled.");
                CancelBurnBall();
                yield break;
            }

            // The ball has been bumped or blocked.
            if (gameManager.gameState == GameManager.GameState.Bumped ||
                gameManager.gameState == GameManager.GameState.Blocked)
            {
                // Make absolutely sure the hit came from the opposing team.
                if (gameManager.leftAttack != ballInteraction.onLeft)
                {
                    Vector3 ballPosition = BallManager.Instance.transform.position;

                    waitingForEnemyHit = false;
                    waitForEnemyHitCoroutine = null;

                    ActivateRotisserie(ballPosition);
                    yield break;
                }
            }

            yield return null;
        }
    }

    private void ActivateRotisserie(Vector3 center)
    {
        transformationActive = true;
        cooldownPending = true;

        TransformOpponentsAroundBall(center);

        // If nobody was inside the radius, the ability still successfully
        // burned the ball, so allow the normal rotisserie duration to run.
        transformationCoroutine = StartCoroutine(RotisserieTimer());

        Debug.Log("[PhoenixOffensive] Enemy touched Burn Ball. Rotisserie activated.");
    }

    private void CancelBurnBall()
    {
        waitingForEnemyHit = false;

        if (waitForEnemyHitCoroutine != null)
        {
            StopCoroutine(waitForEnemyHitCoroutine);
            waitForEnemyHitCoroutine = null;
        }

        // No chickens were created, so there is no cooldown to start.
        cooldownPending = false;
        transformationActive = false;

        // Make sure the ability can be activated again immediately.
        _cooldownRemaining = 0f;

        Debug.Log("[PhoenixOffensive] Burn Ball canceled. Cooldown refunded.");
    }

    private void TransformOpponentsAroundBall(Vector3 center)
    {
        GameManager gameManager = GameManager.Instance;

        if (gameManager == null)
            return;

        List<GameObject> opponents = new List<GameObject>();

        if (gameManager.leftPlayer1 != null && gameManager.leftPlayer1 != gameObject)
            if (!ballInteraction.onLeft)
                opponents.Add(gameManager.leftPlayer1);

        if (gameManager.leftPlayer2 != null && gameManager.leftPlayer2 != gameObject)
            if (!ballInteraction.onLeft)
                opponents.Add(gameManager.leftPlayer2);

        if (gameManager.rightPlayer1 != null && gameManager.rightPlayer1 != gameObject)
            if (ballInteraction.onLeft)
                opponents.Add(gameManager.rightPlayer1);

        if (gameManager.rightPlayer2 != null && gameManager.rightPlayer2 != gameObject)
            if (ballInteraction.onLeft)
                opponents.Add(gameManager.rightPlayer2);

        foreach (GameObject opponent in opponents)
        {
            if (opponent == null)
                continue;

            if (IsAlreadyTransformed(opponent))
                continue;

            float distance = Vector3.Distance(
                center,
                opponent.transform.position
            );

            if (distance <= transformationRadius)
            {
                TransformPlayer(opponent);
            }
        }
    }

    private bool IsAlreadyTransformed(GameObject target)
    {
        foreach (ChickenTarget chickenTarget in activeChickenTargets)
        {
            if (chickenTarget.original == target)
                return true;
        }

        return false;
    }

    private void TransformPlayer(GameObject target)
    {
        ChickenTarget chickenTarget = new ChickenTarget();

        chickenTarget.original = target;
        chickenTarget.originalRotation = target.transform.rotation;

        chickenTarget.characterMovement = target.GetComponent<CharacterMovement>();
        chickenTarget.aiBehavior = target.GetComponent<AIBehavior>();

        chickenTarget.wasAI = chickenTarget.aiBehavior != null;
        chickenTarget.wasActive = target.activeSelf;

        FollowObject[] allFollowers =
            FindObjectsByType<FollowObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (FollowObject follower in allFollowers)
        {
            if (follower != null && follower.target == target.transform)
            {
                chickenTarget.followers.Add(follower);
            }
        }

        if (chickenTarget.characterMovement != null)
        {
            chickenTarget.originalCanMove = true;
            chickenTarget.originalCanJump = true;

            chickenTarget.characterMovement.controlMovement(false, false);
        }

        if (chickenTarget.aiBehavior != null)
        {
            Rigidbody originalRb = target.GetComponent<Rigidbody>();

            if (originalRb != null)
            {
                chickenTarget.originalVelocity = originalRb.linearVelocity;
                originalRb.linearVelocity = new Vector3(
                    0f,
                    originalRb.linearVelocity.y,
                    0f
                );
            }

            chickenTarget.aiBehavior.enabled = false;
        }

        PlayEffect(transformEffectPrefab, target.transform.position);

        bool onLeft = ballInteraction.onLeft;
        int playerID = -1;

        if (chickenTarget.aiBehavior != null)
        {
            playerID = chickenTarget.aiBehavior.playerID;
            onLeft = chickenTarget.aiBehavior.onLeft;
        }
        else
        {
            BallInteract targetBallInteract = target.GetComponent<BallInteract>();

            if (targetBallInteract != null)
                onLeft = targetBallInteract.onLeft;
        }

        Vector3 spawnPosition = target.transform.position;
        Quaternion spawnRotation = target.transform.rotation;

        target.SetActive(false);

        if (rotisserieChickenPrefab == null)
        {
            activeChickenTargets.Add(chickenTarget);
            return;
        }

        GameObject chicken = Instantiate(
            rotisserieChickenPrefab,
            spawnPosition,
            spawnRotation
        );

        chickenTarget.chicken = chicken;

        chicken.SetActive(true);

        foreach (FollowObject follower in chickenTarget.followers)
        {
            if (follower != null)
                follower.target = chicken.transform;
        }

        ConfigureChicken(chicken, onLeft, playerID);

        activeChickenTargets.Add(chickenTarget);
    }

    private void ConfigureChicken(GameObject chicken, bool onLeft, int playerID)
    {
        if (chicken == null)
            return;

        AIBehavior chickenAI = chicken.GetComponent<AIBehavior>();
        CharacterMovement chickenMovement = chicken.GetComponent<CharacterMovement>();
        BallInteract chickenBallInteract = chicken.GetComponent<BallInteract>();

        if (chickenAI != null)
        {
            chickenAI.SetIdentity(onLeft, playerID);
            chickenAI.enabled = true;
        }

        if (chickenMovement != null)
        {
            chickenMovement.enabled = chickenAI == null;

            if (chickenAI == null)
                chickenMovement.controlMovement(true, true);
        }

        if (chickenBallInteract != null)
        {
            chickenBallInteract.enabled = true;
        }

        BirdAbility[] chickenAbilities =
            chicken.GetComponentsInChildren<BirdAbility>(true);

        foreach (BirdAbility ability in chickenAbilities)
        {
            if (ability != null)
                ability.SetAbilitiesDisabled(true);
        }

        PlayEffect(transformEffectPrefab, chicken.transform.position);
    }

    private IEnumerator RotisserieTimer()
    {
        float timer = 0f;

        while (timer < rotisserieDuration)
        {
            if (GameManager.Instance == null)
                break;

            if (GameManager.Instance.gameState == GameManager.GameState.PointEnd)
                break;

            timer += Time.deltaTime;
            yield return null;
        }

        RestoreAllChickens();

        transformationCoroutine = null;
    }

    private void RestoreAllChickens()
    {
        if (!transformationActive)
            return;

        foreach (ChickenTarget chickenTarget in activeChickenTargets)
        {
            if (chickenTarget == null)
                continue;

            Vector3 restorePosition = chickenTarget.original != null
                ? chickenTarget.original.transform.position
                : Vector3.zero;

            if (chickenTarget.chicken != null)
            {
                restorePosition = chickenTarget.chicken.transform.position;

                PlayEffect(
                    restoreEffectPrefab,
                    chickenTarget.chicken.transform.position
                );

                Destroy(chickenTarget.chicken);
            }

            if (chickenTarget.original != null)
            {
                chickenTarget.original.transform.position = restorePosition;
                chickenTarget.original.transform.rotation =
                    chickenTarget.originalRotation;

                foreach (FollowObject follower in chickenTarget.followers)
                {
                    if (follower != null)
                        follower.target = chickenTarget.original.transform;
                }

                chickenTarget.original.SetActive(chickenTarget.wasActive);

                if (chickenTarget.characterMovement != null)
                {
                    chickenTarget.characterMovement.enabled =
                        !chickenTarget.wasAI;

                    if (!chickenTarget.wasAI)
                    {
                        chickenTarget.characterMovement.controlMovement(
                            true,
                            true
                        );
                    }
                }

                if (chickenTarget.aiBehavior != null)
                {
                    chickenTarget.aiBehavior.enabled = true;
                }
            }
        }

        activeChickenTargets.Clear();

        transformationActive = false;

        if (cooldownPending)
        {
            cooldownPending = false;
            StartDelayedCooldown();
        }
    }

    private void StartDelayedCooldown()
    {
        StartCoroutine(BeginCooldownNextFrame());
    }

    private IEnumerator BeginCooldownNextFrame()
    {
        yield return null;

        StartCooldown(_cooldownTime);

        BallInteract ballInteract = GetComponent<BallInteract>();

        if (ballInteract != null && HUDManager.Instance != null)
        {
            HUDManager.Instance.TriggerOffensiveCooldown(
                ballInteract.playerID,
                _cooldownTime
            );
        }
    }

    private void PlayEffect(GameObject prefab, Vector3 position)
    {
        if (prefab == null)
            return;

        GameObject effect = Instantiate(
            prefab,
            position,
            Quaternion.identity
        );

        ParticleSystem particles =
            effect.GetComponentInChildren<ParticleSystem>();

        if (particles != null)
        {
            float lifetime =
                particles.main.duration +
                particles.main.startLifetime.constantMax;

            Destroy(effect, lifetime);
        }
        else
        {
            Destroy(effect, 5f);
        }
    }

    private void OnDisable()
    {
        if (waitForEnemyHitCoroutine != null)
        {
            StopCoroutine(waitForEnemyHitCoroutine);
            waitForEnemyHitCoroutine = null;
        }

        if (transformationActive)
        {
            RestoreAllChickens();
        }
        else if (waitingForEnemyHit)
        {
            CancelBurnBall();
        }
    }

    private void OnDestroy()
    {
        if (waitForEnemyHitCoroutine != null)
        {
            StopCoroutine(waitForEnemyHitCoroutine);
            waitForEnemyHitCoroutine = null;
        }

        if (transformationActive)
        {
            RestoreAllChickens();
        }
    }
}