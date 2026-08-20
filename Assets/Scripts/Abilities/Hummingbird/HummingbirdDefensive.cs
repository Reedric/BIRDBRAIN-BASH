using System.Collections;
using UnityEngine;

public class HummingbirdDefensive : BirdAbility
{
    [Header("Dash Ability")]
    [SerializeField] private float dashDuration = 0.25f;
    [SerializeField] private float dashSpeed = 30.0f;
    [SerializeField] private float speedBoost = 2.0f;
    [SerializeField] private float speedBoostDuration = 2.0f;

    [HideInInspector] public bool isDashing = false;

    [Header("Effects")]
    [SerializeField] private GameObject dashEffectPrefab;
    [SerializeField] private bool applySpeedBoostEffect = true;

    private CharacterMovement cM;
    private Rigidbody rb;
    private BallInteract ballInteract;
    private Renderer[] renderers;
    private Coroutine dashCoroutine;
    private Coroutine speedBoostCoroutine;

    private float originalGroundSpeed;
    private float originalAirSpeed;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        cM = GetComponent<CharacterMovement>();
        ballInteract = GetComponent<BallInteract>();

        renderers = GetComponentsInChildren<Renderer>(true);
    }

    protected override bool Activate()
    {
        if (cM == null || rb == null || !cM.grounded)
            return false;

        if (dashCoroutine != null)
            StopCoroutine(dashCoroutine);

        dashCoroutine = StartCoroutine(StartDash());

        return true;
    }

    IEnumerator StartDash()
    {
        isDashing = true;

        cM.controlMovement(false, false);

        Vector3 dashDirection = Quaternion.Euler(cM.rotationOffsetEuler) * transform.forward;
        dashDirection.y = 0f;

        if (dashDirection.sqrMagnitude < 0.01f)
        {
            dashDirection = transform.forward;
            dashDirection.y = 0f;
        }

        dashDirection.Normalize();

        rb.linearVelocity = new Vector3(
            dashDirection.x * dashSpeed,
            rb.linearVelocity.y,
            dashDirection.z * dashSpeed
        );

        if (ballInteract != null && ballInteract.animator != null)
        {
            ballInteract.animator.SetTrigger("DefensiveAbility");
        }
        else if (cM.animator != null)
        {
            cM.animator.SetTrigger("DefensiveAbility");
        }

        AudioManager.PlayBirdSound(
            BirdType.HUMMINGBIRD,
            SoundType.DEFENSIVE,
            1.0f
        );

        SetVisible(false);

        SpawnDashEffect();

        int playerID = ballInteract != null ? ballInteract.playerID : -1;
        if (playerID >= 0 && HUDManager.Instance != null)
        {
            HUDManager.Instance.TriggerDefensiveCooldown(
                playerID,
                _cooldownTime
            );
        }

        yield return new WaitForSeconds(dashDuration);

        EndDash();

        dashCoroutine = null;
    }

    private void EndDash()
    {
        isDashing = false;

        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(0f, velocity.y, 0f);

        SetVisible(true);

        cM.controlMovement(true, true);

        StartSpeedBoost();
    }

    private void StartSpeedBoost()
    {
        if (speedBoostCoroutine != null)
        {
            StopCoroutine(speedBoostCoroutine);

            cM.maxGroundSpeed = originalGroundSpeed;
            cM.maxAirSpeed = originalAirSpeed;
        }

        speedBoostCoroutine = StartCoroutine(SpeedBoost());
    }

    private IEnumerator SpeedBoost()
    {
        originalGroundSpeed = cM.maxGroundSpeed;
        originalAirSpeed = cM.maxAirSpeed;

        cM.maxGroundSpeed += speedBoost;
        cM.maxAirSpeed += speedBoost;

        if (applySpeedBoostEffect && BuffsDebuffs.Instance != null)
        {
            BuffsDebuffs.Instance.ApplyEffect(
                BuffsDebuffs.EffectType.Buff,
                gameObject,
                speedBoostDuration,
                true
            );
        }

        yield return new WaitForSeconds(speedBoostDuration);

        cM.maxGroundSpeed = originalGroundSpeed;
        cM.maxAirSpeed = originalAirSpeed;

        speedBoostCoroutine = null;
    }

    private void SetVisible(bool visible)
    {
        if (renderers == null)
            return;

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    private void SpawnDashEffect()
    {
        if (dashEffectPrefab == null)
            return;

        GameObject effect = Instantiate(
            dashEffectPrefab,
            transform.position,
            transform.rotation
        );

        ParticleSystem[] particleSystems =
            effect.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem ps in particleSystems)
        {
            var main = ps.main;
            main.loop = false;

            main.stopAction = ParticleSystemStopAction.Destroy;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
        }

        Destroy(effect, 5f);
    }

    private void OnDisable()
    {
        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }

        if (speedBoostCoroutine != null)
        {
            StopCoroutine(speedBoostCoroutine);
            speedBoostCoroutine = null;
        }

        if (cM != null)
        {
            cM.controlMovement(true, true);
        }

        if (rb != null && isDashing)
        {
            Vector3 velocity = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, velocity.y, 0f);
        }

        isDashing = false;
        SetVisible(true);
    }
}