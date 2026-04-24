using UnityEngine;

public enum BirdType
{
    PENGUIN,
    CROW,
    SCISSORTAIL,
    LOVEBIRD,
    DODO,
    PELICAN,
    SEAGULL,
    OWL,
    KIWI,
    TOUCAN,
    PUKEKO,
    OSTRICH,
    CHICKEN,
    EAGLE,
    MACAW, 
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
    [Header("Testing")]
    [Tooltip("Mute background music for testing.")]
    public bool muteBackgroundMusic = false;

    [Header("Sounds")]
    [SerializeField] private AudioClip[] penguinSounds;
    [SerializeField] private AudioClip[] crowSounds;
    [SerializeField] private AudioClip[] scissortailSounds;
    [SerializeField] private AudioClip[] lovebirdSounds;
    [SerializeField] private AudioClip[] dodoSounds;
    [SerializeField] private AudioClip[] seagullSounds;
    [SerializeField] private AudioClip[] pelicanSounds;
    [SerializeField] private AudioClip[] toucanSounds;
    [SerializeField] private AudioClip[] pukekoSounds;
    [SerializeField] private AudioClip[] chickenSounds;
    [SerializeField] private AudioClip[] ostrichSounds;
    [SerializeField] private AudioClip[] eagleSounds;
    [SerializeField] private AudioClip[] kiwiSounds;
    [SerializeField] private AudioClip[] macawSounds;
    [SerializeField] private AudioClip[] owlSounds;

    [Header("Scoring Sounds")]
    [SerializeField] private AudioClip[] scoringSounds;

    [Header("Ball Sounds")]
    [SerializeField] private AudioClip[] ballPlayerInteractionSounds;
    [SerializeField] private AudioClip[] ballNetHitSounds;
    [SerializeField] private AudioClip[] ballGroundHitSounds;

    [Header("Ability Ready Sounds")]
    [Tooltip("Plays when Team 1's defensive ability cooldown is over.")]
    [SerializeField] private AudioClip team1DefensiveReadySound;
    [Tooltip("Plays when Team 1's offensive ability cooldown is over.")]
    [SerializeField] private AudioClip team1OffensiveReadySound;
    [Tooltip("Plays when Team 2's defensive ability cooldown is over.")]
    [SerializeField] private AudioClip team2DefensiveReadySound;
    [Tooltip("Plays when Team 2's offensive ability cooldown is over.")]
    [SerializeField] private AudioClip team2OffensiveReadySound;

    [Header("Buff / Debuff Sounds")]
    [Tooltip("Plays when a buff is applied.")]
    [SerializeField] private AudioClip buffStartSound;
    [Tooltip("Plays when a buff expires.")]
    [SerializeField] private AudioClip buffEndSound;
    [Tooltip("Plays when a debuff is applied.")]
    [SerializeField] private AudioClip debuffStartSound;
    [Tooltip("Plays when a debuff expires.")]
    [SerializeField] private AudioClip debuffEndSound;

    [Header("Background Music")]
    [SerializeField] private AudioClip[] backgroundTracks;

    [Header("Pause Music")]
    [SerializeField] private AudioClip pauseTrack;

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

    void Update()
    {
        // Mute/unmute background music based on inspector toggle
        if (backgroundAudioSource != null)
        {
            backgroundAudioSource.volume = muteBackgroundMusic ? 0f : 1.0f;
        }
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
            case BirdType.CROW:
                birdSounds = instance.crowSounds;
                break;
            case BirdType.SCISSORTAIL:
                birdSounds = instance.scissortailSounds;
                break;
            case BirdType.LOVEBIRD:
                birdSounds = instance.lovebirdSounds;
                break;
            case BirdType.DODO:
                birdSounds = instance.dodoSounds;
                break;
            case BirdType.SEAGULL:
                birdSounds = instance.seagullSounds;
                break;
            case BirdType.PELICAN:
                birdSounds = instance.pelicanSounds;
                break;
            case BirdType.TOUCAN:
                birdSounds = instance.toucanSounds;
                break;
            case BirdType.PUKEKO:
                birdSounds = instance.pukekoSounds;
                break;
            case BirdType.CHICKEN:
                birdSounds = instance.chickenSounds;
                break;
            case BirdType.OSTRICH:
                birdSounds = instance.ostrichSounds;
                break;
            case BirdType.OWL:
                birdSounds = instance.owlSounds;
                break;
            case BirdType.EAGLE:
                birdSounds = instance.eagleSounds;
                break;
            case BirdType.KIWI:
                birdSounds = instance.kiwiSounds;
                break;
            case BirdType.MACAW:
                birdSounds = instance.macawSounds;
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
        instance.backgroundAudioSource.volume = volume * 0.2f;
        instance.backgroundAudioSource.Play();
    }

    // Stops background track if needed
    public static void StopBackgroundTrack()
    {
        instance.backgroundAudioSource.Stop();
    }

    public static void PlayPauseTrack(float volume = 1.0f)
    {
        if (instance.pauseTrack != null)
        {
            instance.backgroundAudioSource.clip = instance.pauseTrack;
            instance.backgroundAudioSource.volume = volume * 0.2f;
            instance.backgroundAudioSource.Play();
        }
    }

    public static void PlayDefaultBackground()
    {
        if (instance.backgroundTracks != null && instance.backgroundTracks.Length > 0)
        {
            PlayBackgroundTrack(instance.backgroundTracks[0]);
        }
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

    // Overload: Play scoring sound by index + volume
    public static void PlayScoringSound(int soundIndex, float volume = 1.0f)
    {
        if (instance.scoringSounds != null && instance.scoringSounds.Length > 0)
        {
            soundIndex = Mathf.Clamp(soundIndex, 0, instance.scoringSounds.Length - 1);
            instance.audioSource.PlayOneShot(instance.scoringSounds[soundIndex], volume);
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


    // Play when Team 1's defensive ability cooldown is over
    public static void PlayTeam1DefensiveReadySound(float volume = 1.0f)
    {
        if (instance.team1DefensiveReadySound != null)
            instance.audioSource.PlayOneShot(instance.team1DefensiveReadySound, volume);
    }

    // Play when Team 1's offensive ability cooldown is over
    public static void PlayTeam1OffensiveReadySound(float volume = 1.0f)
    {
        if (instance.team1OffensiveReadySound != null)
            instance.audioSource.PlayOneShot(instance.team1OffensiveReadySound, volume);
    }

    // Play when Team 2's defensive ability cooldown is over
    public static void PlayTeam2DefensiveReadySound(float volume = 1.0f)
    {
        if (instance.team2DefensiveReadySound != null)
            instance.audioSource.PlayOneShot(instance.team2DefensiveReadySound, volume);
    }

    // Play when Team 2's offensive ability cooldown is over
    public static void PlayTeam2OffensiveReadySound(float volume = 1.0f)
    {
        if (instance.team2OffensiveReadySound != null)
            instance.audioSource.PlayOneShot(instance.team2OffensiveReadySound, volume);
    }


    // Play when a buff is applied
    public static void PlayBuffStartSound(float volume = 1.0f)
    {
        if (instance.buffStartSound != null)
            instance.audioSource.PlayOneShot(instance.buffStartSound, volume);
    }

    // Play when a buff expires
    public static void PlayBuffEndSound(float volume = 1.0f)
    {
        if (instance.buffEndSound != null)
            instance.audioSource.PlayOneShot(instance.buffEndSound, volume);
    }

    // Play when a debuff is applied
    public static void PlayDebuffStartSound(float volume = 1.0f)
    {
        if (instance.debuffStartSound != null)
            instance.audioSource.PlayOneShot(instance.debuffStartSound, volume);
    }

    // Play when a debuff expires
    public static void PlayDebuffEndSound(float volume = 1.0f)
    {
        if (instance.debuffEndSound != null)
            instance.audioSource.PlayOneShot(instance.debuffEndSound, volume);
    }
}