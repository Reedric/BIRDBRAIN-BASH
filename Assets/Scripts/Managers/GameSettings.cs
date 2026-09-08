using UnityEngine;

// Persistent singleton that holds match settings between scenes.
public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance;

    // ============================================================
    // MATCH SETTINGS
    // ============================================================

    public int PointsPerSet = 25;

    // Number of sets in the match.
    // Match Settings defaults this to 3.
    public int BestOfSets = 3;

    public int FinalSetPoints = 15;

    // ============================================================
    // BOT DIFFICULTY
    // ============================================================

    public enum BotDifficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2
    }

    public BotDifficulty CurrentBotDifficulty = BotDifficulty.Hard;

    // ============================================================
    // TEAM ARRANGEMENT
    // ============================================================

    public enum TeamArrangement
    {
        P1P2_vs_P3P4 = 0,
        P1P3_vs_P2P4 = 1,
        P1P4_vs_P2P3 = 2
    }

    // Defaults to classic 1&2 vs 3&4
    public TeamArrangement CurrentTeamArrangement = TeamArrangement.P1P2_vs_P3P4;

    // ============================================================
    // MAP ROTATION
    // ============================================================

    // These are the maps currently allowed to appear in the
    // match rotation.
    //
    // Match Settings defaults all four to true.
    public bool BeachEnabled = true;
    public bool ParkEnabled = true;
    public bool IcebergEnabled = true;
    public bool VolcanoEnabled = true;

    // ============================================================
    // UNITY
    // ============================================================

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

    // ============================================================
    // SINGLETON
    // ============================================================

    public static GameSettings EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject go = new GameObject("GameSettings");

        Instance = go.AddComponent<GameSettings>();

        DontDestroyOnLoad(go);

        return Instance;
    }

    // ============================================================
    // MAP ROTATION
    // ============================================================

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

        // Absolute safety net.
        // There must always be at least one playable map.
        if (!BeachEnabled &&
            !ParkEnabled &&
            !IcebergEnabled &&
            !VolcanoEnabled)
        {
            BeachEnabled = true;
        }
    }
}