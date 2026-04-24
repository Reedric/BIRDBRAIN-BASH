using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sketches a line in the sand using her quill across the middle of the enemy court, 
/// preventing enemies from crossing it.
/// </summary>
public class OwlOffensive : BirdAbility
{
    [SerializeField] private float cooldown = 25f + 8f; // 8s line duration + 25s cooldown
    private bool onCooldown = false;
    [SerializeField] private float lineDuration = 8f;
    [SerializeField] private Color lineColor = Color.red;
    [SerializeField] private float lineWidth = 0.2f;
    public Animator animator; // Assign in inspector

    public void OnOffensiveAbility(InputValue value)
    {
        CaptureCure();
    }

    private void CaptureCure()
    {
        // Trigger offensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
        {
            myBallInteract.animator.SetTrigger("OffensiveAbility");
        }

        // Play sound effect using AudioManager
        AudioManager.PlayBirdSound(BirdType.OWL, SoundType.OFFENSIVE, 1.0f);
        
        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, cooldown);

        if (onCooldown || !CanUseAbilities() || !PointInProgress()) return;

        // Draw line in enemy court for lineDuration seconds, then remove line and start cooldown
        if (transform.position.x > 0) // Facing right, so line goes in right court
        {
            StartCoroutine(DrawOffensiveLine(new Vector3(0, 0.1f, 0), new Vector3(-9, 0.1f, 0)));
        }
        else // Facing left, so line goes in left court
        {
            StartCoroutine(DrawOffensiveLine(new Vector3(0, 0.1f, 0), new Vector3(9, 0.1f, 0)));
        }
    }

    private IEnumerator DrawOffensiveLine(Vector3 start, Vector3 end)
    {
        if (onCooldown) yield break;

        // Create line object and set its position and rotation to be between the start and end points
        GameObject line = new("OwlOffensiveLine") { layer = LayerMask.NameToLayer("Line") };
        LineRenderer lineRenderer = line.AddComponent<LineRenderer>();
        lineRenderer.material = new(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);

        BoxCollider lineCollider = line.AddComponent<BoxCollider>();
        lineCollider.isTrigger = false;
        lineCollider.size = new Vector3(Vector3.Distance(start, end), 20f, 0f);
        lineCollider.center = new Vector3(end.x / 2, 10f, 0f); // moves the collider up so its not underground

        onCooldown = true;
        float time = 0f;
        while (time < lineDuration)
        {
            time += Time.deltaTime;
            yield return null; // this waits for one frame, so essentually unity update but in a coroutine
            
            // If point ended, destroy line
            if (GameManager.Instance.gameState == GameManager.GameState.PointEnd)
            {
                time = lineDuration;
            }
        }
        
        // EJ: For anyone happening to be editing this code make sure the line is destroyed
        // EJ :Its a procedural asset and wont be automatically destroyed creating a memory leak
        Destroy(line);

        yield return new WaitForSeconds(cooldown - lineDuration);
        onCooldown = false;
    }
}
