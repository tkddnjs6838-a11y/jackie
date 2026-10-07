using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds simple persistent crack marks around a creature as its health falls.
/// The marks are runtime geometry, so every enemy prefab and WebGL build can use them
/// without requiring extra textures or hand-authored crack meshes.
/// </summary>
public class WaxCrackVisual : MonoBehaviour
{
    [Range(0.01f, 0.3f)] public float lineWidthRatio = 0.13f;
    public Color crackColor = new Color(0.03f, 0.01f, 0.005f, 1f);

    private int visibleStage;
    private Material crackMaterial;
    private CapsuleCollider capsule;

    void Awake()
    {
        capsule = GetComponent<CapsuleCollider>();

        var sourceRenderer = GetComponentInChildren<Renderer>();
        if (sourceRenderer != null && sourceRenderer.sharedMaterial != null)
        {
            crackMaterial = new Material(sourceRenderer.sharedMaterial);
            ApplyColor(crackMaterial, crackColor);
        }
    }

    public void SetDamage(float damageFraction)
    {
        int targetStage = damageFraction >= 0.72f ? 3
                        : damageFraction >= 0.38f ? 2
                        : damageFraction > 0f ? 1 : 0;

        while (visibleStage < targetStage)
        {
            visibleStage++;
            CreateCrack(visibleStage - 1);
        }
    }

public void ShowCorpseCracks()
    {
        SetDamage(1f);
        if (crackMaterial == null) return;

        float radius = capsule != null ? capsule.radius : 0.5f;
        float height = capsule != null ? capsule.height : 1.5f;
        Vector3 center = capsule != null ? capsule.center : Vector3.up * (height * 0.5f);
        float y = center.y + height * 0.32f;
        float width = Mathf.Max(0.035f, radius * lineWidthRatio * 1.35f);

        MakeLine("WaxCrack_Corpse_A", new[]
        {
            new Vector3(-radius * 0.9f, y, -radius * 0.15f),
            new Vector3(-radius * 0.25f, y, radius * 0.08f),
            new Vector3(radius * 0.15f, y, -radius * 0.12f),
            new Vector3(radius * 0.95f, y, radius * 0.22f)
        }, width);

        MakeLine("WaxCrack_Corpse_B", new[]
        {
            new Vector3(-radius * 0.18f, y, -radius * 0.95f),
            new Vector3(radius * 0.08f, y, -radius * 0.25f),
            new Vector3(-radius * 0.12f, y, radius * 0.2f),
            new Vector3(radius * 0.25f, y, radius * 0.95f)
        }, width);
    }


    void CreateCrack(int stage)
    {
        if (crackMaterial == null) return;

        float radius = capsule != null ? capsule.radius : 0.5f;
        float height = capsule != null ? capsule.height : 1.5f;
        Vector3 center = capsule != null ? capsule.center : Vector3.up * (height * 0.5f);

        int face = stage % 4;
        float surface = radius * 1.04f;
        float y = center.y + height * (0.18f - stage * 0.10f);
        float width = Mathf.Max(0.015f, radius * lineWidthRatio);

        Vector3[] main =
        {
            FacePoint(face, surface, -radius * 0.28f, y + height * 0.18f),
            FacePoint(face, surface,  radius * 0.05f, y + height * 0.06f),
            FacePoint(face, surface, -radius * 0.10f, y - height * 0.04f),
            FacePoint(face, surface,  radius * 0.30f, y - height * 0.17f)
        };
        MakeLine("WaxCrack_Main_" + stage, main, width);

        Vector3 joint = main[1];
        Vector3 branchEnd = FacePoint(face, surface, radius * 0.36f, y + height * 0.11f);
        MakeLine("WaxCrack_Branch_" + stage, new[] { joint, branchEnd }, width * 0.78f);
    }

    Vector3 FacePoint(int face, float surface, float tangent, float y)
    {
        switch (face)
        {
            case 0: return new Vector3(tangent, y, surface);
            case 1: return new Vector3(surface, y, -tangent);
            case 2: return new Vector3(-tangent, y, -surface);
            default: return new Vector3(-surface, y, tangent);
        }
    }

    void MakeLine(string objectName, Vector3[] points, float width)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.startWidth = width;
        line.endWidth = width * 0.55f;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = crackMaterial;
    }

    static void ApplyColor(Material material, Color color)
    {
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("baseColorFactor")) material.SetColor("baseColorFactor", color);
    }

    void OnDestroy()
    {
        if (crackMaterial != null)
            Destroy(crackMaterial);
    }
}
