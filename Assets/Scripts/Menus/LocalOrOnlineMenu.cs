using UnityEngine;

/// <summary>
///  Manages the local or online screen after pressing play on the main menu.
/// If local, go to num players menu. If online, go to host-join menu.
/// Pressing back goes to the main menu.
/// </summary>
public class LocalOrOnlineMenu : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject localOnlinePanel; // Panel for choosing local or online play
    [SerializeField] private GameObject mainMenuPanel; // Panel for main menu
    [SerializeField] private GameObject numPlayersPanel; // Panel for number of players to select
    [SerializeField] private GameObject hostJoinPanel; // Panel for hosting or joining for online play

    /// <summary>
    /// If player wants to go back from the local or online menu, send them back to the main menu.
    /// </summary>
    public void BackToMainMenu()
    {
        if (localOnlinePanel != null) localOnlinePanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    /// <summary>
    /// When the local button is pressed on the local or online menu, disable the local or online menu
    /// and enable the num players menu.
    /// </summary>
    public void LocalButton()
    {
        if (localOnlinePanel != null) localOnlinePanel.SetActive(false);
        if (numPlayersPanel != null) numPlayersPanel.SetActive(true);
    }

    /// <summary>
    /// When the online button is pressed on the local or online menu, disable the local online menu
    /// and enable the host or join menu.
    /// </summary>
    public void OnlineButton()
    {
        if (localOnlinePanel != null) localOnlinePanel.SetActive(false);
        if (hostJoinPanel != null) hostJoinPanel.SetActive(true);
    }
}
