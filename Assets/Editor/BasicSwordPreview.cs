using UnityEngine;
using UnityEditor;
using System.Collections;
using System.IO;
public static class BasicSwordPreview
{
    public static void Run(){UnityEngine.Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Capture());}
    public static void Record(){UnityEngine.Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Frames());}
    static IEnumerator Frames()
    {
        var a=UnityEngine.Object.FindFirstObjectByType<PlayerAutoAttack>();
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Models/WakuballB/B_Ready.anim");
        Directory.CreateDirectory("output/basic-sword-motion/frames");
        for(int i=0;i<32;i++)
        {
            a.ResetSwordPose();clip.SampleAnimation(a.animator.gameObject,0);a.ApplySwordPose(i/31f,true);
            yield return new WaitForEndOfFrame();
            var tex=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("output/basic-sword-motion/frames/"+i.ToString("00")+".png",tex.EncodeToPNG());UnityEngine.Object.Destroy(tex);
        }
        File.WriteAllText("output/basic-sword-motion/frames/done.txt","COMPLETE");
    }
    static IEnumerator Capture()
    {
        var flow=UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
        var map=UnityEngine.Object.FindFirstObjectByType<MapSandbox>();while(map.Busy)yield return null;
        if(!RunStats.Instance.Running)flow.startButton.onClick.Invoke();
        UnityEngine.Object.FindFirstObjectByType<EnemySpawner>().enabled=false;
        foreach(var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(enemy.gameObject);
        var attack=UnityEngine.Object.FindFirstObjectByType<PlayerAutoAttack>();
        attack.GetComponent<PlayerHealth>().GrantInvincibility(1000);
        attack.enabled=false;attack.GetComponent<PlayerController>().enabled=false;attack.GetComponent<PlayerDash>().enabled=false;
        attack.animator.enabled=false;
        var source=Camera.main;var cam=new GameObject("SwordPosePreview").AddComponent<Camera>();cam.CopyFrom(source);source.enabled=false;source.tag="Untagged";cam.tag="MainCamera";
        cam.fieldOfView=35;
        cam.transform.position=attack.transform.position+attack.transform.forward*4.2f+attack.transform.right*2.8f+Vector3.up*2.3f;
        cam.transform.LookAt(attack.transform.position+Vector3.up*.9f);
        var light=new GameObject("PreviewLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=cam.transform.rotation;
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Models/WakuballB/B_Ready.anim");
        Directory.CreateDirectory("output/basic-sword-motion");
        float[] phases={0f,.16f,.28f,.40f,1f};
        for(int i=0;i<phases.Length;i++)
        {
            attack.ResetSwordPose();clip.SampleAnimation(attack.animator.gameObject,0);attack.ApplySwordPose(phases[i],true);
            yield return new WaitForEndOfFrame();
            var tex=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("output/basic-sword-motion/pose-"+i+".png",tex.EncodeToPNG());UnityEngine.Object.Destroy(tex);
        }
        File.WriteAllText("output/basic-sword-motion/capture.txt","COMPLETE: ready, windup, contact, follow-through, recovery; sampled actual gameplay pose; no attack effects");
    }
}
