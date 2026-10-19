using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Reflection;
public static class CombatFeelPreview
{
    public static void Run(string label)
    {
        UnityEngine.Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Capture(label));
    }
    static IEnumerator Capture(string label)
    {
        Random.InitState(7841);
        var flow=UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
        var map=UnityEngine.Object.FindFirstObjectByType<MapSandbox>();
        while(map.Busy)yield return null;
        if(!RunStats.Instance.Running)flow.startButton.onClick.Invoke();
        var spawner=UnityEngine.Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;
        foreach(var e in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(e.gameObject);
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerAutoAttack>();
        player.GetComponent<PlayerHealth>().GrantInvincibility(1000);
        var enemy=UnityEngine.Object.Instantiate(spawner.enemyPrefab,player.transform.position+player.transform.forward*1.8f,Quaternion.LookRotation(-player.transform.forward)).GetComponent<Enemy>();
        enemy.maxHealth=1000;
        yield return new WaitForSeconds(.5f);
        enemy.enabled=false;
        var source=Camera.main;var cam=new GameObject("CombatPreviewCamera").AddComponent<Camera>();cam.CopyFrom(source);source.enabled=false;source.tag="Untagged";cam.tag="MainCamera";cam.transform.position=player.transform.position+player.transform.right*4-player.transform.forward*2.5f+Vector3.up*2.8f;
        cam.transform.LookAt(player.transform.position+player.transform.forward*.9f+Vector3.up*.8f);
        foreach(var script in cam.GetComponents<MonoBehaviour>())script.enabled=false;
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        typeof(PlayerAutoAttack).GetMethod("Swing",flags).Invoke(player,null);
        float until=Time.unscaledTime+3;
        while(UnityEngine.Object.FindFirstObjectByType<HitImpact>()==null && Time.unscaledTime<until)yield return null;
        var impact=UnityEngine.Object.FindFirstObjectByType<HitImpact>();if(impact!=null)impact.enabled=false;
        var burst=UnityEngine.Object.FindFirstObjectByType<MeleeImpactBurst>();if(burst!=null)burst.enabled=false;
        yield return new WaitForEndOfFrame();
        var shot=ScreenCapture.CaptureScreenshotAsTexture();
        System.IO.File.WriteAllBytes("/Users/hsw/jackie/output/combat-feel-preview/"+label+".png",shot.EncodeToPNG());UnityEngine.Object.Destroy(shot);
        System.IO.File.WriteAllText("/Users/hsw/jackie/output/combat-feel-preview/"+label+".txt","healthFraction="+enemy.HealthFraction+" hitEffect="+(UnityEngine.Object.FindFirstObjectByType<HitImpact>()!=null));
    }
}
