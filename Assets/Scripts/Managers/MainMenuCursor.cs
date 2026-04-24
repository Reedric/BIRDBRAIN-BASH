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

    private Transform playerCursor;
    private Coroutine cursorAnimCoroutine;
    private Vector2 cursorPosition;

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
    }

    private void Update()
    {
        // Ensure the hardware mouse stays hidden (in case of alt-tabbing)
        if (Cursor.visible) Cursor.visible = false;

        // Check for Gamepad
        Gamepad pad = Gamepad.current;
        if (pad == null) return; 

        // Back to menu (B Button)
        if (pad.bButton.wasPressedThisFrame)
        {
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

        RectTransform rt = playerCursor.GetComponent<RectTransform>();
        if (rt != null)
            rt.pivot = new Vector2(0.5f, 0.5f); // Center pivot for better clicking accuracy
    }

    private void UpdateControllerInput(Gamepad pad)
    {
        // Move cursor with left stick
        Vector2 input = pad.leftStick.ReadValue();

        // Apply movement
        float moveSpeed = cursorSpeed * Time.deltaTime;
        cursorPosition += input * moveSpeed;

        // Clamp to screen bounds so the cursor can't leave the view
        cursorPosition.x = Mathf.Clamp(cursorPosition.x, 0, Screen.width);
        cursorPosition.y = Mathf.Clamp(cursorPosition.y, 0, Screen.height);

        // Click / select (A Button)
        if (pad.aButton.wasPressedThisFrame)
        {
            PlayCursorPressAnimation();
            HandleButtonPress();
        }
    }

    private void UpdateCursorPosition()
    {
        if (playerCursor == null) return;

        // Convert the screen position to local position for the UI Canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            GetComponentInParent<Canvas>().GetComponent<RectTransform>(),
            cursorPosition,
            null, // Set to null if Canvas is Overlay, or the Camera if it's World Space
            out Vector2 localPos
        );

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
                return;
            }

            // Check for standard Unity Buttons
            Button uiButton = result.gameObject.GetComponentInParent<Button>();
            if (uiButton != null && uiButton.interactable)
            {
                uiButton.onClick.Invoke();
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
            playerCursor.localScale = new Vector3(s, s, 1f);
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
            playerCursor.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        playerCursor.localScale = Vector3.one;
        cursorAnimCoroutine = null;
    }

    public void NavigateToPlay()
    {
        SceneManager.LoadScene(playSceneName);
    }
}