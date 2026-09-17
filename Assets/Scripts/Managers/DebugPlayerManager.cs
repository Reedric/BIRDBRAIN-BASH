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

    // Controller deviceId -> character indices
    private Dictionary<int, List<int>> controllerAssignments = new Dictionary<int, List<int>>();

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
        int connectedControllerCount = Gamepad.all.Count;

        for (int controllerIndex = 0; controllerIndex < connectedControllerCount; controllerIndex++)
        {
            AssignController(Gamepad.all[controllerIndex], controllerIndex, connectedControllerCount);
        }
    }

    private void AssignController(Gamepad controller)
    {
        if (controller == null || controllerAssignments.ContainsKey(controller.deviceId))
            return;

        int controllerIndex = -1;
        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            if (Gamepad.all[i] == controller)
            {
                controllerIndex = i;
                break;
            }
        }

        AssignController(controller, controllerIndex, Gamepad.all.Count);
    }

    private void AssignController(Gamepad controller, int controllerIndex, int connectedControllerCount)
    {
        if (controller == null || controllerAssignments.ContainsKey(controller.deviceId))
            return;

        List<int> targetCharacterIndices = new List<int>();

        if (connectedControllerCount == 2)
        {
            int firstCharacterIndex = controllerIndex * 2;
            targetCharacterIndices.Add(firstCharacterIndex);
            targetCharacterIndices.Add(firstCharacterIndex + 1);
        }
        else if (controllerIndex >= 0 && controllerIndex < 4)
        {
            targetCharacterIndices.Add(controllerIndex);
        }

        foreach (int characterIndex in targetCharacterIndices)
        {
            if (characterIndex >= characters.Length || characterIndex >= 4)
                continue;

            if (characters[characterIndex] == null)
            {
                Debug.LogWarning($"DebugPlayerManager: Character {characterIndex + 1} is not assigned.");
                continue;
            }

            PlayerInput playerInput = characters[characterIndex].GetComponent<PlayerInput>();

            if (playerInput == null)
            {
                Debug.LogWarning($"DebugPlayerManager: Character {characterIndex + 1} has no PlayerInput component.");
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
                Debug.LogError($"DebugPlayerManager: Failed to pair {controller.displayName} to Character {characterIndex + 1}: {e}");
                continue;
            }

            Debug.Log($"DebugPlayerManager: {controller.displayName} assigned to Character {characterIndex + 1}.");
        }

        if (targetCharacterIndices.Count > 0)
        {
            controllerAssignments.Add(controller.deviceId, targetCharacterIndices);
        }
        else
        {
            Debug.LogWarning($"DebugPlayerManager: No available character slot for {controller.displayName}.");
        }
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
                if (controllerAssignments.TryGetValue(controller.deviceId, out List<int> characterIndices))
                {
                    string assignedCharacters = string.Join(", ", characterIndices.Select(index => (index + 1).ToString()));
                    Debug.Log($"DebugPlayerManager: Controller disconnected: {controller.displayName} (Characters {assignedCharacters})");
                    controllerAssignments.Remove(controller.deviceId);
                }
                break;
        }
    }
}