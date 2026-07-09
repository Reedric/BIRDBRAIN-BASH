using UnityEngine;

// Simple persistent singleton to hold match settings between scenes.
public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance;

    public int PointsPerSet = 25;
    public int BestOfSets = 3;
    public int FinalSetPoints = 15;

    public enum BotDifficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2,
        Impossible = 3
    }

    public BotDifficulty CurrentBotDifficulty = BotDifficulty.Easy;

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

    public static GameSettings EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("GameSettings");
        Instance = go.AddComponent<GameSettings>();
        DontDestroyOnLoad(go);
        return Instance;
    }
}
