using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BallInteract))]
[RequireComponent(typeof(PlayerInput))]
public class ChickenOffensive : BirdAbility
{
    [Header("Scrambled Eggs Ability")]
    public GameObject eggSplashPrefab;   //Assign egg splat UI prefab
    public Canvas mainCanvas;            //Single canvas that covers whole screen
    public float displayTime = 4f;       //How long the splat stays
    public float fadeDuration = 0.35f;   //Fade/scale transition duration
    public float scaleFrom = 0f;         //Starting scale

    private BallInteract ballInteract;
    public Animator animator;            //Assign in inspector

    void Start()
    {
        ballInteract = GetComponent<BallInteract>();
        mainCanvas = GameObject.Find("Canvas").GetComponent<Canvas>();
    }

    protected override bool Activate()
    {
        if (!GameManager.PointInProgress())
            return false;

        if (eggSplashPrefab == null || mainCanvas == null)
        {
            Debug.LogWarning("ChickenOffensive missing prefab or opponent canvas!");
            return false;
        }

        //spawn the egg splat on the opponents side
        GameObject splash = Instantiate(eggSplashPrefab, mainCanvas.transform);
        RectTransform rt = splash.GetComponent<RectTransform>();

        //Decide which side
        bool onLeft = ballInteract.onLeft;
        float xMin = onLeft ? 100f : -300f;
        float xMax = onLeft ? 300f : -100f;
        rt.anchoredPosition = new Vector2(Random.Range(xMin, xMax), Random.Range(-150f, 150f));

        // Random rotation
        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, _cooldownTime);

        // Play animation
        if (animator != null)
            animator.SetTrigger("OffensiveAbility");

        // Play sound effect using AudioManager
        AudioManager.PlayBirdSound(BirdType.CHICKEN, SoundType.OFFENSIVE, 1.0f);

        StartCoroutine(AnimateSplash(splash, rt));

        return true;
    }

    private IEnumerator AnimateSplash(GameObject splash, RectTransform rt)
    {
        UnityEngine.UI.Graphic graphic = splash.GetComponent<UnityEngine.UI.Graphic>();

        if (graphic == null)
        {
            Debug.LogWarning("Egg splash prefab needs a UI Graphic component to fade.");
            Destroy(splash);
            yield break;
        }

        float startY = rt.anchoredPosition.y;

        Color originalColor = graphic.color;

        // Start tiny and invisible
        rt.localScale = Vector3.zero;

        Color startColor = originalColor;
        startColor.a = 0f;
        graphic.color = startColor;

        // Bounce in
        float elapsed = 0f;
        float bounceDuration = 0.45f;

        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);

            // Overshoot bounce
            float scale;

            if (t < 0.7f)
            {
                float bounceT = t / 0.7f;
                scale = Mathf.Lerp(0f, 1.15f, Mathf.SmoothStep(0f, 1f, bounceT));
            }
            else
            {
                float bounceT = (t - 0.7f) / 0.3f;
                scale = Mathf.Lerp(1.15f, 1f, Mathf.SmoothStep(0f, 1f, bounceT));
            }

            rt.localScale = Vector3.one * scale;

            Color color = originalColor;
            color.a = Mathf.Clamp01(t * 2f);
            graphic.color = color;

            yield return null;
        }

        rt.localScale = Vector3.one;
        graphic.color = originalColor;

        // Stay visible
        yield return new WaitForSeconds(displayTime);

        // Slowly slide downward and fade out
        elapsed = 0f;
        float slideDuration = 1.5f;
        float endY = startY - 150f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);

            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Slide downward
            rt.anchoredPosition = new Vector2(
                rt.anchoredPosition.x,
                Mathf.Lerp(startY, endY, smoothT)
            );

            // Fade the ACTUAL graphic
            Color color = originalColor;
            color.a = Mathf.Lerp(1f, 0f, smoothT);
            graphic.color = color;

            yield return null;
        }

        Destroy(splash);
    }
}