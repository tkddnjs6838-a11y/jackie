using UnityEngine;

// Explicit opt-in test preset; remove or disable the resource for normal play.
public static class MapSandboxSettings
{
    [System.Serializable] sealed class Config { public bool enabled; }
    static bool? cached;
    public static bool Enabled => cached ?? (cached = Read()).Value;
    static bool Read()
    {
        var asset = Resources.Load<TextAsset>("MapSandboxSettings");
        return asset != null && JsonUtility.FromJson<Config>(asset.text).enabled;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { cached = null; }
}
