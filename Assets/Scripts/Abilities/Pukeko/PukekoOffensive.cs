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
    [SerializeField] private float cooldown = 40f;
    [SerializeField] private float silenceDuration = 3f;
    [SerializeField] private float pushBackForce = 2f;

    [Header("Cone Settings")]
    [SerializeField] private float coneAngle = 45f;
    [SerializeField] private float coneRange = 5f;
    [SerializeField] private int coneRayCount = 10;

    public Animator animator; // Assign in inspector

    private bool onCooldown = false;
    private RaycastHit[] hits;

    void Awake()
    {
        hits = new RaycastHit[coneRayCount];
        _onLeft = GetComponent<BallInteract>().onLeft;
    }

    public void OnOffensiveAbility()
    {
        if (!onCooldown)
        {
            onCooldown = true;
            StartCoroutine(SonicSquawk());
        }
    }

    private IEnumerator SonicSquawk()
    {
        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, cooldown);

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
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
            int hitCount = Physics.RaycastNonAlloc(transform.position, direction, hits, coneRange);
            Debug.DrawRay(transform.position, direction * coneRange, Color.blue, 40f);

            for (int j = 0; j < hitCount; j++)
            {
                // Visualization
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
                    GameObject target = hits[j].collider.gameObject;

                    // Resolve which side the target is on for correct VFX prefab
                    bool targetIsOnLeft = false;
                    BallInteract targetBallInteract = target.GetComponent<BallInteract>();
                    if (targetBallInteract != null)
                        targetIsOnLeft = targetBallInteract.onLeft;
                    else
                    {
                        AIBehavior targetAI = target.GetComponent<AIBehavior>();
                        if (targetAI != null)
                            targetIsOnLeft = targetAI.onLeft;
                    }

                    // Apply silence, BuffsDebuffs handles VFX, audio, and re-enabling abilities
                    BuffsDebuffs.Instance.ApplyEffect(
                        BuffsDebuffs.EffectType.Silence,
                        target,
                        silenceDuration,
                        targetIsOnLeft
                    );

                    // Apply push back force
                    if (hits[j].collider.TryGetComponent<Rigidbody>(out var rb))
                    {
                        Vector3 pushDirection = (hits[j].collider.transform.position - transform.position).normalized;
                        rb.AddForce(pushDirection * pushBackForce, ForceMode.Impulse);
                    }
                }
            }
        }

        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }
}