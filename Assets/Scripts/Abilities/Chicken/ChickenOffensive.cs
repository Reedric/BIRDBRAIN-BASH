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
    public float cooldown = 15f;         //Cooldown for ability
    private bool isAbilityReady = true;
    private BallInteract ballInteract;
    private PlayerInput playerInput;
    public Animator animator; // Assign in inspector


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ballInteract = GetComponent<BallInteract>();
        playerInput = GetComponent<PlayerInput>();
        mainCanvas = GameObject.Find("Canvas").GetComponent<Canvas>();
    }

    // Update is called once per frame
    void Update()
    {
        if (playerInput.actions.FindAction("Offensive Ability").WasCompletedThisFrame() && isAbilityReady
            && CanUseAbilities() && PointInProgress())
        {
            ActivateAbility();
        }
    }

    private void ActivateAbility()
    {
        if (eggSplashPrefab == null || mainCanvas == null)
        {
            Debug.LogWarning("ChickenOffensive missing prefab or opponent canvas!");
            return;
        }

        //spawn the egg splat on the opponents side
        GameObject splash = Instantiate(eggSplashPrefab, mainCanvas.transform);
        RectTransform rt = splash.GetComponent<RectTransform>();

        //Decide which side
        bool onLeft = ballInteract.onLeft;
        float xMin = onLeft ? 100f : -300f; //example positions idk yet
        float xMax = onLeft ? 300f : -100f;
        rt.anchoredPosition = new Vector2(Random.Range(xMin, xMax), Random.Range(-150f, 150f));

        // Random rotation
        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerOffensiveCooldown(playerID, cooldown);

        // Play animation
        if (animator != null)
            animator.SetTrigger("OffensiveAbility"); // Make sure to have a trigger

        // Play sound effect using AudioManager
        AudioManager.PlayBirdSound(BirdType.CHICKEN, SoundType.OFFENSIVE, 1.0f);

        //Destroy splash after displayTime
        Destroy(splash, displayTime);

        //Start cooldown
        StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        isAbilityReady = false; 
        yield return new WaitForSeconds(cooldown);
        isAbilityReady = true;
    }
}
