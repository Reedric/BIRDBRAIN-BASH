using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the "how many players?" overlay that sits between the main menu and
/// the character select screen.
/// </summary>
public class NumPlayersMenu : MonoBehaviour
{
    [Header("Panels")]
    [Tooltip("The GameObject that holds this number-select screen.")]
    [SerializeField] private GameObject numPlayersPanel;

    [Tooltip("The GameObject that holds the main menu buttons (Play, Quit, Credits, etc.)")]
    [SerializeField] private GameObject mainMenuPanel;

    [Header("Scene")]
    [SerializeField] private string characterSelectSceneName = "CharSelect";

    // Button callbacks

    public void SelectOnePlayers()  => SelectPlayers(1);
    public void SelectTwoPlayers()  => SelectPlayers(2);
    public void SelectThreePlayers() => SelectPlayers(3);
    public void SelectFourPlayers() => SelectPlayers(4);

    /// <summary>
    /// Called by the Back button. Hides this panel and re-enables the main menu.
    /// </summary>
    public void GoBack()
    {
        if (numPlayersPanel != null)  numPlayersPanel.SetActive(false);
        if (mainMenuPanel  != null)   mainMenuPanel.SetActive(true);
    }

    /// <summary>
    /// Stores the chosen player count in DataTransferManager, then loads CharSelect.
    /// All players default to gamepad (isKBM = false); CharacterSelectManager will
    /// assign actual devices in SetupPlayerInputStates().
    /// </summary>
    private void SelectPlayers(int count)
    {
        // Populate isKBMInput with 'count' gamepad entries
        DataTransferManager.isKBMInput = new List<bool>();
        for (int i = 0; i < count; i++)
            DataTransferManager.isKBMInput.Add(false); // all gamepad

        // Clear any stale bird selections from a previous run
        DataTransferManager.selectedBirds = new List<BirdType>();

        SceneManager.LoadScene(characterSelectSceneName);
    }
}