using UnityEditor;
using UnityEngine;
public static class MobilePreview
{
    const string Menu="Tools/Mobile/Touch Controls Preview (before Play)";
    [MenuItem(Menu)] static void Toggle(){EditorPrefs.SetBool("Wakuball.MobilePreview",!EditorPrefs.GetBool("Wakuball.MobilePreview",false));}
    [MenuItem(Menu,true)] static bool Validate(){UnityEditor.Menu.SetChecked(Menu,EditorPrefs.GetBool("Wakuball.MobilePreview",false));return !EditorApplication.isPlaying;}
}
