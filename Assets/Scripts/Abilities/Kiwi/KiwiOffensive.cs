using UnityEngine;

[RequireComponent(typeof(BallInteract))]

/// <summary>
/// Fire the Lazar - Kiwi fires a laser beam from its eyes to hit the ball, 
/// which automatically counts as the next action required for the ball in the rally. 
/// If spiking or blocking, increases the ball’s speed.
/// </summary>
public class KiwiOffensive : BirdAbility
{
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

    override protected bool Activate()
    {
        return FireTheLazar();
    }

    private bool FireTheLazar()
    {
        if (!HasPossesion() || IsOwnTeamServing() || gameObject.Equals(GameManager.Instance.lastHit)) return false;  // add !HasPossesion()

        // Play offensive sound
        AudioManager.PlayBirdSound(BirdType.KIWI, SoundType.OFFENSIVE, 1.0f);

        Vector3 ballPosition = BallManager.Instance.gameObject.GetComponent<Transform>().position;
        GameObject leftLazer = CreateLazer(ballPosition, leftEyePosition.position);
        GameObject rightLazer = CreateLazer(ballPosition, rightEyePosition.position);

        switch (GameManager.Instance.gameState)
        {
            case GameManager.GameState.Served:
                if (HasPossesion()) ballInteract.BumpBall(); // technically you can hit over on the serve, but whatevs
                break;

            case GameManager.GameState.Bumped:
                if (HasPossesion()) ballInteract.SetBall();
                break;

            case GameManager.GameState.Set:
                if (HasPossesion())
                {
                    BallManager.Instance.incSpikeSpeed();
                    ballInteract.SpikeBall();
                }
                break;

            case GameManager.GameState.Spiked:
                if (HasPossesion() && IsBallCloseToNet())
                {
                    BallManager.Instance.incSpikeSpeed();
                    ballInteract.BlockBall();
                }
                else if (HasPossesion()) ballInteract.BumpBall();
                break;
                
            default: // We're on defense
                break;
        }

        Destroy(leftLazer, lazerDuration);
        Destroy(rightLazer, lazerDuration);

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, _cooldownTime);

        // Successfully activated ability
        return true;
    }

    private bool IsOwnTeamServing()
    {
        bool onLeft = transform.position.x < 0;

        // Block during point start (serve hasn't launched yet)
        if (GameManager.Instance.gameState == GameManager.GameState.PointStart) return true;

        // Block if ball was served by your own team
        if (GameManager.Instance.gameState == GameManager.GameState.Served && GameManager.Instance.leftAttack == onLeft) return true;

        return false;
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

    private bool HasPossesion()
    {
        bool onLeft = transform.position.x < 0;
        Vector3 ballPosition = BallManager.Instance.gameObject.GetComponent<Transform>().position;
        return onLeft == (ballPosition.x < 0);
    }

    private bool IsBallCloseToNet()
    {
        return Mathf.Abs(BallManager.Instance.transform.position.x) < 1.5f;
    }
}
