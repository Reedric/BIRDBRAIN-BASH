using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ScissortailDefensive : BirdAbility
{
    [Header("Yuriful")]
    public float lineUptime = 3.0f;
    public float lineWidth = 0.5f;
    public float threshold = 1.0f;
    public float lineDrawDuration = 0.3f; // How long the draw animation takes
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

        // Trigger defensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
        {
            myBallInteract.animator.SetTrigger("DefensiveAbility");
        }

        Debug.Log("Creating line....");
        lr.enabled = true;

        // Snapshot positions at the moment of cast for the draw animation
        Vector3 selfPos = gameObject.transform.Find("ContactPoint") == null // Use contact point (if it exists)
            ? gameObject.transform.position : gameObject.transform.Find("ContactPoint").position; 
        Vector3 allyPos = ally.transform.Find("ContactPoint") == null // Use contact point (if it exists)
            ? ally.transform.position : ally.transform.Find("ContactPoint").position;
        Vector3 toAlly = allyPos - selfPos;

        // Slerp the tip outward from self toward ally — sweeps like a quill stroke
        float elapsed = 0f;
        while (elapsed < lineDrawDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / lineDrawDuration);

            // Slerp between a tiny seed vector and the full toAlly vector so the
            // tip sweeps outward along the arc rather than popping in linearly
            Vector3 tipOffset = Vector3.Slerp(toAlly.normalized * 0.01f, toAlly, t);
            lr.SetPosition(0, selfPos);
            lr.SetPosition(1, selfPos + tipOffset);
            yield return null;
        }

        // Line is fully drawn — now track both players live for the uptime
        float timer = 0f;
        while (timer < lineUptime)
        {
            timer += Time.deltaTime;
            selfPos = gameObject.transform.position;
            allyPos = ally.transform.position;
            lr.SetPosition(0, selfPos);
            lr.SetPosition(1, allyPos);

            Vector3 ballPos = BallManager.Instance.gameObject.transform.position;
            Vector3 line = (allyPos - selfPos).normalized;
            Vector3 toBall = ballPos - selfPos;
            float distanceToLine = Vector3.Cross(line, toBall).magnitude;

            // If the ball is within the threshold range of the line ends ability
            if (distanceToLine < threshold && GameManager.Instance.gameState != GameManager.GameState.Set)
            {
                BallInteract interact = GetComponent<BallInteract>();
                if (GameManager.Instance.gameState == GameManager.GameState.Bumped) interact.SetBall();
                else interact.BumpBall();
                break;
            }
            yield return null;
        }

        // Disables the line and starts cooldown
        // Reverse slerp — tip retreats from ally back to self
        Vector3 finalSelfPos = gameObject.transform.position;
        Vector3 finalAllyPos = ally.transform.position;
        Vector3 finalToAlly = finalAllyPos - finalSelfPos;

        elapsed = 0f;
        while (elapsed < lineDrawDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / lineDrawDuration);

            // Reverse of the draw — tip shrinks from full extent back to a tiny seed
            Vector3 tipOffset = Vector3.Slerp(finalToAlly, finalToAlly.normalized * 0.01f, t);
            lr.SetPosition(0, finalSelfPos);
            lr.SetPosition(1, finalSelfPos + tipOffset);
            yield return null;
        }

        lr.enabled = false;

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);
    }

    override protected bool Activate()
    {
        StartCoroutine(Yuriful());

        // Ability successfully activated
        return true;
    }
}