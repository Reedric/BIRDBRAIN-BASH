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

    override protected void Activate()
    {
        FireTheLazar();
    }

    private void FireTheLazar()
    {
        Vector3 ballPosition = BallManager.Instance.gameObject.GetComponent<Transform>().position;
        GameObject leftLazer = CreateLazer(ballPosition, leftEyePosition.position);
        GameObject rightLazer = CreateLazer(ballPosition, rightEyePosition.position);

        switch (GameManager.Instance.gameState)
        {
            case GameManager.GameState.Served:
                if (HasPossesion()) ballInteract.BumpBall(); // technically you han hit over on the serve, but whatevs
                break;

            case GameManager.GameState.Bumped:
                if (HasPossesion()) ballInteract.SetBall();
                break;

            case GameManager.GameState.Set:
                if (HasPossesion()) {
                    BallManager.Instance.incSpikeSpeed();
                    ballInteract.SpikeBall();
                }
                break;

            case GameManager.GameState.Spiked:
                if (HasPossesion()) {
                    BallManager.Instance.incSpikeSpeed();
                    ballInteract.BlockBall();
                }
                break;
                
            default: // We're on defense
                break;
        }

        Destroy(leftLazer, lazerDuration);
        Destroy(rightLazer, lazerDuration);
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
}
