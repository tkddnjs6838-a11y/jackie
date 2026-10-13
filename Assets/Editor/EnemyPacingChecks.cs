using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;
public static class EnemyPacingChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static string report;
    static void Check(bool ok,string message){if(!ok)throw new Exception("PACING FAIL: "+message);report+="PASS: "+message+"\n";File.WriteAllText("output/gun-effects/pacing-checks.txt",report);}
    static void Set(EnemySpawner s,string field,object value)=>typeof(EnemySpawner).GetField(field,Private).SetValue(s,value);
    static object Call(EnemySpawner s,string method)=>typeof(EnemySpawner).GetMethod(method,Private).Invoke(s,null);
    static bool BossSpawned(EnemySpawner s)=>(bool)typeof(EnemySpawner).GetField("bossSpawned",Private).GetValue(s);
    public static void Run()=>Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Test());
    static IEnumerator Test()
    {
        report="";var s=Object.FindFirstObjectByType<EnemySpawner>();var flow=Object.FindFirstObjectByType<GameFlowManager>();s.enabled=false;
        Check(Mathf.Approximately(s.initialInterval,1.4f) && s.enemiesPerSpawn==1 && s.killsToSpawnBoss==40 && s.bossEarliestSeconds==60,"live scene uses normal-mode pacing");
        Call(s,"Update");Check(Enemy.AliveRegularCount==0,"title spawns no immediate charger or regular enemies");
        flow.startButton.onClick.Invoke();var health=Object.FindFirstObjectByType<PlayerHealth>();health.GrantInvincibility(1000);
        var move=health.GetComponent<PlayerController>();move.enabled=false;
        try
        {
            Set(s,"elapsed",0f);Check(Mathf.Approximately(s.CurrentSpawnInterval,1.4f) && s.CurrentWaveCount==1 && s.CurrentAliveCap==18,"opening: one enemy every 1.4 seconds with cap 18");
            for(int i=0;i<100;i++)CheckPrefabBeforeUnlock(s);
            Check(true,"first 25 seconds only select plain enemies");
            foreach(var variant in s.variants)
            {
                if(variant.prefab.GetComponent<EnemyCharger>()!=null)Check(variant.unlockAfterSeconds==25,"charger random pool unlocks at 25 seconds");
                if(variant.prefab.GetComponent<EnemySplitter>()!=null)Check(variant.unlockAfterSeconds==50,"splitter unlocks at 50 seconds");
                if(variant.prefab.GetComponent<EnemyWarden>()!=null)Check(variant.unlockAfterSeconds==80,"warden unlocks at 80 seconds");
            }
            Check(s.firstChargerAtSeconds==25 && s.eliteFirstAt==45,"guaranteed charger and first elite are staggered");
            Set(s,"elapsed",60f);Check(Mathf.Approximately(s.CurrentSpawnInterval,1.2f) && s.CurrentAliveCap==24,"one minute tightens interval gradually");
            Set(s,"elapsed",90f);Check(s.CurrentWaveCount==2,"two-enemy waves start at 90 seconds");
            Set(s,"elapsed",180f);Check(s.CurrentWaveCount==3,"three-enemy waves start at 180 seconds");
            Set(s,"elapsed",3600f);Check(Mathf.Approximately(s.CurrentSpawnInterval,.5f) && s.CurrentAliveCap==35 && s.CurrentWaveCount==3,"late spawn interval and counts remain bounded");
            Set(s,"elapsed",59f);Set(s,"killCount",100);Call(s,"TrySpawnBoss");Check(!BossSpawned(s),"high kill count cannot summon boss before 60 seconds");
            Set(s,"elapsed",60f);Set(s,"killCount",39);Call(s,"TrySpawnBoss");Check(!BossSpawned(s),"time alone cannot summon boss before 40 kills");
            Set(s,"killCount",40);Time.timeScale=0;Call(s,"TrySpawnBoss");Call(s,"Update");Check(!BossSpawned(s) && (float)typeof(EnemySpawner).GetField("elapsed",Private).GetValue(s)==60f,"pause blocks boss and progression clock");
            Time.timeScale=1;s.bossWarningSeconds=0;Call(s,"TrySpawnBoss");Check(BossSpawned(s),"both thresholds summon boss in actual scene");
            int bosses=0;foreach(var e in Enemy.ActiveEnemies)if(e.isBoss)bosses++;
            Call(s,"TrySpawnBoss");int repeated=0;foreach(var e in Enemy.ActiveEnemies)if(e.isBoss)repeated++;
            Check(bosses==1 && repeated==1,"boss summons once only");
            Debug.Log("ENEMY PACING CHECKS COMPLETE\n"+report);
        }
        finally{Time.timeScale=1;move.enabled=true;s.enabled=true;}
        yield break;
    }
    static void CheckPrefabBeforeUnlock(EnemySpawner s)
    {
        Set(s,"elapsed",24f);
        if((GameObject)Call(s,"PickPrefab")!=s.enemyPrefab)throw new Exception("Special enemy appeared before opening unlock");
    }
}
