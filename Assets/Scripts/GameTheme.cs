using System;
using UnityEngine;

// The player's chosen look for the whole game (set in the start screen settings):
// Dark is the start screen's dramatic dusk, Bright the toy-like daylight. Saved in PlayerPrefs.
public static class GameTheme
{
    public enum Mode { Dark, Bright }
    const string Key = "Wakuball.Theme";
    public static event Action<Mode> Changed;

    public static Mode Current => (Mode)PlayerPrefs.GetInt(Key, (int)Mode.Dark);

    public static void Set(Mode mode)
    {
        if (mode == Current) return;
        PlayerPrefs.SetInt(Key, (int)mode);
        PlayerPrefs.Save();
        Changed?.Invoke(mode);
    }
}
