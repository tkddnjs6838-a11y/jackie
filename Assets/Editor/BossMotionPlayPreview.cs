using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;
using System.Collections;

public static class BossMotionPlayPreview
{
    static GameObject boss;
    static Camera camera;
    static int frame;
    static double next;
    static BossAttackMotion motion;
    static string report;
    static string folder;
    public static void Begin(bool waxBoss=false)
    {
        Cleanup();
        folder=waxBoss?"output/wax-boss-approved/unity-play":"output/boss-motion/play";
        var flow=Object.FindFirstObjectByType<GameFlowManager>();
        typeof(GameFlowManager).GetMethod("BeginGameplay",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(flow,null);
        foreach(var e in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))Object.Destroy(e.gameObject);
        var sp=Object.FindFirstObjectByType<EnemySpawner>();sp.enabled=false;
        var player=Object.FindFirstObjectByType<PlayerHealth>();
        var forward=player.transform.forward;forward.y=0;forward.Normalize();
        var pos=player.transform.position+forward*11;
        if(MapNavigation.SpawnPoint(pos,2.5f,out var safe))pos=safe;
        else pos=player.transform.position+forward*6;
        boss=Object.Instantiate(waxBoss?sp.finalBossPrefab:sp.bossPrefab,pos,Quaternion.LookRotation(-forward));
        var enemy=boss.GetComponent<Enemy>();enemy.moveSpeed=0;
        var attack=boss.GetComponent<BossAttacks>();attack.StopAllCoroutines();attack.slamWindUp=1.5f;attack.slamRecover=1.1f;
        motion=boss.GetComponent<BossAttackMotion>();
        camera=new GameObject("BossMotionCaptureCamera").AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;
        camera.transform.position=pos+boss.transform.TransformDirection(new Vector3(11,7,16));camera.transform.LookAt(pos+Vector3.up*2.7f);
        camera.fieldOfView=42;camera.targetTexture=null;
        attack.StartCoroutine((IEnumerator)typeof(BossAttacks).GetMethod("Slam",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(attack,null));
        frame=0;next=EditorApplication.timeSinceStartup;report="Unity Play mode, actual boss attack coroutine\n";
        Directory.CreateDirectory(folder);EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying || boss==null){Cleanup();return;}
        if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.085;
        var rt=RenderTexture.GetTemporary(800,600,24);var old=RenderTexture.active;
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(800,600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,800,600),0,0);image.Apply();
        File.WriteAllBytes(folder+"/"+frame.ToString("D3")+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
        camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);
        report+=frame+" "+motion.Phase+" "+motion.Progress.ToString("F3")+" impacts="+motion.ImpactCount+"\n";
        if(++frame>=34){File.WriteAllText(folder+"/report.txt",report);Cleanup();}
    }
    public static void Cleanup()
    {
        EditorApplication.update-=Tick;
        if(camera!=null)Object.DestroyImmediate(camera.gameObject);
        if(boss!=null)Object.DestroyImmediate(boss);
        camera=null;boss=null;
    }
}
