using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

/// <summary>
/// Keeps the host's match listed publicly for as long as it's in the online lobby screen. This goes away with the
/// main menu scene, so moving on to character select or leaving both take the match off the list.
/// If the game closes before it can do that, the listing disappears once it stops getting heartbeats.
/// </summary>
public class PublicLobbyHost : MonoBehaviour
{
    private const float heartbeatInterval = 15f; // The Lobby service hides a listing after about 30 seconds without one

    private string lobbyId;
    private int maxPlayers;
    private int listedPlayerCount = 1;
    private bool isUpdatingListing;
    private float nextHeartbeatTime;

    // Lists the match. This returns false if that wasn't possible, which still leaves the join code working.
    public async Task<bool> OpenAsync(string matchName, string joinCode, int playerLimit)
    {
        maxPlayers = playerLimit;

        try
        {
            await RelayConnection.SignInAsync();

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(matchName, maxPlayers, new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { PublicLobby.JoinCodeKey, new DataObject(DataObject.VisibilityOptions.Public, joinCode) },
                    { PublicLobby.PlayerCountKey, new DataObject(DataObject.VisibilityOptions.Public, listedPlayerCount.ToString()) }
                }
            });

            // The player could have already left while waiting for that
            if (this == null)
            {
                DeleteListing(lobby.Id);
                return false;
            }

            lobbyId = lobby.Id;
            nextHeartbeatTime = Time.unscaledTime + heartbeatInterval;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not list this match publicly: {e}");
            return false;
        }
    }

    void Update()
    {
        if (lobbyId == null) return;

        if (Time.unscaledTime >= nextHeartbeatTime)
        {
            nextHeartbeatTime = Time.unscaledTime + heartbeatInterval;
            SendHeartbeat();
        }

        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsServer && !isUpdatingListing && manager.ConnectedClientsIds.Count != listedPlayerCount)
            UpdateListing(manager.ConnectedClientsIds.Count);
    }

    void OnDestroy()
    {
        if (lobbyId != null) DeleteListing(lobbyId);
    }

    private async void SendHeartbeat()
    {
        try
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not send a heartbeat for the public match: {e}");
        }
    }

    // A full match gets locked to take it off the list, and comes back when someone leaves. If this fails, the
    // count still won't match the next time Update checks it, so it just tries again.
    private async void UpdateListing(int playerCount)
    {
        isUpdatingListing = true;

        try
        {
            await LobbyService.Instance.UpdateLobbyAsync(lobbyId, new UpdateLobbyOptions
            {
                IsLocked = playerCount >= maxPlayers,
                Data = new Dictionary<string, DataObject>
                {
                    { PublicLobby.PlayerCountKey, new DataObject(DataObject.VisibilityOptions.Public, playerCount.ToString()) }
                }
            });

            listedPlayerCount = playerCount;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not update the public match: {e}");
        }

        isUpdatingListing = false;
    }

    private static async void DeleteListing(string id)
    {
        try
        {
            await LobbyService.Instance.DeleteLobbyAsync(id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not remove the public match: {e}");
        }
    }
}
