using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// A small rigid hair accessory follows the existing head bone without changing its rig.
public static class TopknotWorkshop
{
    [MenuItem("Tools/Character/Add Samurai Topknot %#&l")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        var animator = player.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman) return;
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        var previous = head.Find("SamuraiTopknot");
        

        var skin = animator.GetComponentInChildren<SkinnedMeshRenderer>();
        // Use actual mesh vertices: imported renderer bounds also include the outstretched arms.
        var baked = new Mesh();
        skin.BakeMesh(baked);
        float crown = float.NegativeInfinity;
        foreach (var vertex in baked.vertices)
        {
            // BakeMesh's default output includes scale; transform rotation/translation only.
            var world = skin.transform.position + skin.transform.rotation * vertex;
            crown = Mathf.Max(crown, player.transform.InverseTransformPoint(world).y);
        }
        Object.DestroyImmediate(baked);
        var headCenter = player.transform.InverseTransformPoint(head.position);
        var origin = new Vector3(headCenter.x, crown - 0.022f, headCenter.z - 0.015f);

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Reshape samurai topknot");
        if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
        var root = new GameObject("SamuraiTopknot");
        Undo.RegisterCreatedObjectUndo(root, "Add samurai topknot");
        root.transform.SetParent(head, false);
        var hair = Material("PlayerTopknotHair", new Color(0.055f, 0.055f, 0.055f), 0.22f);
        AddPart("GatheredHair", root.transform, player.transform, origin, hair,
            new[] { 0f, 0.018f, 0.037f, 0.054f, 0.070f, 0.082f },
            new[] { 0.073f, 0.068f, 0.044f, 0.032f, 0.043f, 0.038f },
            new[] { 0f, 0f, 0f, -0.003f, -0.006f, -0.007f });
        // The free end flares after the pinched knot, with a folded rim and inner surface.
        AddPart("SweptTuft", root.transform, player.transform, origin, hair,
            new[] { 0.069f, 0.086f, 0.12f, 0.156f, 0.186f, 0.19f, 0.182f, 0.152f, 0.12f, 0.09f, 0.083f },
            new[] { 0.029f, 0.036f, 0.052f, 0.078f, 0.095f, 0.093f, 0.084f, 0.067f, 0.042f, 0.023f, 0f },
            new[] { -0.006f, -0.008f, -0.013f, -0.019f, -0.025f, -0.025f, -0.025f, -0.019f, -0.013f, -0.008f, -0.008f });
        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveScene(player.scene);
        Debug.Log("TOPKNOT APPLIED: balloon-tail silhouette attached to Head, crown=" + crown);
        CharacterWorkshop.Audit();
    }

    static Material Material(string name, Color color, float smoothness)
    {
        string path = "Assets/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = name;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void AddPart(string name, Transform parent, Transform player, Vector3 origin,
        Material material, float[] heights, float[] radii, float[] offsets)
    {
        const int sides = 64;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int ring = 0; ring < heights.Length; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                // Irregular folds and an uneven open edge match a tied balloon's free end.
                float flare = name == "SweptTuft" ? Mathf.InverseLerp(0.08f, 0.18f, heights[ring]) : 0f;
                float folds = Mathf.Cos(angle * 5f + 0.35f);
                float radius = radii[ring] * (1f + (0.04f + 0.20f * flare) * folds);
                float y = heights[ring] + flare * (0.016f * Mathf.Sin(angle * 3f + 0.5f) + 0.008f * folds);
                var point = origin + 1.2f * new Vector3(Mathf.Cos(angle) * radius, y,
                    offsets[ring] + Mathf.Sin(angle) * radius * 0.85f);
                vertices.Add(parent.InverseTransformPoint(player.TransformPoint(point)));
                if (ring == heights.Length - 1) continue;
                int a = ring * sides + side;
                int b = ring * sides + (side + 1) % sides;
                int c = a + sides;
                int d = b + sides;
                triangles.AddRange(new[] { a, c, b, b, c, d });
            }
        }
        var mesh = new Mesh { name = "Topknot_" + name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        string path = "Assets/Models/Topknot_" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) AssetDatabase.CreateAsset(mesh, path);
        else
        {
            existing.Clear();
            existing.SetVertices(vertices);
            existing.SetTriangles(triangles, 0);
            existing.RecalculateNormals();
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            mesh = existing;
        }
        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = material;
    }
}
