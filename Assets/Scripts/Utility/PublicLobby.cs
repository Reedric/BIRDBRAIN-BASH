using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

/// <summary>
/// Finds public matches through Unity Lobby, which is only used as a list of matches to choose from.
/// Everyone still connects through Relay with a join code, which each listing has, so nobody joins the Lobby itself.
/// That's why a listing keeps its own player count, as the Lobby's would always just be the host.
/// </summary>
public static class PublicLobby
{
    public const string JoinCodeKey = "joinCode";
    public const string PlayerCountKey = "playerCount";

    // Controllers can't type, so hosts get given a name for their match
    private static readonly string[] nameWords = { "Sunny", "Breezy", "Sandy", "Salty", "Speedy", "Mighty", "Sneaky", "Fluffy", "Bouncy", "Spiky", "Squawky", "Noisy", "Pretty", "Tricky", "Sleepy", "Flappy", "Happy", "Lively", "Bubbly", "Merry", "Lovely", "Quirky" };
    private static readonly string[] nameBirds = { "Penguin", "Crow", "Dodo", "Pelican", "Seagull", "Owl", "Toucan", "Kiwi", "Chicken", "Ostrich", "Eagle", "Macaw", "Phoenix", "Hummingbird", "Lovebird" };

    public struct Match
    {
        public string name;
        public string joinCode;
        public int playerCount;
        public int maxPlayers;
    }

    public static string CreateMatchName()
    {
        return $"{nameWords[Random.Range(0, nameWords.Length)]} {nameBirds[Random.Range(0, nameBirds.Length)]}";
    }

    // Gets the newest public matches. The Lobby service already leaves out any that are locked,
    // which is what the host does when their match is full.
    public static async Task<List<Match>> QueryAsync(int count)
    {
        await RelayConnection.SignInAsync();

        QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
        {
            Count = count,
            Order = new List<QueryOrder> { new(false, QueryOrder.FieldOptions.Created) }
        });

        List<Match> matches = new();
        foreach (Lobby lobby in response.Results)
        {
            if (lobby.Data == null || !lobby.Data.TryGetValue(JoinCodeKey, out DataObject joinCode)) continue;

            int playerCount = 1;
            if (lobby.Data.TryGetValue(PlayerCountKey, out DataObject countData)) int.TryParse(countData.Value, out playerCount);

            matches.Add(new Match
            {
                name = lobby.Name,
                joinCode = joinCode.Value,
                playerCount = playerCount,
                maxPlayers = lobby.MaxPlayers
            });
        }
        return matches;
    }
}
