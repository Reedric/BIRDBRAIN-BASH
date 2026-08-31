using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class AviaryManager : MonoBehaviour
{
    [SerializeField] private Camera mainCamera; // Camera in scene
    [SerializeField] private float totalTransitionTime; // How long a transition will take in total
    [SerializeField] private BirdDatabase birds; // Where all the bird information is stored

    [Header("Bird Models")] // IMPORTANT: Use contact point as some transforms are in the ground (hopefully eye level with bird)
    [SerializeField] private GameObject penguin; // Penguin model
    [SerializeField] private GameObject owl; // Owl model
    [SerializeField] private GameObject pelican; // Pelican model
    [SerializeField] private GameObject toucan; // Toucan model
    [SerializeField] private GameObject lovebird; // Lovebird model
    [SerializeField] private GameObject seagull; // Seagull model
    [SerializeField] private GameObject kiwi; // Kiwi model
    [SerializeField] private GameObject dodo; // Dodo model
    [SerializeField] private GameObject pukeko; // Pukeko model
    [SerializeField] private GameObject crow; // Crow model
    [SerializeField] private GameObject scissortail; // Scissortail model
    [SerializeField] private GameObject chicken; // Chicken model
    [SerializeField] private GameObject ostrich; // Ostrich model
    [SerializeField] private GameObject eagle; // Eagle model
    [SerializeField] private GameObject macaw; // Macaw model
    [SerializeField] private GameObject phoenix; // Phoenix model
    [SerializeField] private GameObject robopigeon; // 31rd model
    [SerializeField] private GameObject hummingbird; // Hummingbird model
    [SerializeField] private GameObject shimaEnaga; // Shima Enaga model

    [Header("Menus")]
    [SerializeField] private CanvasGroup birdSelect; // Menu for selecting which bird to view
    [SerializeField] private CanvasGroup birdInfo; // Menu for seeing bird information

    [Header("Bird Information")]
    [SerializeField] private TextMeshProUGUI birdName; // Name of bird to be displayed
    [SerializeField] private TextMeshProUGUI description; // Description of bird to be displayed
    [SerializeField] private RawImage icon; // Icon of bird to display
    [SerializeField] private TextMeshProUGUI offDescription; // Description of offensive ability of bird
    [SerializeField] private RawImage offIcon; // Offensive ability icon of bird
    [SerializeField] private TextMeshProUGUI defDescription; // Description of defensive ability of bird
    [SerializeField] private RawImage defIcon; // Defensive ability icon of bird
    [SerializeField] private GameObject speedIndicators; // How much ground speed bird has
    [SerializeField] private GameObject jumpIndicators; // How much jump force bird has
    [SerializeField] private GameObject strengthIndicators; // How much strength bird has

    [Header("Indicator Prefabs")]
    [SerializeField] private Texture spIndicator; // Texture indicating ground speed
    [SerializeField] private Texture jIndicator; // Texture indicating jump force
    [SerializeField] private Texture strIndicator; // Texture indicating strength
    [SerializeField] private Texture eIndicator; // Texture indicating empty/absence

    private Vector3 birdsEyeViewPos; // The position for the birds eye view
    private Quaternion birdsEyeViewRot; // The orientation of the birds eye view
    private CameraState cameraState; // State the camera is in 
    private float transitionTime = 0f; // Time elapsed for camera transition
    private float distanceFromBird = 3f; // Distance camera will be from bird
    private BirdType birdType; // Bird type for audio manager
    private Vector3 birdPos; // Position of the chosen bird
    private Vector3 birdDir; // Direction in which the bird is facing
    private Vector3 cameraDir; // Direction camera faces for bird (should be opposite of birdDir)

    private enum CameraState
    {
        BirdsEye,
        ToFrontOfBird,
        FrontOfBird,
        ToBirdsEye
    }

    private static AviaryManager instance; // Singleton reference
    public static AviaryManager Instance => instance;

    // All of the bird types that players can choose from (order matches the enum)
    public List<BirdType> availableBirds = new();
    public int numberOfPlayers = 1;
    public Canvas mainCanvas;
    public Transform cursor1Prefab;

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

    private Coroutine iconBounceCoroutines;

    // per-player data maintained while on the select screen
    private int chosenBirdIndex = -1;
    private Transform playerCursor;
    private PlayerInputState playerInputState;

    // UI targets (bird buttons + other buttons) we can snap to
    private List<RectTransform> uiTargets = new();
    // Parallel list of Selectable components for navigation-based movement
    private List<Selectable> uiSelectables = new();

    // Per-player runtime state for snapping and smoothing
    private int currentTargetIndex;
    private float lastMoveTime;
    private Vector2 desiredScreenPosition;
    private bool isHoveringTarget;
    // Per-player currently selected Selectable (for navigation)
    private Selectable currentSelectable;
    private GameObject hoveredUIElement;
    // Preserve the original scale of each cursor instance.
    private Vector3 cursorBaseScale;

    // One coroutine slot per player — stops any in-progress animation before starting a new one
    private Coroutine cursorAnimCoroutine;

    // Name of the main menu scene (update as needed)
    private const string mainMenuSceneName = "MainMenu";

    // Tracks important input info for each player
    private class PlayerInputState
    {
        public bool isKBM;
        public InputDevice device;
        public Vector2 cursorPosition; // Screen space
        public Vector2 inputDirection; // For gamepad stick input
        public bool readyPressed = false;

        public PlayerInputState(bool kbm, InputDevice dev)
        {
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

        CreatePlayerCursor();

        // Subscribe to device changes to handle disconnects and reconnects during character select
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Log assignment errors
        if (mainCamera == null) Debug.LogError("Camera was never assigned for AviaryManager.");
        if (penguin == null) Debug.LogError("Penguin was never assigned for AviaryManager.");

        // Assign variables
        birdsEyeViewPos = mainCamera.transform.position;
        birdsEyeViewRot = mainCamera.transform.rotation;
        birdPos = penguin.transform.position;
        birdDir = Vector3.back;
        cameraDir = Vector3.forward;

        CollectUITargets();
        currentTargetIndex = -1;
        lastMoveTime = -999f;
        desiredScreenPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
        isHoveringTarget = false;
    }

    // Update is called once per frame
    void Update()
    {
        // Update cursor positions and handle input for each player
        UpdatePlayerInput();
        UpdatePlayerCursor();
    

        // Wait for button press to change state
        InputAction action = InputSystem.actions.FindActionMap("UI").FindAction("Cancel");

        if (action.WasPressedThisFrame() && cameraState == CameraState.BirdsEye)
        {
            NavigateBackToMainMenu();
        }
        else if (action.WasPressedThisFrame() && cameraState == CameraState.FrontOfBird)
        {
            // Change to transition to birds eye view
            cameraState = CameraState.ToBirdsEye;
            transitionTime = 0f;
            StartCoroutine(TransitionToSky());
        }

        // If in transition states, transition
        if (cameraState == CameraState.ToFrontOfBird)
        {
            // Increment time elapsed
            transitionTime += Time.deltaTime;

            // Calculate t
            float t = transitionTime / totalTransitionTime;

            // Get destination vector
            Vector3 d = birdPos + birdDir * distanceFromBird;

            // Get quaternions for rotation
            Quaternion a = birdsEyeViewRot;
            a.Normalize();
            Quaternion b = Quaternion.LookRotation(cameraDir);
            b.Normalize();

            // Interpolate position and rotation
            mainCamera.transform.position = Vector3.Slerp(birdsEyeViewPos, d, t);
            mainCamera.transform.rotation = Quaternion.Slerp(a, b, t);

            // If interpolation is complete, change state
            if (transitionTime >= totalTransitionTime)
            {
                cameraState = CameraState.FrontOfBird;
            }
        }
        else if (cameraState == CameraState.ToBirdsEye)
        {
            // Increment time elapsed
            transitionTime += Time.deltaTime;

            // Calculate t
            float t = transitionTime / totalTransitionTime;

            // Get start vector
            Vector3 s = birdPos + birdDir * distanceFromBird;

            // Get quaternions for rotation
            Quaternion a = birdsEyeViewRot;
            a.Normalize();
            Quaternion b = Quaternion.LookRotation(cameraDir);
            b.Normalize();

            // Interpolate position and rotation
            mainCamera.transform.position = Vector3.Slerp(s, birdsEyeViewPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(b, a, t);

            // If interpolation is complete, change state
            if (transitionTime >= totalTransitionTime)
            {
                cameraState = CameraState.BirdsEye;
            }
        }
    }

    private IEnumerator TransitionToBird()
    {
        // Set this bird's information 
        GetBirdInformation();

        // Fade out the Character Select screen
        float half = totalTransitionTime / 2;

        while (transitionTime < half) // Transition time being incremented in Update, should be fine...
        {
            float alpha = Mathf.Max(0f, 1 - (transitionTime / half));

            birdSelect.alpha = alpha;

            yield return null;
        }

        // Fade in the Bird Information screen
        while (transitionTime < totalTransitionTime)
        {
            float alpha = Mathf.Min(1f, (transitionTime - half) / half);

            birdInfo.alpha = alpha;

            yield return null;
        }

        // Play bird happy sound
        AudioManager.PlayBirdSound(birdType, SoundType.HAPPY);
    }

    private IEnumerator TransitionToSky()
    {
        // Fade out the Bird Information screen
        float half = totalTransitionTime / 2;

        while (transitionTime < half) // Transition time being incremented in Update, should be fine...
        {
            float alpha = Mathf.Max(0f, 1 - (transitionTime / half));

            birdInfo.alpha = alpha;

            yield return null;
        }

        // Fade in the Character Select screen
        while (transitionTime < totalTransitionTime)
        {
            float alpha = Mathf.Min(1f, (transitionTime - half) / half);

            birdSelect.alpha = alpha;

            yield return null;
        }
    }

    private void CreatePlayerCursor()
    {
        if (cursor1Prefab == null) return;

        playerCursor = cursor1Prefab;

        Transform cursor = Instantiate(cursor1Prefab, birdSelect.transform);
            cursor.name = $"Cursor_Player1";

        
        RectTransform rt = cursor.GetComponent<RectTransform>();
        if (rt != null)
            rt.pivot = new Vector2(0f, 1f); // Top-left pivot for pointer-style cursor accuracy

        cursorBaseScale = cursor.localScale;

        // Set up player input state
        InputDevice dev;
        bool kbm;
        if (Gamepad.all.Count > 0)
        {
            dev = Gamepad.all[0];
            kbm = false;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            dev = Keyboard.current;
            kbm = true;
        }

        playerInputState = new PlayerInputState(kbm, dev);

        playerCursor = cursor;
    }

    // Handles controller disconnect and reconnect events during character select
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is not Gamepad gamepad) return;

        switch (change)
        {
            case InputDeviceChange.Disconnected:
                Debug.LogWarning($"[AviaryManager] Gamepad disconnected: {gamepad.displayName}");
                // Find the player whose device this was and clear it so UpdatePlayerInput doesn't
                // read from a stale device reference, freezing their cursor silently
                if (playerInputState.device == gamepad)
                {
                    playerInputState.device = null;
                    Debug.LogWarning($"[AviaryManager] Cleared device for Player 1 — awaiting reconnect.");
                    break;
                }
                
                break;

            case InputDeviceChange.Reconnected:
                Debug.Log($"[AviaryManager] Gamepad reconnected: {gamepad.displayName}");
                // Re-pair the reconnected gamepad to whichever player lost their device
                if (!playerInputState.isKBM && playerInputState.device == null)
                {
                    playerInputState.device = gamepad;
                    Debug.Log($"[AviaryManager] Re-paired {gamepad.displayName} to Player 1");
                    break;
                }
        
                break;
        }
    }

    private void UpdatePlayerInput()
    {
        // If the screen is not completely visible (no transitions) then accept no input
        if (cameraState != CameraState.BirdsEye) return;

        PlayerInputState state = playerInputState;

        if (state.isKBM)
        {
            state.cursorPosition = Mouse.current.position.ReadValue();
            UpdateMouseHoverSound(state.cursorPosition);

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                PlayCursorPressAnimation();
                HandlePlayerButtonPress();
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
                    if (currentTargetIndex >= 0)
                        state.cursorPosition = desiredScreenPosition;

                    PlayCursorPressAnimation();
                    bool activated = false;
                    int targetIndex = currentTargetIndex;
                    if (targetIndex >= 0)
                        activated = TryActivateTargetAtIndex(targetIndex);

                    if (activated)
                        AudioManager.PlayButtonSelectSound();

                    if (!activated)
                        HandlePlayerButtonPress();
                }
            }
        }
    }

    private void UpdatePlayerCursor()
    {
        // Guard against null placeholder slots (cursor prefab was missing for this player)
        Transform cursor = playerCursor;
        if (cursor == null) return;

        // If the screen is not completely visible (no transitions) then accept no input
        if (cameraState != CameraState.BirdsEye) return;

        PlayerInputState state = playerInputState;

        // If this player is using KBM, keep mouse behavior unchanged
        if (state.isKBM)
        {
            Vector2 screenPos = state.cursorPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                cursor.parent as RectTransform,
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
        if (mag >= inputDeadzone && Time.time - lastMoveTime >= inputRepeatDelay && uiSelectables.Count > 0)
        {
            Selectable cur = currentSelectable;
            if (cur == null)
            {
                int selIdx = FindClosestSelectableIndex(state.cursorPosition);
                if (selIdx >= 0)
                {
                    cur = uiSelectables[selIdx];
                    currentSelectable = cur;
                    currentTargetIndex = selIdx;
                    desiredScreenPosition = GetPreferredScreenPosition(uiTargets[selIdx]);
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
                    if (idx >= 0 && idx != currentTargetIndex)
                    {
                        currentTargetIndex = idx;
                        currentSelectable = next;
                        desiredScreenPosition = GetPreferredScreenPosition(uiTargets[idx]);
                        isHoveringTarget = false;
                        lastMoveTime= Time.time;
                        AudioManager.PlayButtonHoverSound();
                        // Also update EventSystem selection for consistency
                        if (EventSystem.current != null)
                            EventSystem.current.SetSelectedGameObject(next.gameObject);
                    }
                }
            }
        }

        // If we have a target, compute desired position; otherwise hold current position
        Vector2 desiredScreen = state.cursorPosition;
        int tidx = currentTargetIndex;
        if (tidx >= 0 && tidx < uiTargets.Count)
        {
            desiredScreen = GetPreferredScreenPosition(uiTargets[tidx]);
            desiredScreenPosition = desiredScreen;
        }

        // Smoothly interpolate the internal cursor screen position toward desiredScreen
        float t = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.0001f, cursorMoveSmoothTime));
        state.cursorPosition = Vector2.Lerp(state.cursorPosition, desiredScreenPosition, t);

        // Check hovering threshold (close enough to snap into hover state)
        float hoverThreshold = 10f;
        float dist = Vector2.Distance(state.cursorPosition, desiredScreenPosition);
        isHoveringTarget = tidx >= 0 && dist <= hoverThreshold;

        Vector2 finalScreenPos = state.cursorPosition;

        // When hovering, pin to preferred position and add diagonal bobbing
        if (isHoveringTarget)
        {
            finalScreenPos = desiredScreenPosition;
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
            cursor.parent as RectTransform,
            finalScreenPos,
            mainCanvas.worldCamera,
            out Vector2 localPos
        );

        cursor.GetComponent<RectTransform>().localPosition = localPos;
    }

    // Cursor press animation
    // Triggers the shrink → bounce animation for a player's cursor.
    private void PlayCursorPressAnimation()
    {

        if (cursorAnimCoroutine != null)
        {
            StopCoroutine(cursorAnimCoroutine);
            playerCursor.localScale = Vector3.one; // reset before restarting
        }

        cursorAnimCoroutine = StartCoroutine(CursorPressRoutine());
    }

    private IEnumerator CursorPressRoutine()
    {
        Transform cursor = playerCursor;
        Vector3 baseScale = cursorBaseScale;
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
        cursorAnimCoroutine = null;
    }

    private void HandlePlayerButtonPress()
    {
        int targetIndex = currentTargetIndex;
        if (targetIndex >= 0 && TryActivateTargetAtIndex(targetIndex))
            return;

        Vector2 screenPos = playerInputState.cursorPosition;
        PointerEventData pointerData = new(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            BirdSelectButton birdButton = result.gameObject.GetComponent<BirdSelectButton>();
            if (birdButton != null)
            {
                birdButton.OnPressed();
                AudioManager.PlayButtonSelectSound();
                return;
            }

            Button uiButton = result.gameObject.GetComponent<Button>();
            if (uiButton != null)
            {
                uiButton.onClick.Invoke();
                AudioManager.PlayButtonSelectSound();
                return;
            }
        }
    }

    private void UpdateMouseHoverSound(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return;

        PointerEventData pointerData = new(EventSystem.current) { position = screenPosition };
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(pointerData, results);

        GameObject hoveredElement = null;
        foreach (RaycastResult result in results)
        {
            BirdSelectButton birdButton = result.gameObject.GetComponentInParent<BirdSelectButton>();
            if (birdButton != null)
            {
                hoveredElement = birdButton.gameObject;
                break;
            }

            Button button = result.gameObject.GetComponentInParent<Button>();
            if (button != null && button.interactable)
            {
                hoveredElement = button.gameObject;
                break;
            }
        }

        if (hoveredElement != hoveredUIElement)
        {
            hoveredUIElement = hoveredElement;
            if (hoveredElement != null)
                AudioManager.PlayButtonHoverSound();
        }
    }

    private bool TryActivateTargetAtIndex(int index)
    {
        if (index < 0 || index >= uiTargets.Count) return false;

        Selectable selectable = (index < uiSelectables.Count) ? uiSelectables[index] : null;
        if (TryActivateSelectable(selectable))
            return true;

        RectTransform rt = uiTargets[index];
        if (rt == null) return false;

        BirdSelectButton birdButton = rt.GetComponent<BirdSelectButton>();
        if (birdButton != null)
        {
            birdButton.OnPressed();
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
            birdButton.OnPressed();
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

    private bool TryActivateSelectable(Selectable selectable)
    {
        if (selectable == null) return false;

        if (selectable.TryGetComponent<BirdSelectButton>(out BirdSelectButton birdButton))
        {
            birdButton.OnPressed();
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

    // Setting the player's bird index (used by BirdSelectButton.cs)
    public void SetPlayerBirdIndex(int birdIndex)
    {
        const int randomBirdIndex = 19;
        const int birdCount = 19;

        if (birdIndex == randomBirdIndex)
            birdIndex = Random.Range(0, birdCount);

        if (birdIndex < 0 || birdIndex >= availableBirds.Count) return;

        chosenBirdIndex = birdIndex;
        cameraState = CameraState.ToFrontOfBird;
        transitionTime = 0f;
        StartCoroutine(TransitionToBird());
        // Debug.Log(chosenBirdIndex);
    }

    // Get the bird's stats from the given index and set them in the bird info menu
    private void GetBirdInformation()
    {
        BirdData birdData;

        switch (chosenBirdIndex)
        {
            case 0: // Penguin
                birdData = birds.GetBirdData("Penguin");
                birdType = BirdType.PENGUIN;
                birdPos = penguin.transform.position;
                birdDir = Vector3.back;
                cameraDir = Vector3.forward;
                break;
            case 1: // Owl
                birdData = birds.GetBirdData("Owl");
                birdType = BirdType.OWL;
                birdPos = owl.transform.position;
                birdDir = Vector3.back;
                cameraDir = Vector3.forward;
                break;
            case 2: // Pelican
                birdData = birds.GetBirdData("Pelican");
                birdType = BirdType.PELICAN;
                birdPos = pelican.transform.position;
                birdDir = Vector3.back;
                cameraDir = Vector3.forward;
                break;
            case 3: // Toucan
                birdData = birds.GetBirdData("Toucan");
                birdType = BirdType.TOUCAN;
                birdPos = toucan.transform.position;
                birdDir = Vector3.back;
                cameraDir = Vector3.forward;
                break;
            case 4: // Lovebird
                birdData = birds.GetBirdData("Lovebird");
                birdType = BirdType.LOVEBIRD;
                birdPos = lovebird.transform.position;
                birdDir = Vector3.left;
                cameraDir = Vector3.right;
                break;
            case 5: // Seagull
                birdData = birds.GetBirdData("Seagull");
                birdType = BirdType.SEAGULL;
                birdPos = seagull.transform.position;
                birdDir = Vector3.left;
                cameraDir = Vector3.right;
                break;
            case 6: // Kiwi
                birdData = birds.GetBirdData("Kiwi");
                birdType = BirdType.KIWI;
                birdPos = kiwi.transform.position;
                birdDir = Vector3.left;
                cameraDir = Vector3.right;
                break;
            case 7: // Dodo
                birdData = birds.GetBirdData("Dodo");
                birdType = BirdType.DODO;
                birdPos = dodo.transform.position;
                birdDir = Vector3.left;
                cameraDir = Vector3.right;
                break;
            case 8: // Pukeko
                birdData = birds.GetBirdData("Pukeko");
                birdType = BirdType.PUKEKO;
                birdPos = pukeko.transform.position;
                birdDir = Vector3.left;
                cameraDir = Vector3.right;
                break;
            case 9: // Crow
                birdData = birds.GetBirdData("Crow");
                birdType = BirdType.CROW;
                birdPos = crow.transform.position;
                birdDir = Vector3.left;
                cameraDir = Vector3.right;
                break;
            case 10: // Scissortail
                birdData = birds.GetBirdData("Scissortail");
                birdType = BirdType.SCISSORTAIL;
                birdPos = scissortail.transform.position;
                birdDir = Vector3.forward;
                cameraDir = Vector3.back;
                break;
            case 11: // Chicken
                birdData = birds.GetBirdData("Chicken");
                birdType = BirdType.CHICKEN;
                birdPos = chicken.transform.position;
                birdDir = Vector3.forward;
                cameraDir = Vector3.back;
                break;
            case 12: // Ostrich
                birdData = birds.GetBirdData("Ostrich");
                birdType = BirdType.OSTRICH;
                birdPos = ostrich.transform.position;
                birdDir = Vector3.forward;
                cameraDir = Vector3.back;
                break;
            case 13: // Eagle
                birdData = birds.GetBirdData("Eagle");
                birdType = BirdType.EAGLE;
                birdPos = eagle.transform.position;
                birdDir = Vector3.forward;
                cameraDir = Vector3.back;
                break;
            case 14: // Macaw
                birdData = birds.GetBirdData("Macaw");
                birdType = BirdType.MACAW;
                birdPos = macaw.transform.position;
                birdDir = Vector3.forward;
                cameraDir = Vector3.back;
                break;
            case 15: // Phoenix
                birdData = birds.GetBirdData("Phoenix");
                birdType = BirdType.PHOENIX;
                birdPos = phoenix.transform.position;
                birdDir = Vector3.forward;
                cameraDir = Vector3.back;
                break;
            case 16: // 31rd
                birdData = birds.GetBirdData("31rd");
                birdType = BirdType.ROBOPIGEON;
                birdPos = robopigeon.transform.position;
                birdDir = Vector3.forward;
                cameraDir = Vector3.back;
                break;
            case 17: // Hummingbird
                birdData = birds.GetBirdData("Hummingbird");
                birdType = BirdType.HUMMINGBIRD;
                birdPos = hummingbird.transform.position;
                birdDir = Vector3.forward;
                cameraDir = Vector3.back;
                break;
            case 18: // Shima Enaga
                birdData = birds.GetBirdData("Shima Enaga");
                birdType = BirdType.SHIMAENAGA;
                birdPos = shimaEnaga.transform.position;
                birdDir = Vector3.right;
                cameraDir = Vector3.left;
                break;
            default: // Something weird happened, just display penguin ig
                birdData = birds.GetBirdData("Penguin");
                birdType = BirdType.PENGUIN;
                birdPos = penguin.transform.position;
                birdDir = Vector3.back;
                cameraDir = Vector3.forward;
                Debug.LogWarning("Chosen bird on aviary does not have a bird associated with the given bird index; defaulting to penguin.");
                break;
        }

        birdName.text = birdData.birdName;
        description.text = birdData.description;
        icon.texture = birdData.icon;
        offDescription.text = birdData.offensiveAbility;
        offIcon.texture = birdData.offensiveIcon;
        defDescription.text = birdData.defensiveAbility;
        defIcon.texture = birdData.defensiveIcon;

        for (int i = 0; i < 10; i++)
        {
            if (i < birdData.groundSpeed)
            {
                speedIndicators.transform.Find("Speed" + (i + 1)).GetComponent<RawImage>().texture = spIndicator;
            }
            else
            {
                speedIndicators.transform.Find("Speed" + (i + 1)).GetComponent<RawImage>().texture = eIndicator;
            }

            if (i < birdData.jumpForce)
            {
                jumpIndicators.transform.Find("Jump" + (i + 1)).GetComponent<RawImage>().texture = jIndicator;
            }
            else
            {
                jumpIndicators.transform.Find("Jump" + (i + 1)).GetComponent<RawImage>().texture = eIndicator;
            }

            if (i < birdData.strength)
            {
                strengthIndicators.transform.Find("Strength" + (i + 1)).GetComponent<RawImage>().texture = strIndicator;
            }
            else
            {
                strengthIndicators.transform.Find("Strength" + (i + 1)).GetComponent<RawImage>().texture = eIndicator;
            }
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

    public void NavigateBackToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
