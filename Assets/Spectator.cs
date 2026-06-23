using System;
using UnityEngine;

public class Spectator : MonoBehaviour
{
    [Header("Ball Tracking")]
    [Tooltip("Optional reference to the ball. If left empty, the script uses BallManager.Instance.")]
    [SerializeField] private Transform ballTransform;
    [Tooltip("Add a yaw offset if the prefab faces sideways by default.")]
    [SerializeField] private float lookYawOffset = 0f;

    [Header("Idle Bob")]
    [SerializeField] private float idleAmplitude = 0.14f;
    [SerializeField] private Vector2 idleFrequencyRange = new Vector2(0.8f, 1.4f);

    [Header("Score Reaction")]
    [SerializeField] private float scoreReactionDuration = 1.0f;
    [SerializeField] private float scoreAmplitudeMultiplier = 2.8f;
    [SerializeField] private float scoreFrequencyMultiplier = 2.5f;
    [SerializeField] private float scoreXAmplitude = 0.16f;

    [Header("Appearance")]
    [Tooltip("Renderers (parts) whose material will be randomized per-instance.")]
    [SerializeField] private Renderer[] colorParts;
    [Tooltip("Material options to pick from for each duck instance.")]
    [SerializeField] private Material[] colorOptions;
    [Tooltip("If true, all parts listed will be set to the same randomly chosen material; otherwise each part gets a random choice.")]
    [SerializeField] private bool applySameMaterialToAllParts = true;

    private Vector3 defaultLocalPosition;
    private float baseIdleFrequency;
    private float phaseOffset;
    private float bobPhase;

    private float scoreTimer;
    private float currentScoreAmplitude;
    private float currentScoreFrequency;
    private float currentScoreXAmplitude;

    private bool hasSubscribed;

    private void Awake()
    {
        defaultLocalPosition = transform.localPosition;

        baseIdleFrequency = UnityEngine.Random.Range(
            idleFrequencyRange.x,
            idleFrequencyRange.y);

        phaseOffset = UnityEngine.Random.value * Mathf.PI * 2f;

        currentScoreAmplitude = idleAmplitude;
        currentScoreFrequency = baseIdleFrequency;

        // Randomize appearance per-instance so each prefab instance looks unique.
        RandomizeColors();
    }

    private void RandomizeColors()
    {
        if (colorOptions == null || colorOptions.Length == 0) return;
        if (colorParts == null || colorParts.Length == 0) return;

        if (applySameMaterialToAllParts)
        {
            var mat = colorOptions[UnityEngine.Random.Range(0, colorOptions.Length)];
            foreach (var r in colorParts)
            {
                if (r == null) continue;
                r.material = mat; // use instance material
            }
        }
        else
        {
            for (int i = 0; i < colorParts.Length; i++)
            {
                var r = colorParts[i];
                if (r == null) continue;
                var mat = colorOptions[UnityEngine.Random.Range(0, colorOptions.Length)];
                r.material = mat;
            }
        }
    }

    private void Start()
    {
        if (ballTransform == null && BallManager.Instance != null)
        {
            ballTransform = BallManager.Instance.transform;
        }
    }

    private void OnEnable()
    {
        if (!hasSubscribed)
        {
            EventManager.SubscribeScore(OnScore);
            hasSubscribed = true;
        }
    }

    private void OnDisable()
    {
        if (hasSubscribed)
        {
            EventManager.UnsubscribeScore(OnScore);
            hasSubscribed = false;
        }
    }

    private bool OnScore(bool leftScored)
    {
        scoreTimer = scoreReactionDuration;

        currentScoreAmplitude = idleAmplitude * scoreAmplitudeMultiplier;
        currentScoreFrequency = baseIdleFrequency * scoreFrequencyMultiplier;
        currentScoreXAmplitude = scoreXAmplitude;

        return true;
    }

    private void Update()
    {
        if (ballTransform == null)
        {
            if (BallManager.Instance != null)
            {
                ballTransform = BallManager.Instance.transform;
            }
            else
            {
                return;
            }
        }

        UpdateLookAtBall();
        UpdateBobbing();
    }

    private void UpdateLookAtBall()
    {
        Vector3 direction = ballTransform.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion lookRotation = Quaternion.LookRotation(direction);
        float targetYaw = lookRotation.eulerAngles.y + lookYawOffset;

        transform.rotation = Quaternion.Euler(
            transform.rotation.eulerAngles.x,
            targetYaw,
            transform.rotation.eulerAngles.z);
    }

    private void UpdateBobbing()
    {
        float amplitude = idleAmplitude;
        float frequency = baseIdleFrequency;
        float xWobble = 0f;

        if (scoreTimer > 0f)
        {
            float t = scoreTimer / scoreReactionDuration;

            amplitude = Mathf.Lerp(
                idleAmplitude,
                currentScoreAmplitude,
                t);

            frequency = Mathf.Lerp(
                baseIdleFrequency,
                currentScoreFrequency,
                t);

            xWobble = currentScoreXAmplitude * t *
                      Mathf.Sin((bobPhase + phaseOffset) * 1.2f);

            scoreTimer -= Time.deltaTime;

            if (scoreTimer < 0f)
                scoreTimer = 0f;
        }

        // Advance phase using the current frequency.
        // This prevents frequency compounding.
        bobPhase += frequency * Time.deltaTime;

        float yOffset = amplitude * Mathf.Sin(bobPhase + phaseOffset);

        transform.localPosition =
            defaultLocalPosition +
            new Vector3(xWobble, yOffset, 0f);
    }
}