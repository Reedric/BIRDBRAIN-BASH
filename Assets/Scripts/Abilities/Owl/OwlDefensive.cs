using System.Collections;
using UnityEngine;

/// <summary>
/// predicts the trajectory of the ball visualized by a line.
/// </summary>
public class OwlDefensive : BirdAbility
{
    [Header("Owl Defensive Settings")]
    [SerializeField] private float lineDuration = 10f;

    [Header("Line Settings")]
    [SerializeField] private Gradient lineGradient;
    [SerializeField] private float lineWidth = 0.2f;
    [SerializeField] private int lineSegments = 20; // adjust for smoothness and performance
    [SerializeField] private float predictionTime = 1f;

    // Ball references cache
    private Transform ballTransform;
    private Rigidbody ballRigidbody;

    private Vector3[] predictionPoints;

    private void Awake() 
    {
        GameObject ball = GameObject.FindWithTag("Ball");
        ballTransform = ball.transform;
        ballRigidbody = ball.GetComponent<Rigidbody>();
        predictionPoints = new Vector3[lineSegments];
    }

    override protected void Activate()
    {
        StartCoroutine(Investigation());
    }

    private IEnumerator Investigation()
    {
        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, cooldownTime);
        
        GameObject line = CreateLine();
        LineRenderer lineRenderer = line.GetComponent<LineRenderer>();

        float time = 0f;
        while (time < lineDuration)
        {
            UpdateLine(lineRenderer);
            time += Time.deltaTime;
            yield return null; // this waits for one frame, so essentually unity update but in a coroutine
            
            // If point ended, destroy line
            if (GameManager.Instance.gameState == GameManager.GameState.PointEnd)
            {
                time = lineDuration;
            }
        }
        Destroy(line);
    }

    private GameObject CreateLine()
    {
        GameObject line = new("OwlDefensiveLine");
        LineRenderer lineRenderer = line.AddComponent<LineRenderer>();

        lineRenderer.material = new(Shader.Find("Sprites/Default"));
        lineRenderer.colorGradient = lineGradient;

        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        lineRenderer.positionCount = lineSegments;
        
        // tapers off the line as the prediction gets farther
        AnimationCurve widthCurve = new();
        widthCurve.AddKey(0f, lineWidth);
        widthCurve.AddKey(1f, 0.01f);
        lineRenderer.widthCurve = widthCurve;

        return line;
    }

    private void UpdateLine(LineRenderer lineRenderer)
    {
        Vector3[] points = SimulateTrajectory();
        lineRenderer.SetPositions(points);
    }

    private Vector3[] SimulateTrajectory()
    {
        // EJ: This is a placeholder implementation using physics, the actual trajectory prediction would be more complex using a "ghost simulation" scene under the main scene
        // EJ: But if this works fine then just keep
        Vector3 start = ballTransform.position;
        Vector3 velocity = ballRigidbody.linearVelocity;

        for (int i = 0; i < lineSegments; i++)
        {
            float t = (float) i / (lineSegments - 1) * predictionTime;
            predictionPoints[i] = start + velocity * t + 0.5f * t * t * Physics.gravity;
        }

        return predictionPoints;
    }
}
