using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Reflection;
using System.IO;
public static class AugmentSnapPreview
{
    public static void Run(){UnityEngine.Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Record());}
    static IEnumerator Record()
    {
        var flow=UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
        var map=UnityEngine.Object.FindFirstObjectByType<MapSandbox>();while(map.Busy)yield return null;
        if(!RunStats.Instance.Running)flow.startButton.onClick.Invoke();
        UnityEngine.Object.FindFirstObjectByType<EnemySpawner>().enabled=false;
        UnityEngine.Object.FindFirstObjectByType<UpgradeSystem>().TryOffer(UnityEngine.Object.FindFirstObjectByType<CharacterGrowth>());
        yield return new WaitForSecondsRealtime(.8f);
        var list=UnityEngine.Object.FindObjectsByType<AugmentHoverCrack>(FindObjectsSortMode.None);
        System.Array.Sort(list,(a,b)=>a.transform.position.x.CompareTo(b.transform.position.x));var h=list[1];
        h.enabled=false;h.OnPointerEnter(null);
        var render=typeof(AugmentHoverCrack).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Instance);
        Directory.CreateDirectory("output/augment-burst-random/frames");
        for(int i=0;i<40;i++)
        {
            float time=i/30f;float weight=time<1?Mathf.Clamp01(time*8):Mathf.Clamp01(1-(time-1)*8);
            render.Invoke(h,new object[]{Mathf.Min(time,1),weight});
            Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
            var tex=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("output/augment-burst-random/frames/"+i.ToString("00")+".png",tex.EncodeToPNG());UnityEngine.Object.Destroy(tex);
        }
        h.OnPointerExit(null);h.enabled=true;
        File.WriteAllText("output/augment-burst-random/complete.txt","40 frames: actual UI animation sampled at 30 fps, no gameplay time advance");
    }
}
