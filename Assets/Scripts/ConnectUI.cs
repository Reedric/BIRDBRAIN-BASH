using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using UnityEngine.UI;

/// <summary>
/// The online play screen: hosting a public or private match, joining one from the list of public matches,
/// or joining with a code. Its contents get built in code, so the ones in the scene are only there as placeholders.
/// </summary>
public class ConnectUI : MonoBehaviour
{
    private const string addressPrefsKey = "OnlineHostAddress";
    private const string defaultAddress = "127.0.0.1";
    private const string listenAddress = "0.0.0.0"; // Means the host accepts connections from other machines, not just this one
    private const ushort port = 7777;
    private const int maxPlayers = 4;
    private const int visibleMatches = 5;
    private const float matchRefreshInterval = 5f;

    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button backButton;

    [Header("Panels")]
    [SerializeField] private GameObject hostJoinPanel; // Panel for hosting or joining for online play
    [SerializeField] private GameObject localOnlinePanel; // Panel for choosing local or online play

    private class MatchRow
    {
        public Button button;
        public TMP_Text nameText;
        public TMP_Text countText;
        public string joinCode;
    }

    private readonly List<MatchRow> matchRows = new();

    private TMP_FontAsset font;
    private Button hostPublicButton;
    private Button hostPrivateButton;
    private Button joinCodeButton;
    private TMP_InputField addressField; // Join code or address of the host
    private TMP_Text matchListText; // Shown in the list when there's nothing to choose from
    private TMP_Text statusText; // Connection messages
    private OnlineLobbyUI lobbyUI;

    private bool isConnecting;
    private bool isRefreshingMatches;
    private float nextMatchRefreshTime;
    private int lastClickFrame = -1;

    private void Start()
    {
        backButton.onClick.AddListener(BackToLocalOnlineMenu);

        BuildConnectionUI();

        if (!string.IsNullOrEmpty(NetworkSession.LastDisconnectMessage))
        {
            ShowStatus(NetworkSession.LastDisconnectMessage);
            NetworkSession.LastDisconnectMessage = null;
        }
    }

    private void Update()
    {
        if (!isConnecting && !isRefreshingMatches && Time.unscaledTime >= nextMatchRefreshTime)
            RefreshMatches();
    }

    // Keeps the list of public matches up to date while this screen is open.
    private async void RefreshMatches()
    {
        isRefreshingMatches = true;

        try
        {
            List<PublicLobby.Match> matches = await PublicLobby.QueryAsync(visibleMatches);
            if (this == null) return;

            ShowMatches(matches);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not get the public matches: {e}");
            if (this == null) return;

            ShowMatches(new List<PublicLobby.Match>());
            matchListText.text = "Couldn't load public matches.\nYou can still host, or join with a code.";
        }

        isRefreshingMatches = false;
        nextMatchRefreshTime = Time.unscaledTime + matchRefreshInterval;
    }

    private void ShowMatches(List<PublicLobby.Match> matches)
    {
        for (int i = 0; i < matchRows.Count; ++i)
        {
            bool hasMatch = i < matches.Count;
            matchRows[i].button.gameObject.SetActive(hasMatch);
            if (!hasMatch) continue;

            matchRows[i].joinCode = matches[i].joinCode;
            matchRows[i].nameText.text = matches[i].name;
            matchRows[i].countText.text = $"{matches[i].playerCount}/{matches[i].maxPlayers}";
        }

        matchListText.text = matches.Count == 0 ? "No public matches right now.\nHost one and it'll show up here!" : string.Empty;
        LinkNavigation();
    }

    // Hosts through Relay so that friends can join with a code. Hosting by IP is the fallback for when
    // Relay can't be reached, since that still works for players on the same network. A public match
    // doesn't have that fallback, as nobody on the list would be able to reach it.
    private async void HostMatch(bool isPublic)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || manager.IsListening || isConnecting) return;

        SetConnecting(true);
        ShowStatus("Setting up online match...");

        UnityTransport transport = manager.GetComponent<UnityTransport>();
        string joinCode = null;

        try
        {
            // This has room for one client too many. Relay would otherwise turn away a player joining a
            // full lobby before they reach ApproveConnection, so they wouldn't be told the reason.
            (RelayServerData serverData, string code) = await RelayConnection.HostAsync(maxPlayers);
            if (this == null) return;

            transport.SetRelayServerData(serverData);
            joinCode = code;
        }
        catch (Exception e)
        {
            if (this == null) return;

            if (isPublic)
            {
                Debug.LogWarning($"Could not host a public match through Relay: {e}");
                SetConnecting(false);
                ShowStatus("Could not reach the online service. Check your internet connection.");
                return;
            }

            Debug.LogWarning($"Could not host through Relay, so hosting by IP instead: {e}");
            transport.SetConnectionData(defaultAddress, port, listenAddress);
        }

        PrepareToStart(manager);
        manager.ConnectionApprovalCallback = ApproveConnection;

        if (!manager.StartHost())
        {
            UndoPrepareToStart(manager);
            ShowStatus($"Could not host. Is port {port} already in use?");
            return;
        }

        ShowLobby();
        lobbyUI.SetConnectionInfo(joinCode);

        if (isPublic)
        {
            string matchName = PublicLobby.CreateMatchName();
            lobbyUI.SetMatchName(matchName);

            // This stays with the lobby screen, since this one is hidden while it's showing
            PublicLobbyHost listing = lobbyUI.gameObject.AddComponent<PublicLobbyHost>();
            bool listed = await listing.OpenAsync(matchName, joinCode, maxPlayers);
            if (lobbyUI != null) lobbyUI.SetListed(listed);
        }
    }

    // Joins with a join code, or directly with an IP address.
    private async void JoinMatch(string entry)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || manager.IsListening || isConnecting) return;

        if (string.IsNullOrEmpty(entry))
        {
            ShowStatus("Enter a join code or the host's IP address.");
            return;
        }

        SetConnecting(true);
        ShowStatus("Connecting...");

        UnityTransport transport = manager.GetComponent<UnityTransport>();

        if (IsDirectAddress(entry))
        {
            PlayerPrefs.SetString(addressPrefsKey, entry);
            transport.SetConnectionData(entry, port, listenAddress);
        }
        else
        {
            try
            {
                RelayServerData serverData = await RelayConnection.JoinAsync(entry.ToUpperInvariant());
                if (this == null) return;

                transport.SetRelayServerData(serverData);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not join through Relay: {e}");
                if (this == null) return;

                SetConnecting(false);
                ShowStatus(e is RelayServiceException { Reason: RelayExceptionReason.JoinCodeNotFound or RelayExceptionReason.InvalidRequest }
                    ? "That match wasn't found. Check the code, or try another match."
                    : "Could not reach the online service. Check your internet connection.");
                return;
            }
        }

        PrepareToStart(manager);
        manager.OnClientConnectedCallback += NetworkSession.OnClientConnected;
        manager.OnClientDisconnectCallback += NetworkSession.OnClientDisconnected;

        if (manager.StartClient())
        {
            ShowLobby();
        }
        else
        {
            manager.OnClientConnectedCallback -= NetworkSession.OnClientConnected;
            manager.OnClientDisconnectCallback -= NetworkSession.OnClientDisconnected;
            UndoPrepareToStart(manager);
            ShowStatus("Could not connect to that match.");
        }
    }

    // Join codes are only letters and numbers, so anything that looks like an address connects directly.
    private bool IsDirectAddress(string entry)
    {
        return entry.Contains('.') || entry.Contains(':') || entry.Equals("localhost", StringComparison.OrdinalIgnoreCase);
    }

    // Everything both the host and clients need set before starting. Connection approval has to be on
    // for both sides, since the setting is part of what they compare when connecting.
    private void PrepareToStart(NetworkManager manager)
    {
        DataTransferManager.isLocal = false;
        manager.NetworkConfig.ConnectionApproval = true;
        manager.OnTransportFailure += NetworkSession.OnTransportFailure;
    }

    // Puts things back after failing to start, so that the player can try again or play a local match instead.
    private void UndoPrepareToStart(NetworkManager manager)
    {
        DataTransferManager.isLocal = true;
        manager.OnTransportFailure -= NetworkSession.OnTransportFailure;
        SetConnecting(false);
    }

    // Runs on the host for each player trying to join. Birds are spawned by MultiplayerManager, so
    // NGO should not create a player object of its own.
    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.CreatePlayerObject = false;

        if (NetworkSession.MatchStarted)
        {
            response.Approved = false;
            response.Reason = "That match has already started.";
        }
        else if (NetworkManager.Singleton.ConnectedClientsIds.Count >= maxPlayers)
        {
            response.Approved = false;
            response.Reason = "That lobby is full.";
        }
        else
        {
            response.Approved = true;
        }
    }

    private void ShowLobby()
    {
        if (hostJoinPanel != null) hostJoinPanel.SetActive(false);
        if (lobbyUI == null)
        {
            lobbyUI = gameObject.transform.parent != null
                ? gameObject.transform.parent.gameObject.AddComponent<OnlineLobbyUI>()
                : gameObject.AddComponent<OnlineLobbyUI>();
            lobbyUI.Build(hostJoinPanel.GetComponentInParent<Canvas>(true), font);
        }
        lobbyUI.Show();
    }

    private void ShowStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private void SetConnecting(bool connecting)
    {
        isConnecting = connecting;

        hostPublicButton.interactable = !connecting;
        hostPrivateButton.interactable = !connecting;
        joinCodeButton.interactable = !connecting;
        addressField.interactable = !connecting;
        backButton.interactable = !connecting;

        foreach (MatchRow row in matchRows)
            row.button.interactable = !connecting;
    }

    /// <summary>
    /// Callback that switches the screen from Host-Join menu to Local Online menu
    /// </summary>
    private void BackToLocalOnlineMenu()
    {
        if (hostJoinPanel != null) hostJoinPanel.SetActive(false);
        if (localOnlinePanel != null) localOnlinePanel.SetActive(true);
    }

    // Fills in the host-join panel, and hides the parts from the scene that this replaces.
    private void BuildConnectionUI()
    {
        if (hostJoinPanel == null) return;

        // Without its own background, the main menu's swaying one shows through the same as for credits and options
        if (hostJoinPanel.TryGetComponent(out RawImage panelImage)) panelImage.enabled = false;
        foreach (string placeholder in new[] { "bgd", "Players" })
        {
            Transform placeholderObject = hostJoinPanel.transform.Find(placeholder);
            if (placeholderObject != null) placeholderObject.gameObject.SetActive(false);
        }
        hostButton.gameObject.SetActive(false);
        clientButton.gameObject.SetActive(false);

        MainMenu mainMenu = FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);
        font = mainMenu != null ? mainMenu.MenuFont : null;

        Vector2 center = new(0.5f, 0.5f);
        Vector2 buttonSize = new(540f, 105f);

        MenuStyle.AddText(hostJoinPanel.transform, font, "Online Play", 90f, new Vector2(0f, 365f), new Vector2(1000f, 120f), center, TextAlignmentOptions.Center);

        hostPublicButton = MenuStyle.AddPillButton(hostJoinPanel.transform, font, "Host Public Match", MenuStyle.AccentColor, new Vector2(-530f, 220f), buttonSize, 44f);
        AddClickListener(hostPublicButton, () => HostMatch(true));

        hostPrivateButton = MenuStyle.AddPillButton(hostJoinPanel.transform, font, "Host Private Match", MenuStyle.BlueColor, new Vector2(-530f, 95f), buttonSize, 44f);
        AddClickListener(hostPrivateButton, () => HostMatch(false));

        BuildAddressField(new Vector2(-530f, -30f), buttonSize);

        joinCodeButton = MenuStyle.AddPillButton(hostJoinPanel.transform, font, "Join With Code", MenuStyle.PinkColor, new Vector2(-530f, -155f), buttonSize, 44f);
        AddClickListener(joinCodeButton, () => JoinMatch(addressField.text.Trim()));

        BuildMatchList();

        statusText = MenuStyle.AddText(hostJoinPanel.transform, font, string.Empty, 34f, new Vector2(0f, -305f), new Vector2(1700f, 50f), center, TextAlignmentOptions.Center);

        LinkNavigation();
    }

    private void BuildAddressField(Vector2 position, Vector2 size)
    {
        GameObject fieldObject = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        fieldObject.name = "Host Address";
        fieldObject.transform.SetParent(hostJoinPanel.transform, false);

        RectTransform fieldRect = fieldObject.GetComponent<RectTransform>();
        fieldRect.anchoredPosition = position;
        fieldRect.sizeDelta = size;

        Image fieldImage = fieldObject.GetComponent<Image>();
        fieldImage.color = MenuStyle.CreamColor;
        MenuStyle.MakeRounded(fieldImage, size.y);
        MenuStyle.MakeClickable(fieldImage);

        addressField = fieldObject.GetComponent<TMP_InputField>();
        addressField.pointSize = 40f;
        addressField.text = PlayerPrefs.GetString(addressPrefsKey, string.Empty);

        // Keeps what's typed clear of the field's rounded ends
        addressField.textViewport.offsetMin = new Vector2(40f, 0f);
        addressField.textViewport.offsetMax = new Vector2(-40f, 0f);

        if (font != null) addressField.fontAsset = font;
        addressField.textComponent.alignment = TextAlignmentOptions.Center;
        addressField.textComponent.color = MenuStyle.TextColor;

        if (addressField.placeholder is TMP_Text placeholder)
        {
            placeholder.text = "Join code or IP";
            placeholder.alignment = TextAlignmentOptions.Center;
            if (font != null) placeholder.font = font;
        }
    }

    private void BuildMatchList()
    {
        const float rowWidth = 980f;
        const float rowHeight = 78f;

        Vector2 listCenter = new(330f, -5f);
        Vector2 center = new(0.5f, 0.5f);

        MenuStyle.AddRoundedImage(hostJoinPanel.transform, "Public Matches", new Color(1f, 1f, 1f, 0.4f), listCenter, new Vector2(1060f, 550f), 80f);
        MenuStyle.AddText(hostJoinPanel.transform, font, "Public Matches", 48f, new Vector2(listCenter.x, 215f), new Vector2(rowWidth, 60f), center, TextAlignmentOptions.Center);

        matchListText = MenuStyle.AddText(hostJoinPanel.transform, font, "Looking for matches...", 40f, new Vector2(listCenter.x, -20f), new Vector2(rowWidth, 200f), center, TextAlignmentOptions.Center);

        for (int i = 0; i < visibleMatches; ++i)
        {
            GameObject rowObject = new("Public Match", typeof(RectTransform), typeof(Image), typeof(Button));
            rowObject.transform.SetParent(hostJoinPanel.transform, false);

            // With the pivot on the left, the menu cursor points at the start of the row and not over its name
            RectTransform rect = rowObject.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(listCenter.x - rowWidth / 2f, 130f - i * 88f);
            rect.sizeDelta = new Vector2(rowWidth, rowHeight);

            Image image = rowObject.GetComponent<Image>();
            MenuStyle.MakeRounded(image, rowHeight);
            MenuStyle.MakeClickable(image);

            Button button = rowObject.GetComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.6f);
            colors.highlightedColor = colors.selectedColor = new Color(1f, 0.82f, 0.3f, 1f);
            colors.pressedColor = new Color(1f, 0.7f, 0.2f, 1f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            Vector2 left = new(0f, 0.5f);
            MatchRow row = new()
            {
                button = button,
                nameText = MenuStyle.AddText(rowObject.transform, font, string.Empty, 42f, new Vector2(90f, 0f), new Vector2(680f, rowHeight), left, TextAlignmentOptions.Left),
                countText = MenuStyle.AddText(rowObject.transform, font, string.Empty, 42f, new Vector2(rowWidth - 170f, 0f), new Vector2(130f, rowHeight), left, TextAlignmentOptions.Right)
            };

            AddClickListener(button, () => JoinMatch(row.joinCode));
            rowObject.SetActive(false);
            matchRows.Add(row);
        }
    }

    // Hooks up stick navigation by hand, because the menu cursor only follows explicit links.
    // This changes with how many matches are on the list.
    private void LinkNavigation()
    {
        Button firstMatch = matchRows[0].button.gameObject.activeSelf ? matchRows[0].button : null;

        Navigation backNavigation = backButton.navigation;
        backNavigation.mode = Navigation.Mode.Explicit;
        backNavigation.selectOnUp = null;
        backNavigation.selectOnDown = hostPublicButton;
        backNavigation.selectOnRight = null;
        backButton.navigation = backNavigation;

        SetNavigation(hostPublicButton, backButton, hostPrivateButton, null, firstMatch);
        SetNavigation(hostPrivateButton, hostPublicButton, addressField, null, firstMatch);
        SetNavigation(addressField, hostPrivateButton, joinCodeButton, null, firstMatch);
        SetNavigation(joinCodeButton, addressField, null, null, firstMatch);

        for (int i = 0; i < matchRows.Count; ++i)
        {
            Selectable up = i > 0 ? matchRows[i - 1].button : null;
            Selectable down = i + 1 < matchRows.Count && matchRows[i + 1].button.gameObject.activeSelf ? matchRows[i + 1].button : null;
            SetNavigation(matchRows[i].button, up, down, hostPublicButton, null);
        }
    }

    private void SetNavigation(Selectable selectable, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        selectable.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down,
            selectOnLeft = left,
            selectOnRight = right
        };
    }

    private void AddClickListener(Button button, Action onClick)
    {
        button.onClick.AddListener(() =>
        {
            // Same double-click guard as the main menu
            if (lastClickFrame == Time.frameCount) return;
            lastClickFrame = Time.frameCount;
            onClick();
        });
    }
}
