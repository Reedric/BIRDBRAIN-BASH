using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Sonic Squawk — sound wave with a cone effect that silences birds
/// (unable to use abilities for silenceDuration) and pushes them back (40s cooldown)
/// </summary>
public class PukekoOffensiveAbility : BirdAbility
{
    [Header("Pukeko Offensive Settings")]
    [SerializeField] private float silenceDuration = 3f;
    [SerializeField] private float pushBackForce = 2f;

    [Header("Cone Settings")]
    [SerializeField] private float coneAngle = 45f;
    [SerializeField] private float coneRange = 5f;
    [SerializeField] private int coneRayCount = 10;

    public Animator animator; // Assign in inspector

    private RaycastHit[] hits; // Pre-allocate to avoid garbage collection as long as possible

    void Awake()
    {
        hits = new RaycastHit[coneRayCount];
    }

    override protected void Activate()
    {
        SonicSquawk();
        Debug.Log("Pukeko Offensive Activated");
    }

    private void SonicSquawk()
    {
        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, _cooldownTime);

        // Trigger offensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
            myBallInteract.animator.SetTrigger("OffensiveAbility");

        // Play sound effect
        AudioManager.PlayBirdSound(BirdType.PUKEKO, SoundType.OFFENSIVE, 1.0f);

        // Find all birds in the cone area via raycast
        for (int i = 0; i < coneRayCount; i++)
        {
            float angle = -coneAngle / 2 + coneAngle / (coneRayCount - 1) * i;

            // mirror direction for right-side players
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            int hitCount = Physics.RaycastNonAlloc(transform.position, direction, hits, coneRange);
            Debug.DrawRay(transform.position, direction * coneRange, Color.blue, 40f);

            for (int j = 0; j < hitCount; j++)
            {
                LineRenderer cone = new GameObject("Cone").AddComponent<LineRenderer>();
                cone.positionCount = 2;
                cone.SetPosition(0, transform.position);
                for (int k = 0; k <= coneRayCount; k++)
                {
                    float x = Mathf.Sin(Mathf.Deg2Rad * (angle + coneAngle / 2 * k / coneRayCount)) * coneRange;
                    float y = Mathf.Cos(Mathf.Deg2Rad * (angle + coneAngle / 2 * k / coneRayCount)) * coneRange;
                    cone.SetPosition(1, transform.position + new Vector3(x, y, 0));
                }
                cone.loop = true;
                cone.startWidth = 0.1f;
                cone.endWidth = 0.1f;
                cone.material = new Material(Shader.Find("Sprites/Default")) { color = Color.red };
                Destroy(cone.gameObject, 0.5f);

                if (hits[j].collider.CompareTag("Player") && hits[j].collider.gameObject != gameObject)
                {
                    // Apply silence effect to the bird
                    if (hits[j].collider.TryGetComponent<BirdAbility>(out var birdAbility))
                        StartCoroutine(ApplySilence(silenceDuration, birdAbility));

                    // Apply push back force to the bird
                    if (hits[j].collider.TryGetComponent<Rigidbody>(out var rb))
                    {
                        Vector3 pushDirection = (hits[j].collider.transform.position - transform.position).normalized;
                        rb.AddForce(pushDirection * pushBackForce, ForceMode.Impulse);
                    }
                }
            }
        }
    }

    public IEnumerator ApplySilence(float duration, BirdAbility bird)
    {
        bird.SetAbilitiesDisabled(true);
        yield return new WaitForSeconds(duration);
        bird.SetAbilitiesDisabled(false);
    }
}
