using System.Collections;
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
    public Transform cursor1Prefab;
    public Transform cursor2Prefab;
    public Transform cursor3Prefab;
    public Transform cursor4Prefab;
    public Button readyButton;

    [Header("Player Icons")]
    public RawImage blue1Icon;
    public RawImage blue2Icon;
    public RawImage pink1Icon;
    public RawImage pink2Icon;

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
    public RawImage chickenTexture;
    public RawImage ostrichTexture;
    public RawImage eagleTexture;
    public RawImage macawTexture;
    public RawImage phoenixTexture;
    public RawImage robopigeonTexture;
    public RawImage hummingbirdTexture;
    public RawImage shimaenagaTexture;
    public RawImage randomTexture;

    [Header("Bird Database")]
    [SerializeField] private BirdDatabase database; // Holds all the bird data (used for bird stat overlay)
    
    [Header("Player Overlays")]
    [SerializeField] private CanvasGroup p1Overlay; // Stat overlay for player 1

    [Header("Stat Indicators")]
    [SerializeField] private Texture spIndicator; // Ground speed texture
    [SerializeField] private Texture jIndicator; // Jump force texture
    [SerializeField] private Texture strIndicator; // Strength texture
    [SerializeField] private Texture eIndicator; // Empty texture

    [Header("Ready Indicators")]
    public RawImage p1Ready;
    public RawImage p2Ready;
    public RawImage p3Ready;
    public RawImage p4Ready;

    [Header("Go Button")]
    public RawImage goButton;

    [Header("Cursor Animation")]
    [Range(0.1f, 0.99f)]
    public float cursorPressScale = 0.65f;

    [Tooltip("Seconds to shrink down on press")]
    public float cursorShrinkDuration = 0.07f;

    [Tooltip("Seconds to bounce back after the press")]
    public float cursorBounceDuration = 0.14f;

    [Range(1.0f, 1.5f)]
    public float cursorBounceOvershoot = 1.15f;

    [Header("Cursor Snap & Movement")]
    [Tooltip("Seconds to interpolate the cursor between positions")]
    public float cursorMoveSmoothTime = 0.06f;

    [Tooltip("Minimum stick magnitude to trigger a snap move")]
    public float inputDeadzone = 0.5f;

    [Tooltip("Minimum seconds between directional snap moves (per player)")]
    public float inputRepeatDelay = 0.12f;

    [Tooltip("Bobbing amplitude in pixels when hovering over a target")]
    public float hoverBobAmplitude = 6f;

    [Tooltip("Bobbing frequency in Hz when hovering over a target")]
    public float hoverBobFrequency = 3.0f;

    [Tooltip("Seconds to smooth cursor rotation when multiple players overlap")]
    public float cursorRotationSmoothTime = 0.08f;

    [Header("Bird Icon Bounce")]
    [SerializeField] private float iconShrinkDuration = 0.08f;
    [SerializeField] private float iconBounceDuration = 0.2f;
    [SerializeField] private float iconPressScale = 0.75f;
    [SerializeField] private float iconBounceOvershoot = 1.15f;

    private Coroutine[] iconBounceCoroutines = new Coroutine[4];

    // per-player data maintained while on the select screen
    private List<int> chosenBirdIndices = new();
    private List<bool> isKBMInput = new();
    private List<bool> playerReady = new();
    private List<Transform> playerCursors = new();
    private List<PlayerInputState> playerInputStates = new();

    // UI targets (bird buttons + other buttons) we can snap to
    private List<RectTransform> uiTargets = new();
    // Parallel list of Selectable components for navigation-based movement
    private List<Selectable> uiSelectables = new();

    // Per-player runtime state for snapping and smoothing
    private int[] currentTargetIndex = new int[4];
    private float[] lastMoveTime = new float[4];
    private Vector2[] desiredScreenPositions = new Vector2[4];
    private bool[] isHoveringTarget = new bool[4];
    // Per-player currently selected Selectable (for navigation)
    private Selectable[] currentSelectable = new Selectable[4];
    // Preserve the original scale of each cursor instance.
    private Vector3[] cursorBaseScales = new Vector3[4];

    // One coroutine slot per player — stops any in-progress animation before starting a new one
    private readonly Coroutine[] cursorAnimCoroutines = new Coroutine[4];

    // name of the scene to load once selections are done (MAKE SURE THIS MATCHES MULTIPLAYER MANAGER AND CHANGES WHEN NEEDED)
    private const string mainSceneName = "HowToPlay";

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
            if (DataTransferManager.isKBMInput == null) DataTransferManager.isKBMInput = new List<bool>();
            if (DataTransferManager.selectedBirds == null) DataTransferManager.selectedBirds = new List<BirdType>();
        }

        ResizePlayerLists(numberOfPlayers);
        SetupPlayerInputStates();

        // Subscribe to device changes to handle disconnects and reconnects during character select
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void Start()
    {
        CreatePlayerCursors();

        if (readyButton != null) readyButton.onClick.AddListener(CheckAllPlayersReady);

        // Build list of selectable UI targets and initialize per-player snap state
        CollectUITargets();
        for (int i = 0; i < currentTargetIndex.Length; ++i)
        {
            currentTargetIndex[i] = -1;
            lastMoveTime[i] = -999f;
            desiredScreenPositions[i] = new Vector2(Screen.width / 2f, Screen.height / 2f);
            isHoveringTarget[i] = false;
        }

        // Initialize each player's starting target to the closest to screen center
        Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);
        for (int i = 0; i < playerInputStates.Count; ++i)
        {
            int selIdx = FindClosestSelectableIndex(center);
            if (selIdx >= 0)
            {
                currentTargetIndex[i] = selIdx;
                currentSelectable[i] = uiSelectables[selIdx];
                desiredScreenPositions[i] = GetPreferredScreenPosition(uiTargets[selIdx]);
            }
        }

        if (p1Ready != null) p1Ready.enabled = false;
        if (p2Ready != null) p2Ready.enabled = false;
        if (p3Ready != null) p3Ready.enabled = false;
        if (p4Ready != null) p4Ready.enabled = false;
        if (goButton != null) goButton.enabled = false;

        // Ensure that overlays are not visible
        // p1Overlay.alpha = 0f;
    }

    private void OnDestroy()
    {
        // Always unsubscribe to avoid stale callbacks after scene unload
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void Update()
    {
        // Back button: Escape/Backspace (keyboard)
        if (Keyboard.current != null &&
            (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame))
        {
            NavigateBackToMainMenu();
            return;
        }

        // Update cursor positions and handle input for each player
        for (int i = 0; i < playerInputStates.Count; ++i)
        {
            UpdatePlayerInput(i);
            UpdatePlayerCursor(i);
        }
    }

    // Handles controller disconnect and reconnect events during character select
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is not Gamepad gamepad) return;

        switch (change)
        {
            case InputDeviceChange.Disconnected:
                Debug.LogWarning($"[CharacterSelectManager] Gamepad disconnected: {gamepad.displayName}");
                // Find the player whose device this was and clear it so UpdatePlayerInput doesn't
                // read from a stale device reference, freezing their cursor silently
                foreach (PlayerInputState state in playerInputStates)
                {
                    if (state.device == gamepad)
                    {
                        state.device = null;
                        Debug.LogWarning($"[CharacterSelectManager] Cleared device for Player {state.playerIndex + 1} — awaiting reconnect.");
                        break;
                    }
                }
                break;

            case InputDeviceChange.Reconnected:
                Debug.Log($"[CharacterSelectManager] Gamepad reconnected: {gamepad.displayName}");
                // Re-pair the reconnected gamepad to whichever player lost their device
                foreach (PlayerInputState state in playerInputStates)
                {
                    if (!state.isKBM && state.device == null)
                    {
                        state.device = gamepad;
                        Debug.Log($"[CharacterSelectManager] Re-paired {gamepad.displayName} to Player {state.playerIndex + 1}");
                        break;
                    }
                }
                break;
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
        if (DataTransferManager.isKBMInput != null && DataTransferManager.isKBMInput.Count == numberOfPlayers)
        {
            int gamepadIndex = 0;
            for (int i = 0; i < numberOfPlayers; ++i)
            {
                bool kbm = DataTransferManager.isKBMInput[i];
                InputDevice dev;
                if (kbm)
                {
                    dev = Keyboard.current;
                }
                else if (gamepadIndex < Gamepad.all.Count)
                {
                    dev = Gamepad.all[gamepadIndex++];
                }
                else
                {
                    // No gamepad available for this player slot — log clearly instead of silently assigning null
                    Debug.LogWarning($"[CharacterSelectManager] No gamepad available for Player {i + 1}. Their cursor will be inactive until a controller is connected.");
                    dev = null;
                }
                playerInputStates.Add(new PlayerInputState(i, kbm, dev));
            }
            isKBMInput = new List<bool>(DataTransferManager.isKBMInput);
        }
        else
        {
            for (int i = 0; i < numberOfPlayers; ++i)
            {
                InputDevice dev;
                if (i < Gamepad.all.Count)
                {
                    dev = Gamepad.all[i];
                }
                else
                {
                    // No gamepad available for this player slot — log clearly instead of silently assigning null
                    Debug.LogWarning($"[CharacterSelectManager] No gamepad available for Player {i + 1}. Their cursor will be inactive until a controller is connected.");
                    dev = null;
                }
                playerInputStates.Add(new PlayerInputState(i, false, dev));
            }
            isKBMInput.Clear();
            foreach (var state in playerInputStates) isKBMInput.Add(state.isKBM);
        }
    }

    private void CreatePlayerCursors()
    {
        Transform[] cursorPrefabs = new Transform[] { cursor1Prefab, cursor2Prefab, cursor3Prefab, cursor4Prefab };

        playerCursors.Clear();

        for (int i = 0; i < numberOfPlayers; ++i)
        {
            Transform prefab = (i < cursorPrefabs.Length) ? cursorPrefabs[i] : null;
            if (prefab == null)
            {
                Debug.LogWarning($"Cursor prefab for player {i + 1} not assigned in CharacterSelectManager!");
                // Push a null placeholder so playerCursors stays index-aligned with playerInputStates
                playerCursors.Add(null);
                continue;
            }

            Transform cursor = Instantiate(prefab, mainCanvas.transform);
            cursor.name = $"Cursor_Player{i}";

            // Color cursors per player
            Image cursorImage = cursor.GetComponent<Image>();
            if (cursorImage != null)
                cursorImage.color = GetPlayerColor(i);

            // Pivot (0, 1) = top-left corner.
            RectTransform rt = cursor.GetComponent<RectTransform>();
            if (rt != null)
                rt.pivot = new Vector2(0f, 1f);

            cursorBaseScales[i] = cursor.localScale;
            playerCursors.Add(cursor);
        }
    }

    private void UpdatePlayerInput(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= playerInputStates.Count) return;
        PlayerInputState state = playerInputStates[playerIndex];

        if (state.isKBM)
        {
            state.cursorPosition = Mouse.current.position.ReadValue();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                PlayCursorPressAnimation(playerIndex);
                HandlePlayerButtonPress(playerIndex);
            }
        }
        else
        {
            Gamepad pad = state.device as Gamepad;
            if (pad != null)
            {
                // Read stick direction but do not freely move the cursor. Directional input
                // will be used to snap between selectable UI targets instead.
                state.inputDirection = pad.leftStick.ReadValue();

                if (pad.aButton.wasPressedThisFrame)
                {
                    // Snap cursor position to current desired target before pressing
                    if (currentTargetIndex[playerIndex] >= 0)
                        state.cursorPosition = desiredScreenPositions[playerIndex];

                    PlayCursorPressAnimation(playerIndex);
                    bool activated = false;
                    int targetIndex = currentTargetIndex[playerIndex];
                    if (targetIndex >= 0)
                        activated = TryActivateTargetAtIndex(targetIndex, playerIndex);

                    if (!activated)
                        HandlePlayerButtonPress(playerIndex);
                }

                if (pad.startButton.wasPressedThisFrame)
                    playerReady[playerIndex] = !playerReady[playerIndex];
            }
        }
    }

    private void UpdatePlayerCursor(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= playerCursors.Count) return;
        if (playerIndex >= playerInputStates.Count) return;

        // Guard against null placeholder slots (cursor prefab was missing for this player)
        Transform cursor = playerCursors[playerIndex];
        if (cursor == null) return;

        PlayerInputState state = playerInputStates[playerIndex];

        // If this player is using KBM, keep mouse behavior unchanged
        if (state.isKBM)
        {
            Vector2 screenPos = state.cursorPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mainCanvas.GetComponent<RectTransform>(),
                screenPos,
                mainCanvas.worldCamera,
                out Vector2 localPosKBM
            );
            cursor.GetComponent<RectTransform>().localPosition = localPosKBM;
            return;
        }

        // Gamepad: handle directional snapping
        Vector2 stick = state.inputDirection;
        float mag = stick.magnitude;

        // When stick is pushed beyond deadzone, attempt to navigate using Selectable.navigation
        if (mag >= inputDeadzone && Time.time - lastMoveTime[playerIndex] >= inputRepeatDelay && uiSelectables.Count > 0)
        {
            Selectable cur = currentSelectable[playerIndex];
            if (cur == null)
            {
                int selIdx = FindClosestSelectableIndex(state.cursorPosition);
                if (selIdx >= 0)
                {
                    cur = uiSelectables[selIdx];
                    currentSelectable[playerIndex] = cur;
                    currentTargetIndex[playerIndex] = selIdx;
                    desiredScreenPositions[playerIndex] = GetPreferredScreenPosition(uiTargets[selIdx]);
                }
            }

            if (cur != null)
            {
                Navigation nav = cur.navigation;
                Selectable next = null;
                // Determine primary axis from stick
                if (Mathf.Abs(stick.x) > Mathf.Abs(stick.y))
                {
                    next = (stick.x > 0f) ? nav.selectOnRight : nav.selectOnLeft;
                }
                else
                {
                    next = (stick.y > 0f) ? nav.selectOnUp : nav.selectOnDown;
                }

                if (next != null)
                {
                    int idx = uiSelectables.IndexOf(next);
                    if (idx >= 0 && idx != currentTargetIndex[playerIndex])
                    {
                        currentTargetIndex[playerIndex] = idx;
                        currentSelectable[playerIndex] = next;
                        desiredScreenPositions[playerIndex] = GetPreferredScreenPosition(uiTargets[idx]);
                        isHoveringTarget[playerIndex] = false;
                        lastMoveTime[playerIndex] = Time.time;
                        // Also update EventSystem selection for consistency
                        if (EventSystem.current != null)
                            EventSystem.current.SetSelectedGameObject(next.gameObject);
                    }
                }
            }
        }

        // If we have a target, compute desired position; otherwise hold current position
        Vector2 desiredScreen = state.cursorPosition;
        int tidx = currentTargetIndex[playerIndex];
        if (tidx >= 0 && tidx < uiTargets.Count)
        {
            desiredScreen = GetPreferredScreenPosition(uiTargets[tidx]);
            desiredScreenPositions[playerIndex] = desiredScreen;
        }

        // Smoothly interpolate the internal cursor screen position toward desiredScreen
        float t = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.0001f, cursorMoveSmoothTime));
        state.cursorPosition = Vector2.Lerp(state.cursorPosition, desiredScreenPositions[playerIndex], t);

        // Check hovering threshold (close enough to snap into hover state)
        float hoverThreshold = 10f;
        float dist = Vector2.Distance(state.cursorPosition, desiredScreenPositions[playerIndex]);
        isHoveringTarget[playerIndex] = (tidx >= 0 && dist <= hoverThreshold);

        Vector2 finalScreenPos = state.cursorPosition;

        // When hovering, pin to preferred position and add diagonal bobbing
        if (isHoveringTarget[playerIndex])
        {
            finalScreenPos = desiredScreenPositions[playerIndex];
            float phase = (Time.time * hoverBobFrequency) % 1f;
            float intensity;
            if (phase < 0.5f)
            {
                float normalized = phase / 0.5f;
                intensity = normalized * normalized; // accelerate into the button
            }
            else
            {
                float normalized = (phase - 0.5f) / 0.5f;
                intensity = 1f - (normalized * normalized); // decelerate away
            }
            Vector2 bob = new Vector2(-intensity, -intensity) * hoverBobAmplitude;
            finalScreenPos += bob;

            // Clamp to screen bounds
            float margin = 4f;
            finalScreenPos.x = Mathf.Clamp(finalScreenPos.x, margin, Screen.width - margin);
            finalScreenPos.y = Mathf.Clamp(finalScreenPos.y, margin, Screen.height - margin);
        }

        // Convert final screen position to canvas local and apply
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mainCanvas.GetComponent<RectTransform>(),
            finalScreenPos,
            mainCanvas.worldCamera,
            out Vector2 localPos
        );

        cursor.GetComponent<RectTransform>().localPosition = localPos;

        // If multiple players hover the same target, rotate each cursor around its top-left anchor
        if (tidx >= 0 && isHoveringTarget[playerIndex])
        {
            List<int> overlappingPlayers = new List<int>();
            for (int i = 0; i < playerInputStates.Count; ++i)
            {
                if (i == playerIndex) continue;
                if (playerInputStates[i] == null) continue;
                if (currentTargetIndex[i] == tidx && isHoveringTarget[i])
                    overlappingPlayers.Add(i);
            }

            Quaternion desiredRotation = Quaternion.identity;
            if (overlappingPlayers.Count > 0)
            {
                overlappingPlayers.Add(playerIndex);
                overlappingPlayers.Sort();
                int position = overlappingPlayers.IndexOf(playerIndex);
                int count = overlappingPlayers.Count;
                float angle = (position - (count - 1) * 0.5f) * 30f;
                desiredRotation = Quaternion.Euler(0f, 0f, angle);
            }

            RectTransform cursorRect = cursor.GetComponent<RectTransform>();
            cursorRect.localRotation = Quaternion.Slerp(cursorRect.localRotation, desiredRotation, Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.0001f, cursorRotationSmoothTime)));
        }
        else
        {
            RectTransform cursorRect = cursor.GetComponent<RectTransform>();
            cursorRect.localRotation = Quaternion.Slerp(cursorRect.localRotation, Quaternion.identity, Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.0001f, cursorRotationSmoothTime)));
        }
    }

    // Collect BirdSelectButton rects and other Buttons under the main canvas as snap targets
    private void CollectUITargets()
    {
        uiTargets.Clear();

        uiTargets.Clear();
        uiSelectables.Clear();

        // Prefer Selectable-based navigation (Buttons, Toggle, etc.) under the main canvas
        if (mainCanvas != null)
        {
            Selectable[] selectables = mainCanvas.GetComponentsInChildren<Selectable>(true);
            foreach (var s in selectables)
            {
                RectTransform rt = s.GetComponent<RectTransform>();
                if (rt != null && !uiTargets.Contains(rt))
                {
                    uiTargets.Add(rt);
                    uiSelectables.Add(s);
                }
            }
        }

        // Fallback: include BirdSelectButton instances as rect targets if not already present
        BirdSelectButton[] birdButtons = FindObjectsOfType<BirdSelectButton>(true);
        foreach (var b in birdButtons)
        {
            RectTransform rt = b.GetComponent<RectTransform>();
            if (rt != null && !uiTargets.Contains(rt))
            {
                uiTargets.Add(rt);
                uiSelectables.Add(null);
            }
        }
    }

    // Returns the preferred screen-space position for a target: use the transform center and clamp to screen bounds.
    private Vector2 GetPreferredScreenPosition(RectTransform rt)
    {
        if (rt == null) return new Vector2(Screen.width / 2f, Screen.height / 2f);

        Vector2 center = RectTransformUtility.WorldToScreenPoint(mainCanvas.worldCamera, rt.position);
        float margin = 6f;
        center.x = Mathf.Clamp(center.x, margin, Screen.width - margin);
        center.y = Mathf.Clamp(center.y, margin, Screen.height - margin);
        return center;
    }

    // Find the closest uiTargets index from a given screen point biased by an optional direction.
    // If direction magnitude is > 0, prefer targets that are roughly in that direction from the fromPoint.
    private int FindClosestTargetIndex(Vector2 fromPoint, Vector2 direction)
    {
        if (uiTargets == null || uiTargets.Count == 0) return -1;

        int bestIndex = -1;
        float bestScore = float.MaxValue;

        Vector2 dir = direction.normalized;
        bool useDir = direction.sqrMagnitude > 0.001f;

        for (int i = 0; i < uiTargets.Count; ++i)
        {
            RectTransform rt = uiTargets[i];
            Vector2 targetScreen = GetPreferredScreenPosition(rt);
            Vector2 toTarget = targetScreen - fromPoint;
            float dist = toTarget.sqrMagnitude;

            float score = dist;

            if (useDir)
            {
                Vector2 toDir = toTarget.normalized;
                float dot = Vector2.Dot(dir, toDir);
                // Favor targets that are in roughly the same direction (dot near 1).
                // Subtract from score so higher dot = lower score.
                score = dist * (1f - Mathf.Clamp01((dot + 1f) / 2f));
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    // Find the closest selectable index to a screen point (used for initial placement and fallbacks)
    private int FindClosestSelectableIndex(Vector2 fromPoint)
    {
        if (uiTargets == null || uiTargets.Count == 0) return -1;
        int best = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < uiTargets.Count; ++i)
        {
            Vector2 p = GetPreferredScreenPosition(uiTargets[i]);
            float d = (p - fromPoint).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    // Cursor press animation
    // Triggers the shrink → bounce animation for a player's cursor.
    private void PlayCursorPressAnimation(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= playerCursors.Count) return;

        // Guard against null placeholder slots (cursor prefab was missing for this player)
        if (playerCursors[playerIndex] == null) return;

        if (cursorAnimCoroutines[playerIndex] != null)
        {
            StopCoroutine(cursorAnimCoroutines[playerIndex]);
            playerCursors[playerIndex].localScale = Vector3.one; // reset before restarting
        }

        cursorAnimCoroutines[playerIndex] = StartCoroutine(CursorPressRoutine(playerIndex));
    }

    private IEnumerator CursorPressRoutine(int playerIndex)
    {
        Transform cursor = playerCursors[playerIndex];
        Vector3 baseScale = cursorBaseScales[playerIndex];
        Vector3 pressedScale = baseScale * cursorPressScale;

        // Phase 1 — shrink down to cursorPressScale * base scale
        float elapsed = 0f;
        while (elapsed < cursorShrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / cursorShrinkDuration);
            cursor.localScale = Vector3.Lerp(baseScale, pressedScale, t);
            yield return null;
        }
        cursor.localScale = pressedScale;

        // Phase 2 — bounce back, briefly overshooting base scale before settling
        elapsed = 0f;
        while (elapsed < cursorBounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / cursorBounceDuration);
            Vector3 baseLerp = Vector3.Lerp(pressedScale, baseScale, t);
            float overshoot = Mathf.Sin(t * Mathf.PI) * (cursorBounceOvershoot - 1f);
            cursor.localScale = baseLerp + baseScale * overshoot;
            yield return null;
        }

        cursor.localScale = baseScale; // snap clean
        cursorAnimCoroutines[playerIndex] = null;
    }

    private void HandlePlayerButtonPress(int playerIndex)
    {
        if (playerIndex >= playerInputStates.Count) return;

        int targetIndex = currentTargetIndex[playerIndex];
        if (targetIndex >= 0 && TryActivateTargetAtIndex(targetIndex, playerIndex))
            return;

        Vector2 screenPos = playerInputStates[playerIndex].cursorPosition;
        PointerEventData pointerData = new(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            BirdSelectButton birdButton = result.gameObject.GetComponent<BirdSelectButton>();
            if (birdButton != null)
            {
                birdButton.OnPressed(playerIndex);
                return;
            }

            Button uiButton = result.gameObject.GetComponent<Button>();
            if (uiButton != null)
            {
                uiButton.onClick.Invoke();
                return;
            }
        }
    }

    private bool TryActivateTargetAtIndex(int index, int playerIndex)
    {
        if (index < 0 || index >= uiTargets.Count) return false;

        Selectable selectable = (index < uiSelectables.Count) ? uiSelectables[index] : null;
        if (TryActivateSelectable(selectable, playerIndex))
            return true;

        RectTransform rt = uiTargets[index];
        if (rt == null) return false;

        BirdSelectButton birdButton = rt.GetComponent<BirdSelectButton>();
        if (birdButton != null)
        {
            birdButton.OnPressed(playerIndex);
            return true;
        }

        Button uiButton = rt.GetComponent<Button>();
        if (uiButton != null)
        {
            uiButton.onClick.Invoke();
            return true;
        }

        // Also check children if the button component is nested.
        birdButton = rt.GetComponentInChildren<BirdSelectButton>(true);
        if (birdButton != null)
        {
            birdButton.OnPressed(playerIndex);
            return true;
        }

        uiButton = rt.GetComponentInChildren<Button>(true);
        if (uiButton != null)
        {
            uiButton.onClick.Invoke();
            return true;
        }

        return false;
    }

    private bool TryActivateSelectable(Selectable selectable, int playerIndex)
    {
        if (selectable == null) return false;

        if (selectable.TryGetComponent<BirdSelectButton>(out BirdSelectButton birdButton))
        {
            birdButton.OnPressed(playerIndex);
            return true;
        }

        if (selectable.TryGetComponent<Button>(out Button uiButton))
        {
            uiButton.onClick.Invoke();
            return true;
        }

        // Use EventSystem submit as a final fallback for interactable Selectables.
        if (EventSystem.current != null && selectable.IsInteractable())
        {
            BaseEventData eventData = new BaseEventData(EventSystem.current);
            ExecuteEvents.Execute(selectable.gameObject, eventData, ExecuteEvents.submitHandler);
            return true;
        }

        return false;
    }

    public void ResizePlayerLists(int count)
    {
        numberOfPlayers = Mathf.Clamp(count, 1, 4);

        while (chosenBirdIndices.Count < numberOfPlayers) chosenBirdIndices.Add(0);
        while (isKBMInput.Count < numberOfPlayers)        isKBMInput.Add(true);
        while (playerReady.Count < numberOfPlayers)        playerReady.Add(false);
        while (chosenBirdIndices.Count > numberOfPlayers) chosenBirdIndices.RemoveAt(chosenBirdIndices.Count - 1);
        while (isKBMInput.Count > numberOfPlayers)         isKBMInput.RemoveAt(isKBMInput.Count - 1);
        while (playerReady.Count > numberOfPlayers)        playerReady.RemoveAt(playerReady.Count - 1);
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

    public void SetPlayerBirdIndex(int playerIndex, int birdIndex)
    {
        if (playerIndex < 0 || playerIndex >= chosenBirdIndices.Count) return;
        if (birdIndex < 0 || birdIndex >= availableBirds.Count) return;

        chosenBirdIndices[playerIndex] = birdIndex;
        playerReady[playerIndex] = true;
        UpdatePlayerReadyUI(playerIndex);
        UpdatePlayerBirdUI(playerIndex);
        UpdatePlayerOverlay(playerIndex);
        CheckAllPlayersReady();
    }

    private void CheckAllPlayersReady()
    {
        bool all = true;
        for (int i = 0; i < playerReady.Count; ++i)
        {
            if (!playerReady[i]) { all = false; break; }
        }

        if (goButton != null) goButton.enabled = all;

        if (!all)
            for (int i = 0; i < playerReady.Count; ++i)
                if (!playerReady[i]) Debug.Log($"Player {i + 1} is not ready yet.");
        else
            Debug.Log("All players ready - GO button shown");
            AudioManager.PlayBuffStartSound();
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
        if (img != null) img.enabled = playerReady[playerIndex];
    }

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
    protected virtual void UpdatePlayerBirdUI(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= chosenBirdIndices.Count) return;

        BirdType selectedBird = availableBirds[chosenBirdIndices[playerIndex]];
        AudioManager.PlayBirdSound(selectedBird, SoundType.HAPPY);

        RawImage birdRawImage = GetBirdTexture(selectedBird);
        RawImage playerIcon = GetPlayerIcon(playerIndex);

        if (playerIcon != null && birdRawImage != null)
        {
            playerIcon.texture = birdRawImage.texture;

            RectTransform birdRect = birdRawImage.GetComponent<RectTransform>();
            RectTransform iconRect = playerIcon.GetComponent<RectTransform>();
            if (birdRect != null && iconRect != null)
            {
                iconRect.sizeDelta = birdRect.sizeDelta * 1.2f;
                iconRect.localScale = birdRect.localScale * 1.2f;
            }
        }

        string birdName = selectedBird.ToString();
        TMP_Text nameText = playerIndex switch
        {
            0 => blue1Name,
            1 => blue2Name,
            2 => pink1Name,
            3 => pink2Name,
            _ => null
        };
        if (nameText != null) nameText.text = birdName;

        // Trigger bounce on the player icon
        if (playerIcon != null)
        {
            if (iconBounceCoroutines[playerIndex] != null)
                StopCoroutine(iconBounceCoroutines[playerIndex]);
            iconBounceCoroutines[playerIndex] = StartCoroutine(BirdIconBounceRoutine(playerIndex, playerIcon.GetComponent<RectTransform>()));
        }
    }

    private IEnumerator BirdIconBounceRoutine(int playerIndex, RectTransform iconRect)
    {
        if (iconRect == null) yield break;

        Vector3 originalScale = iconRect.localScale;

        // Phase 1 — shrink down
        float elapsed = 0f;
        while (elapsed < iconShrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / iconShrinkDuration);
            float s = Mathf.Lerp(1f, iconPressScale, t);
            iconRect.localScale = originalScale * s;
            yield return null;
        }
        iconRect.localScale = originalScale * iconPressScale;

        // Phase 2 — bounce back with overshoot
        // sin(t * π) peaks at t = 0.5, giving a smooth overshoot arc
        elapsed = 0f;
        while (elapsed < iconBounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / iconBounceDuration);
            float baseScale = Mathf.Lerp(iconPressScale, 1f, t);
            float overshoot = Mathf.Sin(t * Mathf.PI) * (iconBounceOvershoot - 1f);
            float s = baseScale + overshoot;
            iconRect.localScale = originalScale * s;
            yield return null;
        }

        iconRect.localScale = originalScale; // snap clean
        iconBounceCoroutines[playerIndex] = null;
    }

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
            BirdType.CHICKEN => chickenTexture,
            BirdType.OSTRICH => ostrichTexture,
            BirdType.EAGLE => eagleTexture,
            BirdType.MACAW => macawTexture,
            BirdType.PHOENIX => phoenixTexture,
            BirdType.ROBOPIGEON => robopigeonTexture,
            BirdType.HUMMINGBIRD => hummingbirdTexture,
            BirdType.SHIMAENAGA => shimaenagaTexture,
            BirdType.OTHER => randomTexture,
            _ => null
        };
    }

    private BirdData GetBirdData(BirdType birdType)
    {
        return birdType switch
        {
            BirdType.PENGUIN => database.GetBirdData("Penguin"),
            BirdType.CROW => database.GetBirdData("Crow"),
            BirdType.SCISSORTAIL => database.GetBirdData("Scissortail"),
            BirdType.LOVEBIRD => database.GetBirdData("Lovebird"),
            BirdType.DODO => database.GetBirdData("Dodo"),
            BirdType.PELICAN => database.GetBirdData("Pelican"),
            BirdType.SEAGULL => database.GetBirdData("Seagull"),
            BirdType.OWL => database.GetBirdData("Owl"),
            BirdType.TOUCAN => database.GetBirdData("Toucan"),
            BirdType.PUKEKO => database.GetBirdData("Pukeko"),
            BirdType.KIWI => database.GetBirdData("Kiwi"),
            BirdType.CHICKEN => database.GetBirdData("Chicken"),
            BirdType.OSTRICH => database.GetBirdData("Ostrich"),
            BirdType.EAGLE => database.GetBirdData("Eagle"),
            BirdType.MACAW => database.GetBirdData("Macaw"),
            BirdType.PHOENIX => database.GetBirdData("Phoenix"),
            BirdType.ROBOPIGEON => database.GetBirdData("31rd"),
            BirdType.HUMMINGBIRD => database.GetBirdData("Hummingbird"),
            BirdType.SHIMAENAGA => database.GetBirdData("Shima Enaga"),
            BirdType.OTHER => new BirdData(),
            _ => null
        };
    }

    private CanvasGroup GetPlayerOverlay(int playerIndex)
    {
        return playerIndex switch
        {
            0 => p1Overlay,
            1 => null,
            2 => null,
            3 => null
        };
    }

    private void UpdatePlayerOverlay(int playerIndex)
    {
        // Get the overlay and indicators for this player
        CanvasGroup overlay = GetPlayerOverlay(playerIndex);
        Transform speedIndicators = overlay.transform.Find("SpeedIndicators");
        Transform jumpIndicators = overlay.transform.Find("JumpIndicators");
        Transform strengthIndicators = overlay.transform.Find("StrengthIndicators");

        // Get the bird data for the chosen bird
        BirdType bird = GetSelectedBird(playerIndex);
        BirdData data = GetBirdData(bird);

        // Update overlay using bird data
        for (int i = 0; i < 10; i++)
        {
            // Speed
            if (i < data.groundSpeed)
            {
                speedIndicators.Find("Speed" + (i + 1)).GetComponent<RawImage>().texture = spIndicator;
            }
            else
            {
                speedIndicators.Find("Speed" + (i + 1)).GetComponent<RawImage>().texture = eIndicator;
            }

            // Jump
            if (i < data.jumpForce)
            {
                jumpIndicators.Find("Jump" + (i + 1)).GetComponent<RawImage>().texture = jIndicator;
            }
            else
            {
                jumpIndicators.Find("Jump" + (i + 1)).GetComponent<RawImage>().texture = eIndicator;
            }

            // Strength
            if (i < data.strength)
            {
                strengthIndicators.Find("Strength" + (i + 1)).GetComponent<RawImage>().texture = strIndicator;
            }
            else
            {
                strengthIndicators.Find("Strength" + (i + 1)).GetComponent<RawImage>().texture = eIndicator;
            }
        }
    }

    public void BeginMatch()
    {
        // Ensure every player has actually chosen a bird before allowing the match to start
        for (int i = 0; i < numberOfPlayers; ++i)
        {
            if (!playerReady[i])
            {
                Debug.LogWarning($"Cannot start match — Player {i + 1} has not selected a bird yet.");
                return;
            }
        }

        mainCanvas.enabled = false;

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

    public void NavigateBackToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}