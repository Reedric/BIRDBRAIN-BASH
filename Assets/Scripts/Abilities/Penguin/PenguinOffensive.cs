using System.Collections;
using UnityEngine;

public class PenguinOffensive : BirdAbility
{
    [Header("Snowball Ability")]
    public Collider ballCollider; // Christofort: grabs the dodgeball's collider
    public BoxCollider iceCollider; // Christofort: grabs the ice's collider
    [HideInInspector] public bool usingSnowBall = false;
    private BallInteract ballInteraction; // Christofort: get the ball interaction spike code
    private float iceLength = 5.0f; // christofort: how long the ice effect lasts
    private float iceTimer = 0.0f; // christofort: tracker for the ice effect
    private bool iceMode = false; // Christofort: track if snowball is active
    private bool iceSpawned = false; // Christofort: track if ice has been spawned to prevent multiple spawns

    [Header("Snowball Visual")]
    public GameObject tempIce;
    public float iceSpawnFadeTime = 0.35f; // How long the ice fade-in takes
    public float iceFadeOutTime = 0.35f; // How long the ice fade-out takes
    public GameObject snowballTrackPrefab; // Prefab that follows the ball when it turns into a snowball
    public GameObject iceSpawnBurstPrefab; // Prefab that plays when the ground ice spawns
    public GameObject iceCircularMaskPrefab; // Optional cookie-cutter circular mask prefab for ice reveal
    public Material normalBallMaterial; // Default ball material to restore after snowball ends
    public Material snowballMaterial; // Material to apply to the ball when snowball is active
    private Renderer[] dodgeBallRenderers; // Christofort: grabs the dodgeball's renderer to swap materials

    // New: keep track of the active coroutine so we can actually stop it correctly
    private Coroutine spawnIceCoroutine;
    private Coroutine iceMaskCoroutine;
    private GameObject iceInstance;
    private GameObject iceMaskInstance;
    private GameObject snowballTrackInstance;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ballInteraction = GetComponent<BallInteract>();

        // Subscribe to ball collision event if ballManager is available
        // BallManager.Instance.onBallCollision += checkNetCollision;

        // Christofort: grab all renderers on the dodgeball object and its children
        dodgeBallRenderers = BallManager.Instance.gameObject.GetComponentsInChildren<Renderer>();
        if (dodgeBallRenderers == null || dodgeBallRenderers.Length == 0)
            Debug.LogWarning("Could not find any dodgeBall renderers in Start()", this);
    }

    // Update is called once per frame
    void Update()
    {
        if (iceInstance != null)
        {
            iceTimer -= Time.deltaTime;
            if (iceTimer <= 0)
            {
                EndSnowBall();
                Debug.Log("On cooldown", this);
            }
        }
    }

    protected override bool Activate()
    {
        if (!usingSnowBall)
        {
            StartSnowBall();
            return true;
        }
        return false;
    }

    void StartSnowBall()
    {
        iceMode = true;
        usingSnowBall = true;
        iceSpawned = false; // New: reset spawn flag every time the ability starts
        // hitNet = false; // New: reset net flag every time the ability starts
        iceTimer = iceLength;

        // New: spawn the snowball particles on the spike before the ball hits the ground
        CreateSnowballTrackEffect();
        ballInteraction.SpikeBall();

        // New: stop any old coroutine before starting a new one
        if (spawnIceCoroutine != null)
        {
            StopCoroutine(spawnIceCoroutine);
            spawnIceCoroutine = null;
        }

        // will spawn ice after the conditions in the coroutine are confirmed true
        if (iceMode && !iceSpawned)
            spawnIceCoroutine = StartCoroutine(SpawnIce());

        // Christofort: swap the dodgeball's material to the snowball material
        ApplySnowballMaterial();

        // Do not permanently consume the cooldown yet.
        // The cooldown will begin only if the enemy actually bumps the snowball
        // and the ice successfully spawns.
    }

    void CreateSnowballTrackEffect()
    {
        if (snowballTrackPrefab == null)
            return;

        if (snowballTrackInstance != null)
        {
            Destroy(snowballTrackInstance);
            snowballTrackInstance = null;
        }

        GameObject ballObj = BallManager.Instance?.gameObject;
        if (ballObj == null)
            return;

        snowballTrackInstance = Instantiate(snowballTrackPrefab, ballObj.transform.position, Quaternion.identity);
        snowballTrackInstance.transform.SetParent(ballObj.transform, true);
    }

    void ApplySnowballMaterial()
    {
        // Always refresh renderers before swapping materials
        dodgeBallRenderers = BallManager.Instance.gameObject.GetComponentsInChildren<Renderer>();

        if (dodgeBallRenderers == null || dodgeBallRenderers.Length == 0)
        {
            Debug.LogError("No renderers found on dodgeBall in ApplySnowballMaterial", this);
            return;
        }

        if (snowballMaterial == null)
        {
            Debug.LogError("snowballMaterial reference is null in ApplySnowballMaterial", this);
            return;
        }

        foreach (Renderer rend in dodgeBallRenderers)
        {
            if (rend != null)
            {
                rend.material = snowballMaterial;
                Debug.Log($"Applied snowball material to {rend.gameObject.name}", this);
            }
            else
            {
                Debug.LogWarning("Renderer is null in ApplySnowballMaterial loop", this);
            }
        }

        Debug.Log("Snowball material applied to all renderers", this);
    }

    void EndSnowBall()
    {
        // stop the active coroutine properly
        if (spawnIceCoroutine != null)
        {
            StopCoroutine(spawnIceCoroutine);
            spawnIceCoroutine = null;
        }

        iceMode = false;
        iceSpawned = false;
        usingSnowBall = false;

        if (iceMaskCoroutine != null)
        {
            StopCoroutine(iceMaskCoroutine);
            iceMaskCoroutine = null;
        }

        if (iceInstance != null)
        {
            StartCoroutine(FadeOutAndDestroyIce(iceInstance, iceMaskInstance, iceFadeOutTime));
            iceInstance = null;
            iceMaskInstance = null;
        }

        if (snowballTrackInstance != null)
        {
            Destroy(snowballTrackInstance);
            snowballTrackInstance = null;
        }

        RestoreNormalBallMaterial();
    }

    void RestoreNormalBallMaterial()
    {
        // Always refresh renderers before restoring materials
        dodgeBallRenderers = BallManager.Instance.gameObject.GetComponentsInChildren<Renderer>();

        if (dodgeBallRenderers == null || dodgeBallRenderers.Length == 0)
        {
            Debug.LogError("No renderers found on dodgeBall in RestoreNormalBallMaterial", this);
            return;
        }

        if (normalBallMaterial == null)
        {
            Debug.LogError("normalBallMaterial reference is null in RestoreNormalBallMaterial", this);
            return;
        }

        foreach (Renderer rend in dodgeBallRenderers)
        {
            if (rend != null)
            {
                rend.material = normalBallMaterial;
                Debug.Log($"Restored normal material to {rend.gameObject.name}", this);
            }
            else
            {
                Debug.LogWarning("Renderer is null in RestoreNormalBallMaterial loop", this);
            }
        }

        Debug.Log("Normal ball material restored to all renderers", this);
    }

    IEnumerator SpawnIce()
    {
        GameManager gameManager = GameManager.Instance;

        if (gameManager == null)
        {
            CancelSnowballAndRefund();
            yield break;
        }

        // Wait until the snowball is either bumped, blocked, or the point ends.
        // Only an enemy bump is allowed to actually spawn the ice.
        while (usingSnowBall)
        {
            if (gameManager.gameState == GameManager.GameState.Bumped)
            {
                if (gameManager.lastHit != null && IsOpponentPlayer(gameManager.lastHit))
                {
                    SpawnIceAtOpponent(gameManager.lastHit);
                    spawnIceCoroutine = null;
                    yield break;
                }

                // A teammate bumped it, so the ability did not successfully trigger.
                CancelSnowballAndRefund();
                yield break;
            }

            if (gameManager.gameState == GameManager.GameState.Blocked)
            {
                // A block never creates ice.
                CancelSnowballAndRefund();
                yield break;
            }

            if (gameManager.gameState == GameManager.GameState.PointEnd)
            {
                // Point ended before the enemy bumped it.
                CancelSnowballAndRefund();
                yield break;
            }

            yield return null;
        }

        spawnIceCoroutine = null;
    }

    void SpawnIceAtOpponent(GameObject opponent)
    {
        if (!usingSnowBall || iceSpawned || opponent == null)
            return;

        if (tempIce == null)
        {
            Debug.LogWarning("PenguinOffensive: tempIce is not assigned.", this);
            CancelSnowballAndRefund();
            return;
        }

        // spawn the ice under the opposing player who bumped the ball
        Vector3 hitterPos = opponent.transform.position;
        Vector3 iceSpawnPos = new Vector3(hitterPos.x, 0, hitterPos.z);

        iceInstance = Instantiate(tempIce, iceSpawnPos, Quaternion.identity);
        PrepareIceForFade(iceInstance);
        StartCoroutine(FadeInIce(iceInstance, iceSpawnFadeTime));
        iceSpawned = true;

        if (iceCircularMaskPrefab != null)
        {
            if (iceMaskInstance != null)
            {
                Destroy(iceMaskInstance);
                iceMaskInstance = null;
            }

            if (iceMaskCoroutine != null)
            {
                StopCoroutine(iceMaskCoroutine);
                iceMaskCoroutine = null;
            }

            iceMaskInstance = Instantiate(iceCircularMaskPrefab, iceInstance.transform);
            iceMaskInstance.transform.localPosition = Vector3.zero;
            iceMaskInstance.transform.localRotation = Quaternion.identity;
            iceMaskInstance.transform.localScale = Vector3.zero;
            PrepareIceMaskForFade(iceMaskInstance);
            iceMaskCoroutine = StartCoroutine(AnimateIceMask(iceMaskInstance, iceSpawnFadeTime));
        }

        iceCollider = iceInstance.GetComponent<BoxCollider>();

        if (ballCollider != null && iceCollider != null)
            Physics.IgnoreCollision(ballCollider, iceCollider, true);

        // disable the ball snow effect and restore the normal texture when the ice hits the ground
        if (snowballTrackInstance != null)
        {
            Destroy(snowballTrackInstance);
            snowballTrackInstance = null;
        }

        Debug.Log("Reverting ball material after ice spawns", this);
        RestoreNormalBallMaterial();

        // play a ground particle effect attached to the ice while it exists
        if (iceSpawnBurstPrefab != null)
        {
            GameObject burstInstance = Instantiate(iceSpawnBurstPrefab, iceInstance.transform);
            burstInstance.transform.localPosition = Vector3.zero;
        }

        // The ability has successfully triggered, so NOW start its cooldown.
        StartCooldown(_cooldownTime);

        int playerID = GetComponent<BallInteract>().playerID;

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.TriggerOffensiveCooldown(
                playerID,
                _cooldownTime
            );
        }
    }

    void CancelSnowballAndRefund()
    {
        if (!usingSnowBall)
            return;

        Debug.Log("Penguin Offensive: Snowball did not hit an opposing player. Refunding cooldown.", this);

        if (spawnIceCoroutine != null)
        {
            StopCoroutine(spawnIceCoroutine);
            spawnIceCoroutine = null;
        }

        iceMode = false;
        iceSpawned = false;
        usingSnowBall = false;

        if (iceMaskCoroutine != null)
        {
            StopCoroutine(iceMaskCoroutine);
            iceMaskCoroutine = null;
        }

        if (iceInstance != null)
        {
            Destroy(iceInstance);
            iceInstance = null;
        }

        if (iceMaskInstance != null)
        {
            Destroy(iceMaskInstance);
            iceMaskInstance = null;
        }

        if (snowballTrackInstance != null)
        {
            Destroy(snowballTrackInstance);
            snowballTrackInstance = null;
        }

        RestoreNormalBallMaterial();

        // Explicitly refund the ability.
        _cooldownRemaining = 0f;
    }

    void PrepareIceForFade(GameObject ice)
    {
        if (ice == null)
            return;

        Renderer[] renderers = ice.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            foreach (Material material in renderer.materials)
            {
                if (material != null)
                {
                    EnableMaterialTransparency(material);
                    SetMaterialAlpha(material, 0f);
                }
            }
        }
    }

    void PrepareIceMaskForFade(GameObject mask)
    {
        if (mask == null)
            return;

        Renderer[] renderers = mask.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            foreach (Material material in renderer.materials)
            {
                if (material != null)
                {
                    EnableMaterialTransparency(material);
                    SetMaterialAlpha(material, 1f);
                }
            }
        }
    }

    IEnumerator AnimateIceMask(GameObject mask, float duration)
    {
        if (mask == null || duration <= 0f)
            yield break;

        Renderer[] renderers = mask.GetComponentsInChildren<Renderer>(true);
        float elapsed = 0f;

        while (elapsed < duration && mask != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            mask.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                foreach (Material material in renderer.materials)
                {
                    if (material != null)
                    {
                        SetMaterialAlpha(material, 1f - t);
                    }
                }
            }

            yield return null;
        }

        if (mask != null)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                foreach (Material material in renderer.materials)
                {
                    if (material != null)
                    {
                        SetMaterialAlpha(material, 0f);
                    }
                }
            }

            Destroy(mask);
        }
    }

    IEnumerator FadeOutAndDestroyIce(GameObject ice, GameObject mask, float duration)
    {
        if (ice == null || duration <= 0f)
        {
            if (ice != null) Destroy(ice);
            if (mask != null) Destroy(mask);
            yield break;
        }

        Renderer[] iceRenderers = ice.GetComponentsInChildren<Renderer>(true);
        Renderer[] maskRenderers = mask != null ? mask.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        float elapsed = 0f;

        while (elapsed < duration && (ice != null || mask != null))
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            foreach (Renderer renderer in iceRenderers)
            {
                if (renderer == null)
                    continue;

                foreach (Material material in renderer.materials)
                {
                    if (material != null)
                        SetMaterialAlpha(material, 1f - t);
                }
            }

            if (mask != null)
            {
                mask.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);

                foreach (Renderer renderer in maskRenderers)
                {
                    if (renderer == null)
                        continue;

                    foreach (Material material in renderer.materials)
                    {
                        if (material != null)
                            SetMaterialAlpha(material, 1f - t);
                    }
                }
            }

            yield return null;
        }

        if (ice != null)
            Destroy(ice);

        if (mask != null)
            Destroy(mask);
    }

    IEnumerator FadeInIce(GameObject ice, float duration)
    {
        if (ice == null || duration <= 0f)
            yield break;

        float elapsed = 0f;
        Renderer[] renderers = ice.GetComponentsInChildren<Renderer>(true);

        while (elapsed < duration && ice != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                foreach (Material material in renderer.materials)
                {
                    if (material != null)
                        SetMaterialAlpha(material, t);
                }
            }

            yield return null;
        }

        if (ice != null)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                foreach (Material material in renderer.materials)
                {
                    if (material != null)
                        SetMaterialAlpha(material, 1f);
                }
            }
        }
    }

    void EnableMaterialTransparency(Material material)
    {
        if (material == null)
            return;

        material.SetFloat("_Mode", 2);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    void SetMaterialAlpha(Material material, float alpha)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Color"))
        {
            Color color = material.color;
            color.a = Mathf.Clamp01(alpha);
            material.color = color;
        }
    }

    bool IsOpponentPlayer(GameObject player)
    {
        if (player == null)
        {
            Debug.Log("IsOpponentPlayer: Player is null", this);
            return false;
        }

        GameManager gameManager = GameManager.Instance;

        if (gameManager == null || ballInteraction == null)
            return false;

        BallInteract playerBallInteract = player.GetComponent<BallInteract>();

        if (playerBallInteract != null)
        {
            return playerBallInteract.onLeft != ballInteraction.onLeft;
        }

        AIBehavior aiBehavior = player.GetComponent<AIBehavior>();

        if (aiBehavior != null)
        {
            return aiBehavior.onLeft != ballInteraction.onLeft;
        }

        return false;
    }

    // COMMENTED OUT BC IT DOES NOT ADD ANYTHING LOGICALLY TO THE SCRIPT
    // // New: optional net catch in case the ball hits the net first
    // void checkNetCollision(Collision colInfo)
    // {
    //     if (!usingSnowBall || colInfo == null)
    //         return;

    //     if (colInfo.gameObject.CompareTag("Net"))
    //     {
    //         hitNet = true;
    //     }
    // }

    // private void OnEnable()
    // {
    //     // Christofort: Check if BallManager exists first to avoid errors
    //     BallManager.Instance.onBallCollision += checkNetCollision;
    // }

    // private void OnDisable()
    // {
    //     BallManager.Instance.onBallCollision -= checkNetCollision;
    // }

    // private void OnDestroy()
    // {
    //     BallManager.Instance.onBallCollision -= checkNetCollision;
    // }
}