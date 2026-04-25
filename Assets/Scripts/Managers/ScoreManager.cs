using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using TMPro;
using System;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ScoreManager : MonoBehaviour
{
    public int side1Score = 0;
    public int side2Score = 0;
    public int side1SetsWon = 0;
    public int side2SetsWon = 0;

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
    public GameObject invisWall;
    public Transform[] endLocations;
    public bool[] readiedUp;

    [Header("Confetti")]
    public GameObject blueConfettiPrefab;
    public GameObject pinkConfettiPrefab;
    public Transform confettiSpawnPoint;

    public float confettiSpawnInterval = 0.5f;
    public float confettiLifetime = 4f;

    private Coroutine confettiRoutine;
   
    private bool leftLastScored;
    private bool inPlay;
    private bool outCheckPending; // guard against multiple outCheck coroutines
    public GameObject lastPhysicalTouch;
    UnityEvent LeftScored;
    UnityEvent RightScored;

    private static ScoreManager instance; // Private instance of the GameManager that other classes cannot reference
    public static ScoreManager Instance // Public instance of GameManager that other classes can reference
    {
        get
        {
            if (instance == null)
            {
                instance = new ScoreManager();
            }
            return instance;
        }
    }

    void Awake()
    {
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

        // Make sure main camera is enabled
        endCamera.enabled = false;
        endCamera.GetComponent<AudioListener>().enabled = false;
        mainCamera.enabled = true;
        mainCamera.GetComponent<AudioListener>().enabled = true;

        // Make sure main UI stuff is enabled
        scoreboard.SetActive(true);
        birdBars.SetActive(true);

        // Set the scores to 0, right to serve, and the ball is in play
        side1Score = 0;
        side2Score = 0;
        side1ScoreUI.text = "0";
        side2ScoreUI.text = "0";
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
    }

    // Checking to see if the ball hits the court on either side
    void OnCollisionEnter(Collision collision)
    {
        // if it touches side 1, then side 2 scores
        if (collision.gameObject.CompareTag("Side1") && inPlay)
        {
            side2Score += 1;
            side2ScoreUI.text = side2Score.ToString();
            inPlay = false;
            Debug.Log("side 2 scored! points: " + side2Score);
            RightScored.Invoke();
            StartCoroutine(PlaySounds(false));
            CheckWinSet(false);
        } 
        // if it touches side 2, then side 1 scores
        else if (collision.gameObject.CompareTag("Side2") && inPlay) 
        {
            side1Score += 1;
            side1ScoreUI.text = side1Score.ToString();
            inPlay = false;
            Debug.Log("side 1 scored! points: " + side1Score);
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

    // ducky: IEnumerator coroutine for collision order sorting (b/c out collision was sometimes coming through before court)
    public IEnumerator outCheck()
    {
        yield return new WaitForSeconds(.2f);

        // Get the game manager's instance
        GameManager gameManager = GameManager.Instance;

        // Checks if ball still in play
        if (inPlay)
        {
            GameObject touchSource = lastPhysicalTouch != null ? lastPhysicalTouch : gameManager.lastHit;

            if (touchSource == gameManager.rightPlayer1 || touchSource == gameManager.rightPlayer2)
            {
                side1Score += 1;
                side1ScoreUI.text = side1Score.ToString();
                inPlay = false;
                lastPhysicalTouch = null;
                Debug.Log("Out! side 1 scored! points: " + side1Score);
                StartCoroutine(PlaySounds(true));
                CheckWinSet(true);
            }
            else if (touchSource == gameManager.leftPlayer1 || touchSource == gameManager.leftPlayer2)
            {
                side2Score += 1;
                side2ScoreUI.text = side2Score.ToString();
                inPlay = false;
                lastPhysicalTouch = null;
                Debug.Log("Out! side 2 scored! points: " + side2Score);
                StartCoroutine(PlaySounds(false));
                CheckWinSet(false);
            }
        }

        outCheckPending = false; // always reset so next point can trigger it again
    }

    // After each score, check the win conditions for both sides
    void CheckWinSet(bool leftJustScored)
    {
        // Set game manager state to end of point
        GameManager.Instance.gameState = GameManager.GameState.PointEnd;

        if (side1Score >= 15 && side1Score - side2Score >= 2)
        {
            Debug.Log("side 1 wins! final score: " + side1Score + " to " + side2Score);
            side1SetsWon++;
            side1SetUI.text = side1SetsWon.ToString();
            CheckMatchWin();
        } 
        else if (side2Score >= 15 && side2Score - side1Score >= 2)
        {
            Debug.Log("side 2 wins! final score: " + side1Score + " to " + side2Score);
            side2SetsWon++;
            side2SetUI.text = side2SetsWon.ToString();
            CheckMatchWin();
        }
        else
        {
            StartCoroutine(StartNextPoint(leftJustScored));
        }
    }
    //Checks if the Match is won, Best of 3 format
    void CheckMatchWin()
    {
        if (side1SetsWon == 2)
        {
            Debug.Log("Side 1 wins the match!");
            inPlay = false;
            StartCoroutine(TransitionToEnd(true));
        }
        else if (side2SetsWon == 2)
        {
            Debug.Log("Side 2 wins the match!");
            inPlay = false;
            StartCoroutine(TransitionToEnd(false));
        }
        else
        {
            //Resets the score for next set
            ResetScore();
            bool leftStartServer = SetServerForNewSet();
            StartCoroutine(StartNextPoint(leftStartServer));
        }
    }

    private bool SetServerForNewSet()
    {
        // If the first set was just won, set the server to the first player on the left
        if (side1SetsWon + side2SetsWon == 1)
        {
            GameManager.Instance.server = GameManager.Instance.leftPlayer1;
            leftLastScored = true;
            return true;
        }
        else // The second set was just won, set the server to the second player on the right
        {
            GameManager.Instance.server = GameManager.Instance.rightPlayer2;
            leftLastScored = false;
            return false;
        }
    }

    // Start next point if nobody has won yet
    private IEnumerator StartNextPoint(bool leftJustScored)
    {
        // ducky: Reset additional spike speed to 0.0f
        BallManager.Instance.resetSpikeSpeed();
        outCheckPending = false; // reset for the new point
        lastPhysicalTouch = null;

        // Check for rotation of server
        if (leftJustScored != leftLastScored)
        {
            GameManager.RotateServer();
            leftLastScored = !leftLastScored;
        }

        // Updates UI For Which Side is Serving
        UpdateServeIndicator(leftJustScored);

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
        GameManager.Instance.gameState = GameManager.GameState.GameOver;

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
            GameManager.Instance.leftPlayer1.transform.position = endLocations[0].position;
            GameManager.Instance.leftPlayer2.transform.position = endLocations[1].position;
            GameManager.Instance.rightPlayer1.transform.position = endLocations[2].position;
            GameManager.Instance.rightPlayer2.transform.position = endLocations[3].position;
            blueWin.enabled = true;
            blueWinScore.text = $"{side1SetsWon}-{side2SetsWon}";
            blueWinScore.enabled = true;
            blueTrophy.enabled = true;
            confettiRoutine = StartCoroutine(ConfettiLoop(true));
        }
        else
        {
            GameManager.Instance.rightPlayer1.transform.position = endLocations[0].position;
            GameManager.Instance.rightPlayer2.transform.position = endLocations[1].position;
            GameManager.Instance.leftPlayer1.transform.position = endLocations[2].position;
            GameManager.Instance.leftPlayer2.transform.position = endLocations[3].position;
            pinkWin.enabled = true;
            pinkWinScore.text = $"{side1SetsWon}-{side2SetsWon}";
            pinkWinScore.enabled = true;
            pinkTrophy.enabled = true;
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
        SceneManager.LoadScene("MainMenu");
    }
}
