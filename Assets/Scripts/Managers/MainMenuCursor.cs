using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // Using New Input System
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// The main menu's cursor. It follows the mouse while that's being used, and moves between buttons with a controller or the keys.
public class MainMenuCursor : MonoBehaviour
{
    public Transform cursor1Prefab;

    [Header("Cursor Animation")]
    [Range(0.1f, 0.99f)]
    public float cursorPressScale = 0.65f;
    public float cursorShrinkDuration = 0.07f;
    public float cursorBounceDuration = 0.14f;
    [Range(1.0f, 1.5f)]
    public float cursorBounceOvershoot = 1.15f;

    [Header("Controller Settings")]
    public float cursorSpeed = 1000f;
    [Tooltip("Seconds to smoothly move the cursor toward the target position")]
    public float cursorMoveSmoothTime = 0.06f;

    [Header("Hover Animation")]
    [Tooltip("Bobbing amplitude in pixels when hovering over a target")]
    public float hoverBobAmplitude = 6f;
    [Tooltip("Bobbing frequency in Hz when hovering over a target")]
    public float hoverBobFrequency = 3.0f;

    [Header("Panels")]
    [Tooltip("Assign the main menu panel here so the cursor only targets main menu buttons.")]
    [SerializeField] private GameObject mainMenuPanel;
    [Tooltip("Assign the LocalorOnline panel here so the cursor only targets local online buttons.")]
    [SerializeField] private GameObject localOnlinePanel;
    [Tooltip("Assign the NumPlayers panel here so the cursor knows when to use B to proceed.")]
    [SerializeField] private GameObject numPlayersPanel;
    [Tooltip("Assign the Online panel here so the cursor only targets host join buttons.")]
    [SerializeField] private GameObject hostJoinPanel;

    private Transform playerCursor;
    private Coroutine cursorAnimCoroutine;
    private Vector3 cursorBaseScale = Vector3.one;
    private Vector2 cursorPosition;
    private Vector2 desiredCursorPosition;

    private List<RectTransform> uiTargets = new();
    private List<Selectable> uiSelectables = new();
    private int currentTargetIndex = -1;
    private MenuType lastMenuActive;
    private float lastMoveTime;
    private float panelSwitchedTime;

    [Tooltip("Minimum stick magnitude to trigger a navigation move")]
    public float inputDeadzone = 0.5f;

    [Tooltip("Minimum seconds between directional navigation moves")]
    public float inputRepeatDelay = 0.12f;

    [Tooltip("Seconds to ignore A after the active menu panel changes")]
    public float panelSwitchInputBlockDuration = 0.18f;

    private const string playSceneName = "CharSelect";

    private enum MenuType
    {
        Main,
        LocalOnline,
        NumPlayers,
        HostJoin,
        OnlineLobby,
        Credits,
        Options
    }

    private void Start()
    {
        // The mouse is free to move until a controller gets used
        MenuMouse.Release();

        CreatePlayerCursor();

        // Start virtual cursor at center of screen
        cursorPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
        desiredCursorPosition = cursorPosition;

        MenuType activeMenu = GetActiveMenuType();
        CollectUITargets(activeMenu);
        lastMenuActive = MenuType.Main;
        currentTargetIndex = FindClosestSelectableIndex(desiredCursorPosition);
        if (currentTargetIndex >= 0)
            desiredCursorPosition = GetPreferredScreenPosition(uiTargets[currentTargetIndex]);
    }

    private void Update()
    {
        // The system cursor can come back after alt-tabbing, and the one drawn here stands in for it
        if (Cursor.visible) Cursor.visible = false;

        // Whichever controller was used last, plus the keyboard and mouse, so the main menu works from any device.
        // Character select works out who is player 1 by itself.
        Gamepad pad = LocalPlayerDevices.LastUsedGamepad ?? (Gamepad.all.Count > 0 ? Gamepad.all[0] : null);
        Keyboard keyboard = Keyboard.current;

        MenuType activeMenu = GetActiveMenuType();

        // Refresh UI targets immediately when the active panel changes.
        bool panelChanged = activeMenu != lastMenuActive;
        if (ShouldRefreshUITargets(activeMenu))
        {
            CollectUITargets(activeMenu);
            currentTargetIndex = FindClosestSelectableIndex(cursorPosition);

            // Options starts on its first row, so a stray press of A can't change a setting from the middle of the list
            if (activeMenu == MenuType.Options) currentTargetIndex = 0;

            if (currentTargetIndex >= 0)
                desiredCursorPosition = GetPreferredScreenPosition(uiTargets[currentTargetIndex]);
            lastMenuActive = activeMenu;
            if (panelChanged)
            {
                panelSwitchedTime = Time.time;

                // Panels made in code (like the online lobby) would otherwise be drawn over the cursor
                if (playerCursor != null) playerCursor.SetAsLastSibling();
            }
        }

        // On the NumPlayers panel, B confirms and proceeds to CharSelect.
        if (lastMenuActive == MenuType.NumPlayers && pad != null && pad.bButton.wasPressedThisFrame)
        {
            AudioManager.PlayButtonSelectSound();
            NavigateToPlay();
            return;
        }

        bool typing = IsTyping();
        bool usedMouse = UpdateMouseInput(typing);
        if (!usedMouse && !typing) KeepEventSystemOnTarget();

        // The stick and d-pad, or the arrow keys and WASD, all move between buttons the same way
        Vector2 move = Vector2.zero;
        bool submit = false;
        if (pad != null)
        {
            move = pad.leftStick.ReadValue();
            if (move.sqrMagnitude < 0.01f) move = pad.dpad.ReadValue();
            submit = pad.aButton.wasPressedThisFrame;
        }
        if (keyboard != null && !typing)
        {
            Vector2 keys = new Vector2(
                (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed ? 1f : 0f));
            if (keys != Vector2.zero) move = keys;
            submit |= keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame;
        }

        UpdateControllerInput(move, submit);
        UpdateCursorPosition();
    }

    private bool mouseActive;

    // Lets the mouse point at and click buttons the same as it would anywhere else. Returns whether the mouse is
    // what's being used right now.
    private bool UpdateMouseInput(bool typing)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return false;

        if (MenuMouse.UsedThisFrame())
        {
            if (!mouseActive) MenuMouse.Release();
            mouseActive = true;
        }
        else if (Gamepad.all.Any(LocalPlayerDevices.WasUsedThisFrame))
        {
            // Otherwise a pointer left sitting on a button would keep it highlighted while the stick moves elsewhere
            if (Cursor.lockState != CursorLockMode.Locked) MenuMouse.Lock();
            mouseActive = false;
        }
        else if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            mouseActive = false;
        }

        if (!mouseActive) return false;

        Vector2 pointer = mouse.position.ReadValue();
        cursorPosition = desiredCursorPosition = pointer;

        int hovered = FindTargetUnderPointer(pointer, out bool eventSystemClicksIt);

        // Selecting another button would take the keyboard away from a text field that's being typed in
        if (hovered >= 0 && hovered != currentTargetIndex && !typing)
        {
            currentTargetIndex = hovered;
            if (EventSystem.current != null && uiSelectables[hovered] != null)
                EventSystem.current.SetSelectedGameObject(uiSelectables[hovered].gameObject);
            AudioManager.PlayButtonHoverSound();
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            PlayCursorPressAnimation();

            // The EventSystem clicks whatever the pointer is over by itself, so this only fills in when it can't
            if (hovered >= 0 && !eventSystemClicksIt && Time.time - panelSwitchedTime >= panelSwitchInputBlockDuration
                && TryActivateTargetAtIndex(hovered))
            {
                AudioManager.PlayButtonSelectSound();
            }
        }

        return true;
    }

    // The button under the mouse, or -1 if it isn't over one. eventSystemClicksIt is false for a button with
    // nothing on it that can be hit by a raycast, which the EventSystem can't click.
    private int FindTargetUnderPointer(Vector2 pointer, out bool eventSystemClicksIt)
    {
        eventSystemClicksIt = false;

        if (EventSystem.current != null)
        {
            List<RaycastResult> results = new();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, results);

            // Only the topmost thing counts, the same as for a click
            if (results.Count > 0)
            {
                for (Transform t = results[0].gameObject.transform; t != null; t = t.parent)
                {
                    int index = uiSelectables.FindIndex(selectable => selectable != null && selectable.transform == t);
                    if (index < 0) continue;

                    eventSystemClicksIt = true;
                    return index;
                }
            }
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        for (int i = 0; i < uiTargets.Count; ++i)
        {
            if (uiTargets[i] != null && RectTransformUtility.RectangleContainsScreenPoint(uiTargets[i], pointer, cam))
                return i;
        }

        return -1;
    }

    // Whether a text field has the keyboard, since the arrow keys, WASD, Enter and Space all type into it
    private static bool IsTyping()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return selected != null && selected.TryGetComponent(out TMP_InputField field) && field.isFocused;
    }

    // The EventSystem also clicks its selected button when A is pressed, so it needs to be on the same
    // button as the cursor. Otherwise one press can click two different buttons.
    private void KeepEventSystemOnTarget()
    {
        if (EventSystem.current == null || currentTargetIndex < 0 || currentTargetIndex >= uiSelectables.Count) return;

        Selectable target = uiSelectables[currentTargetIndex];
        if (target != null && EventSystem.current.currentSelectedGameObject != target.gameObject)
            EventSystem.current.SetSelectedGameObject(target.gameObject);
    }

    private void CreatePlayerCursor()
    {
        if (cursor1Prefab == null) return;

        playerCursor = Instantiate(cursor1Prefab, transform.root);
        playerCursor.name = "Cursor_Player1";
        cursorBaseScale = playerCursor.localScale;
        MenuMouse.MakeClickThrough(playerCursor.gameObject);

        RectTransform rt = playerCursor.GetComponent<RectTransform>();
        if (rt != null)
            rt.pivot = new Vector2(0f, 1f); // Top-left pivot for pointer-style cursor accuracy
    }

    private void UpdateControllerInput(Vector2 input, bool submit)
    {
        if (input.magnitude >= inputDeadzone && Time.time - lastMoveTime >= inputRepeatDelay && uiSelectables.Count > 0)
        {
            Selectable current = (currentTargetIndex >= 0 && currentTargetIndex < uiSelectables.Count)
                ? uiSelectables[currentTargetIndex]
                : null;

            if (current == null && uiSelectables.Count > 0)
            {
                currentTargetIndex = FindClosestSelectableIndex(cursorPosition);
                if (currentTargetIndex >= 0)
                {
                    current = uiSelectables[currentTargetIndex];
                    desiredCursorPosition = GetPreferredScreenPosition(uiTargets[currentTargetIndex]);
                }
            }

            if (current != null)
            {
                Navigation nav = current.navigation;
                Selectable next = null;

                if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
                    next = (input.x > 0f) ? nav.selectOnRight : nav.selectOnLeft;
                else
                    next = (input.y > 0f) ? nav.selectOnUp : nav.selectOnDown;

                int nextIndex = -1;
                if (next != null)
                    nextIndex = uiSelectables.IndexOf(next);

                if (nextIndex >= 0 && nextIndex != currentTargetIndex)
                {
                    currentTargetIndex = nextIndex;
                    desiredCursorPosition = GetPreferredScreenPosition(uiTargets[nextIndex]);
                    if (EventSystem.current != null)
                        EventSystem.current.SetSelectedGameObject(next.gameObject);
                    AudioManager.PlayButtonHoverSound();
                    lastMoveTime = Time.time;
                }
                else if (next == null && lastMenuActive != MenuType.Options) // Left and right change a setting there
                {
                    int fallback = FindClosestTargetIndex(cursorPosition, input);
                    if (fallback >= 0 && fallback != currentTargetIndex)
                    {
                        currentTargetIndex = fallback;
                        desiredCursorPosition = GetPreferredScreenPosition(uiTargets[fallback]);
                        AudioManager.PlayButtonHoverSound();
                        lastMoveTime = Time.time;
                    }
                }
            }
        }

        // Click / select (A Button, or Enter/Space)
        if (submit)
        {
            if (Time.time - panelSwitchedTime < panelSwitchInputBlockDuration)
                return;

            PlayCursorPressAnimation();
            if (currentTargetIndex >= 0)
            {
                if (TryActivateTargetAtIndex(currentTargetIndex))
                    AudioManager.PlayButtonSelectSound();
            }
            else
            {
                HandleButtonPress();
            }
        }
    }

    private void UpdateCursorPosition()
    {
        if (playerCursor == null) return;

        // While the mouse is being used, the cursor is already right where the mouse is
        bool snapping = !mouseActive;
        if (snapping)
        {
            // Follows the target every frame, since a new panel's buttons can still be moving as its layout gets built
            if (currentTargetIndex >= 0 && currentTargetIndex < uiTargets.Count)
                desiredCursorPosition = GetPreferredScreenPosition(uiTargets[currentTargetIndex]);

            float t = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.0001f, cursorMoveSmoothTime));
            cursorPosition = Vector2.Lerp(cursorPosition, desiredCursorPosition, t);
        }

        // Convert the screen position to local position for the UI Canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            GetComponentInParent<Canvas>().GetComponent<RectTransform>(),
            cursorPosition,
            null, // Set to null if Canvas is Overlay, or the Camera if it's World Space
            out Vector2 localPos
        );

        if (snapping && currentTargetIndex >= 0 && currentTargetIndex < uiTargets.Count)
        {
            float phase = (Time.time * hoverBobFrequency) % 1f;
            float intensity = Mathf.Sin(phase * Mathf.PI * 2f);
            Vector2 bob = new Vector2(-intensity, -intensity) * hoverBobAmplitude;
            localPos += bob;
        }

        playerCursor.GetComponent<RectTransform>().localPosition = localPos;
    }

    private void HandleButtonPress()
    {
        // Simulate a "Pointer" at our virtual cursor's location
        PointerEventData pointerData = new(EventSystem.current)
        {
            position = cursorPosition
        };

        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            // Check for your custom Bird Button
            BirdSelectButton birdButton = result.gameObject.GetComponent<BirdSelectButton>();
            if (birdButton != null)
            {
                birdButton.OnPressed(0);
                AudioManager.PlayButtonSelectSound();
                return;
            }

            // Check for standard Unity Buttons
            Button uiButton = result.gameObject.GetComponentInParent<Button>();
            if (uiButton != null && uiButton.interactable)
            {
                uiButton.onClick.Invoke();
                AudioManager.PlayButtonSelectSound();
                return;
            }
        }
    }

    // --- ANIMATION LOGIC ---
    private void PlayCursorPressAnimation()
    {
        if (playerCursor == null) return;
        if (cursorAnimCoroutine != null) StopCoroutine(cursorAnimCoroutine);
        cursorAnimCoroutine = StartCoroutine(CursorPressRoutine());
    }

    private IEnumerator CursorPressRoutine()
    {
        float elapsed = 0f;
        while (elapsed < cursorShrinkDuration)
        {
            elapsed += Time.deltaTime;
            float s = Mathf.Lerp(1f, cursorPressScale, elapsed / cursorShrinkDuration);
            playerCursor.localScale = cursorBaseScale * s;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < cursorBounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / cursorBounceDuration;
            float baseScale = Mathf.Lerp(cursorPressScale, 1f, t);
            float overshoot = Mathf.Sin(t * Mathf.PI) * (cursorBounceOvershoot - 1f);
            float s = baseScale + overshoot;
            playerCursor.localScale = cursorBaseScale * s;
            yield return null;
        }

        playerCursor.localScale = cursorBaseScale;
        cursorAnimCoroutine = null;
    }

    public void NavigateToPlay()
    {
        SceneManager.LoadScene(playSceneName);
    }

    private bool ShouldRefreshUITargets(MenuType activeMenu)
    {
        if (activeMenu != lastMenuActive)
            return true;

        List<Selectable> selectables = GetActivePanelSelectables(activeMenu);
        if (selectables == null)
            return false;

        if (selectables.Count != uiSelectables.Count)
            return true;

        for (int i = 0; i < selectables.Count; ++i)
        {
            if (selectables[i] != uiSelectables[i])
                return true;
        }

        return false;
    }

    private void CollectUITargets(MenuType activeMenu)
    {
        uiTargets.Clear();
        uiSelectables.Clear();

        List<Selectable> selectables = GetActivePanelSelectables(activeMenu);
        if (selectables == null) return;

        foreach (var selectable in selectables)
        {
            RectTransform rt = selectable.GetComponent<RectTransform>();
            if (rt == null) continue;
            uiTargets.Add(rt);
            uiSelectables.Add(selectable);
        }
    }

    private List<Selectable> GetActivePanelSelectables(MenuType activeMenu)
    {
        if (activeMenu == MenuType.OnlineLobby)
        {
            // Leaves out hidden buttons, since only hosts have a join code to copy
            return new List<Selectable>(OnlineLobbyUI.ActivePanel.GetComponentsInChildren<Selectable>(false));
        }

        if (activeMenu == MenuType.Credits)
        {
            return new List<Selectable>(MainMenu.ActiveCreditsPanel.GetComponentsInChildren<Selectable>(false));
        }

        if (activeMenu == MenuType.Options)
        {
            return new List<Selectable>(OptionsMenuUI.ActivePanel.GetComponentsInChildren<Selectable>(false));
        }

        if (activeMenu == MenuType.NumPlayers)
        {
            if (numPlayersPanel == null) return null;
            return new List<Selectable>(numPlayersPanel.GetComponentsInChildren<Selectable>(true));
        }

        if (activeMenu == MenuType.Main && mainMenuPanel != null)
        {
            return new List<Selectable>(mainMenuPanel.GetComponentsInChildren<Selectable>(true));
        }

        if (activeMenu == MenuType.LocalOnline && localOnlinePanel != null)
        {
            return new List<Selectable>(localOnlinePanel.GetComponentsInChildren<Selectable>(true));
        }

        if (activeMenu == MenuType.HostJoin && hostJoinPanel != null)
        {
            // Leaves out hidden buttons, since the list of public matches changes in size
            return new List<Selectable>(hostJoinPanel.GetComponentsInChildren<Selectable>(false));
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return null;

        return new List<Selectable>(canvas.GetComponentsInChildren<Selectable>(true));
    }

    private Vector2 GetPreferredScreenPosition(RectTransform rt)
    {
        if (rt == null) return new Vector2(Screen.width / 2f, Screen.height / 2f);

        Vector2 center = RectTransformUtility.WorldToScreenPoint(GetComponentInParent<Canvas>().worldCamera, rt.position);
        float margin = 6f;
        center.x = Mathf.Clamp(center.x, margin, Screen.width - margin);
        center.y = Mathf.Clamp(center.y, margin, Screen.height - margin);
        return center;
    }

    private int FindClosestSelectableIndex(Vector2 fromPoint)
    {
        if (uiTargets == null || uiTargets.Count == 0) return -1;

        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < uiTargets.Count; ++i)
        {
            Vector2 target = GetPreferredScreenPosition(uiTargets[i]);
            float dist = (target - fromPoint).sqrMagnitude;
            if (dist < bestDistance)
            {
                bestDistance = dist;
                best = i;
            }
        }

        return best;
    }

    private int FindClosestTargetIndex(Vector2 fromPoint, Vector2 direction)
    {
        if (uiTargets == null || uiTargets.Count == 0) return -1;

        int best = -1;
        float bestScore = float.MaxValue;
        Vector2 dir = direction.normalized;
        bool useDir = direction.sqrMagnitude > 0.001f;

        for (int i = 0; i < uiTargets.Count; ++i)
        {
            Vector2 target = GetPreferredScreenPosition(uiTargets[i]);
            Vector2 toTarget = target - fromPoint;
            float dist = toTarget.sqrMagnitude;
            float score = dist;
            if (useDir)
            {
                Vector2 toDir = toTarget.normalized;
                float dot = Vector2.Dot(dir, toDir);
                score = dist * (1f - Mathf.Clamp01((dot + 1f) / 2f));
            }
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    private bool TryActivateTargetAtIndex(int index)
    {
        if (index < 0 || index >= uiSelectables.Count) return false;
        Selectable selectable = uiSelectables[index];
        if (selectable == null) return false;

        if (selectable.TryGetComponent<Button>(out Button uiButton) && uiButton.interactable)
        {
            uiButton.onClick.Invoke();
            return true;
        }

        if (selectable.TryGetComponent<BirdSelectButton>(out BirdSelectButton birdButton))
        {
            birdButton.OnPressed(0);
            return true;
        }

        if (EventSystem.current != null && selectable.IsInteractable())
        {
            BaseEventData eventData = new BaseEventData(EventSystem.current);
            ExecuteEvents.Execute(selectable.gameObject, eventData, ExecuteEvents.submitHandler);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Asserts which menu in the hierarcy is active.
    /// </summary>
    /// <returns>The type of menu that is active in the hierarchy.</returns>
    private MenuType GetActiveMenuType()
    {
        if (OnlineLobbyUI.ActivePanel != null) return MenuType.OnlineLobby;
        if (MainMenu.ActiveCreditsPanel != null) return MenuType.Credits;
        if (OptionsMenuUI.ActivePanel != null) return MenuType.Options;
        if (mainMenuPanel != null && mainMenuPanel.activeInHierarchy) return MenuType.Main;
        if (localOnlinePanel != null && localOnlinePanel.activeInHierarchy) return MenuType.LocalOnline;
        if (numPlayersPanel != null && numPlayersPanel.activeInHierarchy) return MenuType.NumPlayers;
        if (hostJoinPanel != null && hostJoinPanel.activeInHierarchy) return MenuType.HostJoin;
        return MenuType.Main; // Hopefully this never hits as I cannot null this out, but we will see lol
    }
}