using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ScissortailDefensive : BirdAbility
{
    [Header("Yuriful")]
    public float lineUptime = 3.0f;
    public float lineWidth = 0.5f;
    public float threshold = 1.0f;
    public Material lineMaterial;
    private LineRenderer lr;

    void Start()
    {
        lr = gameObject.AddComponent<LineRenderer>();
        lr.enabled = false;

        // Initializes the line
        lr.positionCount = 2;
        lr.startWidth = lineWidth;
        lr.generateLightingData = true;
        lr.material = lineMaterial;
    }

    // Returns which game object is ally
    private GameObject GetAlly()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameObject == gameManager.leftPlayer1)
        {
            return gameManager.leftPlayer2;
        }
        else if (gameObject == gameManager.leftPlayer2)
        {
            return gameManager.leftPlayer1;
        }
        else if (gameObject == gameManager.rightPlayer1)
        {
            return gameManager.rightPlayer2;
        }
        else if (gameObject == gameManager.rightPlayer2)
        {
            return gameManager.rightPlayer1;
        }
        else
        {
            Debug.Log("Player not found!");
            return null;
        }
    }

    public IEnumerator Yuriful()
    {
        GameObject ally = GetAlly();
        if (ally == null)
        {
            yield break;
        }

        AudioManager.PlayBirdSound(BirdType.SCISSORTAIL, SoundType.DEFENSIVE, 1.0f);

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, cooldownTime);

        // Trigger defensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
        {
            myBallInteract.animator.SetTrigger("DefensiveAbility");
        }

        float timer = 0f;
        Debug.Log("Creating line....");
        
        // Makes line visible and Updates the lines position for the uptime of the ability (lineUptime).
        lr.enabled = true;
        while (timer < lineUptime)
        {
            timer += Time.deltaTime;
            Vector3 selfPos = gameObject.transform.position;
            Vector3 allyPos = ally.transform.position;
            lr.SetPosition(0, selfPos);
            lr.SetPosition(1, allyPos);

            Vector3 ballPos = BallManager.Instance.gameObject.transform.position;
            Vector3 line = (allyPos - selfPos).normalized;
            Vector3 toBall = ballPos - selfPos;
            float distanceToLine = Vector3.Cross(line, toBall).magnitude;

            // If the ball is within the threshold range of the line ends ability
            if (distanceToLine < threshold)
            {
                BallInteract interact = GetComponent<BallInteract>();
                interact.BumpBall();
                break;
            }
            yield return null;
        }
        // Disables the line and starts cooldown
        lr.enabled = false;
    }

    override protected void Activate()
    {
        StartCoroutine(Yuriful());
    }
}
