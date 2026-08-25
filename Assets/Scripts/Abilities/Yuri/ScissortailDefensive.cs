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
    public float lineHeight = 1.0f; // Consistent height of the defensive line above the ground

    private LineRenderer lr;
    private Coroutine yurifulCoroutine;
    private bool abilityActive;

    void Start()
    {
        lr = gameObject.AddComponent<LineRenderer>();
        lr.enabled = false;

        // Initializes the line
        lr.positionCount = 2;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.generateLightingData = true;
        lr.material = lineMaterial;
    }

    private void OnDisable()
    {
        StopYuriful();
    }

    private void StopYuriful()
    {
        if (yurifulCoroutine != null)
        {
            StopCoroutine(yurifulCoroutine);
            yurifulCoroutine = null;
        }

        abilityActive = false;

        if (lr != null)
            lr.enabled = false;
    }

    private bool PointHasEnded()
    {
        return GameManager.Instance == null ||
               GameManager.Instance.gameState == GameManager.GameState.Set;
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

    private Vector3 GetLinePosition(GameObject bird)
    {
        if (bird == null)
            return Vector3.zero;

        Vector3 position = bird.transform.position;
        position.y = lineHeight;

        return position;
    }

    public IEnumerator Yuriful()
    {
        abilityActive = true;

        GameObject ally = GetAlly();
        if (ally == null)
        {
            abilityActive = false;
            yurifulCoroutine = null;
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

        // Use a consistent elevation for the line regardless of bird root/contact point height.
        Vector3 selfPos = GetLinePosition(gameObject);
        Vector3 allyPos = GetLinePosition(ally);
        Vector3 toAlly = allyPos - selfPos;

        // Slerp the tip outward from self toward ally — sweeps like a quill stroke
        float elapsed = 0f;

        while (elapsed < lineDrawDuration)
        {
            if (PointHasEnded())
            {
                StopYuriful();
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / lineDrawDuration);

            // Slerp between a tiny seed vector and the full toAlly vector so the
            // tip sweeps outward along the arc rather than popping in linearly
            Vector3 tipOffset = Vector3.Slerp(toAlly.normalized * 0.01f, toAlly, t);

            lr.SetPosition(0, selfPos);
            lr.SetPosition(1, selfPos + tipOffset);

            yield return null;
        }

        // Line is fully drawn — now track both players live while maintaining
        // the same elevation above the ground.
        float timer = 0f;

        while (timer < lineUptime)
        {
            if (PointHasEnded())
            {
                StopYuriful();
                yield break;
            }

            timer += Time.deltaTime;

            selfPos = GetLinePosition(gameObject);
            allyPos = GetLinePosition(ally);

            lr.SetPosition(0, selfPos);
            lr.SetPosition(1, allyPos);

            Vector3 ballPos = BallManager.Instance.gameObject.transform.position;
            Vector3 line = (allyPos - selfPos).normalized;
            Vector3 toBall = ballPos - selfPos;
            float distanceToLine = Vector3.Cross(line, toBall).magnitude;

            // If the ball is within the threshold range of the line ends ability
            if (distanceToLine < threshold &&
                GameManager.Instance.gameState != GameManager.GameState.Set)
            {
                BallInteract interact = GetComponent<BallInteract>();

                if (interact != null)
                {
                    if (GameManager.Instance.gameState == GameManager.GameState.Bumped)
                        interact.SetBall();
                    else
                        interact.BumpBall();
                }

                break;
            }

            yield return null;
        }

        // Disables the line and starts cooldown
        // Reverse slerp — tip retreats from ally back to self
        if (PointHasEnded())
        {
            StopYuriful();
            yield break;
        }

        Vector3 finalSelfPos = GetLinePosition(gameObject);
        Vector3 finalAllyPos = GetLinePosition(ally);
        Vector3 finalToAlly = finalAllyPos - finalSelfPos;

        elapsed = 0f;

        while (elapsed < lineDrawDuration)
        {
            if (PointHasEnded())
            {
                StopYuriful();
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / lineDrawDuration);

            Vector3 tipOffset = Vector3.Slerp(
                finalToAlly,
                finalToAlly.normalized * 0.01f,
                t
            );

            lr.SetPosition(0, finalSelfPos);
            lr.SetPosition(1, finalSelfPos + tipOffset);

            yield return null;
        }

        lr.enabled = false;
        abilityActive = false;
        yurifulCoroutine = null;
    }

    override protected bool Activate()
    {
        if (abilityActive)
            return false;

        yurifulCoroutine = StartCoroutine(Yuriful());

        // Start the HUD cooldown immediately when the ability activates.
        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);

        return true;
    }
}