using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class TeamArrangementToggle : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RawImage buttonImage;
    [SerializeField] private Button button;

    [Header("Arrangement Textures")]
    [SerializeField] private Texture p1p2VsP3p4Texture;
    [SerializeField] private Texture p1p3VsP2p4Texture;
    [SerializeField] private Texture p1p4VsP2p3Texture;

    [Header("Player Backgrounds")]
    [SerializeField] private RawImage player1Background;
    [SerializeField] private RawImage player2Background;
    [SerializeField] private RawImage player3Background;
    [SerializeField] private RawImage player4Background;

    [Header("Team Background Textures")]
    [SerializeField] private Texture blueTeamTexture;
    [SerializeField] private Texture pinkTeamTexture;

    private GameSettings gameSettings;

    private void Awake()
    {
        // --------------------------------------------------------
        // GET GAME SETTINGS
        // --------------------------------------------------------

        // Make sure a persistent GameSettings object exists.
        gameSettings = GameSettings.EnsureInstance();

        // --------------------------------------------------------
        // GET UI COMPONENTS
        // --------------------------------------------------------

        button = GetComponent<Button>();

        if (buttonImage == null)
            buttonImage = GetComponent<RawImage>();

        if (buttonImage == null)
        {
            Debug.LogError(
                "TeamArrangementToggle: No RawImage found on " +
                gameObject.name
            );
        }

        // Make sure the Button uses the RawImage as its target graphic.
        if (button.targetGraphic == null && buttonImage != null)
            button.targetGraphic = buttonImage;

        // --------------------------------------------------------
        // BUTTON LISTENER
        // --------------------------------------------------------

        // Prevent duplicate listeners if this object is initialized
        // more than once.
        button.onClick.RemoveListener(OnTogglePressed);
        button.onClick.AddListener(OnTogglePressed);

        Debug.Log(
            "TeamArrangementToggle initialized. " +
            "Current arrangement: " +
            gameSettings.CurrentTeamArrangement
        );
    }

    private void Start()
    {
        UpdateUI();
    }

    // ============================================================
    // TOGGLE
    // ============================================================

    public void OnTogglePressed()
    {
        // Ensure the settings object still exists.
        if (gameSettings == null)
            gameSettings = GameSettings.EnsureInstance();

        if (gameSettings == null)
        {
            Debug.LogError(
                "TeamArrangementToggle: Could not create GameSettings."
            );
            return;
        }

        // --------------------------------------------------------
        // CYCLE ARRANGEMENT
        // --------------------------------------------------------

        // Do NOT rely on the enum's integer order.
        // Explicitly cycle through the three desired arrangements.

        switch (gameSettings.CurrentTeamArrangement)
        {
            case GameSettings.TeamArrangement.P1P2_vs_P3P4:

                gameSettings.CurrentTeamArrangement =
                    GameSettings.TeamArrangement.P1P3_vs_P2P4;

                break;

            case GameSettings.TeamArrangement.P1P3_vs_P2P4:

                gameSettings.CurrentTeamArrangement =
                    GameSettings.TeamArrangement.P1P4_vs_P2P3;

                break;

            case GameSettings.TeamArrangement.P1P4_vs_P2P3:

                gameSettings.CurrentTeamArrangement =
                    GameSettings.TeamArrangement.P1P2_vs_P3P4;

                break;

            default:

                gameSettings.CurrentTeamArrangement =
                    GameSettings.TeamArrangement.P1P2_vs_P3P4;

                break;
        }

        Debug.Log(
            "Team arrangement changed to: " +
            gameSettings.CurrentTeamArrangement
        );

        // Update all UI immediately.
        UpdateUI();
    }

    // ============================================================
    // UI
    // ============================================================

    private void UpdateUI()
    {
        if (gameSettings == null)
            return;

        // --------------------------------------------------------
        // UPDATE ARRANGEMENT PREVIEW
        // --------------------------------------------------------

        if (buttonImage != null)
        {
            switch (gameSettings.CurrentTeamArrangement)
            {
                case GameSettings.TeamArrangement.P1P2_vs_P3P4:

                    buttonImage.texture = p1p2VsP3p4Texture;

                    break;

                case GameSettings.TeamArrangement.P1P3_vs_P2P4:

                    buttonImage.texture = p1p3VsP2p4Texture;

                    break;

                case GameSettings.TeamArrangement.P1P4_vs_P2P3:

                    buttonImage.texture = p1p4VsP2p3Texture;

                    break;

                default:

                    Debug.LogWarning(
                        "TeamArrangementToggle: Unknown team arrangement."
                    );

                    break;
            }
        }

        // --------------------------------------------------------
        // UPDATE PLAYER TEAM BACKGROUNDS
        // --------------------------------------------------------

        switch (gameSettings.CurrentTeamArrangement)
        {
            case GameSettings.TeamArrangement.P1P2_vs_P3P4:

                // Blue: P1, P2
                // Pink: P3, P4

                SetPlayerBackground(
                    player1Background,
                    blueTeamTexture
                );

                SetPlayerBackground(
                    player2Background,
                    blueTeamTexture
                );

                SetPlayerBackground(
                    player3Background,
                    pinkTeamTexture
                );

                SetPlayerBackground(
                    player4Background,
                    pinkTeamTexture
                );

                break;

            case GameSettings.TeamArrangement.P1P3_vs_P2P4:

                // Blue: P1, P3
                // Pink: P2, P4

                SetPlayerBackground(
                    player1Background,
                    blueTeamTexture
                );

                SetPlayerBackground(
                    player2Background,
                    pinkTeamTexture
                );

                SetPlayerBackground(
                    player3Background,
                    blueTeamTexture
                );

                SetPlayerBackground(
                    player4Background,
                    pinkTeamTexture
                );

                break;

            case GameSettings.TeamArrangement.P1P4_vs_P2P3:

                // Blue: P1, P4
                // Pink: P2, P3

                SetPlayerBackground(
                    player1Background,
                    blueTeamTexture
                );

                SetPlayerBackground(
                    player2Background,
                    pinkTeamTexture
                );

                SetPlayerBackground(
                    player3Background,
                    pinkTeamTexture
                );

                SetPlayerBackground(
                    player4Background,
                    blueTeamTexture
                );

                break;
        }
    }

    // ============================================================
    // PLAYER BACKGROUND
    // ============================================================

    private void SetPlayerBackground(
        RawImage background,
        Texture texture
    )
    {
        if (background == null)
            return;

        background.texture = texture;
    }

    // ============================================================
    // REFRESH
    // ============================================================

    /// <summary>
    /// Refreshes the displayed textures from the current
    /// GameSettings value.
    ///
    /// Useful if the Match Settings menu is opened again after
    /// changing settings elsewhere.
    /// </summary>
    public void Refresh()
    {
        if (gameSettings == null)
            gameSettings = GameSettings.EnsureInstance();

        UpdateUI();
    }
}
