using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// predicts the trajectory of the ball visualized by a line.
/// </summary>
public class OwlDefensive : BirdAbility
{
    [SerializeField] private float cooldown = 15f;
    private bool onCooldown = false;
    [SerializeField] private float lineDuration;

    public void OnDefensiveAbility(InputValue value)
    {
        Investigation();
    }

    private void Investigation()
    {
        if (onCooldown) return;

        // Predict ball trajectory and draw line for lineDuration seconds, then remove line and start cooldown
        // EJ: This is a placeholder implementation using physics, the actual trajectory prediction would be more complex using a "ghost simulation" scene under the main scene
        Vector3 ballPosition = GameObject.FindWithTag("Ball").transform.position;
        Vector3 ballVelocity = GameObject.FindWithTag("Ball").GetComponent<Rigidbody>().linearVelocity;
        Vector3 predictedPosition = ballPosition + ballVelocity * 1f; // Predict position 1 second in the future

        StartCoroutine(DrawDefensiveLine(ballPosition, predictedPosition));
    }

    private IEnumerator DrawDefensiveLine(Vector3 start, Vector3 end)
    {
        GameObject line = new("OwlDefensiveLine");
        LineRenderer lineRenderer = line.AddComponent<LineRenderer>();
        lineRenderer.material = new(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.blue;
        lineRenderer.endColor = Color.blue;
        lineRenderer.startWidth = 0.2f;
        lineRenderer.endWidth = 0.2f;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);

        onCooldown = true;
        yield return new WaitForSeconds(lineDuration);
        Destroy(line);

        yield return new WaitForSeconds(cooldown - lineDuration);
        onCooldown = false;
    }
}
