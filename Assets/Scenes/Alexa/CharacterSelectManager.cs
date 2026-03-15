using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// This script is used to manage the character select screen.
// It will handle the character selection and transition to the next scene when the players are ready.
public class CharacterSelectManager : MonoBehaviour
{
    [Header("Player Name Texts")]
    public TMP_Text blue1Name;
    public TMP_Text blue2Name;
    public TMP_Text pink1Name;
    public TMP_Text pink2Name;
    private static CharacterSelectManager instance; // Singleton reference
    public static CharacterSelectManager Instance => instance;

    // All of the bird types that players can choose from (order matches the enum)
    public List<BirdType> availableBirds = new();
    public int numberOfPlayers = 4;
    public Canvas mainCanvas;
    public Transform cursor1Prefab; // Need cursor prefab(s) to show player cursors on the select screen
    public Transform cursor2Prefab; // Need cursor prefab(s) to show player cursors on the select screen
    public Transform cursor3Prefab; // Need cursor prefab(s) to show player cursors on the select screen
    public Transform cursor4Prefab; // Need cursor prefab(s) to show player cursors on the select screen
    public Button readyButton;

    [Header("Player Icons")]
    public RawImage blue1Icon; // Player 0 icon
    public RawImage blue2Icon; // Player 1 icon
    public RawImage pink1Icon; // Player 2 icon
    public RawImage pink2Icon; // Player 3 icon

    [Header("Bird Textures")]
    public RawImage penguinTexture;
    public RawImage crowTexture;
    public RawImage scissortailTexture;
    public RawImage lovebirdTexture;
    public RawImage dodoTexture;
    public RawImage pelicanTexture;
    public RawImage seagullTexture;
    public RawImage owlTexture;
    public RawImage pukekoTexture;
    public RawImage toucanTexture;
    public RawImage kiwiTexture;

    [Header("Ready Indicators")]
    public RawImage p1Ready;
    public RawImage p2Ready;
    public RawImage p3Ready;
    public RawImage p4Ready;

    [Header("Go Button")]
    public RawImage goButton;

    // per-player data maintained while on the select screen
    private List<int> chosenBirdIndices = new();
    private List<bool> isKBMInput = new();
    private List<bool> playerReady = new();
    private List<Transform> playerCursors = new();
    private List<PlayerInputState> playerInputStates = new();

    // name of the scene to load once selections are done (MAKE SURE THIS MATCHES MULTIPLAYER MANAGER AND CHANGES WHEN NEEDED)
    private const string mainSceneName = "Game";

    // Name of the main menu scene (update as needed)
    private const string mainMenuSceneName = "MainMenu";

    // Tracks important input info for each player
    private class PlayerInputState
    {
        public int playerIndex;
        public bool isKBM;
        public InputDevice device;
        public Vector2 cursorPosition; // Screen space
        public Vector2 inputDirection; // For gamepad stick input
        public bool readyPressed = false;

        public PlayerInputState(int index, bool kbm, InputDevice dev)
        {
            playerIndex = index;
            isKBM = kbm;
            device = dev;
            cursorPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
        }
    }

    private void Awake()
    {
        // singleton setup
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        // Auto-find icons if not assigned
        if (blue1Icon == null) blue1Icon = System.Array.Find(FindObjectsByType<RawImage>(FindObjectsSortMode.None), img => img.gameObject.name == "Blue1Icon");
        if (blue2Icon == null) blue2Icon = System.Array.Find(FindObjectsByType<RawImage>(FindObjectsSortMode.None), img => img.gameObject.name == "Blue2Icon");
        if (pink1Icon == null) pink1Icon = System.Array.Find(FindObjectsByType<RawImage>(FindObjectsSortMode.None), img => img.gameObject.name == "Pink1Icon");
        if (pink2Icon == null) pink2Icon = System.Array.Find(FindObjectsByType<RawImage>(FindObjectsSortMode.None), img => img.gameObject.name == "Pink2Icon");

        // If the previous menu passed player/input data use it; otherwise use defaults
        if (DataTransferManager.isKBMInput != null && DataTransferManager.isKBMInput.Count > 0)
        {
            numberOfPlayers = Mathf.Clamp(DataTransferManager.isKBMInput.Count, 1, 4);
            isKBMInput = new List<bool>(DataTransferManager.isKBMInput);
        }
        else
        {
            // ensure transfer lists exist so other code can reference them
            if (DataTransferManager.isKBMInput == null) DataTransferManager.isKBMInput = new List<bool>();
            if (DataTransferManager.selectedBirds == null) DataTransferManager.selectedBirds = new List<BirdType>();
        }

        // make sure internal lists are sized correctly
        ResizePlayerLists(numberOfPlayers);

        // Setup input state for each player, prefer the transferred input scheme where available
        SetupPlayerInputStates();
    }

    // Start is called before the first frame update
    private void Start()
    {
        CreatePlayerCursors();

        // Wire up the ready button if available
        if (readyButton != null) readyButton.onClick.AddListener(CheckAllPlayersReady);

        // Hide all ready indicators and GO button initially
        if (p1Ready != null) p1Ready.enabled = false;
        if (p2Ready != null) p2Ready.enabled = false;
        if (p3Ready != null) p3Ready.enabled = false;
        if (p4Ready != null) p4Ready.enabled = false;
        if (goButton != null) goButton.enabled = false;
    }

    // Update is called once per frame
    private void Update()
    {
        // Update cursor positions and handle input for each player
        for (int i = 0; i < playerInputStates.Count; ++i)
        {
            UpdatePlayerInput(i);
            UpdatePlayerCursor(i);
        }
    }

    /// <summary>
    /// EJ: For now, I'm just assuming player 0 is KBM and the rest are gamepads 
    /// just because alexa told me there's only one KBM allowed, but this can be changed to be more flexible if needed. 
    /// The important part is that the playerInputStates list is set up correctly and matches the order of players for the rest of the code to work, 
    /// and that the isKBMInput list is populated to match what the multiplayer manager will expect when it reads from the transfer manager.
    /// Though I'm sure you can change this with the MultiplayerManager to just read whatever you set up there if you need to be more flexible.
    /// You have my discord if you have concerns.
    /// </summary>
    private void SetupPlayerInputStates()
    {
        playerInputStates.Clear();
        // If transfer manager provided an input scheme, use that
        if (DataTransferManager.isKBMInput != null && DataTransferManager.isKBMInput.Count == numberOfPlayers)
        {
            int gamepadIndex = 0;
            for (int i = 0; i < numberOfPlayers; ++i)
            {
                bool kbm = DataTransferManager.isKBMInput[i];
                InputDevice dev = kbm ? (InputDevice)Keyboard.current : (gamepadIndex < Gamepad.all.Count ? (InputDevice)Gamepad.all[gamepadIndex++] : null);
                playerInputStates.Add(new PlayerInputState(i, kbm, dev));
            }

            isKBMInput = new List<bool>(DataTransferManager.isKBMInput);
        }
        else
        {
            // // Default: first player KBM, others gamepads if present
            // playerInputStates.Add(new PlayerInputState(0, true, Keyboard.current));

            // int gamepadIndex = 0;
            // for (int i = 1; i < numberOfPlayers; ++i)
            // {
            //     InputDevice dev = gamepadIndex < Gamepad.all.Count ? (InputDevice)Gamepad.all[gamepadIndex++] : null;
            //     playerInputStates.Add(new PlayerInputState(i, false, dev));
            // }

            // isKBMInput.Clear();
            // foreach (var state in playerInputStates) isKBMInput.Add(state.isKBM);

            // All players are gamepades
            for (int i = 0; i < numberOfPlayers; ++i)
            {
                InputDevice dev = i < Gamepad.all.Count ? (InputDevice)Gamepad.all[i] : null;
                playerInputStates.Add(new PlayerInputState(i, false, dev));
            }

            isKBMInput.Clear();
            foreach (var state in playerInputStates) isKBMInput.Add(state.isKBM);
        }
    }

    // Create a cursor for each player. (CURSOR PREFABS ARE NEEEDED)
    private void CreatePlayerCursors()
    {
        // Array of cursor prefabs for each player
        Transform[] cursorPrefabs = new Transform[] { cursor1Prefab, cursor2Prefab, cursor3Prefab, cursor4Prefab };

        playerCursors.Clear();

        for (int i = 0; i < numberOfPlayers; ++i)
        {
            Transform prefab = (i < cursorPrefabs.Length) ? cursorPrefabs[i] : null;
            if (prefab == null)
            {
                Debug.LogWarning($"Cursor prefab for player {i + 1} not assigned in CharacterSelectManager!");
                continue;
            }
            Transform cursor = Instantiate(prefab, mainCanvas.transform);
            cursor.name = $"Cursor_Player{i}";

            // Color cursors per player
            Image cursorImage = cursor.GetComponent<Image>();
            if (cursorImage != null)
            {
                cursorImage.color = GetPlayerColor(i);
            }

            playerCursors.Add(cursor);
        }
    }

    // Update input for a given player and check for any button selections.
    private void UpdatePlayerInput(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= playerInputStates.Count) return;
        PlayerInputState state = playerInputStates[playerIndex];

        if (state.isKBM)
        {
            state.cursorPosition = Mouse.current.position.ReadValue();

            // Check for mouse clicks on buttons
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                HandlePlayerButtonPress(playerIndex);
            }
        }
        else
        {
            // Players 1-3: Gamepad input
            Gamepad pad = state.device as Gamepad;
            if (pad != null)
            {
                // Left stick controls cursor movement
                state.inputDirection = pad.leftStick.ReadValue();

                // Update cursor position
                float moveSpeed = 500f * Time.deltaTime;
                state.cursorPosition += state.inputDirection * moveSpeed;

                // Clamp to screen bounds
                state.cursorPosition.x = Mathf.Clamp(state.cursorPosition.x, 0, Screen.width);
                state.cursorPosition.y = Mathf.Clamp(state.cursorPosition.y, 0, Screen.height);

                // Check for button presses (South button)
                if (pad.aButton.wasPressedThisFrame) HandlePlayerButtonPress(playerIndex);

                // Check for "ready" input (Start button, or similar)
                if (pad.startButton.wasPressedThisFrame) playerReady[playerIndex] = !playerReady[playerIndex];
            }
        }
    }

    private void UpdatePlayerCursor(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= playerCursors.Count) return;
        if (playerIndex >= playerInputStates.Count) return;

        Transform cursor = playerCursors[playerIndex];
        Vector2 screenPos = playerInputStates[playerIndex].cursorPosition;

        // Convert screen position to canvas position
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mainCanvas.GetComponent<RectTransform>(),
            screenPos,
            mainCanvas.worldCamera,
            out Vector2 localPos
        );

        cursor.GetComponent<RectTransform>().localPosition = localPos;
    }

    // Check if UI button is under the cursor and respond to it
    private void HandlePlayerButtonPress(int playerIndex)
    {
        if (playerIndex >= playerInputStates.Count) return;
        Vector2 screenPos = playerInputStates[playerIndex].cursorPosition;

        // Raycast to find UI element under cursor
        PointerEventData pointerData = new(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            // Check if this is a bird selection button
            BirdSelectButton birdButton = result.gameObject.GetComponent<BirdSelectButton>();
            if (birdButton != null)
            {
                birdButton.OnPressed(playerIndex);
                return;
            }

            // Check if this is a standard Unity UI Button
            Button uiButton = result.gameObject.GetComponent<Button>();
            if (uiButton != null)
            {
                uiButton.onClick.Invoke();
                return;
            }
        }
    }


    // Make sure the it's the correct size and has defaults
    // Needs to be called whenever player count changes
    public void ResizePlayerLists(int count)
    {
        numberOfPlayers = Mathf.Clamp(count, 1, 4);

        while (chosenBirdIndices.Count < numberOfPlayers)
            chosenBirdIndices.Add(0);
        while (isKBMInput.Count < numberOfPlayers)
            isKBMInput.Add(true);
        while (playerReady.Count < numberOfPlayers)
            playerReady.Add(false);
        while (chosenBirdIndices.Count > numberOfPlayers)
            chosenBirdIndices.RemoveAt(chosenBirdIndices.Count - 1);
        while (isKBMInput.Count > numberOfPlayers)
            isKBMInput.RemoveAt(isKBMInput.Count - 1);
        while (playerReady.Count > numberOfPlayers)
            playerReady.RemoveAt(playerReady.Count - 1);
    }

    /// <summary>
    /// EJ: Alexa has not yet told me which player will be what color, 
    /// so for now I'm just assigning some arbitrary colors to the cursors based on player index, 
    /// but this can be changed to whatever. 
    /// </summary>
    private Color GetPlayerColor(int playerIndex)
    {
        return playerIndex switch
        {
            0 => Color.cyan,    
            1 => Color.yellow,  
            2 => Color.magenta, 
            3 => Color.green,  
            _ => Color.white
        };
    }

    // Set the chosen bird for a specific player
    public void SetPlayerBirdIndex(int playerIndex, int birdIndex)
    {
        if (playerIndex < 0 || playerIndex >= chosenBirdIndices.Count) return;
        if (birdIndex < 0 || birdIndex >= availableBirds.Count) return;

        chosenBirdIndices[playerIndex] = birdIndex;
        playerReady[playerIndex] = true; // ready when they pick a bird
        UpdatePlayerReadyUI(playerIndex);
        UpdatePlayerBirdUI(playerIndex);
        CheckAllPlayersReady(); // update go button visibility
    }

    private void CheckAllPlayersReady()
    {
        bool all = true;
        for (int i = 0; i < playerReady.Count; ++i)
        {
            if (!playerReady[i])
            {
                all = false;
                break;
            }
        }
        if (goButton != null)
            goButton.enabled = all;

        if (!all)
        {
            for (int i = 0; i < playerReady.Count; ++i)
                if (!playerReady[i])
                    Debug.Log($"Player {i + 1} is not ready yet.");
        }
        else
        {
            Debug.Log("All players ready - GO button shown");
        }
    }

    private void UpdatePlayerReadyUI(int playerIndex)
    {
        RawImage img = playerIndex switch
        {
            0 => p1Ready,
            1 => p2Ready,
            2 => p3Ready,
            3 => p4Ready,
            _ => null
        };
        if (img != null)
            img.enabled = playerReady[playerIndex];
    }

    // Get the bird type that the specified player has chosen.
    public BirdType GetSelectedBird(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= chosenBirdIndices.Count) return BirdType.OTHER;
        int idx = chosenBirdIndices[playerIndex];
        if (idx < 0 || idx >= availableBirds.Count) return BirdType.OTHER;
        return availableBirds[idx];
    }

    /// <summary>
    /// Updates the player icon when a bird is selected.
    /// </summary>
    /// <param name="playerIndex"></param>
    protected virtual void UpdatePlayerBirdUI(int playerIndex)

    {
        if (playerIndex < 0 || playerIndex >= chosenBirdIndices.Count) return;

        // Get the bird type for this player
        BirdType selectedBird = availableBirds[chosenBirdIndices[playerIndex]];

        // Play happy sound for selected bird
        AudioManager.PlayBirdSound(selectedBird, SoundType.HAPPY);

        // Get the RawImage for this bird
        RawImage birdRawImage = GetBirdTexture(selectedBird);

        // Get the icon for this player
        RawImage playerIcon = GetPlayerIcon(playerIndex);

        // Update the icon texture from the bird RawImage
        if (playerIcon != null && birdRawImage != null)
        {
            playerIcon.texture = birdRawImage.texture;

            // preserve the original RawImage scaling/size
            RectTransform birdRect = birdRawImage.GetComponent<RectTransform>();
            RectTransform iconRect = playerIcon.GetComponent<RectTransform>();
            if (birdRect != null && iconRect != null)
            {
                // double the original dimensions
                iconRect.sizeDelta = birdRect.sizeDelta * 1.2f;
                iconRect.localScale = birdRect.localScale * 1.2f;
            }
        }

        // Update player name text
        string birdName = selectedBird.ToString();
        TMP_Text nameText = playerIndex switch
        {
            0 => blue1Name,
            1 => blue2Name,
            2 => pink1Name,
            3 => pink2Name,
            _ => null
        };
        if (nameText != null)
            nameText.text = birdName;
    }

    /// <summary>
    /// Returns the RawImage icon for a given player.
    /// </summary>
    private RawImage GetPlayerIcon(int playerIndex)
    {
        return playerIndex switch
        {
            0 => blue1Icon,
            1 => blue2Icon,
            2 => pink1Icon,
            3 => pink2Icon,
            _ => null
        };
    }

    /// <summary>
    /// Returns the texture for a given bird type.
    /// </summary>
    private RawImage GetBirdTexture(BirdType birdType)
    {
        return birdType switch
        {
            BirdType.PENGUIN => penguinTexture,
            BirdType.CROW => crowTexture,
            BirdType.SCISSORTAIL => scissortailTexture,
            BirdType.LOVEBIRD => lovebirdTexture,
            BirdType.DODO => dodoTexture,
            BirdType.PELICAN => pelicanTexture,
            BirdType.SEAGULL => seagullTexture,
            BirdType.OWL => owlTexture,
            BirdType.TOUCAN => toucanTexture,
            BirdType.PUKEKO => pukekoTexture,
            BirdType.KIWI => kiwiTexture,
            _ => null
        };
    }

    // This is pretty much just data transfer to the multiplayer manager
    public void BeginMatch()
    {
        // Hide the character select screen
        mainCanvas.enabled = false;

        // copy lists to the global transfer object; multiplayer manager will read them
        DataTransferManager.isKBMInput = new List<bool>(isKBMInput);
        DataTransferManager.selectedBirds = new List<BirdType>();
        for (int i = 0; i < numberOfPlayers; ++i)
            DataTransferManager.selectedBirds.Add(GetSelectedBird(i));

        SceneManager.LoadScene(mainSceneName);
    }

    public bool IsPlayerReady(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= playerReady.Count) return false;
        return playerReady[playerIndex];
    }

    /// <summary>
    /// Once again, this is just a placeholder for now since we don't have a lot of UI elements yet,
    /// but this is where you would update any UI to reflect whether the player is ready or not when they press the ready button.
    /// Again, override this in the future.
    /// </summary>
    public void SetPlayerReady(int playerIndex, bool ready)
    {
        if (playerIndex < 0 || playerIndex >= playerReady.Count) return;
        playerReady[playerIndex] = ready;
    }

    /// <summary>
    /// Navigates back to the main menu scene.
    /// </summary>
    public void NavigateBackToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
