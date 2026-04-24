using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BallInteract))]

/// <summary>
/// Fire the Lazar - Kiwi fires a laser beam from its eyes to hit the ball, 
/// which automatically counts as the next action required for the ball in the rally. 
/// If spiking or blocking, increases the ball’s speed.
/// 
/// TODO: Find a way to temporarily increase ball speed
/// </summary>
public class KiwiOffensive : BirdAbility
{
    [SerializeField] private float cooldown = 15f;
    private bool onCooldown = false;

    // Positions for the laser to originate from (could be empty GameObjects placed at the eyes in the Unity editor)
    [SerializeField] private Transform leftEyePosition;
    [SerializeField] private Transform rightEyePosition;

    [Header("Lazer Settings")]
    [SerializeField] private float lazerWidth = 0.2f;
    [SerializeField] private Color lazerColor = Color.red;
    [SerializeField] private float lazerDuration = 0.1f;

    BallInteract ballInteract;

    void Awake()
    {
        ballInteract = GetComponent<BallInteract>();
    }

    public void OnOffensiveAbility(InputValue value)
    {
        StartCoroutine(FireTheLazar());
    }

    private IEnumerator FireTheLazar()
    {
        if (onCooldown || !CanUseAbilities() || !PointInProgress()) yield break;
        onCooldown = true;

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, cooldown);

        Vector3 ballPosition = BallManager.Instance.gameObject.GetComponent<Transform>().position;
        GameObject leftLazer = CreateLazer(ballPosition, leftEyePosition.position);
        GameObject rightLazer = CreateLazer(ballPosition, rightEyePosition.position);

        switch (gameManager.gameState)
        {
            case GameManager.GameState.Served:
                ballInteract.BumpBall();
                break;

            case GameManager.GameState.Bumped:
                ballInteract.SetBall();
                break;

            case GameManager.GameState.Set:
                BallManager.Instance.incSpikeSpeed();
                ballInteract.SpikeBall();
                break;

            case GameManager.GameState.Spiked:
                BallManager.Instance.incSpikeSpeed();
                ballInteract.BlockBall();
                break;
                
            default: // We're on defense
                break;
        }

        Destroy(leftLazer, lazerDuration);
        Destroy(rightLazer, lazerDuration);

        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }

    private GameObject CreateLazer(Vector3 ballPosition, Vector3 eyePosition)
    {
        GameObject temp = new();
        LineRenderer lineRenderer = temp.AddComponent<LineRenderer>();
        lineRenderer.material = new(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = lazerColor;
        lineRenderer.endColor = lazerColor;
        lineRenderer.startWidth = lazerWidth;
        lineRenderer.endWidth = lazerWidth;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, eyePosition);
        lineRenderer.SetPosition(1, ballPosition);
        return temp;
    }
}
