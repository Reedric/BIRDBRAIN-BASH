using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Makes input only reach the window that has focus. The game keeps running in the background so that
/// online matches stay connected, but by default a controller would then drive every open window at once,
/// like a Multiplayer Play Mode window or a second build on the same PC.
/// </summary>
public static class InputFocusSettings
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.ResetAndDisableAllDevices;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDevicesRespectGameViewFocus;
    }
}
