using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class MainMenu : MonoBehaviour
{
    [Header("Panels")]
    [Tooltip("The GameObject that holds the main menu buttons (Play, Quit, Credits, etc.)")]
    [SerializeField] private GameObject mainMenuPanel;

    [Tooltip("The GameObject that holds the NumPlayers screen.")]
    [SerializeField] private GameObject numPlayersPanel;

    [Header("Credits")]
    [SerializeField] private GameObject creditsCanvas;
    [SerializeField] private Animator creditsAnimator;
    [SerializeField] private float creditsDuration = 30f;

    [Header("Demo Mode")]
    [Tooltip("The gameplay scene to load directly when entering demo/screensaver mode. " +
             "Should be the actual match scene, not CharSelect or HowToPlay.")]
    [SerializeField] private string gameSceneName = "Game";

    private bool isShowingCredits = false;

    /// <summary>
    /// Opens the NumPlayers screen instead of jumping straight to CharSelect.
    /// </summary>
    public void PlayButton()
    {
        if (mainMenuPanel  != null) mainMenuPanel.SetActive(false);
        if (numPlayersPanel != null) numPlayersPanel.SetActive(true);
    }

    /// <summary>
    /// Starts demo/screensaver mode: skips CharSelect entirely and loads the game scene
    /// with no human players. MultiplayerManager detects the empty isKBMInput list and
    /// spawns 4 Hard-difficulty AI players. Player 1's gamepad can still open the pause menu.
    /// </summary>
    public void DemoButton()
    {
        // Empty list = 0 human players — MultiplayerManager uses this as the demo mode signal
        DataTransferManager.isKBMInput = new List<bool>();
        DataTransferManager.selectedBirds = new List<BirdType>();

        SceneManager.LoadScene(gameSceneName);
    }

    public void Quit()
    {
        Application.Quit();
    }

    public void CreditsButton()
    {
        if (isShowingCredits) return;
        StartCoroutine(PlayCredits());
    }

    private IEnumerator PlayCredits()
    {
        isShowingCredits = true;

        // Enable the credits canvas
        creditsCanvas.SetActive(true);

        // Start the animation
        if (creditsAnimator != null)
        {
            creditsAnimator.Play("ScrollCredits", 0, 0f);
        }

        // Wait for the credits to finish
        yield return new WaitForSeconds(creditsDuration);

        // Hide credits and return to menu state
        creditsCanvas.SetActive(false);

        isShowingCredits = false;
    }
}