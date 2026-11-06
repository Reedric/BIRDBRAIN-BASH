using System.Collections;
using TMPro;
using Unity.Netcode;
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

    // Online, the network time everyone's countdown starts at. Negative means this screen just uses its own timer
    private double sharedStartTime = -1;
    private GameObject waitingOverlay;
    private Animator cameraAnimator;

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

        // Done here rather than in Start, since the message to start the countdown could get here before Start runs
        if (!DataTransferManager.isLocal && targetImage != null)
        {
            CreateWaitingOverlay();
            HoldCameraAnimation();
        }
    }

    // Online, the camera's intro waits on its first frame until everyone is ready, then plays alongside the countdown.
    private void HoldCameraAnimation()
    {
        GameObject mainCamera = GameObject.FindWithTag("MainCamera");
        if (mainCamera != null && mainCamera.TryGetComponent(out cameraAnimator))
            cameraAnimator.speed = 0f;
    }

    // Greys out the screen so that it looks paused, with a message saying why. Built in code so that it doesn't
    // need adding to each of the game scenes. Shows until the countdown starts.
    private void CreateWaitingOverlay()
    {
        Transform parent = targetImage.transform.parent;
        TMP_Text fontSource = parent.GetComponentInChildren<TMP_Text>(true);

        waitingOverlay = new GameObject("Waiting For Players Overlay", typeof(RectTransform), typeof(Image));
        waitingOverlay.transform.SetParent(parent, false);

        Image overlayImage = waitingOverlay.GetComponent<Image>();
        overlayImage.color = new Color(0.2f, 0.2f, 0.2f, 0.55f);
        overlayImage.raycastTarget = false;

        RectTransform overlayRect = waitingOverlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;

        // Goes over the HUD, but under the pause menu so that it doesn't grey that out as well
        PauseMenu pauseMenu = FindFirstObjectByType<PauseMenu>();
        if (pauseMenu != null && pauseMenu.pauseMenuUI != null && pauseMenu.pauseMenuUI.transform.parent == parent)
            waitingOverlay.transform.SetSiblingIndex(pauseMenu.pauseMenuUI.transform.GetSiblingIndex());

        GameObject waitingText = TMP_DefaultControls.CreateText(new TMP_DefaultControls.Resources());
        waitingText.name = "Waiting For Players Text";
        waitingText.transform.SetParent(waitingOverlay.transform, false);

        RectTransform rect = waitingText.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1200f, 100f);

        TMP_Text text = waitingText.GetComponent<TMP_Text>();
        if (fontSource != null) text.font = fontSource.font;
        text.text = "Waiting for players...";
        text.fontSize = 64f;
        text.color = Color.white;
        text.outlineColor = Color.black;
        text.outlineWidth = 0.2f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
    }

    private void Start()
    {
        // Online, everyone starts together once the birds are spawned (see MultiplayerManager)
        if (playOnStart && DataTransferManager.isLocal)
            StartCountdown();
        else if (!DataTransferManager.isLocal && canvasGroup != null)
            canvasGroup.alpha = 0f; // Keep it hidden until then, since the image starts out showing a number
    }

    public void StartCountdown()
    {
        if (targetImage == null)
            return;

        StopAllCoroutines();
        targetImage.gameObject.SetActive(true);
        StartCoroutine(RunCountdown());
    }

    // The online version of StartCountdown. Every screen follows the network time instead of its own timer,
    // so the countdown stays lined up no matter when this message arrived or how the scene load went.
    public void StartCountdownAt(double networkTime)
    {
        sharedStartTime = networkTime;
        StartCountdown();
    }

    // How far through a stage an online countdown should be right now
    private float GetSharedStageTime(int stage)
    {
        return (float)(NetworkManager.Singleton.ServerTime.Time - sharedStartTime) - stage * stageDuration;
    }

    private IEnumerator RunCountdown()
    {
        bool isShared = sharedStartTime >= 0;

        if (isShared)
        {
            while (GetSharedStageTime(0) < 0f)
                yield return null;

            if (waitingOverlay != null) waitingOverlay.SetActive(false);
            if (cameraAnimator != null) cameraAnimator.speed = 1f;
        }
        else
        {
            // Skip a couple frames so Time.deltaTime settles after scene load,
            // then wait the initial delay before the first stage begins
            yield return null;
            yield return null;
            yield return new WaitForSeconds(initialDelay);
        }

        int stages = Mathf.Min(4, textures.Length);
        for (int i = 0; i < stages; i++)
        {
            // If a hitch on this screen made it miss a stage, skip it to catch up with everyone else.
            // GO always runs, since that's what starts the game music.
            if (isShared && i < stages - 1 && GetSharedStageTime(i) >= stageDuration)
                continue;

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
                    t = isShared ? GetSharedStageTime(i) : t + Time.deltaTime;
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