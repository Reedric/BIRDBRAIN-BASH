using UnityEngine;

[CreateAssetMenu(fileName = "New Bird", menuName = "Birds/Bird Data")]
public class BirdData : ScriptableObject
{
    public string birdName;
    public Texture icon;
    [TextArea]
    public string description;

    [TextArea]
    public string offensiveAbility;
    public Texture offensiveIcon;
    [TextArea]
    public string defensiveAbility;
    public Texture defensiveIcon;

    public int groundSpeed;
    public int jumpForce;
    public int strength;
}
