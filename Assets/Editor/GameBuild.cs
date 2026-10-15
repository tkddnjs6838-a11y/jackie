using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class GameBuild
{
    static GameBuild(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        string request=File.Exists("output/build-ios.request")?"output/build-ios.request":"output/build-game.request";
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(request))return;
        File.Delete(request);if(request.Contains("ios"))BuildIOS();else BuildMac();
    }
    [MenuItem("Tools/Build/Mac Game")]
    public static void BuildMac()=>Build(BuildTarget.StandaloneOSX,"output/builds/WakuballSmash.app");
    [MenuItem("Tools/Build/iOS Xcode Project")]
    public static void BuildIOS()
    {
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS,"com.hsw.wakuballsmash");
        PlayerSettings.iOS.appleEnableAutomaticSigning=true;
        PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
        Build(BuildTarget.iOS,"output/builds/iOS");
    }
    static void Build(BuildTarget target,string destination)
    {
        Directory.CreateDirectory("output/builds");
        string result="output/builds/"+target+"-result.txt";
        try
        {
            if(!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target),target))throw new InvalidOperationException(target+" build module is not installed.");
            var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            if(scenes.Length!=6 || scenes[0]!="Assets/Scenes/SampleScene.unity")throw new InvalidOperationException("Expected gameplay scene followed by five stages.");
            File.WriteAllText(result,"BUILDING\n");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=destination,target=target,options=BuildOptions.None});
            File.WriteAllText(result,$"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nPath: {Path.GetFullPath(destination)}\n");
            if(report.summary.result!=BuildResult.Succeeded)Debug.LogError("Game build failed; see "+result);
        }
        catch(Exception e){File.WriteAllText(result,"FAILED\n"+e);Debug.LogException(e);}
    }
}
