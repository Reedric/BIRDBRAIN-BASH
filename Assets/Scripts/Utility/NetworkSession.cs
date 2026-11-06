using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Small static helpers for code that needs to know whether it is running a local or online match.
/// HasAuthority is the check to use for gameplay logic: it is true for every local match,
/// and only true on the host in an online match.
/// </summary>
public static class NetworkSession
{
    private const string mainMenuSceneName = "MainMenu";

    // Set by the lobby so MultiplayerManager knows every client has finished loading the match scene
    public static bool MatchSceneLoaded;

    // Set by the lobby so that new connections are turned away once it has moved on to character select
    public static bool MatchStarted;

    // Shown on the online menu after being sent back there (e.g. the host left)
    public static string LastDisconnectMessage;

    // Whether this client got as far as connecting, which decides how a disconnect gets explained
    private static bool connectedToHost;

    public static bool IsOnline =>
        !DataTransferManager.isLocal && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    public static bool IsServer => IsOnline && NetworkManager.Singleton.IsServer;

    public static bool HasAuthority => DataTransferManager.isLocal || IsServer;

    // Looks up a spawned object by the id stored in a NetworkVariable. An id of 0 means "nothing".
    public static GameObject Resolve(ulong networkObjectId)
    {
        if (networkObjectId == 0 || NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null)
            return null;

        return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj)
            ? netObj.gameObject
            : null;
    }

    // Gets the id to store in a NetworkVariable for an object. Null or unspawned objects are stored as 0.
    public static ulong IdOf(GameObject obj)
    {
        return obj != null && obj.TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned
            ? netObj.NetworkObjectId
            : 0;
    }

    // Moves an object instantly. Online, this stops clients from interpolating across the court.
    public static void Teleport(GameObject obj, Vector3 position)
    {
        if (IsServer && obj.TryGetComponent(out Unity.Netcode.Components.NetworkTransform netTransform) && netTransform.IsSpawned)
            netTransform.Teleport(position, obj.transform.rotation, obj.transform.localScale);
        else
            obj.transform.position = position;
    }

    public static void OnClientConnected(ulong clientId)
    {
        connectedToHost = true;
    }

    // Callback for clients that sends them back to the menu when they lose their connection to the host.
    // This is static since it needs to work in every scene, and the handler goes away with the NetworkManager.
    public static void OnClientDisconnected(ulong clientId)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || manager.IsServer || (clientId != manager.LocalClientId && manager.IsConnectedClient)) return;

        // Reasons from the host (like the lobby being full) are written for players. Ones from the transport aren't.
        string reason = manager.DisconnectReason;
        if (string.IsNullOrEmpty(reason) || reason.StartsWith("[Disconnect Event]"))
            reason = connectedToHost ? "Lost connection to the host." : "Could not connect to the host. Is that match still open?";

        LeaveToMainMenu(reason);
    }

    // Callback for when the connection itself fails, like a Relay server dropping the match. NGO shuts down
    // right after this, which would otherwise leave the host sitting in a dead match.
    public static void OnTransportFailure()
    {
        LeaveToMainMenu("Lost connection to the online service.");
    }

    // Leaves the online session and goes back to the main menu. The NetworkManager is destroyed here
    // because it lives in DontDestroyOnLoad and the main menu scene brings its own.
    public static void LeaveToMainMenu(string message = null)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null)
        {
            // Shutting down fires these too, but leaving on purpose isn't losing the connection
            manager.OnClientDisconnectCallback -= OnClientDisconnected;
            manager.OnTransportFailure -= OnTransportFailure;

            if (manager.IsListening) manager.Shutdown();
            Object.Destroy(manager.gameObject);
        }

        LastDisconnectMessage = message;
        MatchSceneLoaded = false;
        MatchStarted = false;
        connectedToHost = false;
        DataTransferManager.isLocal = true;
        DataTransferManager.onlineRoster = null;
        Time.timeScale = 1f;

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
