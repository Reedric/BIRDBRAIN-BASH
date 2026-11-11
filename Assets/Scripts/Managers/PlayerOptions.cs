using UnityEngine;

/// <summary>
/// The settings from the options screen, which are saved between sessions.
/// Fullscreen and resolution aren't here since Unity already remembers those itself.
/// </summary>
public static class PlayerOptions
{
    private const string masterVolumeKey = "Options.MasterVolume";
    private const string musicVolumeKey = "Options.MusicVolume";
    private const string sfxVolumeKey = "Options.SfxVolume";
    private const string vSyncKey = "Options.VSync";

    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 1f;
    public static float SfxVolume { get; private set; } = 1f;
    public static bool VSync { get; private set; }

    // Before the first scene loads, so that its AudioManager never plays anything at the wrong volume
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        SetMasterVolume(PlayerPrefs.GetFloat(masterVolumeKey, 1f));
        SetMusicVolume(PlayerPrefs.GetFloat(musicVolumeKey, 1f));
        SetSfxVolume(PlayerPrefs.GetFloat(sfxVolumeKey, 1f));
        SetVSync(PlayerPrefs.GetInt(vSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1);
    }

    public static void SetMasterVolume(float volume)
    {
        MasterVolume = CleanVolume(volume);
        AudioListener.volume = MasterVolume;
        PlayerPrefs.SetFloat(masterVolumeKey, MasterVolume);
    }

    // AudioManager applies these two, since that's what owns the audio sources
    public static void SetMusicVolume(float volume)
    {
        MusicVolume = CleanVolume(volume);
        PlayerPrefs.SetFloat(musicVolumeKey, MusicVolume);
    }

    public static void SetSfxVolume(float volume)
    {
        SfxVolume = CleanVolume(volume);
        PlayerPrefs.SetFloat(sfxVolumeKey, SfxVolume);
    }

    public static void SetVSync(bool enabled)
    {
        VSync = enabled;
        QualitySettings.vSyncCount = enabled ? 1 : 0;
        PlayerPrefs.SetInt(vSyncKey, enabled ? 1 : 0);
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    // Rounded so that repeatedly adding a step doesn't drift away from a whole percentage
    private static float CleanVolume(float volume)
    {
        return Mathf.Clamp01(Mathf.Round(volume * 100f) / 100f);
    }
}
