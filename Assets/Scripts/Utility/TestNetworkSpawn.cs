using Unity.Netcode;
using UnityEngine;

public class TestNetworkSpawn : NetworkBehaviour
{
    [SerializeField] private NetworkObject testPrefab;

    public override void OnNetworkSpawn()
    {
        Debug.Log(IsServer);
        if (!IsServer)
            return;

        var instance = Instantiate(
            testPrefab,
            Vector3.zero,
            Quaternion.identity
        );

        instance.Spawn();
    }
}
