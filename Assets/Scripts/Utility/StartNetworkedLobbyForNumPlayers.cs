using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// One player in the online lobby.
/// </summary>
public struct LobbyPlayer : INetworkSerializable, IEquatable<LobbyPlayer>
{
    public ulong clientId;
    public BirdType bird;
    public bool ready;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref bird);
        serializer.SerializeValue(ref ready);
    }

    public bool Equals(LobbyPlayer other) => clientId == other.clientId && bird == other.bird && ready == other.ready;
}

/// <summary>
/// The players in an online game, in player number order. There's one of these in the main menu for the lobby,
/// and another in the character select scene, where a player's bird and ready values are filled in.
/// </summary>
public class StartNetworkedLobbyForNumPlayers : NetworkBehaviour
{
    private const string characterSelectSceneName = "CharSelect";

    [Range(1, 4)]
    public int numHumanPlayers = 4; // Number of networked players that can join the lobby

    [Tooltip("Start as soon as numHumanPlayers have joined, instead of waiting for the host to press start. Handy for test scenes.")]
    public bool autoStartWhenFull;

    public static StartNetworkedLobbyForNumPlayers Instance { get; private set; }

    public NetworkList<LobbyPlayer> Players { get; private set; }

    private bool matchLoading;

    void Awake()
    {
        Instance = this;
        Players = new NetworkList<LobbyPlayer>();
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.OnConnectionEvent += OnConnectionEvent;

        // The host's own connection has already happened by the time this object is spawned
        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            AddPlayer(clientId);

        TryAutoStart();
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager != null) NetworkManager.OnConnectionEvent -= OnConnectionEvent;
    }

    private void OnConnectionEvent(NetworkManager manager, ConnectionEventData eventData)
    {
        if (eventData.EventType == ConnectionEvent.ClientConnected)
        {
            AddPlayer(eventData.ClientId);
            TryAutoStart();
        }
        else if (eventData.EventType == ConnectionEvent.ClientDisconnected)
        {
            int index = IndexOf(eventData.ClientId);
            if (index >= 0) Players.RemoveAt(index);
        }
    }

    private void AddPlayer(ulong clientId)
    {
        if (IndexOf(clientId) >= 0) return;
        Players.Add(new LobbyPlayer { clientId = clientId, bird = BirdType.PENGUIN, ready = false });
    }

    private int IndexOf(ulong clientId)
    {
        for (int i = 0; i < Players.Count; ++i)
        {
            if (Players[i].clientId == clientId) return i;
        }
        return -1;
    }

    /// <summary>
    /// Takes everyone in the lobby to character select. New players are turned away from this point on.
    /// </summary>
    public void GoToCharacterSelect()
    {
        if (!IsServer || NetworkSession.MatchStarted) return;

        NetworkSession.MatchStarted = true;
        NetworkManager.SceneManager.LoadScene(characterSelectSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    // Sets the bird of whichever player sent this, which also makes them ready.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void PickBirdRpc(BirdType bird, RpcParams rpcParams = default)
    {
        int index = IndexOf(rpcParams.Receive.SenderClientId);
        if (index < 0) return;

        LobbyPlayer player = Players[index];
        player.bird = bird;
        player.ready = true;
        Players[index] = player;
    }

    /// <summary>
    /// Passes a player's character select cursor on to everyone else. Sent as a fraction of the screen, since
    /// players' screens can be different sizes. Sent unreliably and repeatedly, since only the latest one matters.
    /// </summary>
    [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Everyone)]
    public void SendCursorRpc(Vector2 screenFraction, RpcParams rpcParams = default)
    {
        ShowCursorRpc(rpcParams.Receive.SenderClientId, screenFraction);
    }

    [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable)]
    private void ShowCursorRpc(ulong clientId, Vector2 screenFraction)
    {
        CharacterSelectManager manager = CharacterSelectManager.Instance;
        int slot = IndexOf(clientId);
        if (manager != null && slot >= 0) manager.SetRemoteCursor(slot, screenFraction);
    }

    /// <summary>
    /// Any player can press the start button, the same as in a local game.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestStartMatchRpc()
    {
        if (CanStartMatch()) StartMatch();
    }

    public bool CanStartMatch()
    {
        if (Players.Count == 0) return false;

        foreach (LobbyPlayer player in Players)
        {
            if (!player.ready) return false;
        }
        return true;
    }

    private void TryAutoStart()
    {
        if (autoStartWhenFull && Players.Count == numHumanPlayers)
            StartMatch();
    }

    /// <summary>
    /// Starts the match on a random enabled map. Any empty slots are filled with AI by MultiplayerManager.
    /// </summary>
    public void StartMatch()
    {
        if (!IsServer || matchLoading) return;
        matchLoading = true;

        // Save the players for MultiplayerManager, since this object doesn't come along to the match scene
        List<LobbyPlayer> roster = new();
        foreach (LobbyPlayer player in Players)
            roster.Add(player);
        DataTransferManager.onlineRoster = roster;

        NetworkSession.MatchStarted = true;
        NetworkSession.MatchSceneLoaded = false;

        NetworkManager.SceneManager.OnLoadEventCompleted += OnMatchSceneLoaded;
        NetworkManager.SceneManager.LoadScene(PickMapScene(), UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private static void OnMatchSceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode,
        List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnMatchSceneLoaded;
        NetworkSession.MatchSceneLoaded = true;
    }

    private static string PickMapScene()
    {
        GameSettings settings = GameSettings.EnsureInstance();

        List<string> scenes = new();
        if (settings.BeachEnabled) scenes.Add("Game_Beach");
        if (settings.ParkEnabled) scenes.Add("Game_Forest");
        if (settings.IcebergEnabled) scenes.Add("Game_Iceberg");
        if (settings.VolcanoEnabled) scenes.Add("Game_Volcano");

        return scenes.Count > 0 ? scenes[UnityEngine.Random.Range(0, scenes.Count)] : "Game_Beach";
    }
}
