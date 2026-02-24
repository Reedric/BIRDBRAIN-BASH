using UnityEngine;

public enum BirdType
{
    PENGUIN,
    SCISSORTAIL,
    LOVEBIRD,
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
    [SerializeField] private AudioClip[] scissortailSounds;
    [SerializeField] private AudioClip[] lovebirdSounds;

    [Header("Scoring Sounds")]
    [SerializeField] private AudioClip[] scoringSounds;

    [Header("Ball Sounds")]
    [SerializeField] private AudioClip[] ballPlayerInteractionSounds;
    [SerializeField] private AudioClip[] ballNetHitSounds;
    [SerializeField] private AudioClip[] ballGroundHitSounds;

    [Header("Background Music")]
    [SerializeField] private AudioClip[] backgroundTracks;

    private static AudioManager instance;
    private AudioSource audioSource;
    private AudioSource backgroundAudioSource;

    void Awake()
    {
        // Assign instance
        instance = this;
        // Create background audio source
        backgroundAudioSource = instance.gameObject.AddComponent<AudioSource>();
        backgroundAudioSource.loop = true;
        PlayBackgroundTrack(backgroundTracks[0]);
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
            case BirdType.SCISSORTAIL:
                birdSounds = instance.scissortailSounds;
                break;
            case BirdType.LOVEBIRD:
                birdSounds = instance.lovebirdSounds;
                break;
            default:
                birdSounds = instance.penguinSounds;
                break;
        }

        // Play the desired sound
        instance.audioSource.PlayOneShot(birdSounds[(int)soundType], volume);
    }

    // For playing the background track
    public static void PlayBackgroundTrack(AudioClip audioClip, float volume = 1.0f)
    {

        instance.backgroundAudioSource.clip = audioClip;
        instance.backgroundAudioSource.volume = volume;
        instance.backgroundAudioSource.Play();
    }

    // stops background track if needed
    public static void StopBackgroundTrack()
    {
        instance.backgroundAudioSource.Stop();
    }

    // Play a scoring sound when a point is scored
    public static void PlayScoringSound(float volume = 1.0f)
    {
        if (instance.scoringSounds != null && instance.scoringSounds.Length > 0)
        {
            int randomIndex = Random.Range(0, instance.scoringSounds.Length);
            instance.audioSource.PlayOneShot(instance.scoringSounds[randomIndex], volume);
        }
    }

    // Play a sound when the ball interacts with a player
    public static void PlayBallPlayerInteractionSound(float volume = 1.0f)
    {
        if (instance.ballPlayerInteractionSounds != null && instance.ballPlayerInteractionSounds.Length > 0)
        {
            int randomIndex = Random.Range(0, instance.ballPlayerInteractionSounds.Length);
            instance.audioSource.PlayOneShot(instance.ballPlayerInteractionSounds[randomIndex], volume);
        }
    }

    // Play a sound when the ball hits the net
    public static void PlayBallNetHitSound(float volume = 1.0f)
    {
        if (instance.ballNetHitSounds != null && instance.ballNetHitSounds.Length > 0)
        {
            int randomIndex = Random.Range(0, instance.ballNetHitSounds.Length);
            instance.audioSource.PlayOneShot(instance.ballNetHitSounds[randomIndex], volume);
        }
    }

    // Play a sound when the ball hits the ground
    public static void PlayBallGroundHitSound(float volume = 1.0f)
    {
        if (instance.ballGroundHitSounds != null && instance.ballGroundHitSounds.Length > 0)
        {
            int randomIndex = Random.Range(0, instance.ballGroundHitSounds.Length);
            instance.audioSource.PlayOneShot(instance.ballGroundHitSounds[randomIndex], volume);
        }
    }
}
