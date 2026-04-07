using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// predicts the trajectory of the ball visualized by a line.
/// </summary>
public class OwlDefensive : BirdAbility
{
    [SerializeField] private float cooldown = 25f;
    private bool onCooldown = false;
    [SerializeField] private float lineDuration;

    public void OnDefensiveAbility(InputAction.CallbackContext context)
    {
        
    }
}
