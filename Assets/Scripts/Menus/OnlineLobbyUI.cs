using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The online lobby screen: who has joined, and how friends can join. Everyone picks their bird on the
/// character select screen once the host continues. It has no background of its own, so that the main menu's shows through.
/// All of its logic goes through StartNetworkedLobbyForNumPlayers.
/// </summary>
public class OnlineLobbyUI : MonoBehaviour
{
    // The lobby's panel once it's showing, so the menu cursor knows to target its buttons
    public static GameObject ActivePanel { get; private set; }

    private const string backButtonPath = "MenuBackButton"; // In Resources
    private const string cursorPathPrefix = "MenuCursor"; // These too, with the player's number after it
    private const int playerCount = 4;

    private static readonly Color emptyCardColor = new(0.72f, 0.7f, 0.6f);

    private class PlayerCard
    {
        public Image edge;
        public RawImage cursor;
        public TMP_Text title;
        public TMP_Text status;
    }

    private readonly PlayerCard[] cards = new PlayerCard[playerCount];

    private GameObject panel;
    private TMP_Text titleText;
    private GameObject codePill;
    private TMP_Text codeText;
    private Button copyCodeButton;
    private Button continueButton;
    private Button backButton;
    private TMP_Text waitingText;
    private TMP_Text listingText;
    private string joinCode;

    private int lastClickFrame = -1;

    public const string PanelName = "Online Lobby Panel";

    private const string titleName = "Title";
    private const string codePillName = "Join Code";
    private const string codeTextName = "Code";
    private const string copyCodeName = "Copy Code";
    private const string continueName = "Continue to Character Select";
    private const string waitingName = "Waiting";
    private const string listingName = "Listing";
    private const string backButtonName = "Back";
    private const string cardCursorName = "Cursor";
    private const string cardTitleName = "Name";
    private const string cardStatusName = "Status";

    private static string CardName(int index) => $"Player {index + 1} Card";

    // Gets the lobby on the given canvas ready to show. The MainMenu scene has its layout in it already (see
    // MenuScreenBuilder), so it only gets made here, in the menu font, for a scene that doesn't.
    public void Build(Canvas canvas, TMP_FontAsset fontAsset)
    {
        Transform existing = canvas.transform.Find(PanelName);
        panel = existing != null ? existing.gameObject : BuildPanel(canvas, fontAsset);
        Transform root = panel.transform;

        titleText = root.Find(titleName).GetComponent<TMP_Text>();

        for (int i = 0; i < cards.Length; ++i)
        {
            Transform card = root.Find(CardName(i));
            cards[i] = new PlayerCard
            {
                edge = card.GetComponent<Image>(),
                cursor = card.Find(cardCursorName).GetComponent<RawImage>(),
                title = card.Find(cardTitleName).GetComponent<TMP_Text>(),
                status = card.Find(cardStatusName).GetComponent<TMP_Text>()
            };
        }

        codePill = root.Find(codePillName).gameObject;
        codeText = codePill.transform.Find(codeTextName).GetComponent<TMP_Text>();
        copyCodeButton = root.Find(copyCodeName).GetComponent<Button>();
        continueButton = root.Find(continueName).GetComponent<Button>();
        waitingText = root.Find(waitingName).GetComponent<TMP_Text>();
        listingText = root.Find(listingName).GetComponent<TMP_Text>();
        backButton = root.Find(backButtonName).GetComponent<Button>();

        // These only show for the host, once the match is set up (see SetConnectionInfo). The texts can have
        // something in them in the scene, to show where they are.
        codePill.SetActive(false);
        copyCodeButton.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(false);
        waitingText.text = string.Empty;
        listingText.text = string.Empty;

        AddClickListener(copyCodeButton, CopyJoinCode);
        AddClickListener(continueButton, OnContinueButton);
        AddClickListener(backButton, () => NetworkSession.LeaveToMainMenu());

        LinkNavigation();
        panel.SetActive(false);
    }

    // Makes the lobby's layout on the given canvas, hidden. Also used in the editor to put it into the MainMenu scene,
    // where it can be seen and edited (see MenuScreenBuilder). Build finds its parts by name and makes them work, so
    // they need to keep their names.
    public static GameObject BuildPanel(Canvas canvas, TMP_FontAsset font)
    {
        GameObject panel = new(PanelName, typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);

        Vector2 center = new(0.5f, 0.5f);
        MenuStyle.AddText(panel.transform, font, "Online Lobby", 90f, new Vector2(0f, 365f), new Vector2(1400f, 120f), center, TextAlignmentOptions.Center).name = titleName;

        for (int i = 0; i < playerCount; ++i)
            BuildPlayerCard(panel.transform, font, i);

        Image pill = MenuStyle.AddRoundedImage(panel.transform, codePillName, MenuStyle.CreamColor, new Vector2(-170f, -115f), new Vector2(640f, 84f), 84f);
        MenuStyle.AddText(pill.transform, font, "Join code: ABC123", 40f, Vector2.zero, new Vector2(620f, 84f), center, TextAlignmentOptions.Center).name = codeTextName;

        MenuStyle.AddPillButton(panel.transform, font, copyCodeName, MenuStyle.PillColor.Blue, new Vector2(310f, -115f), new Vector2(300f, 84f), 36f);
        MenuStyle.AddPillButton(panel.transform, font, continueName, MenuStyle.PillColor.Yellow, new Vector2(0f, -225f), new Vector2(760f, 96f), 42f);

        // The waiting message is where the continue button is, since a player only ever sees one of the two
        MenuStyle.AddText(panel.transform, font, string.Empty, 42f, new Vector2(0f, -225f), new Vector2(1200f, 96f), center, TextAlignmentOptions.Center).name = waitingName;
        MenuStyle.AddText(panel.transform, font, "This match is public, so anyone can join.", 32f, new Vector2(0f, -300f), new Vector2(1400f, 50f), center, TextAlignmentOptions.Center).name = listingName;

        Instantiate(Resources.Load<GameObject>(backButtonPath), panel.transform).name = backButtonName;

        panel.SetActive(false);
        return panel;
    }

    public void Show()
    {
        panel.SetActive(true);
        ActivePanel = panel;
    }

    private static void BuildPlayerCard(Transform panel, TMP_FontAsset font, int index)
    {
        Vector2 cardSize = new(340f, 300f);
        Vector2 center = new(0.5f, 0.5f);

        Image edge = MenuStyle.AddRoundedImage(panel, CardName(index), emptyCardColor, new Vector2(-570f + index * 380f, 110f), cardSize, 80f);
        MenuStyle.AddRoundedImage(edge.transform, "Fill", MenuStyle.CreamColor, Vector2.zero, cardSize - Vector2.one * 20f, 64f);

        GameObject cursorObject = new(cardCursorName, typeof(RectTransform), typeof(RawImage));
        cursorObject.transform.SetParent(edge.transform, false);
        cursorObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 50f);
        cursorObject.GetComponent<RectTransform>().sizeDelta = new Vector2(105f, 140f);

        RawImage cursor = cursorObject.GetComponent<RawImage>();
        cursor.raycastTarget = false;
        cursor.texture = Resources.Load<GameObject>($"{cursorPathPrefix}{index + 1}").GetComponent<RawImage>().texture;

        // Shrinks to fit a long Steam name on one line
        TMP_Text title = MenuStyle.AddText(edge.transform, font, $"Player {index + 1}", 40f, new Vector2(0f, -55f), new Vector2(320f, 50f), center, TextAlignmentOptions.Center);
        title.name = cardTitleName;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.enableAutoSizing = true;
        title.fontSizeMin = 18f;
        title.fontSizeMax = 40f;

        MenuStyle.AddText(edge.transform, font, "AI", 34f, new Vector2(0f, -100f), new Vector2(320f, 44f), center, TextAlignmentOptions.Center).name = cardStatusName;
    }

    // Explicit navigation links, same reason as in ConnectUI
    private void LinkNavigation()
    {
        Button copy = copyCodeButton.gameObject.activeSelf ? copyCodeButton : null;
        Button next = continueButton.gameObject.activeSelf ? continueButton : null;

        SetNavigation(backButton, null, copy != null ? copy : next);
        SetNavigation(copyCodeButton, backButton, next);
        SetNavigation(continueButton, copy != null ? copy : backButton, null);
    }

    private void SetNavigation(Button button, Button up, Button down)
    {
        button.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down
        };
    }

    // Tells the host how friends can join. Without a code, Relay wasn't available and the match was hosted by IP.
    public void SetConnectionInfo(string code)
    {
        joinCode = code;

        codePill.SetActive(true);
        codeText.text = code != null ? $"Join code: {code}" : "Friends can join with your IP";
        copyCodeButton.gameObject.SetActive(code != null);
        continueButton.gameObject.SetActive(true);

        // There's no copy button next to it without a code, so it can move to the middle
        if (code == null) codePill.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -115f);

        LinkNavigation();
    }

    // For a public match, which goes by the name it has on the list.
    public void SetMatchName(string matchName)
    {
        titleText.text = matchName;
        listingText.text = "Listing this match...";
    }

    public void SetListed(bool listed)
    {
        listingText.text = listed
            ? "This match is public, so anyone can join."
            : "Couldn't make this match public, but friends can still join with the code.";
    }

    private void CopyJoinCode()
    {
        GUIUtility.systemCopyBuffer = joinCode;

        // A button with its own artwork (see MenuArt) can have no label to change
        TMP_Text label = copyCodeButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "Copied!";
    }

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        StartNetworkedLobbyForNumPlayers lobby = StartNetworkedLobbyForNumPlayers.Instance;
        NetworkManager manager = NetworkManager.Singleton;

        if (lobby == null || !lobby.IsSpawned || manager == null)
        {
            waitingText.text = "Connecting...";
            return;
        }

        waitingText.text = manager.IsServer ? string.Empty : "Waiting for the host to continue...";

        for (int i = 0; i < cards.Length; ++i)
        {
            if (i < lobby.Players.Count)
            {
                LobbyPlayer player = lobby.Players[i];
                string role = player.clientId == NetworkManager.ServerClientId ? "Host" : "Joined";
                bool isLocalPlayer = player.clientId == manager.LocalClientId;
                ShowCard(cards[i], MenuStyle.GetPlayerColor(i), 1f, lobby.DisplayName(i), isLocalPlayer ? $"{role} (You)" : role);
            }
            else
            {
                ShowCard(cards[i], emptyCardColor, 0.35f, $"Player {i + 1}", "AI");
            }
        }
    }

    private void ShowCard(PlayerCard card, Color edgeColor, float cursorAlpha, string title, string status)
    {
        card.edge.color = edgeColor;
        card.cursor.color = new Color(1f, 1f, 1f, cursorAlpha);
        card.title.text = title;
        card.status.text = status;
    }

    private void OnContinueButton()
    {
        StartNetworkedLobbyForNumPlayers lobby = StartNetworkedLobbyForNumPlayers.Instance;
        if (lobby != null && lobby.IsSpawned) lobby.GoToCharacterSelect();
    }

    private void AddClickListener(Button button, Action onClick)
    {
        button.onClick.AddListener(() =>
        {
            // Ignore the second click from the same A press
            if (lastClickFrame == Time.frameCount) return;
            lastClickFrame = Time.frameCount;
            onClick();
        });
    }
}
