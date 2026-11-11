using UnityEngine;
using System.Collections;

/// <summary>
/// Sonic Squawk - sound wave with a cone effect that silences birds
/// (unable to use abilities for silenceDuration) and pushes them back (40s cooldown)
/// </summary>
// The class name has to match the file name, or Unity can't load it as a component on the Pukeko prefabs
public class PukekoOffensive : BirdAbility
{
    [Header("Pukeko Offensive Settings")]
    [SerializeField] private float silenceDuration = 3f;
    [SerializeField] private float pushBackForce = 8f; // bumped up from 2f, impulse needs more weight

    [Header("Effects")]
    [SerializeField] private GameObject squawkParticlesPrefab; // Assign prefab in inspector

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

    override protected bool Activate()
    {
        SonicSquawk();

        // Successfully activated ability
        return true;
    }

    private void SonicSquawk()
    {
        Vector3 firingForward = Quaternion.Euler(-GetComponent<CharacterMovement>().rotationOffsetEuler) * transform.forward;
        firingForward.y = 0f;
        firingForward.Normalize();

        if (squawkParticlesPrefab != null)
        {
            Quaternion facingRotation = Quaternion.LookRotation(firingForward, Vector3.up);
            Quaternion vfxRotation = facingRotation * Quaternion.Euler(0f, -90f, 0f);

            GameObject particles =
                NetworkFx.Instantiate(squawkParticlesPrefab, transform.position, vfxRotation);

            ParticleSystem[] allSystems = particles.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in allSystems)
            {
                var main = ps.main;
                main.loop = false;
                main.stopAction = ParticleSystemStopAction.Destroy;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                ps.Play(true);
            }

            Destroy(particles, 10f);
        }
        // Trigger offensive ability animation if animator exists
        var myBallInteract = GetComponent<BallInteract>();
        if (myBallInteract != null && myBallInteract.animator != null)
            myBallInteract.animator.SetTriggerSynced("OffensiveAbility");

        // Play sound effect
        AudioManager.PlayBirdSound(BirdType.PUKEKO, SoundType.OFFENSIVE, 1.0f);

        // Several rays usually hit the same bird. Silencing it again for each one would restart the effect and replay
        // its sound every time, so each bird is only silenced once. (The push still adds up per ray, which is how
        // pushBackForce has been tuned.)
        System.Collections.Generic.HashSet<GameObject> alreadySilenced = new();

        // Find all birds in the cone area via raycast
        for (int i = 0; i < coneRayCount; i++)
        {
            float angle = -coneAngle / 2 + coneAngle / (coneRayCount - 1) * i;

            // Use the bird's actual facing direction so the cone follows the player orientation
            Vector3 baseDirection = firingForward;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * baseDirection;

            int hitCount = Physics.RaycastNonAlloc(transform.position, direction, hits, coneRange);
            Debug.DrawRay(transform.position, direction * coneRange, Color.blue, 40f);

            for (int j = 0; j < hitCount; j++)
            {
                // Found by component rather than the "Player" tag, which some birds' prefabs don't have
                GameObject target = BirdLookup.FindBird(hits[j].collider.gameObject);
                if (target != null && target != gameObject)
                {

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

                    // Skip allies (same side as the caster)
                    if (targetIsOnLeft == (transform.position.x < 0)) continue;

                    // Ostrich is immune to silence!
                    BallInteract birdPlayer = target.GetComponent<BallInteract>();
                    BirdType birdType = birdPlayer != null
                        ? birdPlayer.GetBirdType()
                        : target.GetComponent<AIBehavior>().GetBirdType();

                    if (birdType == BirdType.OSTRICH) continue;

                    if (alreadySilenced.Add(target))
                    {
                        BuffsDebuffs.Instance.ApplyEffect(
                            BuffsDebuffs.EffectType.Silence,
                            target,
                            silenceDuration,
                            targetIsOnLeft
                        );
                    }

                    // Push enemy directly away from the bird's facing direction
                    if (target.TryGetComponent<Rigidbody>(out var rb))
                    {
                        Vector3 pushDirection = firingForward;
                        rb.AddForce(pushDirection * pushBackForce, ForceMode.Impulse);
                    }
                }
            }
        }

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, _cooldownTime);
    }
}
