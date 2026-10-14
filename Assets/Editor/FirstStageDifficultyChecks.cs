using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class FirstStageDifficultyChecks
{
    static FirstStageDifficultyChecks(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="output/gun-effects/first-stage-check.txt";
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !File.Exists(request))return;
        var maps=Object.FindFirstObjectByType<MapSandbox>();if(maps==null || maps.Busy)return;
        File.Delete(request);Run();
    }
    static void Check(bool value,string label)
    {
        File.AppendAllText("output/gun-effects/first-stage-results.txt",(value?"PASS: ":"FAIL: ")+label+"\n");
        if(!value)throw new Exception(label);
    }
    static void Run()
    {
        File.WriteAllText("output/gun-effects/first-stage-results.txt","RUNNING\n");
        var spawner=Object.FindFirstObjectByType<EnemySpawner>();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        spawner.ConfigureStage(0);
        Check(spawner.CurrentAliveCap==24 && spawner.CurrentWaveCount==2 && spawner.CurrentSpawnInterval==1.6f,"opening keeps a large, steady swarm");
        typeof(EnemySpawner).GetField("elapsed",flags).SetValue(spawner,600f);
        Check(spawner.CurrentAliveCap==24 && spawner.CurrentWaveCount==2 && spawner.CurrentSpawnInterval==1.6f,"no first-stage escalation after ten minutes");
        foreach(var name in new[]{"Enemy","BossEnemy"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+name+".prefab");
            var go=Object.Instantiate(prefab,new Vector3(1000,0,1000),Quaternion.identity);
            var e=go.GetComponent<Enemy>();
            typeof(EnemySpawner).GetMethod("ApplyStageDifficulty",flags).Invoke(spawner,new object[]{e});
            Check(e.CurrentHealth==(e.isBoss?600:18) && e.contactDamage<=(e.isBoss?8:4),name+" health and damage applied to live instance");
            if(e.isBoss){var b=go.GetComponent<BossAttacks>();Check(b.slamWindUp>=1.5f && b.restBetweenAttacks>=4 && b.chargeDamage==12,"boss gives longer dodge and attack windows");}
            Object.Destroy(go);
        }
        spawner.ConfigureStage(1);Check(spawner.CurrentAliveCap!=24 && spawner.killsToSpawnBoss==25,"stage two retains its own difficulty");
        spawner.ConfigureStage(0);
        File.AppendAllText("output/gun-effects/first-stage-results.txt","COMPLETE\n");
    }
}
