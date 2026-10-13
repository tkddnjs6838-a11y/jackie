using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

[InitializeOnLoad]
public static class CombatBuildWorkshop
{
    static CombatBuildWorkshop(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="output/combat-performance/build.request";
        if(EditorApplication.isCompiling||EditorApplication.isPlaying||!File.Exists(request))return;
        File.Delete(request);Build();
    }
    [MenuItem("Tools/Validation/Build Combat Performance Player")]
    public static void Build()
    {
        WaxPerformanceChecks.Run();
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=new[]{"Assets/Scenes/SampleScene.unity"},
            locationPathName="output/combat-performance/CombatProbe.app",
            target=BuildTarget.StandaloneOSX,options=BuildOptions.Development
        });
        File.WriteAllText("output/combat-performance/build-result.txt",report.summary.result+" errors="+report.summary.totalErrors);
        Debug.Log("COMBAT BUILD "+report.summary.result);
    }
}
