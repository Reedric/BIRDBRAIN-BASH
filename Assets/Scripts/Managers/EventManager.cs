using System;
using System.Collections.Generic;
public static class EventManager
{
    // Listeners for the Score event
    public static List<Func<bool, bool>> scoreListeners = new();

    public static void SubscribeScore(Func<bool, bool> function)
    {
        scoreListeners.Add(function);
    }

    public static void UnsubscribeScore(Func<bool, bool> function)
    {
        if (function == null) return;
        // Remove first matching listener if present
        scoreListeners.Remove(function);
    }

    // If the left scores, alerts Score Listeners
    public static void LeftScored()
    {
        foreach (Func<bool, bool> listener in scoreListeners)
        {
            listener(true);
        }
    }

    // If the right scores, alerts the Score Listeners
    public static void RightScored()
    {
        foreach (Func<bool, bool> listener in scoreListeners)
        {
            listener(false);
        }
    }
}
