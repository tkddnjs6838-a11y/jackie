using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;
public static class BossArrivalChecks
{
    const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
    static string report;
    static void Check(bool ok,string msg){if(!ok)throw new Exception("BOSS ARRIVAL FAIL: "+msg);report+="PASS: "+msg+"\n";File.WriteAllText("output/gun-effects/boss-arrival-checks.txt",report);}
    static void Call(EnemySpawner s,string method,params object[] args)=>typeof(EnemySpawner).GetMethod(method,P).Invoke(s,args);
    static void Set(EnemySpawner s,string field,object value)=>typeof(EnemySpawner).GetField(field,P).SetValue(s,value);
    public static void Run()=>Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Test());
    static IEnumerator Test()
    {
        report="";var flow=Object.FindFirstObjectByType<GameFlowManager>();flow.startButton.onClick.Invoke();
        var s=Object.FindFirstObjectByType<EnemySpawner>();s.enabled=false;var player=Object.FindFirstObjectByType<PlayerHealth>();player.GrantInvincibility(1000);
        var controller=player.GetComponent<CharacterController>();var movement=player.GetComponent<PlayerController>();movement.enabled=false;controller.enabled=false;var original=player.transform.position;
        try
        {
            Set(s,"elapsed",s.bossEarliestSeconds);Set(s,"killCount",s.killsToSpawnBoss);
            Check(s.bossWarningSeconds==3,"normal warning lasts three seconds");Call(s,"TrySpawnBoss");
            Check(s.BossWarningActive && BossCount()==0,"threshold opens warning without spawning boss");
            var marked=s.BossArrivalPosition;float remaining=s.BossWarningRemaining;Call(s,"TrySpawnBoss");
            Check(s.BossArrivalPosition==marked && s.BossWarningRemaining==remaining,"repeated requests preserve one warning and fixed position");
            var visual=Object.FindFirstObjectByType<BossArrivalWarning>();var ring=visual.GetComponent<LineRenderer>();
            Check(ring.positionCount==64 && ring.loop && ring.sharedMaterial.shader.isSupported,"ground ring uses supported material and bounded geometry");
            Capture(marked);
            Call(s,"TickBossWarning",1f);Check(Mathf.Approximately(s.BossWarningRemaining,2),"scaled countdown advances");
            Time.timeScale=0;Call(s,"TickBossWarning",100f);Check(Mathf.Approximately(s.BossWarningRemaining,2) && BossCount()==0,"pause freezes countdown and boss spawn");Time.timeScale=1;
            player.transform.position=marked;Call(s,"TickBossWarning",2.1f);
            Check(!s.BossWarningActive && BossCount()==0,"occupied spawn point cancels arrival without damaging player");
            player.transform.position=original;Call(s,"TrySpawnBoss");Check(s.BossWarningActive && Mathf.Approximately(s.BossWarningRemaining,3),"unsafe arrival retries with a full warning");
            Call(s,"TickBossWarning",2.9f);Check(BossCount()==0,"boss absent before warning completes");
            marked=s.BossArrivalPosition;Call(s,"TickBossWarning",.2f);
            Check(!s.BossWarningActive && BossCount()==1,"warning completes and spawns one boss");
            foreach(var e in Enemy.ActiveEnemies)if(e.isBoss)Check(Vector3.Distance(e.transform.position,marked)<.01f,"boss spawns at advertised position");
            Call(s,"TrySpawnBoss");Check(!s.BossWarningActive && BossCount()==1,"boss cannot warn or spawn twice");
            Set(s,"bossSpawned",false);Call(s,"TrySpawnBoss");Check(s.BossWarningActive,"cleanup fixture opens warning");
            typeof(RunStats).GetField("<Running>k__BackingField",P).SetValue(RunStats.Instance,false);Call(s,"TickBossWarning",0f);
            Check(!s.BossWarningActive,"run end clears warning even while paused");
            typeof(RunStats).GetField("<Running>k__BackingField",P).SetValue(RunStats.Instance,true);
            Call(s,"TrySpawnBoss");Call(s,"OnDisable");Check(!s.BossWarningActive,"disabling spawner clears warning");
            yield return null;
            Check(Object.FindObjectsByType<BossArrivalWarning>(FindObjectsSortMode.None).Length==0,"warning objects cleaned up");
            Debug.Log("BOSS ARRIVAL CHECKS COMPLETE\n"+report);
        }
        finally{Time.timeScale=1;Call(s,"CancelBossWarning");player.transform.position=original;controller.enabled=true;movement.enabled=true;s.enabled=true;}
    }
    static int BossCount(){int n=0;foreach(var e in Enemy.ActiveEnemies)if(e.isBoss)n++;return n;}
    static void Capture(Vector3 at)
    {
        var root=new GameObject("WarningCapture",typeof(Camera));var camera=root.GetComponent<Camera>();camera.transform.position=at+new Vector3(0,12,-14);camera.transform.LookAt(at);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;
        var hud=GameObject.Find("BossArrivalHUD").GetComponent<Canvas>();hud.renderMode=RenderMode.ScreenSpaceCamera;hud.worldCamera=camera;hud.planeDistance=1;
        var rt=new RenderTexture(1280,720,24);var old=RenderTexture.active;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("output/gun-effects/boss-arrival.png",texture.EncodeToPNG());}
        finally{hud.renderMode=RenderMode.ScreenSpaceOverlay;hud.worldCamera=null;RenderTexture.active=old;Object.Destroy(rt);Object.Destroy(texture);Object.Destroy(root);}
    }
}
