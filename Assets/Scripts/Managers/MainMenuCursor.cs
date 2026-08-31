using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // Using New Input System
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Main Menu Cursor - Controller Only
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
    [Tooltip("Assign the NumPlayers panel here so the cursor knows when to use B to proceed.")]
    [SerializeField] private GameObject numPlayersPanel;

    private Transform playerCursor;
    private Coroutine cursorAnimCoroutine;
    private Vector3 cursorBaseScale = Vector3.one;
    private Vector2 cursorPosition;
    private Vector2 desiredCursorPosition;

    private List<RectTransform> uiTargets = new();
    private List<Selectable> uiSelectables = new();
    private int currentTargetIndex = -1;
    private bool lastNumPlayersActive;
    private float lastMoveTime;
    private float panelSwitchedTime;

    [Tooltip("Minimum stick magnitude to trigger a navigation move")]
    public float inputDeadzone = 0.5f;

    [Tooltip("Minimum seconds between directional navigation moves")]
    public float inputRepeatDelay = 0.12f;

    [Tooltip("Seconds to ignore A after the active menu panel changes")]
    public float panelSwitchInputBlockDuration = 0.18f;

    private const string playSceneName = "CharSelect";

    private void Start()
    {
        // 1. HIDE THE SYSTEM MOUSE
        // This stops the OS cursor from showing up and locks it to the center
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        CreatePlayerCursor();

        // Start virtual cursor at center of screen
        cursorPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
        desiredCursorPosition = cursorPosition;

        bool numPlayersActive = numPlayersPanel != null && numPlayersPanel.activeInHierarchy;
        CollectUITargets(numPlayersActive);
        lastNumPlayersActive = numPlayersActive;
        currentTargetIndex = FindClosestSelectableIndex(desiredCursorPosition);
        if (currentTargetIndex >= 0)
            desiredCursorPosition = GetPreferredScreenPosition(uiTargets[currentTargetIndex]);
    }

    private void Update()
    {
        // Ensure the hardware mouse stays hidden (in case of alt-tabbing)
        if (Cursor.visible) Cursor.visible = false;

        // Only read from Gamepad.all[0] so that Player 1 is always the first connected
        // controller — this matches how CharacterSelectManager assigns players via
        // Gamepad.all[index], preventing ordering mismatches between scenes and
        // ensuring controllers 2/3/4 cannot move or click the main menu cursor.
        if (Gamepad.all.Count == 0) return;
        Gamepad pad = Gamepad.all[0];

        bool numPlayersActive = numPlayersPanel != null && numPlayersPanel.activeInHierarchy;

        // Refresh UI targets immediately when the active panel changes.
        bool panelChanged = numPlayersActive != lastNumPlayersActive;
        if (ShouldRefreshUITargets(numPlayersActive))
        {
            CollectUITargets(numPlayersActive);
            currentTargetIndex = FindClosestSelectableIndex(cursorPosition);
            if (currentTargetIndex >= 0)
                desiredCursorPosition = GetPreferredScreenPosition(uiTargets[currentTargetIndex]);
            lastNumPlayersActive = numPlayersActive;
            if (panelChanged)
                panelSwitchedTime = Time.time;
        }

        // On the NumPlayers panel, B confirms and proceeds to CharSelect.
        if (numPlayersActive && pad.bButton.wasPressedThisFrame)
        {
            AudioManager.PlayButtonSelectSound();
            NavigateToPlay();
            return;
        }

        UpdateControllerInput(pad);
        UpdateCursorPosition();
    }

    private void CreatePlayerCursor()
    {
        if (cursor1Prefab == null) return;

        playerCursor = Instantiate(cursor1Prefab, transform.root);
        playerCursor.name = "Cursor_Player1";
        cursorBaseScale = playerCursor.localScale;

        RectTransform rt = playerCursor.GetComponent<RectTransform>();
        if (rt != null)
            rt.pivot = new Vector2(0f, 1f); // Top-left pivot for pointer-style cursor accuracy
    }

    private void UpdateControllerInput(Gamepad pad)
    {
        Vector2 input = pad.leftStick.ReadValue();

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
                else if (next == null)
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

        // Click / select (A Button)
        if (pad.aButton.wasPressedThisFrame)
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

        float t = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.0001f, cursorMoveSmoothTime));
        cursorPosition = Vector2.Lerp(cursorPosition, desiredCursorPosition, t);

        // Convert the screen position to local position for the UI Canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            GetComponentInParent<Canvas>().GetComponent<RectTransform>(),
            cursorPosition,
            null, // Set to null if Canvas is Overlay, or the Camera if it's World Space
            out Vector2 localPos
        );

        if (currentTargetIndex >= 0 && currentTargetIndex < uiTargets.Count)
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

    private bool ShouldRefreshUITargets(bool numPlayersActive)
    {
        if (numPlayersActive != lastNumPlayersActive)
            return true;

        List<Selectable> selectables = GetActivePanelSelectables(numPlayersActive);
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

    private void CollectUITargets(bool numPlayersActive)
    {
        uiTargets.Clear();
        uiSelectables.Clear();

        List<Selectable> selectables = GetActivePanelSelectables(numPlayersActive);
        if (selectables == null) return;

        foreach (var selectable in selectables)
        {
            RectTransform rt = selectable.GetComponent<RectTransform>();
            if (rt == null) continue;
            uiTargets.Add(rt);
            uiSelectables.Add(selectable);
        }
    }

    private List<Selectable> GetActivePanelSelectables(bool numPlayersActive)
    {
        if (numPlayersActive)
        {
            if (numPlayersPanel == null) return null;
            return new List<Selectable>(numPlayersPanel.GetComponentsInChildren<Selectable>(true));
        }

        if (mainMenuPanel != null)
        {
            return new List<Selectable>(mainMenuPanel.GetComponentsInChildren<Selectable>(true));
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
}