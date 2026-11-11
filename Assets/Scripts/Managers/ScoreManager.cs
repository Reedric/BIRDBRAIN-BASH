using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using Unity.Netcode;

public class ScoreManager : NetworkBehaviour
{
    private int side1ScoreLocal;
    private NetworkVariable<int> side1ScoreNet = new();
    public int side1Score
    {
        get => GameManager.Instance.isLocal ? side1ScoreLocal : side1ScoreNet.Value;
        set
        {
            if (GameManager.Instance.isLocal) side1ScoreLocal = value;
            else side1ScoreNet.Value = value;
        }
    }

    private int side2ScoreLocal;
    private NetworkVariable<int> side2ScoreNet = new();
    public int side2Score
    {
        get => GameManager.Instance.isLocal ? side2ScoreLocal : side2ScoreNet.Value;
        set
        {
            if (GameManager.Instance.isLocal) side2ScoreLocal = value;
            else side2ScoreNet.Value = value;
        }
    }

    private int side1SetsWonLocal;
    private NetworkVariable<int> side1SetsWonNet = new();
    public int side1SetsWon
    {
        get => GameManager.Instance.isLocal ? side1SetsWonLocal : side1SetsWonNet.Value;
        set
        {
            if (GameManager.Instance.isLocal) side1SetsWonLocal = value;
            else side1SetsWonNet.Value = value;
        }
    }

    private int side2SetsWonLocal;
    private NetworkVariable<int> side2SetsWonNet = new();
    public int side2SetsWon
    {
        get => GameManager.Instance.isLocal ? side2SetsWonLocal : side2SetsWonNet.Value;
        set
        {
            if (GameManager.Instance.isLocal) side2SetsWonLocal = value;
            else side2SetsWonNet.Value = value;
        }
    }

    [Header("Score UI")]
    public TextMeshProUGUI side1ScoreUI;
    public TextMeshProUGUI side1SetUI;
    public TextMeshProUGUI side2ScoreUI;
    public TextMeshProUGUI side2SetUI;

    [Header("Serve Indicator")]
    public GameObject side1ServeIndicator;
    public GameObject side2ServeIndicator;

    [Header("Cameras")]
    public Camera mainCamera;
    public Camera endCamera;

    [Header("Main UI Stuff")]
    public GameObject scoreboard;
    public GameObject birdBars;

    [Header("End Screen Stuff")]
    public RawImage fadeScreen;
    public RawImage blueWin;
    public RawImage pinkWin;
    public RawImage blueTrophy;
    public RawImage pinkTrophy;
    public TMP_Text blueWinScore;
    public TMP_Text pinkWinScore;
    public TMP_Text blueContinue;
    public TMP_Text pinkContinue;
    public GameObject invisWall;
    public Transform[] endLocations;
    public bool[] readiedUp;

    [Header("Confetti")]
    public GameObject blueConfettiPrefab;
    public GameObject pinkConfettiPrefab;
    public Transform confettiSpawnPoint;

    public float confettiSpawnInterval = 0.5f;
    public float confettiLifetime = 4f;

    [Header("Score Feedback")]
    public GameObject scoreUpdateFlourishPrefab;
    public Canvas scoreEffectCanvas; // Optional canvas to render the score flourish on top of UI
    public float scoreBounceDuration = 0.35f;
    public float scoreBounceScale = 1.25f;
    public float scoreFlourishLifetime = 1.0f;

    private Coroutine confettiRoutine;
    private Coroutine side1ScoreBounceCoroutine;
    private Coroutine side2ScoreBounceCoroutine;

    private bool leftLastScored;
    private bool inPlay;
    private bool outCheckPending; // guard against multiple outCheck coroutines
    private bool pointIntercepted;
    private float lastInterceptTime = -10f; // When a defensive ability last saved a landing

    private GameObject lastPhysicalTouchLocal;
    private NetworkVariable<ulong> lastPhysicalTouchNet = new();
    public GameObject lastPhysicalTouch
    {
        get
        {
            return GameManager.Instance.isLocal ? lastPhysicalTouchLocal : NetworkSession.Resolve(lastPhysicalTouchNet.Value);
        }
        set
        {
            if (GameManager.Instance.isLocal) lastPhysicalTouchLocal = value;
            else lastPhysicalTouchNet.Value = NetworkSession.IdOf(value);
        }
    }

    UnityEvent LeftScored;
    UnityEvent RightScored;

    // Lets a defensive ability (e.g. Phoenix) intercept a ground-touch point before it's scored.
    // leftConceding is true when the point is about to go against the LEFT side (a "Side1" touch).
    // A subscriber returns true to save/revive the ball; ScoreManager then skips the score entirely.
    public static event Func<bool, Rigidbody, Vector3, bool, bool> InterceptPoint;

    private static ScoreManager instance; // Private instance of the GameManager that other classes cannot reference

    public static ScoreManager Instance => instance; // Public instance of GameManager that other classes can reference

    void Awake()
    {
        // Check for duplicates to ensure singleton
        if (instance != null && instance != this)
        {
            Debug.Log("Getting rid of duplicate ScoreManager.");
            Destroy(this);
            return;
        }

        // Initialize singleton to this script
        instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Make sure the invisible wall is in the right spot and the end screens aren't showing
        invisWall.transform.position = new Vector3(16, -0.2f, -0.08f);
        fadeScreen.color = new Color(0, 0, 0, 0);
        blueWin.enabled = false;
        pinkWin.enabled = false;
        blueTrophy.enabled = false;
        pinkTrophy.enabled = false;
        blueWinScore.enabled = false;
        pinkWinScore.enabled = false;
        blueContinue.enabled = false;
        pinkContinue.enabled = false;

        // Make sure main camera is enabled
        endCamera.enabled = false;
        endCamera.GetComponent<AudioListener>().enabled = false;
        mainCamera.enabled = true;
        mainCamera.GetComponent<AudioListener>().enabled = true;

        // Make sure main UI stuff is enabled
        scoreboard.SetActive(true);
        birdBars.SetActive(true);

        // Set the score text to 0. Everything after this is gameplay, which online is only for the host
        side1ScoreUI.text = "0";
        side2ScoreUI.text = "0";
        side1SetUI.text = "0";
        side2SetUI.text = "0";

        if (!NetworkSession.HasAuthority) return;

        // Set the scores to 0, right to serve, and the ball is in play
        side1Score = 0;
        side2Score = 0;
        leftLastScored = false;
        inPlay = true;

        // Initializes events for when the left or right side scores
        LeftScored = new UnityEvent();
        RightScored = new UnityEvent();

        LeftScored.AddListener(EventManager.LeftScored);
        RightScored.AddListener(EventManager.RightScored);

        // Set the scores to 0, the sets to 0, right to serve, and the ball is in play
        ResetMatch();
        LeftScored.Invoke();

        if (GameSettings.Instance != null)
        {
            Debug.Log("Gamesettings loaded.");
        }
    }

    // Checking to see if the ball hits the court on either side
    void OnCollisionEnter(Collision collision)
    {
        // Only the host decides when a point is scored
        if (!NetworkSession.HasAuthority) return;

        // if it touches side 1, then side 2 scores
        if (collision.gameObject.CompareTag("Side1") && inPlay)
        {
            Rigidbody ballRb = BallManager.Instance?.gameObject?.GetComponent<Rigidbody>();

            if (ballRb == null)
                ballRb = collision.rigidbody != null ? collision.rigidbody : collision.gameObject.GetComponent<Rigidbody>();

            if (ballRb == null)
                Debug.LogWarning("ScoreManager: could not resolve ball Rigidbody for Side1 collision.");

            // Give any subscribed defensive ability a chance to save the point first. pointIntercepted only matters
            // to an out check already waiting on this landing: set any other time, it would stay set and make the next
            // time the ball goes out in the rally score nothing, leaving the point stuck.
            if (RaiseInterceptPoint(true, ballRb, collision.GetContact(0).point, false))
            {
                lastInterceptTime = Time.time;
                return;
            }

            side2Score += 1;
            UpdateScoreDisplay(side2ScoreUI, side2Score);
            inPlay = false;
            // Debug.Log("side 2 scored! points: " + side2Score);
            RightScored.Invoke();
            StartCoroutine(PlaySounds(false));
            CheckWinSet(false);
        }

        // if it touches side 2, then side 1 scores
        else if (collision.gameObject.CompareTag("Side2") && inPlay)
        {
            Rigidbody ballRb = BallManager.Instance?.gameObject?.GetComponent<Rigidbody>();

            if (ballRb == null)
                ballRb = collision.rigidbody != null ? collision.rigidbody : collision.gameObject.GetComponent<Rigidbody>();

            if (ballRb == null)
                Debug.LogWarning("ScoreManager: could not resolve ball Rigidbody for Side2 collision.");

            if (RaiseInterceptPoint(false, ballRb, collision.GetContact(0).point, false))
            {
                lastInterceptTime = Time.time;
                return;
            }

            side1Score += 1;
            UpdateScoreDisplay(side1ScoreUI, side1Score);
            inPlay = false;
            // Debug.Log("side 1 scored! points: " + side1Score);
            LeftScored.Invoke();
            StartCoroutine(PlaySounds(true));
            CheckWinSet(true);
        }

        // ducky: If ball goes out, run coroutine in case out collision was registered before court collision
        else if (collision.gameObject.CompareTag("Out") && inPlay && !outCheckPending)
        {
            outCheckPending = true;
            StartCoroutine(outCheck());
        }
    }

    // Invokes InterceptPoint by hand instead of calling the multicast delegate directly, so that if
    // more than one ability is ever subscribed, each one gets checked. Calling a non-void multicast
    // delegate directly only returns the last subscriber's result and ignores the others.
    private bool RaiseInterceptPoint(bool leftConceding, Rigidbody ballRb, Vector3 contactPoint, bool isOut)
    {
        if (InterceptPoint == null)
            return false;

        foreach (Func<bool, Rigidbody, Vector3, bool, bool> handler in InterceptPoint.GetInvocationList())
        {
            if (handler(leftConceding, ballRb, contactPoint, isOut))
                return true;
        }

        return false;
    }

    // ducky: IEnumerator coroutine for collision order sorting (b/c out collision was sometimes coming through before court)
    public IEnumerator outCheck()
    {
        // Wait briefly because the Out collision can sometimes happen
        // before the actual court collision.
        yield return new WaitForSeconds(.2f);

        // A save in the last moment (either just before or just after the ball touched out) covers this out too
        if (pointIntercepted || Time.time - lastInterceptTime < 0.5f)
        {
            pointIntercepted = false;
            outCheckPending = false;
            yield break;
        }

        GameManager gameManager = GameManager.Instance;

        // Check if ball is still in play
        if (inPlay)
        {
            GameObject touchSource = lastPhysicalTouch != null
                ? lastPhysicalTouch
                : gameManager.lastHit;

            Rigidbody ballRb = BallManager.Instance?.gameObject?.GetComponent<Rigidbody>();

            if (ballRb == null)
            {
                Debug.LogWarning("ScoreManager: could not resolve ball Rigidbody during Out check.");
            }

            // Whoever touched the ball last sent it out, so their side concedes the point.
            // That can also be a bird that isn't one of the four players, like Phoenix's chickens or
            // 31RD's mini 31RD, which still need to count for a side or the point never gets scored.
            // If there's no telling who touched it, it goes against the team that last played it, since leaving
            // the point unscored would leave the match stuck.
            if (!TryGetSide(touchSource, out bool touchedByLeft))
                touchedByLeft = gameManager.leftAttack;

            {
                Vector3 contactPoint = ballRb != null
                    ? ballRb.position
                    : transform.position;

                // Give defensive abilities such as Phoenix one last chance to save the point
                if (RaiseInterceptPoint(touchedByLeft, ballRb, contactPoint, true))
                {
                    // Phoenix saved the point, so don't award a score.
                    lastPhysicalTouch = null;
                    outCheckPending = false;
                    yield break;
                }

                if (touchedByLeft)
                {
                    side2Score += 1;
                    UpdateScoreDisplay(side2ScoreUI, side2Score);
                    inPlay = false;
                    lastPhysicalTouch = null;

                    // Same as a point scored on the court, so score listeners (e.g. Ostrich) hear about it
                    RightScored.Invoke();
                    StartCoroutine(PlaySounds(false));
                    CheckWinSet(false);
                }
                else
                {
                    side1Score += 1;
                    UpdateScoreDisplay(side1ScoreUI, side1Score);
                    inPlay = false;
                    lastPhysicalTouch = null;

                    LeftScored.Invoke();
                    StartCoroutine(PlaySounds(true));
                    CheckWinSet(true);
                }
            }
        }

        // Always reset this so another Out collision can be handled.
        outCheckPending = false;
    }

    // For a ball that will never land (stuck somewhere, or fallen out of the world): scores it like it went out
    public void HandleBallLost()
    {
        if (!NetworkSession.HasAuthority || !inPlay || outCheckPending) return;

        outCheckPending = true;
        StartCoroutine(outCheck());
    }

    // Works out which side of the court a bird that touched the ball plays for.
    public static bool TryGetSide(GameObject bird, out bool onLeft)
    {
        onLeft = false;
        if (bird == null) return false;

        GameManager gameManager = GameManager.Instance;
        if (bird == gameManager.leftPlayer1 || bird == gameManager.leftPlayer2)
        {
            onLeft = true;
            return true;
        }
        if (bird == gameManager.rightPlayer1 || bird == gameManager.rightPlayer2)
        {
            onLeft = false;
            return true;
        }

        // Stand-ins for a player, which are set up with the side of the bird they came from
        if (bird.TryGetComponent(out BallInteract ballInteract))
        {
            onLeft = ballInteract.onLeft;
            return true;
        }
        if (bird.TryGetComponent(out AIBehavior aiBehavior))
        {
            onLeft = aiBehavior.onLeft;
            return true;
        }

        return false;
    }

    // After each score, check the win conditions for both sides
    void CheckWinSet(bool leftJustScored)
    {
        // Set game manager state to end of point
        GameManager.Instance.gameState = GameManager.GameState.PointEnd;

        // Read settings (use defaults if GameSettings not present)
        int pointsPerSet = 15;
        int finalSetPoints = 15;
        int bestOf = 3;

        if (GameSettings.Instance != null)
        {
            pointsPerSet = GameSettings.Instance.PointsPerSet > 0 ? GameSettings.Instance.PointsPerSet : pointsPerSet;
            finalSetPoints = GameSettings.Instance.FinalSetPoints > 0 ? GameSettings.Instance.FinalSetPoints : finalSetPoints;
            bestOf = GameSettings.Instance.BestOfSets > 0 ? GameSettings.Instance.BestOfSets : bestOf;
        }

        int totalSetsPlayed = side1SetsWon + side2SetsWon;
        bool isFinalSet = totalSetsPlayed == bestOf - 1;
        int targetPoints = isFinalSet ? finalSetPoints : pointsPerSet;

        if (side1Score >= targetPoints && side1Score - side2Score >= 2)
        {
            Debug.Log("side 1 wins! final score: " + side1Score + " to " + side2Score);
            side1SetsWon++;
            side1SetUI.text = side1SetsWon.ToString();
            CheckMatchWin(true);
        }
        else if (side2Score >= targetPoints && side2Score - side1Score >= 2)
        {
            Debug.Log("side 2 wins! final score: " + side1Score + " to " + side2Score);
            side2SetsWon++;
            side2SetUI.text = side2SetsWon.ToString();
            CheckMatchWin(false);
        }
        else
        {
            StartCoroutine(StartNextPoint(leftJustScored));
        }
    }

    // Check whether the match is over after a set win
    private void CheckMatchWin(bool leftSideJustWon)
    {
        int bestOf = 3;
        if (GameSettings.Instance != null)
        {
            bestOf = GameSettings.Instance.BestOfSets > 0 ? GameSettings.Instance.BestOfSets : bestOf;
        }

        int setsToWin = bestOf / 2 + 1;

        if (side1SetsWon >= setsToWin)
        {
            Debug.Log("Side 1 wins the match!");
            inPlay = false;
            BeginEndTransition(true);
        }
        else if (side2SetsWon >= setsToWin)
        {
            Debug.Log("Side 2 wins the match!");
            inPlay = false;
            BeginEndTransition(false);
        }
        else
        {
            //Resets the score for next set
            ResetScore();
            StartCoroutine(StartNextPoint(leftSideJustWon));
        }
    }

    // Start next point if nobody has won yet
    private IEnumerator StartNextPoint(bool leftJustScored)
    {
        // ducky: Reset additional spike speed to 0.0f
        BallManager.Instance.resetSpikeSpeed();
        outCheckPending = false;
        pointIntercepted = false;
        lastPhysicalTouch = null;

        // Check for rotation of server
        if (leftJustScored != leftLastScored)
        {
            GameManager.RotateServer();
            leftLastScored = !leftLastScored;
        }

        // Updates UI For Which Side is Serving
        UpdateServeIndicator(leftJustScored);
        if (NetworkSession.IsServer) UpdateServeIndicatorRpc(leftJustScored);

        // Wait 3 seconds
        yield return new WaitForSeconds(3.0f);

        // Start the next point
        GameManager.Instance.leftAttack = leftLastScored;
        GameManager.NextPoint();
        inPlay = true;
    }

    // Reset the only the set points
    public void ResetScore()
    {
        // Set the scores to 0 and the ball is in play
        side1Score = 0;
        side2Score = 0;
        side1ScoreUI.text = "0";
        side2ScoreUI.text = "0";
    }

    //Reset the entire Match
    void ResetMatch()
    {
        // Set the scores to 0, the sets to 0, right to serve, and the ball is in play
        side1Score = 0;
        side2Score = 0;
        side1SetsWon = 0;
        side2SetsWon = 0;
        side1ScoreUI.text = "0";
        side2ScoreUI.text = "0";
        side1SetUI.text = "0";
        side2SetUI.text = "0";
        leftLastScored = false;
        inPlay = true;
    }

    // Updates the Indicator on scorebug for which team is currently serving
    void UpdateServeIndicator(bool leftJustScored)
    {
        if (side1ServeIndicator != null && side2ServeIndicator != null)
        {
            if (leftJustScored)
            {
                side1ServeIndicator.SetActive(true);
                side2ServeIndicator.SetActive(false);
            }
            else
            {
                side1ServeIndicator.SetActive(false);
                side2ServeIndicator.SetActive(true);
            }
        }
    }

    // Play sounds once a point is scored
    IEnumerator PlaySounds(bool leftJustScored)
    {
        AudioManager.PlayScoringSound(1.0f);

        yield return new WaitForSeconds(1.0f);

        // Get the four players' bird types from the game manager
        BirdType lbt1 = GetBirdType(GameManager.Instance.leftPlayer1);
        BirdType lbt2 = GetBirdType(GameManager.Instance.leftPlayer2);
        BirdType rbt1 = GetBirdType(GameManager.Instance.rightPlayer1);
        BirdType rbt2 = GetBirdType(GameManager.Instance.rightPlayer2);

        // Play the correct sounds depending on which team just scored
        if (leftJustScored)
        {
            // Play left team happy noises, wait a second, then play right side sad noises
            AudioManager.PlayBirdSound(lbt1, SoundType.HAPPY, 1.0f);
            AudioManager.PlayBirdSound(lbt2, SoundType.HAPPY, 1.0f);

            yield return new WaitForSeconds(1.0f);

            AudioManager.PlayBirdSound(rbt1, SoundType.SAD, 1.0f);
            AudioManager.PlayBirdSound(rbt2, SoundType.SAD, 1.0f);
        }
        else
        {
            // Play left team happy noises, wait a second, then play right side sad noises
            AudioManager.PlayBirdSound(rbt1, SoundType.HAPPY, 1.0f);
            AudioManager.PlayBirdSound(rbt2, SoundType.HAPPY, 1.0f);

            yield return new WaitForSeconds(1.0f);

            AudioManager.PlayBirdSound(lbt1, SoundType.SAD, 1.0f);
            AudioManager.PlayBirdSound(lbt2, SoundType.SAD, 1.0f);
        }
    }

    BirdType GetBirdType(GameObject bird)
    {
        try
        {
            return bird.GetComponent<BallInteract>().GetBirdType();
        }
        catch (NullReferenceException)
        {
            return bird.GetComponent<AIBehavior>().GetBirdType();
        }
    }

    IEnumerator ConfettiLoop(bool leftSideWon)
    {
        GameObject prefabToSpawn = leftSideWon ? blueConfettiPrefab : pinkConfettiPrefab;

        if (prefabToSpawn == null)
        {
            Debug.LogWarning("Confetti prefab not assigned!");
            yield break;
        }

        Transform spawnPoint = confettiSpawnPoint != null ? confettiSpawnPoint : transform;

        while (true)
        {
            GameObject confetti = Instantiate(prefabToSpawn, spawnPoint.position, Quaternion.identity);

            // Clean up each piece after a bit
            Destroy(confetti, confettiLifetime);

            yield return new WaitForSeconds(confettiSpawnInterval);
        }
    }

    // The end of the match is mostly UI and cameras, so it plays on every machine
    private void BeginEndTransition(bool leftSideJustWon)
    {
        if (NetworkSession.IsServer) BeginEndTransitionRpc(leftSideJustWon);
        else StartCoroutine(TransitionToEnd(leftSideJustWon));
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void BeginEndTransitionRpc(bool leftSideJustWon)
    {
        StartCoroutine(TransitionToEnd(leftSideJustWon));
    }

    [Rpc(SendTo.NotServer)]
    private void UpdateServeIndicatorRpc(bool leftJustScored)
    {
        UpdateServeIndicator(leftJustScored);
    }

    private IEnumerator TransitionToEnd(bool leftSideJustWon)
    {
        // Fade to black
        float time = 0.0f;

        while (time < 2.0f)
        {
            time += Time.deltaTime;
            fadeScreen.color = new Color(0, 0, 0, time / 2.0f);
            yield return null;
        }

        // Indicate end of game
        if (NetworkSession.HasAuthority) GameManager.Instance.gameState = GameManager.GameState.GameOver;

        // Switch cameras
        endCamera.enabled = true;
        endCamera.GetComponent<AudioListener>().enabled = true;
        mainCamera.enabled = false;
        mainCamera.GetComponent<AudioListener>().enabled = false;

        // Move the back wall
        invisWall.transform.position = new Vector3(26, -0.2f, -0.08f);

        // Disable the game UI elements
        scoreboard.SetActive(false);
        birdBars.SetActive(false);

        // Move all of the birds and show the correct screen
        if (leftSideJustWon)
        {
            MoveBirdsToEndLocations(GameManager.Instance.leftPlayer1, GameManager.Instance.leftPlayer2,
                GameManager.Instance.rightPlayer1, GameManager.Instance.rightPlayer2);
            blueWin.enabled = true;
            blueWinScore.text = $"{side1SetsWon}-{side2SetsWon}";
            blueWinScore.enabled = true;
            blueContinue.enabled = true;

            blueTrophy.enabled = true;
            confettiRoutine = StartCoroutine(ConfettiLoop(true));
        }
        else
        {
            MoveBirdsToEndLocations(GameManager.Instance.rightPlayer1, GameManager.Instance.rightPlayer2,
                GameManager.Instance.leftPlayer1, GameManager.Instance.leftPlayer2);
            pinkWin.enabled = true;
            pinkWinScore.text = $"{side1SetsWon}-{side2SetsWon}";
            pinkWinScore.enabled = true;
            pinkTrophy.enabled = true;
            pinkContinue.enabled = true;
            confettiRoutine = StartCoroutine(ConfettiLoop(false));
        }

        // Fade out of black
        time = 0.0f;

        while (time < 2.0f)
        {
            time += Time.deltaTime;
            fadeScreen.color = new Color(0, 0, 0, 1 - time / 2.0f);
            yield return null;
        }
    }

    private void MoveBirdsToEndLocations(params GameObject[] birds)
    {
        if (!NetworkSession.HasAuthority) return;

        for (int i = 0; i < birds.Length; ++i)
            NetworkSession.Teleport(birds[i], endLocations[i].position);
    }

    void UpdateScoreDisplay(TextMeshProUGUI scoreText, int score)
    {
        if (scoreText == null)
            return;

        scoreText.text = score.ToString();

        if (scoreText == side1ScoreUI)
        {
            if (side1ScoreBounceCoroutine != null)
                StopCoroutine(side1ScoreBounceCoroutine);

            side1ScoreBounceCoroutine = StartCoroutine(BounceScore(scoreText));
        }
        else if (scoreText == side2ScoreUI)
        {
            if (side2ScoreBounceCoroutine != null)
                StopCoroutine(side2ScoreBounceCoroutine);

            side2ScoreBounceCoroutine = StartCoroutine(BounceScore(scoreText));
        }
        else
        {
            StartCoroutine(BounceScore(scoreText));
        }

        SpawnScoreFlourish(scoreText);
    }

    IEnumerator BounceScore(TextMeshProUGUI scoreText)
    {
        if (scoreText == null)
            yield break;

        RectTransform rectTransform = scoreText.rectTransform;

        if (rectTransform == null)
            yield break;

        Vector3 originalScale = rectTransform.localScale;
        float elapsed = 0f;

        while (elapsed < scoreBounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scoreBounceDuration;
            float bounce = Mathf.Sin(t * Mathf.PI);
            float scale = 1f + (scoreBounceScale - 1f) * bounce;
            rectTransform.localScale = originalScale * scale;
            yield return null;
        }

        rectTransform.localScale = originalScale;
    }

    void SpawnScoreFlourish(TextMeshProUGUI scoreText)
    {
        if (scoreUpdateFlourishPrefab == null || scoreText == null)
            return;

        Canvas targetCanvas = scoreEffectCanvas != null
            ? scoreEffectCanvas
            : scoreText.GetComponentInParent<Canvas>();

        if (targetCanvas != null)
        {
            targetCanvas.overrideSorting = true;
            targetCanvas.sortingOrder = Mathf.Max(targetCanvas.sortingOrder, 100);
        }

        Transform parentTransform = targetCanvas != null
            ? targetCanvas.transform
            : scoreText.transform.parent;

        GameObject flourish = Instantiate(scoreUpdateFlourishPrefab, parentTransform);
        RectTransform flourishRect = flourish.GetComponent<RectTransform>();
        RectTransform scoreRect = scoreText.rectTransform;

        if (flourishRect != null && scoreRect != null && targetCanvas != null)
        {
            Vector2 localPoint;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                targetCanvas.worldCamera,
                scoreRect.position);

            RectTransform canvasRect = parentTransform as RectTransform;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPoint,
                targetCanvas.worldCamera,
                out localPoint))
            {
                flourishRect.anchoredPosition = localPoint;
            }
            else
            {
                flourishRect.anchoredPosition = Vector2.zero;
            }

            flourishRect.localScale = Vector3.one;
            flourishRect.localRotation = Quaternion.identity;
            flourishRect.SetAsLastSibling();
        }
        else
        {
            flourish.transform.SetParent(parentTransform, false);
            flourish.transform.localPosition = scoreText.transform.localPosition;
            flourish.transform.localRotation = Quaternion.identity;
            flourish.transform.localScale = Vector3.one;
            flourish.transform.SetAsLastSibling();
        }

        Destroy(flourish, scoreFlourishLifetime);
    }

    public void CheckReturnToMenu()
    {
        // Iterate over readied up array to see if anyone is not readied up
        foreach (bool isReady in readiedUp)
        {
            if (!isReady) return;
        }

        // Stop confetti loop before leaving
        if (confettiRoutine != null)
        {
            StopCoroutine(confettiRoutine);
        }

        // Everyone is ready, go back to main menu
        if (NetworkSession.IsServer)
        {
            ReturnToMenuRpc();
            StartCoroutine(HostReturnToMenu());
            return;
        }

        SceneManager.LoadScene("MainMenu");
    }

    public override void OnNetworkSpawn()
    {
        // The host updates its score text as points happen. Clients follow along from the network variables.
        if (IsServer) return;

        side1ScoreNet.OnValueChanged += OnSide1ScoreChanged;
        side2ScoreNet.OnValueChanged += OnSide2ScoreChanged;
        side1SetsWonNet.OnValueChanged += OnSide1SetsChanged;
        side2SetsWonNet.OnValueChanged += OnSide2SetsChanged;

        // In case any of them changed before this object was spawned here
        side1ScoreUI.text = side1ScoreNet.Value.ToString();
        side2ScoreUI.text = side2ScoreNet.Value.ToString();
        side1SetUI.text = side1SetsWonNet.Value.ToString();
        side2SetUI.text = side2SetsWonNet.Value.ToString();
    }

    public override void OnNetworkDespawn()
    {
        side1ScoreNet.OnValueChanged -= OnSide1ScoreChanged;
        side2ScoreNet.OnValueChanged -= OnSide2ScoreChanged;
        side1SetsWonNet.OnValueChanged -= OnSide1SetsChanged;
        side2SetsWonNet.OnValueChanged -= OnSide2SetsChanged;
    }

    private void OnSide1ScoreChanged(int previous, int current) => ShowNetworkedScore(side1ScoreUI, previous, current);
    private void OnSide2ScoreChanged(int previous, int current) => ShowNetworkedScore(side2ScoreUI, previous, current);
    private void OnSide1SetsChanged(int previous, int current) => side1SetUI.text = current.ToString();
    private void OnSide2SetsChanged(int previous, int current) => side2SetUI.text = current.ToString();

    private void ShowNetworkedScore(TextMeshProUGUI scoreText, int previous, int current)
    {
        // Only a point gets the bounce and flourish, not the score going back to 0 for a new set
        if (current > previous) UpdateScoreDisplay(scoreText, current);
        else scoreText.text = current.ToString();
    }

    [Rpc(SendTo.NotServer)]
    private void ReturnToMenuRpc()
    {
        NetworkSession.LeaveToMainMenu();
    }

    // The host waits a moment so the message has time to reach the clients before the connection closes
    private IEnumerator HostReturnToMenu()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        NetworkSession.LeaveToMainMenu();
    }
}