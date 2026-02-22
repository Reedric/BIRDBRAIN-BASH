using UnityEngine;

public enum BirdType
{
    PENGUIN,
    OTHER
}

public enum SoundType
{
    HAPPY,
    SAD,
    BUMP,
    SET,
    SPIKE,
    BLOCK,
    DEFENSIVE,
    OFFENSIVE
}

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    [Header("Sounds")]
    [SerializeField] private AudioClip[] penguinSounds;

    private static AudioManager instance;
    private AudioSource audioSource;

    void Awake()
    {
        // Assign instance
        instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Assign audio source
        audioSource = instance.GetComponent<AudioSource>();
    }

    public static void PlayBirdSound(BirdType birdType, SoundType soundType, float volume = 1.0f)
    {
        // Initialize bird sounds
        AudioClip[] birdSounds;

        // Decide which sounds to use
        switch (birdType)
        {
            case BirdType.PENGUIN:
                birdSounds = instance.penguinSounds;
                break;
            default:
                birdSounds = instance.penguinSounds;
                break;
        }

        // Play the desired sound
        instance.audioSource.PlayOneShot(birdSounds[(int)soundType], volume);
    }
}
