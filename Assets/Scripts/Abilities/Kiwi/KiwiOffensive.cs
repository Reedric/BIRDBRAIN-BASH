using UnityEngine;

/// <summary>
/// Fire the Lazar - Kiwi fires a laser beam from its eyes to hit the ball, 
/// which automatically counts as the next action required for the ball in the rally. 
/// If spiking or blocking, increases the ball’s speed.
/// </summary>
public class KiwiOffensive : BirdAbility
{
    [SerializeField] private float cooldown = 15f;

    // Positions for the laser to originate from (could be empty GameObjects placed at the eyes in the Unity editor)
    [SerializeField] private Transform leftEyePosition;
    [SerializeField] private Transform rightEyePosition;



    private void FireTheLazar()
    {
        
    }
}
