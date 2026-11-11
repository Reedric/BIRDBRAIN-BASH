using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

/// <summary>
/// Gets the connection details for playing through Unity Relay, which passes traffic between players
/// so that nobody has to set up port forwarding. Players sign in anonymously, so they don't need an account.
/// </summary>
public static class RelayConnection
{
    private const string connectionType = "dtls"; // UDP with encryption

    // Reserves space on a Relay server for a new match. maxClients doesn't include the host.
    public static async Task<(RelayServerData serverData, string joinCode)> HostAsync(int maxClients)
    {
        await SignInAsync();

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxClients);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        return (allocation.ToRelayServerData(connectionType), joinCode);
    }

    // Finds the Relay server of the match with this join code.
    public static async Task<RelayServerData> JoinAsync(string joinCode)
    {
        await SignInAsync();

        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        return allocation.ToRelayServerData(connectionType);
    }

    public static async Task SignInAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }
}
