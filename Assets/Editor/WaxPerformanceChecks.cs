using System;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class WaxPerformanceChecks
{
    [MenuItem("Tools/Validation/Measure Wax Effect Cost")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { UnityEngine.Debug.LogWarning("Run wax benchmark outside Play Mode."); return; }
        var preview = new PreviewRenderUtility();
        try
        {
            var obj = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab"));
            preview.AddSingleGO(obj);
            foreach (var behaviour in obj.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            var animator = obj.GetComponentInChildren<Animator>();
            if (animator != null) { animator.Rebind(); animator.Update(0); animator.enabled = false; }
            var skin = obj.GetComponentInChildren<SkinnedMeshRenderer>();
            var dent = obj.AddComponent<WaxDent>();
            var contact = skin.bounds.center + Vector3.forward * skin.bounds.size.magnitude;
            var tick = typeof(WaxDent).GetMethod("AdvanceSurface", BindingFlags.Instance | BindingFlags.NonPublic);
            var advance = tick == null ? null : (Action<float, bool>)Delegate.CreateDelegate(typeof(Action<float, bool>), dent, tick);
            var times = new double[7];
            for (int pass = -1; pass < times.Length; pass++)
            {
                dent.Punch(contact, .8f, false);
                var timer = Stopwatch.StartNew();
                for (int frame = 1; frame <= 120; frame++)
                {
                    if (advance == null) dent.Sample(frame / 60f);
                    else advance(1f / 60f, true);
                }
                timer.Stop();
                if (pass >= 0) times[pass] = timer.Elapsed.TotalMilliseconds;
            }
            Array.Sort(times);
            string label = advance == null ? "baseline" : "optimized";
            string report = $"WAX COST {label}: median CPU {times[3]:F3} ms for one enemy / 120 simulated frames at 60 Hz. Vertices={skin.sharedMesh.vertexCount}. Fixed pose; not a game FPS measurement.";
            System.IO.Directory.CreateDirectory("output/wax-performance");
            System.IO.File.WriteAllText("output/wax-performance/" + label + ".txt", report);
            UnityEngine.Debug.Log(report);
            if (advance != null) Validate(dent, advance, contact, obj, preview);

        }
        finally { preview.Cleanup(); }
    }
    static void Validate(WaxDent dent, Action<float, bool> advance, Vector3 contact, GameObject obj, PreviewRenderUtility preview)
    {
        dent.Punch(contact, .8f, false);
        advance(0f, true);
        if (dent.CurrentWeight != 0f) throw new Exception("Paused dent advanced");
        advance(.1f, false);
        if (dent.CurrentWeight != 0f) throw new Exception("Hidden dent baked");
        advance(1f/60f, true);
        if (dent.CurrentWeight <= 0f) throw new Exception("Visible dent failed to resume");
        dent.FreezeSurface();
        float frozenWeight = dent.CurrentWeight;
        advance(1f, true);
        if (dent.CurrentWeight != frozenWeight) throw new Exception("Corpse deformation advanced");
        dent.Punch(contact, .8f, false);
        advance(.045f, true);
        if (dent.CurrentWeight < 99f) throw new Exception("Repeated hit failed to reset dent");

        var before = new System.Collections.Generic.HashSet<WaxShardPiece>(Object.FindObjectsByType<WaxShardPiece>(FindObjectsSortMode.None));
        var burst = obj.GetComponent<WaxShardBurst>();
        if (burst == null) burst = obj.AddComponent<WaxShardBurst>();
        burst.Burst(Color.green, true);
        var pieces = new System.Collections.Generic.List<WaxShardPiece>();
        foreach (var piece in Object.FindObjectsByType<WaxShardPiece>(FindObjectsSortMode.None))
            if (!before.Contains(piece)) { pieces.Add(piece); preview.AddSingleGO(piece.gameObject); }
        if (pieces.Count != burst.chipsOnDeath) throw new Exception("Shard count changed");
        var pair = pieces[0].GetComponent<Renderer>().sharedMaterials;
        foreach (var piece in pieces)
        {
            var renderer = piece.GetComponent<Renderer>();
            var values = renderer.sharedMaterials;
            if (values[0] != pair[0] || values[1] != pair[1]) throw new Exception("Shard materials are not shared");
            if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off || renderer.receiveShadows)
                throw new Exception("Shard shadows still enabled");
        }
        Object.DestroyImmediate(pieces[0].gameObject);
        if (pair[0] == null || pair[1] == null) throw new Exception("Materials destroyed before last fragment");
        for (int i = 1; i < pieces.Count; i++) Object.DestroyImmediate(pieces[i].gameObject);
        if (pair[0] != null || pair[1] != null) throw new Exception("Shard materials leaked");
        UnityEngine.Debug.Log("WAX OPTIMIZATION CHECKS PASS: pause, hidden/resume, corpse freeze, repeated hit, shard count, shared materials, no shadows, lifetime cleanup.");
    }

}
