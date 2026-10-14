using UnityEngine;

/// <summary>
/// Health orb dropped by dying monsters. Bobs and spins, drifts toward the player when
/// they get close, and expires after a while so the field does not fill up with them.
/// Pickup is a plain distance check rather than a trigger — fewer moving parts, and it
/// cannot be missed by a fast-moving player between physics steps.
/// </summary>
public class HealthPickup : MonoBehaviour
{
    public float healAmount = 20f;
    public float lifetime = 25f;
    public float pickupRadius = 0.9f;

    [Header("Feel")]
    public float magnetRadius = 3.5f;
    public float magnetSpeed = 7f;
    public float bobHeight = 0.16f;
    public float bobSpeed = 2.6f;
    public float spinSpeed = 80f;
    public float blinkFrom = 4f;          // start blinking this long before expiry

    public Color coreColor = new Color(0.45f, 1f, 0.55f, 1f);
    public Color haloColor = new Color(0.15f, 0.8f, 0.35f, 1f);

    private Transform player;
    private Transform core;
    private Transform halo;
    private Material coreMat, haloMat;
    private float age;
    private float baseY;

    /// <summary>How many orbs are lying around right now, so the dropper can cap them.</summary>
    public static int ActiveCount { get; private set; }

    public static void Spawn(Vector3 position, float heal)
    {
        if(!MapNavigation.DropPoint(position,out position))return;
        var go = new GameObject("HealthPickup");
        go.transform.position = position;
        var pickup = go.AddComponent<HealthPickup>();
        pickup.healAmount = heal;
    }

    void OnDestroy()
    {
        ActiveCount--;
        if (coreMat != null) Destroy(coreMat);
        if (haloMat != null) Destroy(haloMat);
    }

    void Awake()
    {
        ActiveCount++;

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        var template = Resources.Load<Material>("AdditiveFX");
        var shader = template != null ? template.shader : Shader.Find("Universal Render Pipeline/Unlit");

        halo = MakeBall("Halo", 0.85f, shader, haloColor, out haloMat);
        core = MakeBall("Core", 0.42f, shader, coreColor, out coreMat);

        baseY = transform.position.y + 0.75f;
        transform.position = new Vector3(transform.position.x, baseY, transform.position.z);
    }

    Transform MakeBall(string name, float size, Shader shader, Color color, out Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = FxShapes.Sphere;

        mat = new Material(shader);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

        var r = go.AddComponent<MeshRenderer>();
        r.material = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        go.transform.localScale = Vector3.one * size;
        return go.transform;
    }

    void Update()
    {
        if (Time.timeScale <= 0f) return;
        age += Time.deltaTime;
        if (age >= lifetime) { Destroy(gameObject); return; }

        float t = Time.time;
        if (core != null) core.localRotation = Quaternion.Euler(0f, t * spinSpeed, 0f);
        if (halo != null) halo.localScale = Vector3.one * (0.85f + 0.08f * Mathf.Sin(t * bobSpeed * 1.7f));

        // blink out as it is about to vanish, so it never disappears without warning
        float visible = 1f;
        float remaining = lifetime - age;
        if (remaining < blinkFrom)
            visible = Mathf.Repeat(remaining * 6f, 1f) > 0.45f ? 1f : 0.25f;
        Tint(coreMat, coreColor, visible);
        Tint(haloMat, haloColor, visible * 0.7f);

        if (player == null) return;

        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        float dist = flat.magnitude;

        if (dist <= pickupRadius && MapNavigation.CanCollect(transform.position,player.position))
        {
            Collect();
            return;
        }

        if (dist <= magnetRadius && MapNavigation.CanCollect(transform.position,player.position))
        {
            Vector3 pull = flat.normalized * magnetSpeed * Time.deltaTime;
            var next=transform.position+Vector3.ClampMagnitude(new Vector3(pull.x,0,pull.z),dist);
            if(MapNavigation.Current==null)transform.position=next;
            else if(MapNavigation.Sample(next,1.5f,out var floor)){baseY=floor.y+.75f;transform.position=new Vector3(floor.x,baseY,floor.z);}
        }

        transform.position = new Vector3(transform.position.x,
                                         baseY + Mathf.Sin(t * bobSpeed) * bobHeight,
                                         transform.position.z);
    }

    void Collect()
    {
        var health = player.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.Heal(healAmount);
            GameAudio.Play(GameAudio.Sfx.Pickup);
            DamageNumber.Create(transform.position + Vector3.up * 0.4f,
                                "+" + Mathf.RoundToInt(healAmount), coreColor);
        }
        HitImpact.Spawn(transform.position, Vector3.up, 0.7f);
        Destroy(gameObject);
    }

    static void Tint(Material mat, Color baseColor, float strength)
    {
        if (mat == null) return;
        var c = new Color(baseColor.r * strength, baseColor.g * strength, baseColor.b * strength, 1f);
        mat.color = c;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
    }
}
