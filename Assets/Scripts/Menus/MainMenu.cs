using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MainMenu : MonoBehaviour
{
    [Header("Panels")]
    [Tooltip("The GameObject that holds the main menu buttons (Play, Quit, Credits, etc.)")]
    [SerializeField] private GameObject mainMenuPanel;

    [Tooltip("The GameObject that holds the online or local buttons.")]
    [SerializeField] private GameObject localOrOnlinePanel;

    [Header("Credits")]
    [SerializeField] private GameObject creditsCanvas;
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private float creditsDuration = 30f;
    [Tooltip("Space between each section of the credits")]
    [SerializeField] private float creditsSectionSpacing = 200f;

    [Header("Demo Mode")]
    [Tooltip("The gameplay scene to load directly when entering demo/screensaver mode. " +
             "Should be the actual match scene, not CharSelect or HowToPlay.")]
    [SerializeField] private string gameSceneName = "Game";

    [Header("Aviary")]
    [SerializeField] private string aviarySceneName = "Aviary";

    // The credits screen once it's showing, so the menu cursor knows to target its back button
    public static GameObject ActiveCreditsPanel { get; private set; }

    // For the menus that get built in code to match the rest
    public TMP_FontAsset MenuFont => creditsText.font;

    private bool isShowingCredits = false;
    private Coroutine creditsCoroutine;
    private RectTransform creditsRow;
    private float creditsRowWidth;
    private OptionsMenuUI optionsMenu;

    void Start()
    {
        Transform optionsButton = mainMenuPanel != null ? mainMenuPanel.transform.Find("Options") : null;
        if (optionsButton != null) optionsButton.GetComponent<Button>().onClick.AddListener(OptionsButton);

        // Moved out of the main menu panel so that it stays visible and keeps swaying behind the credits and options.
        // The other menus all have their own background that covers it.
        Transform background = mainMenuPanel != null ? mainMenuPanel.transform.Find("Background") : null;
        if (background != null)
        {
            background.SetParent(transform, false);
            background.SetAsFirstSibling();
            creditsCanvas.GetComponent<RawImage>().enabled = false;
        }

        // If an online session ended on its own (like the host leaving), go back to the online menu to say why
        if (string.IsNullOrEmpty(NetworkSession.LastDisconnectMessage)) return;

        ConnectUI connectUI = FindFirstObjectByType<ConnectUI>(FindObjectsInactive.Include);
        if (connectUI == null) return;

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        connectUI.gameObject.SetActive(true);
    }

    /// <summary>
    /// Opens the NumPlayers screen instead of jumping straight to CharSelect.
    /// </summary>
    public void PlayButton()
    {
        if (mainMenuPanel  != null) mainMenuPanel.SetActive(false);
        if (localOrOnlinePanel != null)
        {
            localOrOnlinePanel.SetActive(true);
            StartCoroutine(SelectFirstButtonNextFrame(localOrOnlinePanel)); // prevent bleed-through
        }
    }

    /// <summary>
    /// Clears the EventSystem selection for one frame, then re-selects the first
    /// button on the new panel. This prevents the "A" press that opened this panel
    /// from instantly firing a button here too.
    /// </summary>
    private IEnumerator SelectFirstButtonNextFrame(GameObject panel)
    {
        // Deselect everything so the current A-press doesn't carry over
        EventSystem.current.SetSelectedGameObject(null);

        // Wait one frame for the button-press event to fully clear
        yield return null;

        // Now safely select the first interactable button on the new panel
        Button firstButton = panel.GetComponentInChildren<Button>();
        if (firstButton != null)
            EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
    }

    /// <summary>
    /// Starts demo/screensaver mode: skips CharSelect entirely and loads the game scene
    /// with no human players. MultiplayerManager detects the empty isKBMInput list and
    /// spawns 4 Hard-difficulty AI players. Player 1's gamepad can still open the pause menu.
    /// </summary>
    public void DemoButton()
    {
        DataTransferManager.isLocal = true;

        // Empty list = 0 human players, which MultiplayerManager takes as the demo mode signal
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
        isShowingCredits = true;

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        creditsCanvas.SetActive(true);
        ActiveCreditsPanel = creditsCanvas;

        StartCoroutine(SelectFirstButtonNextFrame(creditsCanvas)); // prevent bleed-through
        creditsCoroutine = StartCoroutine(PlayCredits());
    }

    // Goes back to the main menu. Used by the back button, and when the credits reach the end.
    // A single press of A can trigger this twice (menu cursor + EventSystem), hence the check.
    public void CloseCredits()
    {
        if (!isShowingCredits) return;
        isShowingCredits = false;

        StopCoroutine(creditsCoroutine);
        creditsCanvas.SetActive(false);
        ActiveCreditsPanel = null;

        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
            StartCoroutine(SelectFirstButtonNextFrame(mainMenuPanel)); // prevent bleed-through
        }
    }

    public void OptionsButton()
    {
        // Same double-press problem as CloseCredits
        if (OptionsMenuUI.ActivePanel != null) return;

        if (optionsMenu == null)
        {
            optionsMenu = gameObject.AddComponent<OptionsMenuUI>();
            // Whichever controller is being used, the same as MainMenuCursor
            optionsMenu.Build(GetComponentInParent<Canvas>(), creditsText.font,
                () => LocalPlayerDevices.LastUsedGamepad ?? (Gamepad.all.Count > 0 ? Gamepad.all[0] : null), false, CloseOptions);
        }

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        optionsMenu.Show();
        StartCoroutine(SelectFirstButtonNextFrame(OptionsMenuUI.ActivePanel)); // prevent bleed-through
    }

    private void CloseOptions()
    {
        optionsMenu.Hide();

        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
            StartCoroutine(SelectFirstButtonNextFrame(mainMenuPanel)); // prevent bleed-through
        }
    }

    public void AviaryButton()
    {
        SceneManager.LoadScene(aviarySceneName);
    }

    // Scrolls the sections from left to right, starting off the left of the screen until they've all gone off the right.
    // The distance comes from the text's size, so the credits can be edited without changing anything here.
    private IEnumerator PlayCredits()
    {
        if (creditsRow == null) BuildCreditsRow();

        // The credits screen is scaled up, so the canvas's width needs converting to the row's scale
        RectTransform canvasRect = creditsCanvas.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
        float screenWidth = canvasRect.rect.width * canvasRect.lossyScale.x / creditsRow.parent.lossyScale.x;

        // The row's position is its right edge, which is where the first section is
        float startX = -screenWidth / 2f;
        float endX = screenWidth / 2f + creditsRowWidth;

        float elapsed = 0f;
        while (elapsed < creditsDuration)
        {
            elapsed += Time.deltaTime;
            creditsRow.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, elapsed / creditsDuration), 0f);
            yield return null;
        }

        CloseCredits();
    }

    // Splits the credits text into a section for each block separated by a blank line, laid out in a row.
    // They go from right to left, since the rightmost one is the first to come onto the screen.
    private void BuildCreditsRow()
    {
        creditsRow = new GameObject("Credits Row", typeof(RectTransform)).GetComponent<RectTransform>();
        creditsRow.SetParent(creditsText.transform.parent, false);
        creditsRow.SetSiblingIndex(creditsText.transform.GetSiblingIndex()); // Keeps it behind the back button

        creditsRowWidth = 0f;
        string[] sections = creditsText.text.Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string section in sections)
        {
            if (creditsRowWidth > 0f) creditsRowWidth += creditsSectionSpacing;

            TMP_Text sectionText = Instantiate(creditsText, creditsRow);
            sectionText.name = "Credits Section";
            sectionText.textWrappingMode = TextWrappingModes.NoWrap;
            sectionText.verticalAlignment = VerticalAlignmentOptions.Middle;
            sectionText.text = section.Trim();

            Vector2 size = sectionText.GetPreferredValues(sectionText.text);
            sectionText.rectTransform.sizeDelta = size;
            sectionText.rectTransform.anchoredPosition = new Vector2(-creditsRowWidth - size.x / 2f, 0f);
            creditsRowWidth += size.x;
        }

        creditsText.gameObject.SetActive(false);
    }
}