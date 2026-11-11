using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Keeps track of which devices belong to which local player, from character select through the match, so that
/// players can swap between keyboard and controller at any time instead of being stuck with whatever they had when
/// a screen loaded.
///
/// - The keyboard and mouse belong to one player: whoever chose keyboard, or player 1 if nobody did. That player can
///   use them and a controller interchangeably, switching just by using the other one.
/// - A controller nobody has gets claimed by pressing anything on it. It goes back to the player who last had it,
///   or to a player left without one (theirs died or was unplugged), or else to the keyboard player.
/// - Online, each machine has one player, who can use any of its devices.
/// </summary>
public static class LocalPlayerDevices
{
    private const int maxPlayers = 4;

    private static readonly Gamepad[] gamepads = new Gamepad[maxPlayers];
    private static readonly bool[] usingKeyboard = new bool[maxPlayers];
    private static readonly Dictionary<int, int> homeSlots = new Dictionary<int, int>(); // gamepad deviceId -> player
    private static int playerCount;

    /// The player the keyboard and mouse belong to
    public static int KeyboardSlot { get; private set; }

    /// Whether the keyboard or mouse was used more recently than any controller, by anyone. For screens with only
    /// one player (the main menu, the Aviary, online), where everything belongs to them.
    public static bool KeyboardUsedLast { get; private set; }

    /// The controller used most recently, by anyone
    public static Gamepad LastUsedGamepad { get; private set; }

    /// Sets up the players for a local match, one entry per player saying whether they chose keyboard.
    /// Controllers players already have are kept, so they carry over from character select into the match.
    public static void Configure(IList<bool> isKBM)
    {
        EnsureRunner();

        playerCount = isKBM == null ? 0 : Mathf.Clamp(isKBM.Count, 0, maxPlayers);

        KeyboardSlot = 0;
        for (int i = 0; i < playerCount; ++i)
        {
            if (isKBM[i]) { KeyboardSlot = i; break; }
        }

        for (int i = 0; i < maxPlayers; ++i)
        {
            if (i >= playerCount || (gamepads[i] != null && !gamepads[i].added)) gamepads[i] = null;
            usingKeyboard[i] = i == KeyboardSlot && playerCount > 0 && isKBM[i];
        }

        // Hand out the remaining controllers in order to the players who chose one
        for (int i = 0; i < playerCount; ++i)
        {
            if (gamepads[i] != null || isKBM[i]) continue;

            Gamepad free = FirstUnassignedGamepad();
            if (free == null) break;
            Assign(free, i);
        }
    }

    public static bool IsConfigured => playerCount > 0;

    /// The controller a player is using, if they have one
    public static Gamepad GetGamepad(int slot)
    {
        if (slot < 0 || slot >= maxPlayers) return null;
        Gamepad pad = gamepads[slot];
        return pad != null && pad.added ? pad : null;
    }

    public static bool OwnsKeyboard(int slot) => slot == KeyboardSlot;

    /// Whether a player's last input came from the keyboard or mouse (as opposed to their controller)
    public static bool IsUsingKeyboard(int slot) => slot == KeyboardSlot && slot >= 0 && slot < maxPlayers && usingKeyboard[slot];

    /// Every device a player can use right now
    public static List<InputDevice> DevicesFor(int slot)
    {
        List<InputDevice> devices = new List<InputDevice>(3);
        if (OwnsKeyboard(slot))
        {
            if (Keyboard.current != null) devices.Add(Keyboard.current);
            if (Mouse.current != null) devices.Add(Mouse.current);
        }

        Gamepad pad = GetGamepad(slot);
        if (pad != null) devices.Add(pad);
        return devices;
    }

    /// Whether anything on a controller was pressed or pushed this frame
    public static bool WasUsedThisFrame(Gamepad pad)
    {
        if (pad == null || !pad.added) return false;

        return pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame
            || pad.buttonNorth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame
            || pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame
            || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame
            || pad.leftTrigger.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame
            || pad.leftStickButton.wasPressedThisFrame || pad.rightStickButton.wasPressedThisFrame
            || pad.dpad.ReadValue().sqrMagnitude > 0.25f
            || pad.leftStick.ReadValue().sqrMagnitude > 0.25f
            || pad.rightStick.ReadValue().sqrMagnitude > 0.25f;
    }

    /// Whether the keyboard or mouse was used this frame
    public static bool KeyboardOrMouseUsedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

        Mouse mouse = Mouse.current;
        return mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame
            || mouse.delta.ReadValue().sqrMagnitude > 4f);
    }

    // Runs at the start of every frame, before the screens and birds read their input
    private static void Tick()
    {
        if (KeyboardOrMouseUsedThisFrame())
        {
            KeyboardUsedLast = true;
            if (KeyboardSlot >= 0 && KeyboardSlot < maxPlayers) usingKeyboard[KeyboardSlot] = true;
        }

        foreach (Gamepad pad in Gamepad.all)
        {
            if (!WasUsedThisFrame(pad)) continue;

            KeyboardUsedLast = false;
            LastUsedGamepad = pad;

            if (playerCount == 0) continue;

            int slot = SlotOf(pad);
            if (slot < 0) slot = ClaimSlotFor(pad);
            if (slot >= 0) usingKeyboard[slot] = false;
        }

        if (LastUsedGamepad != null && !LastUsedGamepad.added) LastUsedGamepad = null;
    }

    private static int SlotOf(Gamepad pad)
    {
        for (int i = 0; i < playerCount; ++i)
        {
            if (gamepads[i] == pad) return i;
        }
        return -1;
    }

    // Decides who gets a controller nobody has yet. Returns -1 if every player already has one.
    private static int ClaimSlotFor(Gamepad pad)
    {
        int slot = -1;

        // Back to whoever had it last (e.g. it was unplugged and plugged back in)
        if (homeSlots.TryGetValue(pad.deviceId, out int home) && home < playerCount && GetGamepad(home) == null)
            slot = home;

        // A player with nothing to play with
        if (slot < 0)
        {
            for (int i = 0; i < playerCount; ++i)
            {
                if (i != KeyboardSlot && GetGamepad(i) == null) { slot = i; break; }
            }
        }

        // The keyboard player picking up a controller
        if (slot < 0 && GetGamepad(KeyboardSlot) == null && KeyboardSlot < playerCount)
            slot = KeyboardSlot;

        if (slot >= 0) Assign(pad, slot);
        return slot;
    }

    private static void Assign(Gamepad pad, int slot)
    {
        gamepads[slot] = pad;
        homeSlots[pad.deviceId] = slot;
    }

    private static Gamepad FirstUnassignedGamepad()
    {
        foreach (Gamepad pad in Gamepad.all)
        {
            if (SlotOf(pad) < 0) return pad;
        }
        return null;
    }

    // A hidden object that calls Tick every frame for the whole session
    private static LocalPlayerDevicesRunner runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRunner()
    {
        if (runner != null) return;

        GameObject runnerObject = new GameObject("LocalPlayerDevices");
        Object.DontDestroyOnLoad(runnerObject);
        runner = runnerObject.AddComponent<LocalPlayerDevicesRunner>();
    }

    [DefaultExecutionOrder(-1000)]
    private class LocalPlayerDevicesRunner : MonoBehaviour
    {
        // Update still runs while a local match is paused (Time.timeScale is 0), so swapping works in the pause menu too
        private void Update() => Tick();
    }
}
