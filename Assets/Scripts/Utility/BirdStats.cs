using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One change an ability makes to a bird's movement stats for as long as it is active.
/// </summary>
public struct StatModifier
{
    public float speedAdd;        // Added to both ground and air speed
    public float airSpeedAdd;     // Added to air speed only
    public float jumpAdd;         // Added to jump force
    public float speedMultiplier; // Scales ground and air speed. 0 means "not set" and counts as 1
    public bool stun;             // Drops every movement stat to the minimum

    public static StatModifier Speed(float add) => new StatModifier { speedAdd = add };
    public static StatModifier AirSpeed(float add) => new StatModifier { airSpeedAdd = add };
    public static StatModifier Multiply(float multiplier) => new StatModifier { speedMultiplier = multiplier };
    public static StatModifier Stunned => new StatModifier { stun = true };
}

/// <summary>
/// The stat changes currently on one bird. If abilities remembered a bird's speed and put it back when they
/// wore off, it would go wrong whenever two overlapped (a stun ending during a slow puts the slow back for good,
/// a boost ending during a stun cuts the stun short, and so on). Here every ability only adds or removes its own
/// change, and the bird's stats are always worked out again from the ones it spawned with.
/// </summary>
public class StatModifierStack
{
    private readonly Dictionary<object, StatModifier> modifiers = new Dictionary<object, StatModifier>();

    public int Count => modifiers.Count;

    public void Set(object key, StatModifier modifier)
    {
        if (key != null) modifiers[key] = modifier;
    }

    public bool Remove(object key)
    {
        return key != null && modifiers.Remove(key);
    }

    public void Clear()
    {
        modifiers.Clear();
    }

    public void Evaluate(float baseGround, float baseAir, float baseJump, out float ground, out float air, out float jump)
    {
        float speedAdd = 0f;
        float airAdd = 0f;
        float jumpAdd = 0f;
        float multiplier = 1f;
        bool stunned = false;

        foreach (StatModifier modifier in modifiers.Values)
        {
            speedAdd += modifier.speedAdd;
            airAdd += modifier.airSpeedAdd;
            jumpAdd += modifier.jumpAdd;
            if (modifier.speedMultiplier > 0f) multiplier *= modifier.speedMultiplier;
            stunned |= modifier.stun;
        }

        // Slows can't take a stat below 1 (or below where it started, for a bird that is slower than that anyway)
        float groundFloor = Mathf.Min(baseGround, 1f);
        float airFloor = Mathf.Min(baseAir, 1f);
        float jumpFloor = Mathf.Min(baseJump, 1f);

        if (stunned)
        {
            ground = groundFloor;
            air = airFloor;
            jump = jumpFloor;
            return;
        }

        ground = Mathf.Max(groundFloor, (baseGround + speedAdd) * multiplier);
        air = Mathf.Max(airFloor, (baseAir + speedAdd + airAdd) * multiplier);
        jump = Mathf.Max(jumpFloor, baseJump + jumpAdd);
    }
}

/// <summary>
/// Finds the bird a collider belongs to. Checking the "Player" tag misses birds whose prefabs don't have it
/// (and colliders on a bird's child objects), so those birds wouldn't slip on fish, be silenced by Pukeko,
/// or count as touching the ball.
/// </summary>
public static class BirdLookup
{
    // The bird (anything that can play the ball: one of the four players, Phoenix's chickens, the mini 31RD), or null
    public static GameObject FindBird(GameObject touched)
    {
        if (touched == null) return null;

        // Dodo's companions are built from bird prefabs, but they're obstacles rather than birds
        if (touched.GetComponentInParent<DodoCompanion>() != null) return null;

        BallInteract ballInteract = touched.GetComponentInParent<BallInteract>();
        if (ballInteract != null) return ballInteract.gameObject;

        AIBehavior ai = touched.GetComponentInParent<AIBehavior>();
        return ai != null ? ai.gameObject : null;
    }

    public static BirdType GetBirdType(GameObject bird)
    {
        if (bird == null) return BirdType.SEAGULL;
        if (bird.TryGetComponent(out BallInteract ballInteract)) return ballInteract.GetBirdType();
        return bird.TryGetComponent(out AIBehavior ai) ? ai.GetBirdType() : BirdType.SEAGULL;
    }
}

/// <summary>
/// Adds and removes stat changes on a bird, whichever of CharacterMovement and AIBehavior it moves with.
/// The key says whose change it is, so the same ability setting it twice replaces it instead of stacking.
/// </summary>
public static class BirdStats
{
    public static void Apply(GameObject bird, object key, StatModifier modifier)
    {
        if (bird == null) return;

        if (bird.TryGetComponent(out CharacterMovement movement)) movement.SetStatModifier(key, modifier);
        if (bird.TryGetComponent(out AIBehavior ai)) ai.SetStatModifier(key, modifier);
    }

    public static void Remove(GameObject bird, object key)
    {
        if (bird == null) return;

        if (bird.TryGetComponent(out CharacterMovement movement)) movement.RemoveStatModifier(key);
        if (bird.TryGetComponent(out AIBehavior ai)) ai.RemoveStatModifier(key);
    }
}
