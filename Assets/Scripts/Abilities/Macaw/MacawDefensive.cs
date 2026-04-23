using UnityEngine;

/// <summary>
/// Repeat After You - Will randomly mimic any ability from one of the birds on the court;
///  the defense ability icon will change to the icon of the mimicked bird
/// </summary>
public class MacawDefensive : BirdAbility
{
    [SerializeField] private float cooldown = 20f;
    private bool onCooldown = false;

    public void OnDefensiveAbility()
    {
        
    }

    private void RepeatAfterYou()
    {
        
    }
}
