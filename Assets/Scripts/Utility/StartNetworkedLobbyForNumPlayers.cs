using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class StartNetworkedLobbyForNumPlayers : NetworkBehaviour
{
    [Range(1, 4)]
    public int numHumanPlayers; // Number of networked players that need to join before starting the lobby

    public StartNetworkedLobbyForNumPlayers Instance;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
    }

    /// <summary>
    /// Callback for when a client starts a session, checks if the client is the server and if so assigns the client callback.
    /// </summary>
    private void OnServerStarted()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        TryStartMatch();
    }

    /// <summary>
    /// Callback for when a player joins the lobby
    /// </summary>
    /// <param name="clientID">ID for the client that just joined</param>
    private void OnClientConnected(ulong clientID)
    {
        TryStartMatch();
    }

    /// <summary>
    /// Try to start the match when a client joins the lobby
    /// </summary>
    private void TryStartMatch()
    {
        // Get number of connected clients
        int connected = NetworkManager.Singleton.ConnectedClients.Count;

        // If number of connected clients equals the number of human players, start match
        if (connected == numHumanPlayers)
        {
            
            StartMatch();
        }
    }

    /// <summary>
    /// Starts the match when desired number of clients join the lobby
    /// </summary>
    private void StartMatch()
    {
        // Load the beach scene
        NetworkManager.Singleton.SceneManager.LoadScene("Game_Beach", UnityEngine.SceneManagement.LoadSceneMode.Single);

        // Remove callback
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }
}
