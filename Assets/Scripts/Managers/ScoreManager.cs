using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using TMPro;
using System;

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
   
    private bool leftLastScored;
    private bool inPlay;
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
        else if (collision.gameObject.CompareTag("Out"))
        {
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
            // Left side scores
            if (gameManager.lastHit == gameManager.rightPlayer1 || gameManager.lastHit == gameManager.rightPlayer2)
            {
                side1Score += 1;
                side1ScoreUI.text = side1Score.ToString();
                inPlay = false;
                Debug.Log("Out! side 1 scored! points: " + side1Score);
                StartCoroutine(PlaySounds(true));
                CheckWinSet(true);
            }

            // Right side scores
            else if (gameManager.lastHit == gameManager.leftPlayer1 || gameManager.lastHit == gameManager.leftPlayer2)
            {
                side2Score += 1;
                side2ScoreUI.text = side2Score.ToString();
                inPlay = false;
                Debug.Log("Out! side 2 scored! points: " + side2Score);
                StartCoroutine(PlaySounds(false));
                CheckWinSet(false);
            }
        }
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
        }
        else if (side2SetsWon == 2)
        {
            Debug.Log("Side 2 wins the match!");
            inPlay = false;
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
}
