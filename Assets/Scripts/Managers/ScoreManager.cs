using UnityEngine;
using System.Collections;
using UnityEngine.Events;

public class ScoreManager : MonoBehaviour
{
    public int side1Score = 0;
    public int side2Score = 0;
    public int side1SetsWon = 0;
    public int side2SetsWon = 0;
    public GameManager gameManager;

    [Header("Bird Types")]
    [SerializeField] private BirdType rightBirdType1;
    private bool leftLastScored;
    private bool inPlay;
    UnityEvent LeftScored;
    UnityEvent RightScored;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
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

        // Make sure game manager is not null in inspector
        if (gameManager == null)
        {
            Debug.LogError("Game Manager was not set in inspector for Score Manager!");
        }
        LeftScored.Invoke();
    }

    // Checking to see if the ball hits the court on either side
    void OnCollisionEnter(Collision collision)
    {
        // if it touches side 1, then side 2 scores
        if (collision.gameObject.CompareTag("Side1") && inPlay)
        {
            side2Score += 1;
            inPlay = false;
            Debug.Log("side 2 scored! points: " + side2Score);
            LeftScored.Invoke();
            PlaySounds(true);
            CheckWinSet(true);
        } 
        // if it touches side 2, then side 1 scores
        else if (collision.gameObject.CompareTag("Side2") && inPlay) 
        {
            side1Score += 1;
            inPlay = false;
            Debug.Log("side 1 scored! points: " + side1Score);
            RightScored.Invoke();
            PlaySounds(false);
            CheckWinSet(false);
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

        // Checks if ball still in play
        if (inPlay)
        {
            // Left side scores
            if (gameManager.lastHit == gameManager.leftPlayer1 || gameManager.lastHit == gameManager.leftPlayer2)
            {
                side1Score += 1;
                inPlay = false;
                Debug.Log("Out! side 1 scored! points: " + side1Score);
                PlaySounds(false);
                CheckWinSet(false);
            }

            // Right side scores
            else if (gameManager.lastHit == gameManager.rightPlayer1 || gameManager.lastHit == gameManager.rightPlayer2)
            {
                side2Score += 1;
                inPlay = false;
                Debug.Log("Out! side 2 scored! points: " + side2Score);
                PlaySounds(true);
                CheckWinSet(true);
            }
        }
    }

    // After each score, check the win conditions for both sides
    void CheckWinSet(bool leftJustScored)
    {
        if (side1Score >= 15 && side1Score - side2Score >= 2)
        {
            Debug.Log("side 1 wins! final score: " + side1Score + " to " + side2Score);
            side1SetsWon++;
            CheckMatchWin(leftJustScored);
        } 
        else if (side2Score >= 15 && side2Score - side1Score >= 2)
        {
            Debug.Log("side 2 wins! final score: " + side1Score + " to " + side2Score);
            side2SetsWon++;
            CheckMatchWin(leftJustScored);
        }
        else
        {
            StartCoroutine(StartNextPoint(leftJustScored));
        }
    }
    //Checks if the Match is won, Best of 3 format
    void CheckMatchWin(bool leftJustScored)
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
            StartCoroutine(StartNextPoint(leftJustScored));
        }
    }

    // Start next point if nobody has won yet
    private IEnumerator StartNextPoint(bool leftJustScored)
    {
        // Check for rotation of server
        if (leftJustScored != leftLastScored)
        {
            gameManager.RotateServer();
            leftLastScored = !leftLastScored;
        }

        // Wait 2 seconds
        yield return new WaitForSeconds(2.0f);

        // Start the next point
        gameManager.leftAttack = leftLastScored;
        gameManager.NextPoint();
        inPlay = true;
    }

    // Reset the only the set points
    public void ResetScore()
    {
        // Set the scores to 0 and the ball is in play
        side1Score = 0;
        side2Score = 0;
        inPlay = true;
    }
    //Reset the entire Match
    void ResetMatch()
    {
        // Set the scores to 0, the sets to 0, right to serve, and the ball is in play
        side1Score = 0;
        side2Score = 0;
        side1SetsWon = 0;
        side2SetsWon = 0;
        leftLastScored = false;
        inPlay = true;
    }

    // Play sounds once a point is scored
    void PlaySounds(bool leftJustScored)
    {
        // Play the correct sounds depending on which team just scored
        if (leftJustScored)
        {
            AudioManager.PlayBirdSound(rightBirdType1, SoundType.SAD, 1.0f);
        }
        else
        {
            AudioManager.PlayBirdSound(rightBirdType1, SoundType.HAPPY, 1.0f);
        }
    }
}
