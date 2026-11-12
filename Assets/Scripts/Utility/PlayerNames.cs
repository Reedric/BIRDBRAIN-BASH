#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
#define STEAM_AVAILABLE
#endif

using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;
#if STEAM_AVAILABLE
using Steamworks;
#endif

/// <summary>
/// The name this player goes by in online matches. That's their Steam name when Steam is running, or else the name
/// Unity Authentication generates for their account, for testing in the editor or in builds that aren't on Steam.
/// Local matches keep using "Player 1" and so on.
///
/// Steam only starts up when the game is launched through Steam, or when a steam_appid.txt file with the game's
/// App ID is next to it (in the project folder, for the editor).
/// </summary>
public static class PlayerNames
{
    // Steam names go up to 32 characters. A little under that keeps any name within what LobbyPlayer can hold.
    public const int MaxLength = 30;

    private static string localName;
    private static Task<string> lookup;

    // Statics survive between play sessions in the editor when domain reloading is turned off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        localName = null;
        lookup = null;
#if STEAM_AVAILABLE
        steamTried = false;
        steamRunning = false;
#endif
    }

    // This player's own name, or an empty string if there isn't one to be had. It's looked up once and then
    // remembered for the rest of the session.
    public static Task<string> GetLocalNameAsync()
    {
        if (!string.IsNullOrEmpty(localName)) return Task.FromResult(localName);
        return lookup ??= LookUpAsync();
    }

    // The name to show for a player, with "Player 2" and so on for a player whose name isn't known (yet)
    public static string Display(string name, int playerIndex)
    {
        return string.IsNullOrWhiteSpace(name) ? $"Player {playerIndex + 1}" : name;
    }

    // Trims a name down to what can be sent and shown
    public static string Clean(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        name = name.Trim();
        return name.Length > MaxLength ? name.Substring(0, MaxLength) : name;
    }

    private static async Task<string> LookUpAsync()
    {
        string name = Clean(SteamName());
        if (name.Length == 0) name = Clean(await UnityName());

        localName = name;

        // Tries again next time if neither worked (like being offline for a moment), rather than giving up for good
        if (name.Length == 0) lookup = null;
        return name;
    }

    private static async Task<string> UnityName()
    {
        try
        {
            await RelayConnection.SignInAsync();
            string name = await AuthenticationService.Instance.GetPlayerNameAsync();

            // Generated names end with a number, like "SunnyPenguin#1234"
            int hash = name != null ? name.LastIndexOf('#') : -1;
            return hash > 0 ? name.Substring(0, hash) : name;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerNames] Couldn't get a name from Unity Authentication: {e.Message}");
            return null;
        }
    }

#if STEAM_AVAILABLE
    private static bool steamTried;
    private static bool steamRunning;

    private static string SteamName()
    {
        if (!steamTried)
        {
            steamTried = true;
            steamRunning = StartSteam();
        }

        return steamRunning ? SteamFriends.GetPersonaName() : null;
    }

    private static bool StartSteam()
    {
        try
        {
            if (!SteamAPI.Init())
            {
                Debug.Log("[PlayerNames] Steam isn't running for this game, so Unity's generated names get used instead.");
                return false;
            }
        }
        catch (Exception e)
        {
            // The Steam library itself can be missing, like on a platform it doesn't support
            Debug.LogWarning($"[PlayerNames] Couldn't start Steam: {e.Message}");
            return false;
        }

        GameObject runner = new("Steam");
        UnityEngine.Object.DontDestroyOnLoad(runner);
        runner.AddComponent<SteamRunner>();
        return true;
    }
#else
    private static string SteamName() => null;
#endif
}

#if STEAM_AVAILABLE
// Keeps Steam running while it's in use, the way Steamworks expects
public class SteamRunner : MonoBehaviour
{
    private void Update() => SteamAPI.RunCallbacks();

    private void OnApplicationQuit() => SteamAPI.Shutdown();
}
#endif
