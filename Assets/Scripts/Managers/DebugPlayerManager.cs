using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Pairs up to 4 connected gamepads to 4 pre-placed characters that already
// carry their own PlayerInput/CharacterMovement — no movement logic here.
public class DebugPlayerManager : MonoBehaviour
{
    [Header("Characters")]
    [Tooltip("Assign up to 4 characters in the Inspector. Each must already have a PlayerInput component.")]
    public Transform[] characters = new Transform[4];

    // Controller deviceId -> character index
    private Dictionary<int, int> controllerAssignments = new Dictionary<int, int>();

    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void Start()
    {
        foreach (Gamepad controller in Gamepad.all)
        {
            AssignController(controller);
        }
    }

    private void AssignController(Gamepad controller)
    {
        if (controller == null || controllerAssignments.ContainsKey(controller.deviceId))
            return;

        // Find the first unused character slot
        for (int i = 0; i < characters.Length && i < 4; i++)
        {
            if (controllerAssignments.Values.Contains(i))
                continue;

            if (characters[i] == null)
            {
                Debug.LogWarning($"DebugPlayerManager: Character {i + 1} is not assigned.");
                continue;
            }

            PlayerInput playerInput = characters[i].GetComponent<PlayerInput>();

            if (playerInput == null)
            {
                Debug.LogWarning($"DebugPlayerManager: Character {i + 1} has no PlayerInput component.");
                continue;
            }

            // Wrapped so one bad pairing can't throw out of the loop and silently skip the rest of the gamepads.
            try
            {
                // Re-pairs the device exclusively to this character's existing input/movement setup.
                playerInput.SwitchCurrentControlScheme("Gamepad", controller);
                playerInput.actions.FindActionMap("Player")?.Enable();
                playerInput.actions.FindActionMap("UI")?.Enable();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"DebugPlayerManager: Failed to pair {controller.displayName} to Character {i + 1}: {e}");
                continue;
            }

            controllerAssignments.Add(controller.deviceId, i);

            Debug.Log($"DebugPlayerManager: {controller.displayName} assigned to Character {i + 1}.");
            return;
        }

        Debug.LogWarning($"DebugPlayerManager: No available character slot for {controller.displayName}.");
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is not Gamepad controller)
            return;

        switch (change)
        {
            case InputDeviceChange.Added:
            case InputDeviceChange.Reconnected:
                Debug.Log($"DebugPlayerManager: Controller connected: {controller.displayName}");
                AssignController(controller);
                break;

            case InputDeviceChange.Disconnected:
                if (controllerAssignments.TryGetValue(controller.deviceId, out int characterIndex))
                {
                    Debug.Log($"DebugPlayerManager: Controller disconnected: {controller.displayName} (Character {characterIndex + 1})");
                    controllerAssignments.Remove(controller.deviceId);
                }
                break;
        }
    }
}