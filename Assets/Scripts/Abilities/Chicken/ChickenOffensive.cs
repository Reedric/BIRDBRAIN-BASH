using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(BallInteract))]
[RequireComponent(typeof(PlayerInput))]
public class NewMonoBehaviourScript : MonoBehaviour
{
    [Header("Scrambled Eggs Ability")]

    public GameObject eggSplashPrefab;   //Assign egg splat UI prefab
    public Canvas mainCanvas;            //Single canvas that covers whole screen
    public Key keyToUse = Key.E;         //*will probably be changed*
    public float displayTime = 4f;       //How long the splat stays
    public float cooldown = 15f;         //Cooldown for ability
    [HideInInspector] private bool isAbilityReady = true;
    private BallInteract ballInteract;
    private PlayerInput playerInput;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ballInteract = GetComponent<BallInteract>();
        playerInput = GetComponent<PlayerInput>();
    }

    // Update is called once per frame
    void Update()
    {
         if (Keyboard.current[keyToUse].wasPressedThisFrame && isAbilityReady)
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
