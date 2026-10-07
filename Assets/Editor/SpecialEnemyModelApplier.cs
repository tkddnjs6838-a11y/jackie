using UnityEditor;
using UnityEngine;
using System.IO;

[InitializeOnLoad]
public static class SpecialEnemyModelApplier
{
    const string SessionKey = "Wakkuball.SpecialEnemyModels.20260825.v6";

    static SpecialEnemyModelApplier()
    {
        EditorApplication.delayCall += ApplyOnce;
    }

    static void ApplyOnce()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        Apply();
    }

    [MenuItem("Tools/Wakkuball/Apply Special Enemy Models")]
    public static void Apply()
    {
        // BossEnemy.prefab is deliberately left untouched.
        ApplyModel(
            "Assets/Prefabs/EnemyCharger.prefab",
            "Assets/FBX/workflow-result-d05f41cf5379b82a25c8a59a887e9ebc.fbx",
            1.75f);
        ApplyModel(
            "Assets/Prefabs/EnemySplitter.prefab",
            "Assets/FBX/workflow-result-8b411bf451a51505fb8035b3b3a11911.fbx",
            1.45f);
        ApplyModel(
            "Assets/Prefabs/EnemyWarden.prefab",
            "Assets/FBX/workflow-result-30a235d1bb06f8278bb162cbf790fe2f.fbx",
            1.75f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Wakkuball] Applied Charger, Splitter, and Warden FBX models. Boss kept unchanged.");
    }

    static void ApplyModel(string prefabPath, string modelPath, float targetHeight)
    {
        ExtractEmbeddedTextures(modelPath);
        Material originalColorMaterial = CreateOriginalColorMaterial(modelPath);
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (modelAsset == null)
        {
            Debug.LogError("[Wakkuball] Model not found: " + modelPath);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform oldVisual = root.transform.Find("MonsterModel");
            if (oldVisual != null)
                Object.DestroyImmediate(oldVisual.gameObject);

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            visual.name = "MonsterModel";
            visual.transform.localPosition = Vector3.zero;
            // VARCO FBX files are Z-up. Rotate them into Unity's Y-up space.
            visual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            visual.transform.localScale = Vector3.one;

            FitToHeight(visual, targetHeight);

            CreatureWaddle waddle = root.GetComponent<CreatureWaddle>();
            if (waddle != null)
                waddle.body = visual.transform;

            Enemy enemy = root.GetComponent<Enemy>();
            if (enemy != null)
                enemy.preserveModelColors = true;

            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (originalColorMaterial != null)
                {
                    Material[] materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                    for (int i = 0; i < materials.Length; i++)
                        materials[i] = originalColorMaterial;
                    renderer.sharedMaterials = materials;
                }
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log("[Wakkuball] Applied " + modelPath + " to " + prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Material CreateOriginalColorMaterial(string modelPath)
    {
        string modelName = Path.GetFileNameWithoutExtension(modelPath);
        string textureFolder = "Assets/FBX/ExtractedTextures/" + modelName;
        string diffusePath = textureFolder + "/diffuse.png";
        string normalPath = textureFolder + "/normal.png";
        Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        if (diffuse == null)
        {
            Debug.LogError("[Wakkuball] Diffuse texture not found: " + diffusePath);
            return null;
        }

        if (normal != null)
        {
            TextureImporter normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            }
        }

        string materialFolder = "Assets/FBX/GeneratedMaterials";
        if (!AssetDatabase.IsValidFolder(materialFolder))
            AssetDatabase.CreateFolder("Assets/FBX", "GeneratedMaterials");
        string materialPath = materialFolder + "/" + modelName + "_OriginalColor.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.color = Color.white;
        material.SetTexture("_BaseMap", diffuse);
        material.SetTexture("_MainTex", diffuse);
        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }
        material.SetFloat("_Smoothness", 0.45f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void ExtractEmbeddedTextures(string modelPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null) return;

        string modelName = Path.GetFileNameWithoutExtension(modelPath);
        string textureFolder = "Assets/FBX/ExtractedTextures/" + modelName;
        if (!AssetDatabase.IsValidFolder("Assets/FBX/ExtractedTextures"))
            AssetDatabase.CreateFolder("Assets/FBX", "ExtractedTextures");
        if (!AssetDatabase.IsValidFolder(textureFolder))
            AssetDatabase.CreateFolder("Assets/FBX/ExtractedTextures", modelName);

        bool extracted = importer.ExtractTextures(textureFolder);
        if (extracted)
        {
            importer.SaveAndReimport();
            Debug.Log("[Wakkuball] Extracted FBX textures: " + modelPath);
        }
    }

    static void FitToHeight(GameObject visual, float targetHeight)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        if (bounds.size.y < 0.0001f) return;

        float scale = targetHeight / bounds.size.y;
        visual.transform.localScale = Vector3.one * scale;

        // Recalculate after scaling and place the feet on the prefab root's ground plane.
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        visual.transform.position += Vector3.up * (visual.transform.position.y - bounds.min.y);
    }
}
