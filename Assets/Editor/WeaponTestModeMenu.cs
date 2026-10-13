using System.IO;
using UnityEditor;

public static class WeaponTestModeMenu
{
    const string Menu="Tools/Weapons/All Guns At Start (Test)";
    [MenuItem(Menu)]
    static void Toggle()
    {
        bool enabled=!WeaponTestSettings.AllGunsAtStart;
        File.WriteAllText("Assets/Resources/WeaponTestSettings.json", "{\"allGunsAtStart\": "+(enabled?"true":"false")+"}\n");
        AssetDatabase.Refresh();
    }
    [MenuItem(Menu,true)]
    static bool Validate(){UnityEditor.Menu.SetChecked(Menu,WeaponTestSettings.AllGunsAtStart);return !EditorApplication.isPlayingOrWillChangePlaymode;}
}
