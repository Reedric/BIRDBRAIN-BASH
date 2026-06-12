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
    public Material normalBallMaterial; // Default ball material to restore after snowball ends
    public Material snowballMaterial; // Material to apply to the ball when snowball is active
    private Renderer[] dodgeBallRenderers; // Christofort: grabs the dodgeball's renderer to swap materials

    // New: keep track of the active coroutine so we can actually stop it correctly
    private Coroutine spawnIceCoroutine;
    private GameObject iceInstance;

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

        ballInteraction.SpikeBall();

        // New: stop any old coroutine before starting a new one
        if (spawnIceCoroutine != null)
        {
            StopCoroutine(spawnIceCoroutine);
            spawnIceCoroutine = null;
        }

        // will spawn ice after the conditions in the coroutine are confirmed true
        if (iceMode && !iceSpawned) spawnIceCoroutine = StartCoroutine(SpawnIce());

        // Christofort: swap the dodgeball's material to the snowball material
        ApplySnowballMaterial();

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, _cooldownTime);
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
        // New: stop the active coroutine properly
        if (spawnIceCoroutine != null)
        {
            StopCoroutine(spawnIceCoroutine);
            spawnIceCoroutine = null;
        }

        iceMode = false;
        iceSpawned = false;
        usingSnowBall = false;

        if (iceInstance != null) Debug.Log("Ice Destroyed", this);

        Destroy(iceInstance);
        iceInstance = null;

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
        // New: wait until the snowball gets touched by someone and the state becomes
        // either Bumped or Blocked. These are the states that mean the other side made contact.
        GameManager gameManager = GameManager.Instance;
        yield return new WaitUntil(() =>
            usingSnowBall &&
            gameManager.lastHit != null &&
            (gameManager.gameState == GameManager.GameState.Bumped ||
            gameManager.gameState == GameManager.GameState.Blocked));

        // New: if the snowball got canceled while waiting, stop here
        if (!usingSnowBall || gameManager == null || gameManager.lastHit == null)
            yield break;

        // New: make sure the player who touched it is actually on the opposing team
        if (!IsOpponentPlayer(gameManager.lastHit))
            yield break;

        if (!iceSpawned)
        {
            // New: spawn the ice under the opposing player who last touched the ball
            Vector3 hitterPos = gameManager.lastHit.transform.position;
            Vector3 iceSpawnPos = new Vector3(hitterPos.x, 0, hitterPos.z);

            iceInstance = Instantiate(tempIce, iceSpawnPos, Quaternion.identity);
            iceSpawned = true;

            iceCollider = iceInstance.GetComponent<BoxCollider>();
            if (ballCollider != null && iceCollider != null)
                Physics.IgnoreCollision(ballCollider, iceCollider, true);

            // New: revert the ball texture as soon as the ice spawns
            Debug.Log("Reverting ball material after ice spawns", this);
            RestoreNormalBallMaterial();
        }

        spawnIceCoroutine = null;
    }

    bool IsOpponentPlayer(GameObject player)
    {
        if (player == null)
        {
            Debug.Log("IsOpponentPlayer: Player is null", this);
            return false;
        }

        // Always return true if game state is Blocked or Bumped
        GameManager gameManager = GameManager.Instance;
        if (gameManager.gameState == GameManager.GameState.Blocked || gameManager.gameState == GameManager.GameState.Bumped)
        {
            return true;
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
    //     // Christofort: Check if ballManager exists first to avoid errors
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
