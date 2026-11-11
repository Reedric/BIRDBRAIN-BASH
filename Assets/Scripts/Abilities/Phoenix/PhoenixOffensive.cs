using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class PhoenixOffensive : BirdAbility
{
    public override bool RequiresSpikeToActivate => true;

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
        public Vector3 originalPosition;

        public CharacterMovement characterMovement;
        public AIBehavior aiBehavior;

        public bool wasAI;
        public bool wasActive;

        public bool originalCanMove;
        public bool originalCanJump;

        public Vector3 originalVelocity;
        public Quaternion originalRotation;

        // Local matches only: the devices the player was using, so they can be handed to the chicken and back.
        // Deactivating a bird unpairs its PlayerInput, and re-enabling one pairs it with whatever is free.
        public PlayerInput originalInput;
        public InputDevice[] originalDevices;
        public string originalControlScheme;

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

        StartRotisserie();

        return true;
    }

    private void StartRotisserie()
    {
        waitingForEnemyHit = true;
        cooldownPending = false;

        // The ball has already been spiked by BallInteract; start watching for the enemy touch.
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
                // Make sure the hit came from the opposing team.
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

        // If nobody was inside the radius, the ability still
        // burned the ball, so let the normal rotisserie duration run.
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

        // Let the ability be used again right away.
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
        chickenTarget.originalPosition = target.transform.position;
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
            {
                onLeft = targetBallInteract.onLeft;
                playerID = targetBallInteract.playerID;
            }
        }

        Vector3 spawnPosition = target.transform.position;
        Quaternion spawnRotation = target.transform.rotation;

        // Remember a local human's controller before deactivating the bird releases it
        if (!NetworkSession.IsOnline && !chickenTarget.wasAI && target.TryGetComponent(out PlayerInput targetInput))
        {
            chickenTarget.originalInput = targetInput;
            chickenTarget.originalDevices = targetInput.devices.ToArray();
            chickenTarget.originalControlScheme = targetInput.currentControlScheme;
        }

        NetworkFx.SetActive(target, false);

        if (rotisserieChickenPrefab == null)
        {
            activeChickenTargets.Add(chickenTarget);
            return;
        }

        GameObject chicken;

        if (HasRememberedDevices(chickenTarget))
        {
            // Pair the chicken with the player's own controller, the same way MultiplayerManager spawns players.
            // Otherwise it would grab whichever device happens to be free.
            chicken = PlayerInput.Instantiate(
                rotisserieChickenPrefab,
                controlScheme: chickenTarget.originalControlScheme,
                pairWithDevices: chickenTarget.originalDevices
            ).gameObject;

            chicken.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        }
        else
        {
            // A human's chicken belongs to them online, so that their input goes to it
            ulong ownerClientId = target.TryGetComponent(out Unity.Netcode.NetworkObject targetNetObj) && !chickenTarget.wasAI
                ? targetNetObj.OwnerClientId
                : Unity.Netcode.NetworkManager.ServerClientId;

            chicken = NetworkFx.InstantiateOwned(
                rotisserieChickenPrefab,
                spawnPosition,
                spawnRotation,
                ownerClientId
            );
        }

        chickenTarget.chicken = chicken;

        chicken.SetActive(true);

        foreach (FollowObject follower in chickenTarget.followers)
        {
            if (follower != null)
                follower.target = chicken.transform;
        }

        NetworkFx.RetargetFollowers(target, chicken);

        ConfigureChicken(chicken, onLeft, playerID, chickenTarget.wasAI);

        activeChickenTargets.Add(chickenTarget);
    }

    private void ConfigureChicken(GameObject chicken, bool onLeft, int playerID, bool wasAI)
    {
        if (chicken == null)
            return;

        AIBehavior chickenAI = chicken.GetComponent<AIBehavior>();
        CharacterMovement chickenMovement = chicken.GetComponent<CharacterMovement>();
        BallInteract chickenBallInteract = chicken.GetComponent<BallInteract>();

        // Only let the AI drive the chicken if the original player was AI-controlled.
        // Human players stay in control of their chicken via Input System -> CharacterMovement/BallInteract.
        if (chickenAI != null)
        {
            chickenAI.SetIdentity(onLeft, playerID);
            chickenAI.enabled = wasAI;
        }

        if (chickenMovement != null)
        {
            chickenMovement.enabled = !wasAI;

            if (!wasAI)
                chickenMovement.controlMovement(true, true);
        }

        if (chickenBallInteract != null)
        {
            chickenBallInteract.enabled = true;
            chickenBallInteract.onLeft = onLeft;
            chickenBallInteract.playerID = playerID;
        }

        // An AI's chicken is owned by the host, whose own input should not reach it
        if (wasAI && chicken.TryGetComponent(out PlayerNetworkController networkController))
            networkController.enabled = false;

        if (chicken.TryGetComponent(out PlayerInput chickenInput))
        {
            // The chicken prefab has a PlayerInput, which pairs itself with any free controller when it spawns.
            // An AI's chicken shouldn't listen to one, or a spare controller could hit the ball for it.
            if (wasAI)
                chickenInput.enabled = false;
            else if (!NetworkSession.IsOnline && chickenInput.actions != null)
                chickenInput.actions.FindActionMap("UI")?.Enable(); // So the player can still pause, like MultiplayerManager sets up
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

            // Where the bird was when it got turned into a chicken, which is always on its own side.
            Vector3 restorePosition = chickenTarget.originalPosition;

            if (chickenTarget.chicken != null)
            {
                PlayEffect(
                    restoreEffectPrefab,
                    chickenTarget.chicken.transform.position
                );

                // Destroy waits for the end of the frame, so free the chicken's controller now. The original
                // pairs itself with a free controller as soon as it's reactivated below.
                if (chickenTarget.chicken.TryGetComponent(out PlayerInput chickenInput))
                    chickenInput.enabled = false;

                Destroy(chickenTarget.chicken);
            }

            if (chickenTarget.original != null)
            {
                Rigidbody originalRb = chickenTarget.original.GetComponent<Rigidbody>();

                // Restore the original bird to its safe position and reset physics
                // before giving control back to it.
                chickenTarget.original.transform.position = restorePosition;
                chickenTarget.original.transform.rotation =
                    chickenTarget.originalRotation;

                if (originalRb != null)
                {
                    originalRb.linearVelocity = Vector3.zero;
                    originalRb.angularVelocity = Vector3.zero;
                    originalRb.Sleep();
                }

                Physics.SyncTransforms();

                foreach (FollowObject follower in chickenTarget.followers)
                {
                    if (follower != null)
                        follower.target = chickenTarget.original.transform;
                }

                if (chickenTarget.chicken != null)
                    NetworkFx.RetargetFollowers(chickenTarget.chicken, chickenTarget.original);

                NetworkFx.SetActive(chickenTarget.original, chickenTarget.wasActive);

                // Online, this also stops clients from sliding the bird over from where it was hidden
                if (chickenTarget.original.activeInHierarchy)
                    NetworkSession.Teleport(chickenTarget.original, restorePosition);

                RestoreRememberedDevices(chickenTarget);

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

    private static bool HasRememberedDevices(ChickenTarget chickenTarget)
    {
        return chickenTarget.originalInput != null
            && chickenTarget.originalDevices != null
            && chickenTarget.originalDevices.Length > 0
            && !string.IsNullOrEmpty(chickenTarget.originalControlScheme);
    }

    // Gives a local player back the exact controller they had before becoming a chicken, since reactivating
    // their bird pairs it with whichever device is free first (which can be someone's spare controller).
    private static void RestoreRememberedDevices(ChickenTarget chickenTarget)
    {
        PlayerInput input = chickenTarget.originalInput;
        if (!HasRememberedDevices(chickenTarget) || !input.isActiveAndEnabled) return;

        InputDevice[] devices = chickenTarget.originalDevices.Where(device => device != null && device.added).ToArray();

        if (devices.Length > 0 && input.user.valid)
            input.SwitchCurrentControlScheme(chickenTarget.originalControlScheme, devices);

        // MultiplayerManager turns this on for pausing. Reactivating can leave the bird with a fresh copy of its actions.
        input.actions?.FindActionMap("UI")?.Enable();
    }

    private void StartDelayedCooldown()
    {
        // Coroutines can't start on a bird that has been switched off (another Phoenix turning this one into a
        // chicken), which would skip the cooldown altogether
        if (!isActiveAndEnabled)
        {
            StartCooldown(_cooldownTime);
            return;
        }

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

        GameObject effect = NetworkFx.Instantiate(
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