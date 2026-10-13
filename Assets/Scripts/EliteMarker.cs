using UnityEngine;

/// <summary>
/// Promotes an ordinary monster into an elite: bigger, tougher, and worth hunting because
/// killing it hands over an upgrade choice on the spot.
///
/// The swarm has no targets in it — every monster is interchangeable and dies in two hits,
/// so there is never a reason to prefer one over another. An elite gives the player
/// something to chase and a decision to make about whether to chase it.
///
/// The look is built from runtime meshes rather than a tint: whether a tint shows depends on
/// the prefab's preserveModelColors flag, but a gold ring at its feet and a halo above it
/// read the same on every model.
/// </summary>
public class EliteMarker : MonoBehaviour
{
    public static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);

    [Header("Promotion")]
    public float healthMultiplier = 5f;
    public float damageMultiplier = 1.5f;
    public float speedMultiplier = 0.85f;
    public float scaleMultiplier = 1.55f;

    [Header("Halo")]
    public float haloSpin = 90f;
    public float haloBob = 0.12f;

    private Transform halo;
    private Transform ring;
    private float bobPhase;
    private readonly System.Collections.Generic.List<Material> ownedMaterials = new System.Collections.Generic.List<Material>();

    /// <summary>
    /// Applies the promotion. Call straight after Instantiate: Enemy re-reads maxHealth in
    /// RefreshConfiguredStatsAndColor, so the boosted pool actually takes effect.
    /// </summary>
    public void Promote()
    {
        var enemy = GetComponent<Enemy>();
        if (enemy == null) return;

        enemy.maxHealth *= healthMultiplier;
        enemy.contactDamage *= damageMultiplier;
        enemy.moveSpeed *= speedMultiplier;
        transform.localScale *= scaleMultiplier;
        enemy.RefreshConfiguredStatsAndColor();

        BuildLook(enemy);
        DamageNumber.Create(transform.position + Vector3.up * 2f, "ELITE", Gold);
    }

    void BuildLook(Enemy enemy)
    {
        float radius = Mathf.Max(0.6f, enemy.BodyRadiusWorld);

        ring = NewPiece("EliteRing", FxShapes.Cylinder);
        ring.localPosition = new Vector3(0f, 0.05f, 0f);
        ring.localScale = new Vector3(radius * 2.4f, 0.02f, radius * 2.4f);

        halo = NewPiece("EliteHalo", FxShapes.Cylinder);
        halo.localPosition = new Vector3(0f, radius * 2.6f, 0f);
        halo.localScale = new Vector3(radius * 1.1f, 0.03f, radius * 1.1f);
    }

    Transform NewPiece(string name, Mesh mesh)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        // the additive template lives in Resources because Shader.Find returns null in a
        // built player, which is how the first web build ended up with invisible effects
        var template = Resources.Load<Material>("AdditiveFX");
        if (template != null)
        {
            var mat = new Material(template);
            ownedMaterials.Add(mat);
            mat.color = Gold;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Gold);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Gold * 2.2f);
            mr.sharedMaterial = mat;
        }
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go.transform;
    }

    void OnDestroy()
    {
        foreach (var material in ownedMaterials) if (material != null) Destroy(material);
    }

    void Update()
    {
        if (halo == null) return;

        bobPhase += Time.deltaTime;
        halo.Rotate(Vector3.up, haloSpin * Time.deltaTime, Space.Self);

        Vector3 p = halo.localPosition;
        p.y += Mathf.Sin(bobPhase * 2.4f) * haloBob * Time.deltaTime;
        halo.localPosition = p;
    }
}
