using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class WaxCloneChecks
{
    [MenuItem("Tools/Validation/Check Split Clone Materials")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || RunStats.Instance == null || RunStats.Instance.Running) return;
        Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Check());
    }
    static IEnumerator Check()
    {
        var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnemySplitter.prefab"), Vector3.up*100, Quaternion.identity);
        GameObject child = null;
        try
        {
            source.GetComponent<WaxCrackVisual>().SetDamage(.8f);
            child = Object.Instantiate(source);
            Object.Destroy(source);
            yield return null; yield return null;
            var invalid = child.GetComponentsInChildren<Renderer>().Where(r => r.sharedMaterials.Any(m => m == null || m.shader == null || !m.shader.isSupported)).ToArray();
            Directory.CreateDirectory("output/wax-clone-check");
            var report = "SPLIT CLONE materials: invalid renderers=" + invalid.Length + "; " + string.Join(", ", invalid.Select(r => r.name));
            File.WriteAllText("output/wax-clone-check/result.txt", report);
            Debug.Log(report);
            if (invalid.Length > 0) Debug.LogError("SPLIT CLONE REPRODUCED: parent-owned crack material destroyed while inherited overlay survives.");
            else
            {
                child.GetComponent<WaxCrackVisual>().SetDamage(.7f);
                yield return null;
                var overlays = child.GetComponentsInChildren<Renderer>().Where(r => r.name == "WaxSurfaceCracks").ToArray();
                if (overlays.Length != 1 || overlays[0].sharedMaterial == null) throw new System.Exception("Child must create exactly one independent crack material");
                Debug.Log("SPLIT CLONE PASS: parent deletion and new child damage preserve valid materials.");
            }
        }
        finally { if(source != null) Object.Destroy(source); if(child != null) Object.Destroy(child); }
    }
    [MenuItem("Tools/Validation/Log Render Cost Inventory")]
    public static void Inventory()
    {
        foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && r is not ParticleSystemRenderer)
            .Select(r => new { renderer = r, mesh = r is SkinnedMeshRenderer skin ? skin.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh })
            .Where(p => p.mesh != null).OrderByDescending(p => Enumerable.Range(0,p.mesh.subMeshCount).Sum(s => (long)p.mesh.GetIndexCount(s))).Take(20))
            Debug.Log($"RENDER COST {r.renderer.name} mesh={r.mesh.name} triangles={Enumerable.Range(0,r.mesh.subMeshCount).Sum(s=>(long)r.mesh.GetIndexCount(s))/3} shader={r.renderer.sharedMaterial?.shader?.name}");
        foreach(var t in Terrain.activeTerrains) Debug.Log($"TERRAIN COST {t.name} resolution={t.terrainData.heightmapResolution} error={t.heightmapPixelError} instanced={t.drawInstanced}");
    }
}
