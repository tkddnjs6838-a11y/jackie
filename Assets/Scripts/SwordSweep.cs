using UnityEngine;

/// <summary>
/// Wide horizontal slash arc drawn in front of the player. The mesh is a triangle fan
/// built at runtime, so the sweep width always matches the actual hit arc.
/// </summary>
public class SwordSweep : MonoBehaviour
{
    public float lifetime = 0.22f;
    public Color color = new Color(1f, 0.88f, 0.45f, 1f);
    public float growth = 1.12f;

    private float timer;
    private Material mat;
    private Mesh ownedMesh;

public static void Spawn(Vector3 origin, Vector3 forward, float radius, float angleDegrees, float innerRadius)
    {
        Spawn(origin, forward, radius, angleDegrees, innerRadius,
              new Color(1f, 0.88f, 0.45f, 1f), 0.22f, 1.12f);
    }

public static void Spawn(Vector3 origin, Vector3 forward, float radius, float angleDegrees,
                             float innerRadius, Color sweepColor, float sweepLifetime, float sweepGrowth)
    {
        var go = new GameObject("SwordSweep");
        go.transform.position = origin;

        Vector3 flat = forward;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
        go.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);

        var sweep = go.AddComponent<SwordSweep>();
        sweep.color = sweepColor;
        sweep.lifetime = sweepLifetime;
        sweep.growth = sweepGrowth;
        sweep.Build(radius, angleDegrees, innerRadius);
    }


    void Build(float radius, float angleDegrees, float innerRadius)
    {
        const int segments = 24;

        var verts = new Vector3[(segments + 1) * 2];
        var tris = new int[segments * 6];
        var uvs = new Vector2[verts.Length];

        float half = angleDegrees * 0.5f;

        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Lerp(-half, half, (float)i / segments) * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));

            verts[i * 2] = dir * innerRadius;
            verts[i * 2 + 1] = dir * radius;
            uvs[i * 2] = new Vector2((float)i / segments, 0f);
            uvs[i * 2 + 1] = new Vector2((float)i / segments, 1f);
        }

        for (int i = 0; i < segments; i++)
        {
            int v = i * 2;
            int t = i * 6;
            tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 3;
            tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
        }

        var mesh = new Mesh();
        ownedMesh = mesh;
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        gameObject.AddComponent<MeshFilter>().mesh = mesh;

        // Resources assets survive build stripping; Shader.Find does not.
        var template = Resources.Load<Material>("AdditiveFX");
        var shader = template != null ? template.shader : Shader.Find("Universal Render Pipeline/Unlit");
        mat = new Material(shader);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.material = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Apply(1f);
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(timer / lifetime);

        transform.localScale = Vector3.one * Mathf.Lerp(1f, growth, t);
        Apply(1f - t);

        if (timer >= lifetime)
            Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
        if (ownedMesh != null) Destroy(ownedMesh);
    }

    void Apply(float strength)
    {
        if (mat == null) return;
        var c = new Color(color.r * strength, color.g * strength, color.b * strength, strength);
        mat.color = c;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
    }
}
