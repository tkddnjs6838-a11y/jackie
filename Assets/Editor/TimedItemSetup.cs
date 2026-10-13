using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class TimedItemSetup
{
    static TimedItemSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string path="output/gun-effects/timed-setup.txt";
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(path))return;
        File.Delete(path);Apply();
    }
    [MenuItem("Tools/Items/Apply Timed Item Durations")]
    static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        foreach(var effects in Object.FindObjectsByType<PowerUpEffects>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            Undo.RecordObject(effects,"Apply timed item durations");
            effects.rageSeconds=5;effects.frenzySeconds=5;effects.shieldSeconds=3;effects.slowSeconds=5;
            EditorUtility.SetDirty(effects);EditorSceneManager.MarkSceneDirty(effects.gameObject.scene);
        }
        File.WriteAllText("output/gun-effects/timed-setup-result.txt","Applied live scene durations: Rage 5, Frenzy 5, Shield 3, Slow 5.\n");
    }
}
