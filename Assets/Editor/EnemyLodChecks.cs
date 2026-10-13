using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class EnemyLodChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    [MenuItem("Tools/Optimization/Review And Validate Enemy LODs")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) return;
        Directory.CreateDirectory("output/enemy-lod/review");
        foreach (string name in EnemyLodWorkshop.Prefabs)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                var obj = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab"));
                preview.AddSingleGO(obj);
                obj.transform.position = Vector3.zero; obj.transform.rotation = Quaternion.identity;
                foreach (var behaviour in obj.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
                var animator = obj.GetComponentInChildren<Animator>();
                if (animator != null) { animator.Rebind(); animator.Update(.2f); animator.enabled = false; }
                var lod = obj.GetComponent<EnemyMeshLod>();
                var body = lod.bindings[0].renderer;
                var skin = body as SkinnedMeshRenderer;
                var bounds = body.bounds;
                var camera = preview.camera;
                camera.fieldOfView = 60f;
                camera.orthographic = true; camera.orthographicSize = bounds.size.magnitude * .5f;
                camera.nearClipPlane = .01f; camera.farClipPlane = 500f;
                camera.transform.position = bounds.center + new Vector3(3f, 1.3f, 5f).normalized * bounds.size.magnitude * 2f;
                camera.transform.LookAt(bounds.center);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.18f, .20f, .24f);
                preview.lights[0].intensity = 1.3f; preview.lights[0].transform.rotation = Quaternion.Euler(35, -25, 0);
                preview.lights[1].intensity = .7f; preview.ambientColor = Color.gray;
                var baked = new Mesh();
                try
                {
                    Bounds baseBounds = default;
                    for (int level = 0; level <= 2; level++)
                    {
                        lod.SetLevel(level);
                        if (skin != null)
                        {
                            skin.BakeMesh(baked);
                            Debug.Log($"LOD POSE {name}/{level} mesh={skin.sharedMesh.vertexCount} baked={baked.vertexCount} bounds={baked.bounds} renderer={skin.bounds}");
                        }
                        var poseBounds = skin != null ? baked.bounds : EnemyLodWorkshop.Source(body).bounds;
                        if (level == 0) baseBounds = poseBounds;
                        else
                        {
                            Check(Vector3.Distance(baseBounds.center, poseBounds.center) < baseBounds.size.magnitude*.06f, name + " animated LOD center diverged");
                            Check(Vector3.Distance(baseBounds.size, poseBounds.size) < baseBounds.size.magnitude*.1f, name + " animated skin binding failed");
                        }
                        Capture(preview, name + "-lod" + level);
                    }
                    // Exercise both camera types and hysteresis without depending on the project quality bias.
                    camera.orthographicSize = bounds.size.y * QualitySettings.lodBias;
                    lod.UpdateForCamera(camera); Check(lod.CurrentLevel == 0, name + " near LOD failed");
                    camera.orthographicSize *= 3f; lod.UpdateForCamera(camera); Check(lod.CurrentLevel == 1, name + " medium LOD failed");
                    camera.orthographicSize *= 4f; lod.UpdateForCamera(camera); Check(lod.CurrentLevel == 2, name + " far LOD failed");
                    camera.orthographic = false;
                    camera.transform.position = bounds.center - camera.transform.forward * bounds.size.y * QualitySettings.lodBias;
                    lod.UpdateForCamera(camera); Check(lod.CurrentLevel == 0, name + " perspective near failed");
                    camera.transform.position = bounds.center - camera.transform.forward * bounds.size.y * QualitySettings.lodBias * 30f;
                    lod.UpdateForCamera(camera); Debug.Log($"LOD FAR {name} bounds={body.bounds} cam={camera.transform.position} level={lod.CurrentLevel}"); Check(lod.CurrentLevel == 2, name + " perspective far failed");
                    camera.orthographic = true; camera.orthographicSize = bounds.size.magnitude * .5f;
                    camera.transform.position = bounds.center + new Vector3(3f, 1.3f, 5f).normalized * bounds.size.magnitude * 2f;
                    camera.transform.LookAt(bounds.center);
                    lod.SetLevel(0);
                    var dent = obj.GetComponent<WaxDent>(); if (dent == null) dent = obj.AddComponent<WaxDent>();
                    var cracks = obj.GetComponent<WaxCrackVisual>(); if (cracks == null) cracks = obj.AddComponent<WaxCrackVisual>();
                    bool deformed = dent.Punch(bounds.center + Vector3.forward * bounds.size.magnitude, .8f, false);
                    Check(deformed == (skin != null), name + " dent/fallback mismatch");
                    cracks.SetDamage(.8f);
                    foreach (int level in new[] { 0, 1, 2, 0 })
                    {
                        lod.SetLevel(level); dent.Sample(.045f); cracks.SyncSurface();
                        var surface = deformed ? dent.SurfaceRenderer : body;
                        var surfaceMesh = surface.GetComponent<MeshFilter>().sharedMesh;
                        Check(surfaceMesh.vertexCount == EnemyLodWorkshop.Source(body).vertexCount, name + " dent topology mismatch");
                        var crackFilter = surface.transform.Find("WaxSurfaceCracks").GetComponent<MeshFilter>();
                        Check(crackFilter.sharedMesh == surfaceMesh, name + " detached cracks");
                        foreach (var v in surfaceMesh.vertices) Check(float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z), name + " non-finite dent vertex");
                        Capture(preview, name + "-dent-lod" + level);
                    }
                }
                finally { Object.DestroyImmediate(baked); }
                Debug.Log("LOD CHECK PASS " + name + ": skinning, camera thresholds, reduced topology, dent/crack transitions 0-1-2-0");
            }
            finally { preview.Cleanup(); }
        }
        Debug.Log("LOD REVIEW PASS: all five enemy types");
    }
    static void Capture(PreviewRenderUtility preview, string name)
    {
        preview.BeginStaticPreview(new Rect(0,0,480,480)); preview.Render(true);
        var texture = preview.EndStaticPreview();
        File.WriteAllBytes("output/enemy-lod/review/" + name + ".png", texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
    }
}
