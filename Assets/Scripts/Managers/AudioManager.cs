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
    PHOENIX,
    ROBOPIGEON,
    HUMMINGBIRD,
    SHIMAENAGA,
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

// One labeled multi-clip slot per SoundType. Each bird gets exactly one of these.
// Unity auto-labels each field by name in the Inspector (Happy, Sad, Bump, Set,
// Spike, Block, Defensive, Offensive), and each is a normal AudioClip[] so it can
// hold 1 or many clips - a random one is chosen whenever that sound plays.
[System.Serializable]
public class BirdSoundSet
{
    public AudioClip[] happy;
    public AudioClip[] sad;
    public AudioClip[] bump;
    public AudioClip[] set;
    public AudioClip[] spike;
    public AudioClip[] block;
    public AudioClip[] defensive;
    public AudioClip[] offensive;

    // Returns the clip pool for the requested SoundType.
    public AudioClip[] GetClips(SoundType soundType)
    {
        switch (soundType)
        {
            case SoundType.HAPPY: return happy;
            case SoundType.SAD: return sad;
            case SoundType.BUMP: return bump;
            case SoundType.SET: return set;
            case SoundType.SPIKE: return spike;
            case SoundType.BLOCK: return block;
            case SoundType.DEFENSIVE: return defensive;
            case SoundType.OFFENSIVE: return offensive;
            default: return null;
        }
    }

    // Picks a random clip from the requested SoundType's pool.
    // Works fine with a pool of exactly 1 clip too (Random.Range(0, 1) always returns 0).
    public AudioClip GetRandomClip(SoundType soundType)
    {
        AudioClip[] options = GetClips(soundType);
        if (options == null || options.Length == 0)
            return null;

        return options[Random.Range(0, options.Length)];
    }
}

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    [Header("Testing")]
    [Tooltip("Mute background music for testing.")]
    public bool muteBackgroundMusic = false;

    [Header("Bird Sounds")]
    [Tooltip("Each bird has one labeled slot per action (Happy, Sad, Bump, Set, Spike, Block, Defensive, Offensive). Drop 1 or more clips into any slot - if there's more than one, a random one is chosen each time that sound plays.")]
    [SerializeField] private BirdSoundSet penguinSounds;
    [SerializeField] private BirdSoundSet crowSounds;
    [SerializeField] private BirdSoundSet scissortailSounds;
    [SerializeField] private BirdSoundSet lovebirdSounds;
    [SerializeField] private BirdSoundSet dodoSounds;
    [SerializeField] private BirdSoundSet seagullSounds;
    [SerializeField] private BirdSoundSet pelicanSounds;
    [SerializeField] private BirdSoundSet toucanSounds;
    [SerializeField] private BirdSoundSet pukekoSounds;
    [SerializeField] private BirdSoundSet chickenSounds;
    [SerializeField] private BirdSoundSet ostrichSounds;
    [SerializeField] private BirdSoundSet eagleSounds;
    [SerializeField] private BirdSoundSet kiwiSounds;
    [SerializeField] private BirdSoundSet macawSounds;
    [SerializeField] private BirdSoundSet owlSounds;
    [SerializeField] private BirdSoundSet phoenixSounds;
    [SerializeField] private BirdSoundSet robopigeonSounds;
    [SerializeField] private BirdSoundSet hummingbirdSounds;
    [SerializeField] private BirdSoundSet shimaenagaSounds;

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
    
    [Header("Game Music (plays after countdown)")]
    [SerializeField] private AudioClip[] gameMusicTracks;

    [Header("Intro / Countdown SFX")]
    [Tooltip("Slots for countdown SFX: index 0 = '3', 1 = '2', 2 = '1', 3 = 'GO'")]
    [SerializeField] private AudioClip[] countdownSfx = new AudioClip[4];

    [Header("Behavior")]
    [Tooltip("When true the background music will play automatically in Awake. Set false to delay until the intro/GO event.")]
    [SerializeField] private bool playBackgroundOnAwake = true;
    [Tooltip("Set to true for game scenes. When true, background music will NOT play on awake (only after countdown finishes). When false, background music plays normally on menus.")]
    [SerializeField] private bool isGameScene = false;

    [Header("Pause Music")]
    [SerializeField] private AudioClip pauseTrack;

    private static AudioManager instance;
    private AudioSource audioSource;
    private AudioSource backgroundAudioSource;
    void Awake()
    {
        // Assign instance
        instance = this;
        // assign audio source (required for PlayOneShot calls)
        audioSource = instance.GetComponent<AudioSource>();
        // Create background audio source
        backgroundAudioSource = instance.gameObject.AddComponent<AudioSource>();
        backgroundAudioSource.loop = true;
        // Only play on awake if explicitly allowed
        // For menus: play background music
        // For game scenes: don't play anything (game music plays after countdown)
        if (playBackgroundOnAwake && backgroundTracks != null && backgroundTracks.Length > 0)
        {
            if (!isGameScene)
            {
                PlayBackgroundTrack(backgroundTracks[0]);
            }
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // audioSource already assigned in Awake
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
        // Decide which bird's sound set to use
        BirdSoundSet soundSet;
        switch (birdType)
        {
            case BirdType.PENGUIN:
                soundSet = instance.penguinSounds;
                break;
            case BirdType.CROW:
                soundSet = instance.crowSounds;
                break;
            case BirdType.SCISSORTAIL:
                soundSet = instance.scissortailSounds;
                break;
            case BirdType.LOVEBIRD:
                soundSet = instance.lovebirdSounds;
                break;
            case BirdType.DODO:
                soundSet = instance.dodoSounds;
                break;
            case BirdType.SEAGULL:
                soundSet = instance.seagullSounds;
                break;
            case BirdType.PELICAN:
                soundSet = instance.pelicanSounds;
                break;
            case BirdType.TOUCAN:
                soundSet = instance.toucanSounds;
                break;
            case BirdType.PUKEKO:
                soundSet = instance.pukekoSounds;
                break;
            case BirdType.CHICKEN:
                soundSet = instance.chickenSounds;
                break;
            case BirdType.OSTRICH:
                soundSet = instance.ostrichSounds;
                break;
            case BirdType.OWL:
                soundSet = instance.owlSounds;
                break;
            case BirdType.EAGLE:
                soundSet = instance.eagleSounds;
                break;
            case BirdType.KIWI:
                soundSet = instance.kiwiSounds;
                break;
            case BirdType.MACAW:
                soundSet = instance.macawSounds;
                break;
            case BirdType.PHOENIX:
                soundSet = instance.phoenixSounds;
                break;
            case BirdType.ROBOPIGEON:
                soundSet = instance.robopigeonSounds;
                break;
            case BirdType.HUMMINGBIRD:
                soundSet = instance.hummingbirdSounds;
                break;
            case BirdType.SHIMAENAGA:
                soundSet = instance.shimaenagaSounds;
                break;
            default:
                soundSet = instance.penguinSounds;
                break;
        }

        if (soundSet == null)
            return;

        AudioClip clip = soundSet.GetRandomClip(soundType);
        if (clip != null)
        {
            instance.audioSource.PlayOneShot(clip, volume);
        }
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

    // Play game-specific music (called after countdown finishes)
    public static void PlayGameMusic()
    {
        if (instance.gameMusicTracks != null && instance.gameMusicTracks.Length > 0)
        {
            PlayBackgroundTrack(instance.gameMusicTracks[0]);
        }
        else if (instance.backgroundTracks != null && instance.backgroundTracks.Length > 0)
        {
            // Fallback to regular background if no game music defined
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

    // Play a countdown SFX by index (0..3)
    public static void PlayCountdownSfx(int index, float volume = 1.0f)
    {
        if (instance == null || instance.countdownSfx == null || index < 0 || index >= instance.countdownSfx.Length)
            return;

        AudioClip clip = instance.countdownSfx[index];
        if (clip != null && instance.audioSource != null)
            instance.audioSource.PlayOneShot(clip, volume);
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

    // Set whether this is a game scene (stops background music on awake if true)
    public static void SetIsGameScene(bool value)
    {
        if (instance != null)
            instance.isGameScene = value;
    }
}