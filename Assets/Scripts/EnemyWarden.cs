using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shields nearby monsters, drawing a visible tether to each one it is protecting.
/// Aimed at the habit of hitting whatever is closest: while a Warden lives the crowd
/// around it barely takes damage, so it has to be picked out first.
/// </summary>
[RequireComponent(typeof(Enemy))]
public class EnemyWarden : MonoBehaviour
{
    public float auraRadius = 7f;
    public float damageReduction = 0.65f;      // shielded monsters take 35% damage
    public int maxShielded = 8;
    public float refreshInterval = 0.25f;

    [Header("Look")]
    public Color tetherColor = new Color(0.55f, 0.45f, 1f, 1f);
    public float tetherWidth = 0.09f;
    public float bobSpeed = 2.4f;

    private Enemy self;
    private float refreshTimer;
    private readonly List<Enemy> shielded = new List<Enemy>();
    private readonly List<LineRenderer> tethers = new List<LineRenderer>();
    
    private LineRenderer auraRing;
private Material tetherMaterial;

void Awake()
    {
        
        damageReduction = Mathf.Min(damageReduction, 0.45f);
self = GetComponent<Enemy>();

        var template = Resources.Load<Material>("AdditiveFX");
        var shader = template != null ? template.shader : Shader.Find("Universal Render Pipeline/Unlit");
        tetherMaterial = new Material(shader);
        tetherMaterial.SetFloat("_Surface", 1f);
        tetherMaterial.SetFloat("_Blend", 2f);
        tetherMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        tetherMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        tetherMaterial.SetFloat("_ZWrite", 0f);
        tetherMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        tetherMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        tetherMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        tetherMaterial.color = tetherColor;
        if (tetherMaterial.HasProperty("_BaseColor")) tetherMaterial.SetColor("_BaseColor", tetherColor);

        CreateAuraRing();
    }

    void OnDisable()
    {
        // never leave the crowd permanently shielded if this dies mid-frame
        ReleaseAll();
        foreach (var tether in tethers) if (tether != null) tether.enabled = false;
        if (auraRing != null) auraRing.enabled = false;
    }

    void OnEnable()
    {
        if (auraRing != null) auraRing.enabled = true;
        refreshTimer = 0f;
    }

void OnDestroy()
    {
        ReleaseAll();
        if (tetherMaterial != null) Destroy(tetherMaterial);
    }

    void ReleaseAll()
    {
        foreach (var e in shielded)
            if (e != null) e.RemoveShield(this);
        shielded.Clear();
    }

void Update()
    {
        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = refreshInterval;
            Refresh();
        }
        DrawTethers();
        DrawAuraRing();
    }

    void Refresh()
    {
        ReleaseAll();

        var all = Enemy.ActiveEnemies;
        foreach (var e in all)
        {
            if (e == null || e == self || e.isBoss || e.IsDying) continue;
            if (shielded.Count >= maxShielded) break;

            Vector3 flat = e.transform.position - transform.position;
            flat.y = 0f;
            if (flat.magnitude > auraRadius) continue;

            e.SetShield(this, damageReduction);
            shielded.Add(e);
        }
    }

    void DrawTethers()
    {
        while (tethers.Count < shielded.Count)
        {
            var go = new GameObject("Tether");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = tetherMaterial;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            tethers.Add(lr);
        }

        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * bobSpeed);
        Vector3 from = transform.position + Vector3.up * 0.5f;

        for (int i = 0; i < tethers.Count; i++)
        {
            bool live = i < shielded.Count && shielded[i] != null;
            tethers[i].enabled = live;
            if (!live) continue;

            tethers[i].startWidth = tetherWidth * pulse;
            tethers[i].endWidth = tetherWidth * 0.5f * pulse;
            tethers[i].SetPosition(0, from);
            tethers[i].SetPosition(1, shielded[i].transform.position + Vector3.up * 0.35f);
        }
    }

    public int ShieldedCount { get { return shielded.Count; } }


void CreateAuraRing()
    {
        var go = new GameObject("ShieldAura");
        go.transform.SetParent(transform, false);
        auraRing = go.AddComponent<LineRenderer>();
        auraRing.useWorldSpace = false;
        auraRing.loop = true;
        auraRing.positionCount = 48;
        auraRing.numCornerVertices = 2;
        auraRing.sharedMaterial = tetherMaterial;
        auraRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        for (int i = 0; i < auraRing.positionCount; i++)
        {
            float angle = i / (float)auraRing.positionCount * Mathf.PI * 2f;
            auraRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * auraRadius, 0.08f,
                                                Mathf.Sin(angle) * auraRadius));
        }
    }

    void DrawAuraRing()
    {
        if (auraRing == null) return;
        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * bobSpeed);
        auraRing.startWidth = auraRing.endWidth = Mathf.Lerp(0.06f, 0.16f, pulse);
    }
}
