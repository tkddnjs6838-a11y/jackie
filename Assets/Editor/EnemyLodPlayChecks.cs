using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class EnemyLodPlayChecks
{
    static bool active;
    [MenuItem("Tools/Optimization/Compare Enemy LODs In Play")]
    public static void Run()
    {
        if (active || !EditorApplication.isPlaying || RunStats.Instance == null || RunStats.Instance.Running)
        { Debug.LogWarning("Run LOD play comparison at the title screen."); return; }
        Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Compare());
    }
    static IEnumerator Compare()
    {
        active = true;
        var camera = Camera.main; var position = camera.transform.position; var rotation = camera.transform.rotation;
        float scale = Time.timeScale;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var states = canvases.Select(c => c.enabled).ToArray();
        var objects = new List<GameObject>(); var errors = new List<string>();
        Application.LogCallback onLog = (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || message.Contains("Rendering stopped") || message.Contains("Bones do not match")) errors.Add(message); };
        Application.logMessageReceived += onLog;
        var report = new System.Text.StringBuilder();
        Directory.CreateDirectory("output/enemy-lod/play");
        try
        {
            Time.timeScale = 0f;
            foreach (var canvas in canvases) canvas.enabled = false;
            camera.transform.position = new Vector3(0, 27, -12); camera.transform.LookAt(new Vector3(0, 20, 3));
            for (int i = 0; i < 21; i++)
            {
                string name = EnemyLodWorkshop.Prefabs[i == 20 ? 4 : i % 4];
                var obj = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab"));
                objects.Add(obj);
                obj.transform.position = new Vector3((i % 5 - 2) * 1.8f, 20f, i / 5 * 1.8f);
                foreach (var behaviour in obj.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
                var animator = obj.GetComponentInChildren<Animator>();
                if (animator != null) { animator.Rebind(); animator.Update(.2f); animator.enabled = false; }
                obj.GetComponent<EnemyMeshLod>().SetLevel(0);
            }
            var props = Object.FindObjectsByType<EnemyMeshLod>(FindObjectsSortMode.None).Where(lod => lod.GetComponent<Enemy>() == null).ToArray();
            foreach (string stage in new[] { "original", "automatic", "damaged" })
            {
                foreach (var prop in props) { if (stage == "original") prop.SetLevel(0); else prop.UpdateForCamera(camera); }
                var levels = new int[3]; long meshTriangles = 0;
                foreach (var obj in objects)
                {
                    var lod = obj.GetComponent<EnemyMeshLod>();
                    if (stage != "original") lod.UpdateForCamera(camera);
                    if (stage == "damaged")
                    {
                        var bounds = lod.bindings[0].renderer.bounds;
                        var dent = obj.GetComponent<WaxDent>(); if (dent == null) dent = obj.AddComponent<WaxDent>();
                        dent.enabled = false; dent.Punch(bounds.center + Vector3.back * bounds.size.magnitude, .8f, false); dent.Sample(.045f);
                        var cracks = obj.GetComponent<WaxCrackVisual>(); if (cracks == null) cracks = obj.AddComponent<WaxCrackVisual>();
                        cracks.SetDamage(.8f); cracks.SyncSurface();
                    }
                    levels[lod.CurrentLevel]++;
                    foreach (var binding in lod.bindings)
                    {
                        var mesh = EnemyLodWorkshop.Source(binding.renderer);
                        for (int sub = 0; sub < mesh.subMeshCount; sub++) meshTriangles += (long)mesh.GetIndexCount(sub) / 3;
                    }
                }
                for (int frame = 0; frame < 8; frame++) yield return new WaitForEndOfFrame();
                string line = $"{stage}: 21 enemies, LOD counts {levels[0]}/{levels[1]}/{levels[2]}, body triangles={meshTriangles}, UnityStats triangles={UnityStats.triangles}, batches={UnityStats.batches}";
                report.AppendLine(line); Debug.Log("LOD PLAY " + line);
                ScreenCapture.CaptureScreenshot("output/enemy-lod/play/" + stage + ".png");
                yield return new WaitForSecondsRealtime(.2f);
            }
            // Exercise damaged meshes switching in both directions over real rendered frames.
            foreach (int level in new[] { 0, 1, 2, 0 })
            {
                foreach (var obj in objects)
                {
                    obj.GetComponent<EnemyMeshLod>().SetLevel(level);
                    obj.GetComponent<WaxDent>().Sample(.15f);
                    obj.GetComponent<WaxCrackVisual>().SyncSurface();
                }
                yield return new WaitForEndOfFrame();
            }
            if (errors.Count > 0) throw new Exception("LOD play errors: " + string.Join("; ", errors.Distinct()));
            report.AppendLine("PASS: real-frame mesh swaps, damaged LOD swaps and no runtime/render errors. This compares geometry, not game FPS.");
            File.WriteAllText("output/enemy-lod/play/comparison.txt", report.ToString());
            Debug.Log("LOD PLAY COMPARISON PASS");
        }
        finally
        {
            foreach (var obj in objects) if (obj != null) Object.Destroy(obj);
            camera.transform.SetPositionAndRotation(position, rotation);
            for (int i = 0; i < canvases.Length; i++) if (canvases[i] != null) canvases[i].enabled = states[i];
            Time.timeScale = scale;
            Application.logMessageReceived -= onLog;
            active = false;
        }
    }
}
