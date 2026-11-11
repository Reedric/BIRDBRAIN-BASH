using System.Collections.Generic;
using UnityEngine;

// Persistent singleton that holds match settings between scenes.
public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance;

    // Match settings

    public int PointsPerSet = 25;

    // Number of sets in the match.
    // Match Settings defaults this to 3.
    public int BestOfSets = 3;

    public int FinalSetPoints = 15;

    // Bot difficulty

    public enum BotDifficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2
    }

    public BotDifficulty CurrentBotDifficulty = BotDifficulty.Hard;

    // Team arrangement

    public enum TeamArrangement
    {
        P1P2_vs_P3P4 = 0,
        P1P3_vs_P2P4 = 1,
        P1P4_vs_P2P3 = 2
    }

    // Defaults to classic 1&2 vs 3&4
    public TeamArrangement CurrentTeamArrangement = TeamArrangement.P1P2_vs_P3P4;

    // Map rotation

    // These are the maps currently allowed to appear in the
    // match rotation.
    //
    // Match Settings defaults all four to true.
    public bool BeachEnabled = true;
    public bool ParkEnabled = true;
    public bool IcebergEnabled = true;
    public bool VolcanoEnabled = true;

    // Tutorial

    private const string showTutorialKey = "Settings.ShowTutorial";

    // Whether the tutorial plays before a local match. It's on until someone turns it off in Match Settings, and
    // that gets remembered between sessions, so players who've already learned the game don't keep seeing it.
    public bool ShowTutorial
    {
        get => PlayerPrefs.GetInt(showTutorialKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(showTutorialKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    // Unity

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Singleton

    public static GameSettings EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject go = new GameObject("GameSettings");

        Instance = go.AddComponent<GameSettings>();

        DontDestroyOnLoad(go);

        return Instance;
    }

    // Map rotation

    /// <summary>
    /// Saves which maps are enabled in the match rotation.
    /// At least one map is always guaranteed to remain enabled.
    /// </summary>
    public void SetMapRotation(
        bool beach,
        bool park,
        bool iceberg,
        bool volcano)
    {
        BeachEnabled = beach;
        ParkEnabled = park;
        IcebergEnabled = iceberg;
        VolcanoEnabled = volcano;

        // There always has to be at least one playable map.
        if (!BeachEnabled &&
            !ParkEnabled &&
            !IcebergEnabled &&
            !VolcanoEnabled)
        {
            BeachEnabled = true;
        }
    }

    // The scene of a random map out of the ones in the rotation
    public string PickMapScene()
    {
        List<string> scenes = new();
        if (BeachEnabled) scenes.Add("Game_Beach");
        if (ParkEnabled) scenes.Add("Game_Forest");
        if (IcebergEnabled) scenes.Add("Game_Iceberg");
        if (VolcanoEnabled) scenes.Add("Game_Volcano");

        return scenes.Count > 0 ? scenes[Random.Range(0, scenes.Count)] : "Game_Beach";
    }
}