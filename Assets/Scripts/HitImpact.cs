using UnityEngine;

/// <summary>
/// Short-lived sword impact flash. Built from primitives at runtime so it needs no
/// imported VFX assets: a bright core that pops and fades, plus a flat expanding ring.
/// </summary>
public class HitImpact : MonoBehaviour
{
    public float lifetime = 0.12f;
    public float startScale = 0.12f;
    public float endScale = 0.5f;
    public Color coreColor = new Color(1f, 0.95f, 0.65f, 1f);
    public Color ringColor = new Color(1f, 0.72f, 0.25f, 1f);

    private float timer;
    private Transform core;
    private Transform ring;
    private Material coreMat;
    private Material ringMat;

    public static void Spawn(Vector3 position, Vector3 facing, float sizeMultiplier)
    {
        var go = new GameObject("HitImpact");
        go.transform.position = position;
        if (facing.sqrMagnitude > 0.0001f)
            go.transform.rotation = Quaternion.LookRotation(facing);

        var impact = go.AddComponent<HitImpact>();
        impact.Build(sizeMultiplier);
    }

    void Build(float sizeMultiplier)
    {
        // Resources assets survive build stripping; Shader.Find does not.
        var template = Resources.Load<Material>("AdditiveFX");
        var shader = template != null ? template.shader : Shader.Find("Universal Render Pipeline/Unlit");

        core = MakePiece(PrimitiveType.Sphere, shader, coreColor);
        core.localScale = Vector3.one * startScale;

        ring = MakePiece(PrimitiveType.Sphere, shader, ringColor);
        ring.localScale = new Vector3(startScale, startScale * 0.12f, startScale);
        ring.localRotation = Quaternion.Euler(90f, 0f, 0f);   // face the swing direction

        transform.localScale = Vector3.one * sizeMultiplier;
    }

    Transform MakePiece(PrimitiveType type, Shader shader, Color color)
    {
        // CreatePrimitive always attaches a collider, and those collider classes are
        // stripped from the player, which spams "class doesn't exist" on every hit.
        // Build the renderer by hand from a mesh shipped in Resources instead.
        var go = new GameObject(type.ToString());
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = FxShapes.Mesh(type);
        go.AddComponent<MeshRenderer>();

        var mat = new Material(shader);
        SetTransparent(mat, color);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (coreMat == null) coreMat = mat; else ringMat = mat;
        return go.transform;
    }

    static void SetTransparent(Material mat, Color color)
    {
        // URP needs the actual blend state set, not just the keyword — flipping
        // _Surface/_Blend alone leaves SrcBlend/DstBlend untouched and the mesh
        // renders as nothing at all. Additive keeps the flash bright over any surface.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(timer / lifetime);

        float scale = Mathf.Lerp(startScale, endScale, t);
        if (core != null) core.localScale = Vector3.one * Mathf.Lerp(startScale, 0f, t);
        if (ring != null) ring.localScale = new Vector3(scale, scale * 0.08f, scale);

        float alpha = 1f - t;
        Fade(coreMat, coreColor, alpha);
        Fade(ringMat, ringColor, alpha * 0.85f);

        if (timer >= lifetime)
            Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (coreMat != null) Destroy(coreMat);
        if (ringMat != null) Destroy(ringMat);
    }

    static void Fade(Material mat, Color baseColor, float alpha)
    {
        if (mat == null) return;
        // additive blending ignores alpha, so dim the RGB instead
        var c = new Color(baseColor.r * alpha, baseColor.g * alpha, baseColor.b * alpha, alpha);
        mat.color = c;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
    }
}
