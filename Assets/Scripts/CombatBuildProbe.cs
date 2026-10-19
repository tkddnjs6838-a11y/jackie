#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using Unity.Profiling;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

// Explicit command-line opt-in only; never runs during normal gameplay.
public sealed class CombatBuildProbe : MonoBehaviour
{
    static string destination;
    static bool horde;
    static readonly string[] names = { "WaxDent.Sample", "Combat.Split", "Combat.Shards", "Combat.DamageNumber", "Combat.Damage" };
    readonly List<string> rows = new List<string>(30000);
    readonly List<float> frames = new List<float>(30000);
    readonly List<string> errors = new List<string>();
    ProfilerRecorder[] recorders;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var args=Environment.GetCommandLineArgs();
        int i=Array.IndexOf(args,"--combat-benchmark");
        if(i<0 || i+1>=args.Length)return;
        horde=Array.IndexOf(args,"--horde")>=0;
        destination=args[i+1]; UnityEngine.Random.InitState(713);
        DontDestroyOnLoad(new GameObject("CombatBuildProbe",typeof(CombatBuildProbe)));
    }
    IEnumerator Start()
    {
        Application.runInBackground=true; Application.targetFrameRate=-1; QualitySettings.vSyncCount=0;
        Application.logMessageReceived+=Log;
        yield return null; yield return null;
        var flow=FindFirstObjectByType<GameFlowManager>();
        var spawner=FindFirstObjectByType<EnemySpawner>();
        var attack=FindFirstObjectByType<PlayerAutoAttack>();
        spawner.enabled=false;
        flow.startButton.onClick.Invoke();
        Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        FindFirstObjectByType<PlayerHealth>().GrantInvincibility(1000);
        var swing=typeof(PlayerAutoAttack).GetMethod("Swing",BindingFlags.NonPublic|BindingFlags.Instance);
        var probes = new List<ProfilerRecorder>();
        var labels = new List<string>{"dent_ms","split_ms","shards_ms","numbers_ms","damage_ms"};
        for(int j=0;j<names.Length;j++)probes.Add(ProfilerRecorder.StartNew(ProfilerCategory.Scripts,names[j],1));
        var handles = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
        Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
        var catalog = new List<string>();
        foreach(var handle in handles)
        {
            var desc = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(handle);
            catalog.Add(desc.Name+" | "+desc.Category+" | "+desc.UnitType);
            if(desc.Name=="Main Thread" || desc.Name=="Render Thread" || desc.Name=="PlayerLoop" ||
               desc.Name.Contains("GC.Collect") || desc.Name.Contains("GarbageCollect") ||
               desc.Name.Contains("WaitForPresent") || desc.Name=="Physics.Simulate" || desc.Name=="Instantiate" || desc.Name=="Destroy" ||
               desc.Name.StartsWith("Update.") || desc.Name.StartsWith("PreLateUpdate.") || desc.Name.StartsWith("PostLateUpdate.") || desc.Name.StartsWith("EarlyUpdate."))
            {
                probes.Add(new ProfilerRecorder(handle,1,ProfilerRecorderOptions.Default|ProfilerRecorderOptions.StartImmediately));
                labels.Add(desc.Name.Replace(',', '_')+"_ms");
            }
        }
        Directory.CreateDirectory(destination);
        File.WriteAllLines(Path.Combine(destination,"markers.txt"),catalog);
        recorders=probes.ToArray();
        const int capacity=120000;
        var timings=new double[capacity];var durations=new float[capacity];
        var samples=new long[capacity,recorders.Length];int count=0;
        double start=Time.realtimeSinceStartupAsDouble, spawnAt=0, swingAt=0;
        int wave=0, peakAlive=0, materialChecks=0; double fullCrowdSeconds=0, scanAt=5;
        rows.Add("elapsed,frame_ms,"+string.Join(",",labels));
        while(Time.realtimeSinceStartupAsDouble-start<65)
        {
            double elapsed=Time.realtimeSinceStartupAsDouble-start;
            int alive=Enemy.AliveRegularCount;peakAlive=Math.Max(peakAlive,alive);
            if(elapsed>=5)
            {
                if(alive>=60)fullCrowdSeconds+=Time.unscaledDeltaTime;
                if(count<capacity)
                {
                    timings[count]=elapsed;durations[count]=Time.unscaledDeltaTime*1000;
                    for(int j=0;j<recorders.Length;j++)samples[count,j]=recorders[j].LastValue;
                    count++;
                }
            }
            if(elapsed>=spawnAt)
            {
                spawnAt+=horde ? .5 : 2;
                int spawnCount=horde ? Math.Max(0,60-Enemy.AliveRegularCount) : 3;
                for(int j=0;j<spawnCount && Enemy.AliveRegularCount<(horde ? 60 : 50);j++)
                {
                    var prefab=wave%3==0 && spawner.variants.Length>0 ? spawner.variants[wave%spawner.variants.Length].prefab : spawner.enemyPrefab;
                    var at=attack.transform.position+Quaternion.Euler(0,wave*37+j*137.5f,0)*Vector3.forward*(horde ? 3f+(j%5)*.7f : 2.2f+j*.5f);
                    if(Terrain.activeTerrain!=null)at.y=Terrain.activeTerrain.SampleHeight(at)+Terrain.activeTerrain.transform.position.y;
                    Instantiate(prefab,at,Quaternion.identity);
                }
                wave++;
            }
            if(horde && elapsed>=scanAt)
            {
                scanAt+=5;
                foreach(var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if(!renderer.enabled || !renderer.gameObject.activeInHierarchy)continue;
                    foreach(var material in renderer.sharedMaterials)
                    {
                        materialChecks++;
                        if(material==null || material.shader==null || !material.shader.isSupported || material.shader.name=="Hidden/InternalErrorShader")
                            errors.Add("Invalid material: "+renderer.name);
                    }
                }
            }
            if(elapsed>=swingAt && (!horde || elapsed>=35))
            {
                swingAt=elapsed+.55;
                Enemy nearest=null;float distance=float.MaxValue;
                foreach(var enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                {
                    if(enemy.IsDying)continue;
                    float d=(enemy.transform.position-attack.transform.position).sqrMagnitude;
                    if(d<distance){nearest=enemy;distance=d;}
                }
                if(nearest!=null){var direction=nearest.transform.position-attack.transform.position;direction.y=0;if(direction.sqrMagnitude>.01f)attack.transform.rotation=Quaternion.LookRotation(direction);}
                swing.Invoke(attack,null);
            }
            yield return null;
        }
        foreach(var recorder in recorders)recorder.Dispose();
        for(int i=0;i<count;i++)
        {
            frames.Add(durations[i]);
            string row=timings[i].ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+","+durations[i].ToString("F4",System.Globalization.CultureInfo.InvariantCulture);
            for(int j=0;j<recorders.Length;j++)row+=","+(samples[i,j]/1e6).ToString("F4",System.Globalization.CultureInfo.InvariantCulture);
            rows.Add(row);
        }
        File.WriteAllLines(Path.Combine(destination,"frames.csv"),rows);
        frames.Sort();
        string summary=$"Standalone development build; 5s warm-up + 60s measured, {Screen.width}x{Screen.height}, uncapped. Scripted waves, invincible player, real sword/AI/death/splits. Horde={horde}; peakAlive={peakAlive}; seconds at 60={fullCrowdSeconds:F2}; material slots checked={materialChecks}.\nFrames {frames.Count}; median {frames[frames.Count/2]:F2}ms; p95 {frames[(int)(frames.Count*.95)]:F2}ms; p99 {frames[(int)(frames.Count*.99)]:F2}ms; worst {frames[frames.Count-1]:F2}ms. Kills {RunStats.Instance.Kills}; errors {errors.Count}.\n"+string.Join("\n",errors);
        File.WriteAllText(Path.Combine(destination,"summary.txt"),summary);
        ScreenCapture.CaptureScreenshot(Path.Combine(destination,"combat.png"));
        yield return null;yield return null;
        Application.Quit();
    }
    void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
}
#endif
