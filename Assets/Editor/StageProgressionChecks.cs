using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class StageProgressionChecks
{
    static StageProgressionChecks(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string file="output/gun-effects/stage-check.txt";
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !File.Exists(file))return;
        File.Delete(file);Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Run());
    }
    static void Check(bool pass,string name)
    {
        File.AppendAllText("output/gun-effects/stage-results.txt",$"{(pass?"PASS":"FAIL")}: {name}\n");
        if(!pass)throw new Exception(name);
    }
    static IEnumerator Run()
    {
        File.WriteAllText("output/gun-effects/stage-results.txt","RUNNING\n");
        var maps=Object.FindFirstObjectByType<MapSandbox>();while(maps.Busy)yield return null;
        var flow=Object.FindFirstObjectByType<GameFlowManager>();flow.startButton.onClick.Invoke();
        Check(!MapSandboxSettings.Enabled,"normal mode without sandbox cheats");
        var player=Object.FindFirstObjectByType<PlayerWeapons>();
        for(int stage=0;stage<5;stage++)
        {
            var spawner=Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;
            if(stage==0){yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("output/gun-effects/stage-ui.png");yield return null;}
            Check(maps.CurrentMap==stage,"ordered stage "+stage);
            var guide=Object.FindFirstObjectByType<ObjectiveGuide>();
            Check(guide!=null && guide.target!=null && guide.target.name.StartsWith("Portal_"),"navigation bound to current portal");
            Check(!guide.guideRoot.activeSelf && !guide.bannerRoot.activeSelf,"old portal arrow and clear banner reset");
            Check(!maps.TravelTo((stage+1)%5),"portal locked before boss");
            var allowed=typeof(EnemySpawner).GetMethod("StageAllows",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(var pair in new[]{("EnemyCharger",1),("EnemySplitter",2),("EnemyWarden",3)})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+pair.Item1+".prefab");
                Check((bool)allowed.Invoke(spawner,new object[]{prefab})==(stage>=pair.Item2),pair.Item1+" unlock stage "+stage);
            }
            // Exercise the same boss-defeat event that Enemy.Die emits.
            ((Action)typeof(Enemy).GetField("OnBossDefeated",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null))?.Invoke();
            Check((bool)typeof(MapSandbox).GetField("portalUnlocked",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(maps),"boss opens portal");
            Check(guide.guideRoot.activeSelf,"current portal guidance revealed after boss");
            if(stage<4)
            {
                Check(maps.TravelTo(stage+1),"next stage accepted");while(maps.Busy)yield return null;
                Check(player==Object.FindFirstObjectByType<PlayerWeapons>(),"player and upgrades persist");
                Check(spawner.KillCount==0,"stage kill target resets");
            }
        }
        flow.CompleteStages();
        Check(!RunStats.Instance.Running && flow.victoryPanel.activeSelf,"final victory shown");
        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("output/gun-effects/result-ui.png");yield return null;
        File.AppendAllText("output/gun-effects/stage-results.txt","COMPLETE\n");
    }
}
