using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BallInteract))]
public class OstrichOffensive : BirdAbility
{
    [Header("Ostrich Offensive Settings")]
    [SerializeField] private float baseDuration = 30f;
    [SerializeField] private float durationExtensionOnTeamScore = 15f;
    [SerializeField] private float cooldownAfterDuration = 30f;
    [SerializeField] private float maxSpikePower = 10f;

    private BallInteract ballInteract;
    private bool abilityActive;
    private float activeTimeRemaining;
    private Coroutine activeCoroutine;

    private void Awake()
    {
        ballInteract = GetComponent<BallInteract>();
        _cooldownTime = 0f; // Delay the cooldown until after the active duration ends.
    }

    private void OnEnable()
    {
        EventManager.SubscribeScore(OnScore);
    }

    private void OnDisable()
    {
        EventManager.UnsubscribeScore(OnScore);
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
        abilityActive = false;
    }

    protected override bool Activate()
    {
        if (abilityActive) return false;

        abilityActive = true;
        activeTimeRemaining = baseDuration;
        SetAbilitiesDisabled(true);

        PlayActivationEffects();
        ApplyBuffEffect(activeTimeRemaining);

        activeCoroutine = StartCoroutine(RunAbilityDuration());
        return true;
    }

    private IEnumerator RunAbilityDuration()
    {
        while (abilityActive && activeTimeRemaining > 0f)
        {
            activeTimeRemaining -= Time.deltaTime;
            yield return null;
        }

        EndAbility();
    }

    private bool OnScore(bool leftScored)
    {
        if (!abilityActive) return false;

        bool isOwnTeam = ballInteract != null && ballInteract.onLeft == leftScored;
        IncreaseSpikePower();

        if (isOwnTeam)
        {
            activeTimeRemaining += durationExtensionOnTeamScore;
            ApplyBuffEffect(activeTimeRemaining);
        }

        return true;
    }

    private void PlayActivationEffects()
    {
        if (ballInteract != null && ballInteract.animator != null)
            ballInteract.animator.SetTrigger("OffensiveAbility");

        AudioManager.PlayBirdSound(BirdType.OSTRICH, SoundType.HAPPY, 1.0f);
    }

    private void ApplyBuffEffect(float duration)
    {
        if (BuffsDebuffs.Instance != null && ballInteract != null)
        {
            BuffsDebuffs.Instance.ApplyEffect(
                BuffsDebuffs.EffectType.Buff,
                gameObject,
                duration,
                ballInteract.onLeft
            );
            BuffsDebuffs.Instance.PreserveEffect(gameObject, BuffsDebuffs.EffectType.Buff);
        }
    }

    private void IncreaseSpikePower()
    {
        if (ballInteract == null) return;

        float currentPower = ballInteract.spikeStat;
        float nextPower = Mathf.Min(maxSpikePower, currentPower + 1f);

        if (nextPower > currentPower)
        {
            ballInteract.spikeStat = nextPower;
            AudioManager.PlayBirdSound(BirdType.OSTRICH, SoundType.HAPPY, 1.0f);
        }
    }

    private void EndAbility()
    {
        abilityActive = false;
        SetAbilitiesDisabled(false);
        activeCoroutine = null;

        if (BuffsDebuffs.Instance != null)
        {
            BuffsDebuffs.Instance.ReleaseEffect(gameObject, BuffsDebuffs.EffectType.Buff);
        }

        if (ballInteract != null)
        {
            int playerID = ballInteract.playerID;
            if (playerID >= 0 && HUDManager.Instance != null)
            {
                StartCooldown(cooldownAfterDuration);
                HUDManager.Instance.TriggerOffensiveCooldown(playerID, cooldownAfterDuration);
            }
            else
            {
                StartCooldown(cooldownAfterDuration);
            }
        }
        else
        {
            StartCooldown(cooldownAfterDuration);
        }
    }
}
