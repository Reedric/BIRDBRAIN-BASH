using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The NetworkManager moves itself to DontDestroyOnLoad, and every load of the main menu scene creates
/// another one. This removes the extra copy before NetworkManager.Awake runs.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class NetworkManagerGuard : MonoBehaviour
{
    void Awake()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.gameObject != gameObject)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
