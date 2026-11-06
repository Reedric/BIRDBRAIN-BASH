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
    private TMP_FontAsset font;
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

    // Builds the lobby on the given canvas. Takes the menu font so it doesn't look out of place.
    public void Build(Canvas canvas, TMP_FontAsset fontAsset)
    {
        font = fontAsset;

        panel = new GameObject("Online Lobby Panel", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);

        Vector2 center = new(0.5f, 0.5f);
        titleText = MenuStyle.AddText(panel.transform, font, "Online Lobby", 90f, new Vector2(0f, 365f), new Vector2(1400f, 120f), center, TextAlignmentOptions.Center);

        for (int i = 0; i < cards.Length; ++i)
            cards[i] = AddPlayerCard(i);

        Image pill = MenuStyle.AddRoundedImage(panel.transform, "Join Code", MenuStyle.CreamColor, new Vector2(-170f, -115f), new Vector2(640f, 84f), 84f);
        codePill = pill.gameObject;
        codePill.SetActive(false);
        codeText = MenuStyle.AddText(pill.transform, font, string.Empty, 40f, Vector2.zero, new Vector2(620f, 84f), center, TextAlignmentOptions.Center);

        copyCodeButton = MenuStyle.AddPillButton(panel.transform, font, "Copy Code", MenuStyle.BlueColor, new Vector2(310f, -115f), new Vector2(300f, 84f), 36f);
        AddClickListener(copyCodeButton, CopyJoinCode);
        copyCodeButton.gameObject.SetActive(false);

        continueButton = MenuStyle.AddPillButton(panel.transform, font, "Continue to Character Select", MenuStyle.AccentColor, new Vector2(0f, -225f), new Vector2(760f, 96f), 42f);
        AddClickListener(continueButton, OnContinueButton);
        continueButton.gameObject.SetActive(false);

        waitingText = MenuStyle.AddText(panel.transform, font, string.Empty, 42f, new Vector2(0f, -225f), new Vector2(1200f, 96f), center, TextAlignmentOptions.Center);
        listingText = MenuStyle.AddText(panel.transform, font, string.Empty, 32f, new Vector2(0f, -300f), new Vector2(1400f, 50f), center, TextAlignmentOptions.Center);

        backButton = Instantiate(Resources.Load<GameObject>(backButtonPath), panel.transform).GetComponent<Button>();
        AddClickListener(backButton, () => NetworkSession.LeaveToMainMenu());

        LinkNavigation();
        panel.SetActive(false);
    }

    public void Show()
    {
        panel.SetActive(true);
        ActivePanel = panel;
    }

    private PlayerCard AddPlayerCard(int index)
    {
        Vector2 cardSize = new(340f, 300f);
        Vector2 center = new(0.5f, 0.5f);

        Image edge = MenuStyle.AddRoundedImage(panel.transform, $"Player {index + 1} Card", emptyCardColor, new Vector2(-570f + index * 380f, 110f), cardSize, 80f);
        MenuStyle.AddRoundedImage(edge.transform, "Fill", MenuStyle.CreamColor, Vector2.zero, cardSize - Vector2.one * 20f, 64f);

        GameObject cursorObject = new("Cursor", typeof(RectTransform), typeof(RawImage));
        cursorObject.transform.SetParent(edge.transform, false);
        cursorObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 50f);
        cursorObject.GetComponent<RectTransform>().sizeDelta = new Vector2(105f, 140f);

        RawImage cursor = cursorObject.GetComponent<RawImage>();
        cursor.raycastTarget = false;
        cursor.texture = Resources.Load<GameObject>($"{cursorPathPrefix}{index + 1}").GetComponent<RawImage>().texture;

        return new PlayerCard
        {
            edge = edge,
            cursor = cursor,
            title = MenuStyle.AddText(edge.transform, font, string.Empty, 40f, new Vector2(0f, -55f), new Vector2(320f, 50f), center, TextAlignmentOptions.Center),
            status = MenuStyle.AddText(edge.transform, font, string.Empty, 34f, new Vector2(0f, -100f), new Vector2(320f, 44f), center, TextAlignmentOptions.Center)
        };
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
        copyCodeButton.GetComponentInChildren<TMP_Text>().text = "Copied!";
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
                bool isLocalPlayer = player.clientId == manager.LocalClientId;
                ShowCard(cards[i], MenuStyle.GetPlayerColor(i), 1f,
                    isLocalPlayer ? $"Player {i + 1} (You)" : $"Player {i + 1}",
                    player.clientId == NetworkManager.ServerClientId ? "Host" : "Joined");
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
