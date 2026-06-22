using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class countdown : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RawImage targetImage;

    [Header("Countdown Textures (3,2,1,GO)")]
    [SerializeField] private Texture[] textures = new Texture[4];

    [Header("Animation")]
    [SerializeField] private float stageDuration = 1f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Optional Animator (use triggers)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string[] animatorTriggers = new string[4];

    [Header("Behavior")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool disableOnGO = true;
    [SerializeField] private float initialDelay = 1f; // buffer before first stage starts

    private CanvasGroup canvasGroup;
    private bool isCountdownComplete = false;

    public bool IsCountdownComplete => isCountdownComplete;

    private void Awake()
    {
        if (targetImage == null)
            Debug.LogError("countdown: Target RawImage is not assigned.");

        // Ensure we have a CanvasGroup for fading
        if (targetImage != null)
        {
            canvasGroup = targetImage.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = targetImage.gameObject.AddComponent<CanvasGroup>();
        }

        // Ensure curves have usable defaults
        if (scaleCurve == null || scaleCurve.keys.Length == 0)
            scaleCurve = AnimationCurve.EaseInOut(0, 0.2f, 1, 1);
        if (alphaCurve == null || alphaCurve.keys.Length == 0)
            alphaCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    }

    private void Start()
    {
        if (playOnStart)
            StartCountdown();
    }

    public void StartCountdown()
    {
        if (targetImage == null)
            return;

        StopAllCoroutines();
        targetImage.gameObject.SetActive(true);
        StartCoroutine(RunCountdown());
    }

    private IEnumerator RunCountdown()
    {
        // Skip a couple frames so Time.deltaTime settles after scene load,
        // then wait the initial delay before the first stage begins
        yield return null;
        yield return null;
        yield return new WaitForSeconds(initialDelay);

        int stages = Mathf.Min(4, textures.Length);
        for (int i = 0; i < stages; i++)
        {
            // Play countdown sfx for this stage (0=3, 1=2, 2=1, 3=GO)
            AudioManager.PlayCountdownSfx(i);

            // If this is the final 'GO' stage, start game music now
            if (i == stages - 1)
                AudioManager.PlayGameMusic();

            // Swap texture and use its native pixel size to avoid stretching
            targetImage.texture = textures[i];
            targetImage.SetNativeSize();

            // Reset transform + alpha for built-in animation
            targetImage.rectTransform.localScale = Vector3.one * 0.2f;
            if (canvasGroup != null) canvasGroup.alpha = 0f;

            // If animator + trigger is provided, use it
            if (animator != null && animatorTriggers != null && animatorTriggers.Length > i && !string.IsNullOrEmpty(animatorTriggers[i]))
            {
                animator.SetTrigger(animatorTriggers[i]);
                yield return new WaitForSeconds(stageDuration);
            }
            else
            {
                // Built-in simple scale+fade animation using curves
                float t = 0f;
                while (t < stageDuration)
                {
                    t += Time.deltaTime;
                    float p = Mathf.Clamp01(t / stageDuration);
                    float s = scaleCurve.Evaluate(p);
                    float a = alphaCurve.Evaluate(p);
                    targetImage.rectTransform.localScale = Vector3.one * s;
                    if (canvasGroup != null) canvasGroup.alpha = a;
                    yield return null;
                }
            }
        }

        // After GO stage finishes
        isCountdownComplete = true;
        if (disableOnGO)
            targetImage.gameObject.SetActive(false);
        else
            enabled = false;
    }
}