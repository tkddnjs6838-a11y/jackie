using System.IO;
using UnityEditor;
using UnityEngine;

// Presets take effect on the next Play session; never alter a running inventory.
public static class WeaponModeMenu
{
    const string Normal="Tools/Gameplay/Normal Progression";
    const string Test="Tools/Gameplay/All Guns Test";
    [MenuItem(Normal)] static void UseNormal()=>Save(false);
    [MenuItem(Test)] static void UseTest()=>Save(true);
    [MenuItem(Normal,true)] static bool ValidateNormal(){Menu.SetChecked(Normal,!WeaponTestSettings.AllGunsAtStart);return !EditorApplication.isPlayingOrWillChangePlaymode;}
    [MenuItem(Test,true)] static bool ValidateTest(){Menu.SetChecked(Test,WeaponTestSettings.AllGunsAtStart);return !EditorApplication.isPlayingOrWillChangePlaymode;}
    static void Save(bool allGuns)
    {
        File.WriteAllText("Assets/Resources/WeaponTestSettings.json","{\"allGunsAtStart\": "+(allGuns?"true":"false")+"}\n");
        AssetDatabase.ImportAsset("Assets/Resources/WeaponTestSettings.json");
        Debug.Log(allGuns?"Next Play: all guns test loadout.":"Next Play: normal sword-first progression.");
    }
}
