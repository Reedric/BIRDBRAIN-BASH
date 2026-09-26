using System;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

public class ConnectUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button backButton;

    [Header("Panels")]
    [SerializeField] private GameObject hostJoinPanel; // Panel for hosting or joining for online play
    [SerializeField] private GameObject localOnlinePanel; // Panel for choosing local or online play

    private void Start()
    {
        hostButton.onClick.AddListener(HostButtonOnClick);
        clientButton.onClick.AddListener(ClientButtonOnClick);
        backButton.onClick.AddListener(BackToLocalOnlineMenu);
    }

    private void HostButtonOnClick()
    {
        if (hostJoinPanel != null) hostJoinPanel.SetActive(false); // REMOVE, THIS IS HERE FOR Network SCENE
        NetworkManager.Singleton.StartHost();
    }

    private void ClientButtonOnClick()
    {
        if (hostJoinPanel != null) hostJoinPanel.SetActive(false); // REMOVE, THIS IS HERE FOR Network SCENE
        NetworkManager.Singleton.StartClient();
    }

    /// <summary>
    /// Callback that switches the screen from Host-Join menu to Local Online menu
    /// </summary>
    private void BackToLocalOnlineMenu()
    {
        if (hostJoinPanel != null) hostJoinPanel.SetActive(false);
        if (localOnlinePanel != null) localOnlinePanel.SetActive(true);
    }
}