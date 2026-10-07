using UnityEngine;

/// <summary>
/// Turns the goal zone into a portal: it picks a random spot on the map each run,
/// hides the plain marker box, and only reveals itself once the boss is down.
/// All the visuals are built from primitives at runtime, so no VFX assets are needed.
/// </summary>
public class GoalPortal : MonoBehaviour
{
    [Header("Random placement")]
    public bool randomizeOnStart = true;
    public float mapExtent = 52f;
    public float riverHalfWidth = 9f;          // the river runs down the middle of the map
    public float minDistanceFromStart = 30f;
    public int placementAttempts = 40;

    [Header("Look")]
    public Color ringColor = new Color(0.35f, 0.95f, 1f, 1f);
    public Color coreColor = new Color(0.7f, 0.95f, 1f, 1f);
    public float outerRadius = 2.4f;
    public float innerRadius = 1.7f;
    public float gateRadius = 2.1f;
    public float columnHeight = 9f;
    public float spinSpeed = 40f;
    public float pulseSpeed = 2.2f;
    public float revealDuration = 0.8f;

    private Transform visuals;
    private Transform gate;
    private Transform ring;
    private Transform swirl;
    private Material ringMat, swirlMat, columnMat, padMat;
    private Vector3 baseScale = Vector3.one;
    private float reveal;
    private bool revealed;

    void Awake()
    {
        // the plain grey box was the structure sitting on the start point
        var box = GetComponent<MeshRenderer>();
        if (box != null) box.enabled = false;

        BuildVisuals();
        if (visuals != null) visuals.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        Enemy.OnBossDefeated += Reveal;
        if (Enemy.BossDefeated) Reveal();
    }

    void OnDisable()
    {
        Enemy.OnBossDefeated -= Reveal;
    }

    void Start()
    {
        if (randomizeOnStart) PlaceRandomly();
    }

    void PlaceRandomly()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        Vector3 start = playerObj != null ? playerObj.transform.position : Vector3.zero;
        var terrain = Terrain.activeTerrain;

        var box = GetComponent<BoxCollider>();
        float halfHeight = box != null ? box.size.y * transform.lossyScale.y * 0.5f : 1.5f;

        for (int i = 0; i < placementAttempts; i++)
        {
            float x = Random.Range(-mapExtent, mapExtent);
            float z = Random.Range(-mapExtent, mapExtent);
            if (Mathf.Abs(x) < riverHalfWidth) continue;              // never in the water

            Vector3 candidate = new Vector3(x, 0f, z);
            if (Vector3.Distance(new Vector3(start.x, 0f, start.z), candidate) < minDistanceFromStart) continue;

            float groundY = terrain != null
                ? terrain.SampleHeight(candidate) + terrain.transform.position.y
                : start.y;

            transform.position = new Vector3(x, groundY + halfHeight, z);
            Debug.Log("Goal portal placed at " + transform.position.ToString("F1"));
            return;
        }

        Debug.LogWarning("GoalPortal: no valid random spot found, keeping the authored position.");
    }

    void Reveal()
    {
        if (revealed) return;
        revealed = true;
        if (visuals != null) visuals.gameObject.SetActive(true);
    }

    void BuildVisuals()
    {
        // Resources assets survive build stripping; Shader.Find does not.
        var template = Resources.Load<Material>("AdditiveFX");
        var shader = template != null ? template.shader : Shader.Find("Universal Render Pipeline/Unlit");

        var host = new GameObject("PortalVisuals");
        visuals = host.transform;
        visuals.SetParent(transform, false);

        // sit on the floor of the trigger volume rather than at its centre
        var box = GetComponent<BoxCollider>();
        float drop = box != null ? box.size.y * 0.5f : 1.5f;
        visuals.localPosition = new Vector3(0f, -drop, 0f);

        // the trigger box is scaled 5x3x5, and children inherit that; cancel it so the
        // radii and heights below are plain metres
        Vector3 parentScale = transform.lossyScale;
        visuals.localScale = new Vector3(
            parentScale.x != 0f ? 1f / parentScale.x : 1f,
            parentScale.y != 0f ? 1f / parentScale.y : 1f,
            parentScale.z != 0f ? 1f / parentScale.z : 1f);
        baseScale = visuals.localScale;

        // the gate itself: an upright ring with a swirling core, kept facing the player
        gate = new GameObject("PortalGate").transform;
        gate.SetParent(visuals, false);
        gate.localPosition = new Vector3(0f, gateRadius + 0.15f, 0f);

        var gateRing = BuildAnnulus("GateRing", gateRadius * 0.7f, gateRadius, shader, ringColor, out ringMat, true);
        gateRing.transform.SetParent(gate, false);
        ring = gateRing.transform;

        var gateCore = BuildAnnulus("GateCore", 0.05f, gateRadius * 0.68f, shader, coreColor, out swirlMat, true);
        gateCore.transform.SetParent(gate, false);
        gateCore.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        swirl = gateCore.transform;

        // a thin beam so the portal is findable from across the map.
        // built by hand rather than with CreatePrimitive, whose collider class is
        // stripped from the player build
        var col = new GameObject("PortalBeam");
        col.AddComponent<MeshFilter>().sharedMesh = FxShapes.Cylinder;
        col.AddComponent<MeshRenderer>();
        col.transform.SetParent(visuals, false);
        col.transform.localPosition = new Vector3(0f, columnHeight * 0.5f, 0f);
        col.transform.localScale = new Vector3(0.4f, columnHeight * 0.5f, 0.4f);

        columnMat = MakeAdditive(shader, ringColor * 0.35f);
        var colRend = col.GetComponent<Renderer>();
        colRend.material = columnMat;
        colRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        colRend.receiveShadows = false;

        // a flat ring on the floor to anchor it to the ground
        var pad = BuildAnnulus("PortalPad", innerRadius, outerRadius, shader, ringColor * 0.7f, out padMat, false);
        pad.transform.SetParent(visuals, false);
        pad.transform.localPosition = new Vector3(0f, 0.05f, 0f);
    }

    GameObject BuildAnnulus(string name, float rInner, float rOuter, Shader shader, Color color, out Material mat, bool upright)
    {
        const int segments = 48;
        var verts = new Vector3[(segments + 1) * 2];
        var uvs = new Vector2[verts.Length];
        var tris = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float a = (float)i / segments * Mathf.PI * 2f;
            var dir = upright
                ? new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f)     // stands up like a doorway
                : new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));    // lies flat on the floor
            verts[i * 2] = dir * rInner;
            verts[i * 2 + 1] = dir * rOuter;
            uvs[i * 2] = new Vector2((float)i / segments, 0f);
            uvs[i * 2 + 1] = new Vector2((float)i / segments, 1f);
        }
        for (int i = 0; i < segments; i++)
        {
            int v = i * 2, t = i * 6;
            tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 3;
            tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
        }

        var mesh = new Mesh();
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(name);
        go.transform.SetParent(visuals, false);
        go.AddComponent<MeshFilter>().mesh = mesh;

        mat = MakeAdditive(shader, color);
        var r = go.AddComponent<MeshRenderer>();
        r.material = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    static Material MakeAdditive(Shader shader, Color color)
    {
        var mat = new Material(shader);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        // the rings are flat single-sided meshes; without this they vanish from one side
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.doubleSidedGI = true;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        return mat;
    }

    void Update()
    {
        if (visuals == null || !visuals.gameObject.activeSelf) return;

        if (reveal < 1f)
            reveal = Mathf.Clamp01(reveal + Time.unscaledDeltaTime / Mathf.Max(0.01f, revealDuration));

        float t = Time.time;

        // keep the gate turned toward whoever is looking at it, upright
        if (gate != null)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - gate.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.0001f)
                    gate.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            }
        }

        if (ring != null) ring.localRotation = Quaternion.Euler(0f, 0f, t * spinSpeed);
        if (swirl != null) swirl.localRotation = Quaternion.Euler(0f, 0f, -t * spinSpeed * 2.4f);

        float pulse = 0.85f + 0.15f * Mathf.Sin(t * pulseSpeed);
        float grow = Mathf.SmoothStep(0f, 1f, reveal);

        visuals.localScale = baseScale * grow;
        Tint(ringMat, ringColor, pulse * grow * 1.6f);
        Tint(swirlMat, coreColor, (1.8f - pulse) * 0.55f * grow);
        Tint(columnMat, ringColor * 0.35f, pulse * grow);
        Tint(padMat, ringColor * 0.7f, pulse * grow);
    }

    static void Tint(Material mat, Color baseColor, float strength)
    {
        if (mat == null) return;
        var c = new Color(baseColor.r * strength, baseColor.g * strength, baseColor.b * strength, 1f);
        mat.color = c;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
    }
}
