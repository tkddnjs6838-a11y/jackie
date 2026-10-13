using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class EnemyLodWorkshop
{
    public static readonly string[] Prefabs = { "Enemy", "EnemyCharger", "EnemySplitter", "EnemyWarden", "BossEnemy" };
    public static readonly string[] Props = { "TreeProp", "RockProp", "BushProp", "CrystalProp", "PillarProp" };
    static bool propsOnly;
    static string PrefabPath(string name) => "Assets/Prefabs/" + (name.EndsWith("Prop") ? "Props/" : "") + name + ".prefab";
    [MenuItem("Tools/Optimization/Export Prop LOD Sources")]
    public static void ExportProps() { propsOnly = true; try { Export(); } finally { propsOnly = false; } }
    [MenuItem("Tools/Optimization/Import And Apply Prop LODs")]
    public static void ImportProps() { propsOnly = true; try { Import(); } finally { propsOnly = false; } }
    const string Work = "output/enemy-lod";
    [Serializable] public class Indices { public int[] values; }
    [Serializable] public class Data
    {
        public Vector3[] vertices;
        public Vector2[] uv;
        public Vector3[] normals;
        public int[] bones;
        public float[] weights;
        public Indices[] submeshes;
    }
    public static Mesh Source(Renderer renderer) => renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
    public static Renderer[] Bodies(GameObject obj) => obj.GetComponentsInChildren<Renderer>(true).Where(r => Source(r) != null).ToArray();
    [MenuItem("Tools/Optimization/Export Enemy LOD Sources")]
    public static void Export()
    {
        if (EditorApplication.isPlaying) return;
        Directory.CreateDirectory(Work);
        foreach (var name in propsOnly ? Props : Prefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name));
            var renderers = Bodies(prefab);
            for (int index = 0; index < renderers.Length; index++)
            {
                var mesh = Source(renderers[index]);
                if (mesh.blendShapeCount != 0) throw new Exception("Blend shapes need explicit preservation: " + name);
                var weights = mesh.boneWeights;
                var data = new Data { vertices = mesh.vertices, uv = mesh.uv, normals = mesh.normals,
                    bones = new int[weights.Length * 4], weights = new float[weights.Length * 4],
                    submeshes = new Indices[mesh.subMeshCount] };
                for (int i = 0; i < weights.Length; i++)
                {
                    var w = weights[i]; int k = i * 4;
                    data.bones[k] = w.boneIndex0; data.bones[k+1] = w.boneIndex1; data.bones[k+2] = w.boneIndex2; data.bones[k+3] = w.boneIndex3;
                    data.weights[k] = w.weight0; data.weights[k+1] = w.weight1; data.weights[k+2] = w.weight2; data.weights[k+3] = w.weight3;
                }
                for (int s = 0; s < mesh.subMeshCount; s++) data.submeshes[s] = new Indices { values = mesh.GetTriangles(s) };
                File.WriteAllText(Work + "/" + name + "_" + index + "_source.json", JsonUtility.ToJson(data));
                Debug.Log($"LOD SOURCE {name}/{index}: {mesh.vertexCount} vertices, {mesh.triangles.Length/3} triangles, {mesh.bindposes.Length} bind poses, {weights.Length} weights, {renderers[index].GetType().Name}");
            }
        }
        Debug.Log("LOD EXPORT PASS");
    }
    [MenuItem("Tools/Optimization/Import And Apply Enemy LODs")]
    public static void Import()
    {
        if (EditorApplication.isPlaying) return;
        const string folder = "Assets/Models/EnemyLODs";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Models", "EnemyLODs");
        foreach (var name in propsOnly ? Props : Prefabs)
        {
            string path = PrefabPath(name);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var renderers = Bodies(root);
                var controller = root.GetComponent<EnemyMeshLod>();
                if (controller == null) controller = root.AddComponent<EnemyMeshLod>();
                controller.bindings = new EnemyMeshLod.Binding[renderers.Length];
                for (int index = 0; index < renderers.Length; index++)
                {
                    var source = Source(renderers[index]);
                    var levels = new Mesh[] { source, null, null };
                    for (int level = 1; level <= 2; level++)
                    {
                        string key = name + "_" + index + "_lod" + level;
                        var data = JsonUtility.FromJson<Data>(File.ReadAllText(Work + "/" + key + ".json"));
                        var mesh = new Mesh { name = key, indexFormat = data.vertices.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                        mesh.vertices = data.vertices; mesh.uv = data.uv; mesh.normals = data.normals;
                        mesh.bindposes = source.bindposes;
                        // Optimized rigs bind by hashed bone names rather than exposed Transform arrays.
                        var sourceData = new SerializedObject(source);
                        var targetData = new SerializedObject(mesh);
                        foreach (string field in new[] { "m_BoneNameHashes", "m_RootBoneNameHash", "m_BonesAABB" })
                        {
                            var property = sourceData.FindProperty(field);
                            if (property != null) targetData.CopyFromSerializedProperty(property);
                        }
                        targetData.ApplyModifiedPropertiesWithoutUndo();
                        if (data.weights.Length > 0)
                        {
                            var weights = new BoneWeight[data.vertices.Length];
                            for (int i = 0; i < weights.Length; i++)
                            {
                                int k = i * 4;
                                weights[i] = new BoneWeight { boneIndex0 = data.bones[k], boneIndex1 = data.bones[k+1], boneIndex2 = data.bones[k+2], boneIndex3 = data.bones[k+3],
                                    weight0 = data.weights[k], weight1 = data.weights[k+1], weight2 = data.weights[k+2], weight3 = data.weights[k+3] };
                            }
                            mesh.boneWeights = weights;
                        }
                        mesh.subMeshCount = data.submeshes.Length;
                        for (int sub = 0; sub < mesh.subMeshCount; sub++) mesh.SetTriangles(data.submeshes[sub].values, sub);
                        mesh.RecalculateBounds(); mesh.RecalculateTangents();
                        ValidateMesh(source, mesh);
                        string assetPath = folder + "/" + key + ".asset";
                        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                        if (existing == null) { AssetDatabase.CreateAsset(mesh, assetPath); levels[level] = mesh; }
                        else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); levels[level] = existing; EditorUtility.SetDirty(existing); }
                    }
                    controller.bindings[index] = new EnemyMeshLod.Binding { renderer = renderers[index], filter = renderers[index].GetComponent<MeshFilter>(), levels = levels };
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("LOD IMPORT PASS: " + (propsOnly ? "props" : "enemies") + ", five prefabs, two reduced meshes each; originals preserved.");
    }
    static void ValidateMesh(Mesh original, Mesh mesh)
    {
        if (mesh.triangles.Length >= original.triangles.Length || mesh.subMeshCount != original.subMeshCount)
            throw new Exception("LOD topology did not reduce safely");
        if (Vector3.Distance(mesh.bounds.center, original.bounds.center) > original.bounds.size.magnitude * .03f ||
            Vector3.Distance(mesh.bounds.size, original.bounds.size) > original.bounds.size.magnitude * .06f)
            throw new Exception("LOD silhouette bounds changed too much: " + mesh.name);
        foreach (var weight in mesh.boneWeights)
        {
            float sum = weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3;
            if (Mathf.Abs(sum - 1f) > .001f) throw new Exception("LOD weight normalization failed");
            if (weight.boneIndex0 >= original.bindposes.Length || weight.boneIndex1 >= original.bindposes.Length ||
                weight.boneIndex2 >= original.bindposes.Length || weight.boneIndex3 >= original.bindposes.Length)
                throw new Exception("LOD bone index out of range");
        }
    }

}
