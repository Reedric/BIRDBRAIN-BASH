using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Repeat After You - Randomly copies a defensive ability from an ally or opponent on the court.
/// The first activation steals/locks in the currently previewed ability (swapping the HUD icon
/// to match its owner); the second activation actually uses the stolen ability.
/// </summary>
public class MacawDefensive : BirdAbility
{
    [SerializeField] private float mimicDuration = 15f;

    private List<BirdAbility> playerAbilities = new();
    private const int playerCount = 4;
    private BirdAbility currentAbility;
    private BirdAbility stolenAbilityInstance;
    private float mimicTimer;
    private bool isArmed;
    private int playerID = -1;

    void Awake()
    {
        AbilitySlot = AbilitySlot.Defensive;
    }

    void Start()
    {
        BallInteract ballInteract = GetComponent<BallInteract>();
        playerID = ballInteract != null ? ballInteract.playerID : GetComponent<AIBehavior>()?.playerID ?? -1;

        // get all the defensive abilities on the court (except Macaw's own) to mimic, from allies and enemies alike
        for (int i = 0; i < playerCount; i++)
        {
            GameObject player = i switch
            {
                0 => GameManager.Instance.leftPlayer1,
                1 => GameManager.Instance.leftPlayer2,
                2 => GameManager.Instance.rightPlayer1,
                3 => GameManager.Instance.rightPlayer2,
                _ => null
            };

            if (player == null || player == gameObject) continue;

            foreach (BirdAbility ability in player.GetComponents<BirdAbility>())
            {
                if (ability.AbilitySlot == AbilitySlot.Defensive)
                    playerAbilities.Add(ability);
            }
        }

        PrimeRandomAbility();
    }

    override protected bool Activate()
    {
        if (!GameManager.PointInProgress()) return false;
        if (currentAbility == null) return false;

        if (!isArmed)
        {
            // First press: lock in the currently previewed ability; nothing is actually used yet
            isArmed = true;
            mimicTimer = 0f;
            UpdateIconForCurrentAbility();
            return false;
        }

        // Second press: actually use the stolen ability, but run it on Macaw itself, not the original owner
        if (stolenAbilityInstance != null)
        {
            Destroy(stolenAbilityInstance);
            stolenAbilityInstance = null;
        }

        BirdAbility stolen = CreateStolenAbilityCopy(currentAbility);
        if (stolen == null) return false;

        if (!stolen.TryActivate(AbilitySlot.Defensive))
        {
            Destroy(stolen);
            return false;
        }

        // keep the copy alive so any in-flight coroutine it started (dash, buff, etc.) can finish
        stolenAbilityInstance = stolen;

        AudioManager.PlayBirdSound(BirdType.MACAW, SoundType.DEFENSIVE, 1.0f);

        if (playerID >= 0 && HUDManager.Instance != null)
            HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);

        isArmed = false;
        mimicTimer = 0f;
        ResetIconToMacaw();
        PrimeRandomAbility();

        return true;
    }

    // TODO: find better way to do this to not clog update
    void Update()
    {
        if (isArmed || playerAbilities.Count == 0) return;

        mimicTimer += Time.deltaTime;
        if (mimicTimer >= mimicDuration)
        {
            PrimeRandomAbility();
            mimicTimer = 0f;
        }
    }

    // called every mimicDuration so long as the ability hasn't been armed/stolen
    private void PrimeRandomAbility()
    {
        if (playerAbilities.Count == 0) return;

        currentAbility = playerAbilities[Random.Range(0, playerAbilities.Count)];
        if (!isArmed) UpdateIconForCurrentAbility();
    }

    private void UpdateIconForCurrentAbility()
    {
        if (currentAbility == null || playerID < 0 || HUDManager.Instance == null) return;

        BirdType sourceBird = GetBirdType(currentAbility.gameObject);
        HUDManager.Instance.SetDefensiveIcon(playerID, HUDManager.Instance.GetDefensiveIconForBird(sourceBird));
    }

    // Attaches a fresh copy of the stolen ability's type directly onto Macaw so its effect
    // (movement, buffs, VFX, GetComponent<> lookups, etc.) applies to Macaw, not the original owner.
    private BirdAbility CreateStolenAbilityCopy(BirdAbility source)
    {
        if (source == null) return null;

        System.Type type = source.GetType();
        BirdAbility copy = gameObject.AddComponent(type) as BirdAbility;
        if (copy == null) return null;

        // copy only the ability's own tuned fields (e.g. dash speed, buff amount) — leave BirdAbility's
        // cooldown/runtime state untouched so the copy starts fresh and ready to use
        for (System.Type walk = type; walk != null && walk != typeof(BirdAbility); walk = walk.BaseType)
        {
            foreach (FieldInfo field in walk.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                field.SetValue(copy, field.GetValue(source));
        }

        SetBaseField(copy, "_cooldownTime", GetBaseField(source, "_cooldownTime"));
        copy.AbilitySlot = AbilitySlot.Defensive;

        // re-run Unity's init callbacks so cached component refs (Rigidbody, CharacterMovement, etc.)
        // re-resolve against Macaw instead of pointing at the original owner
        InvokeLifecycleMethod(copy, type, "Awake");
        InvokeLifecycleMethod(copy, type, "Start");

        return copy;
    }

    private static object GetBaseField(object target, string fieldName)
    {
        for (System.Type t = target.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo field = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field.GetValue(target);
        }
        return null;
    }

    private static void SetBaseField(object target, string fieldName, object value)
    {
        for (System.Type t = target.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo field = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) { field.SetValue(target, value); return; }
        }
    }

    private static void InvokeLifecycleMethod(object target, System.Type type, string methodName)
    {
        for (System.Type t = type; t != null && t != typeof(BirdAbility); t = t.BaseType)
        {
            MethodInfo method = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method != null) { method.Invoke(target, null); return; }
        }
    }

    private void ResetIconToMacaw()
    {
        if (playerID >= 0 && HUDManager.Instance != null)
            HUDManager.Instance.RefreshPlayerCard(playerID);
    }

    private static BirdType GetBirdType(GameObject bird)
    {
        BallInteract ballInteract = bird.GetComponent<BallInteract>();
        if (ballInteract != null) return ballInteract.GetBirdType();

        AIBehavior aiBehavior = bird.GetComponent<AIBehavior>();
        return aiBehavior != null ? aiBehavior.GetBirdType() : BirdType.MACAW;
    }
}

