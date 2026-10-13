using UnityEngine;

// Explicit test preset. Missing config means normal progression.
public static class WeaponTestSettings
{
    [System.Serializable] sealed class Settings { public bool allGunsAtStart; }
    public static bool AllGunsAtStart
    {
        get
        {
            var config=Resources.Load<TextAsset>("WeaponTestSettings");
            return config!=null && JsonUtility.FromJson<Settings>(config.text).allGunsAtStart;
        }
    }
}
