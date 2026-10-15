using UnityEngine;
using UnityEngine.Rendering;

// Bounded scene-local pool: no Mesh/Material/GameObject creation during a burst.
public sealed class MobileWaxShards : MonoBehaviour
{
    const int Capacity = 32;
    static MobileWaxShards instance;
    readonly Transform[] pieces = new Transform[Capacity];
    readonly MeshRenderer[] renderers = new MeshRenderer[Capacity];
    readonly Vector3[] velocity = new Vector3[Capacity], spin = new Vector3[Capacity];
    readonly float[] age = new float[Capacity], sizes = new float[Capacity], floors = new float[Capacity];
    Mesh mesh;
    Material material;
    MaterialPropertyBlock tint;
    int cursor, frame = -1, emittedThisFrame;
    public static void Prepare()
    {
        if (instance != null) return;
        instance = new GameObject("MobileWaxShardPool").AddComponent<MobileWaxShards>();
    }
    void Awake()
    {
        mesh = WaxShardBurst.MakeShell(1f, 3f);
        var triangles = mesh.triangles;
        mesh.subMeshCount = 1;
        mesh.triangles = triangles;
        tint = new MaterialPropertyBlock();
        for (int i = 0; i < Capacity; i++)
        {
            var go = new GameObject("Shell", typeof(MeshFilter), typeof(MeshRenderer));
            pieces[i] = go.transform; pieces[i].SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderers[i] = go.GetComponent<MeshRenderer>();
            renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
            go.SetActive(false);
        }
    }
    public static void Emit(Material source, Color color, Vector3 center, float radius, float floor, bool lethal)
    {
        Prepare();
        instance.Spawn(source, color, center, radius, floor, lethal);
    }
    void Spawn(Material source, Color color, Vector3 center, float radius, float floor, bool lethal)
    {
        if (material == null)
        {
            material = new Material(source);
            foreach (var key in new[] {"_BaseMap", "_MainTex", "baseColorTexture"})
                if (material.HasProperty(key)) material.SetTexture(key, Texture2D.whiteTexture);
            foreach (var renderer in renderers) renderer.sharedMaterial = material;
        }
        if (frame != Time.frameCount) { frame = Time.frameCount; emittedThisFrame = 0; }
        int count = Mathf.Min(lethal ? 6 : 1, 12 - emittedThisFrame);
        emittedThisFrame += count;
        tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color); tint.SetColor("baseColorFactor", color);
        for (int n = 0; n < count; n++)
        {
            int i = cursor++ % Capacity;
            Vector3 direction = Random.onUnitSphere; direction.y = Mathf.Abs(direction.y);
            pieces[i].position = center + direction * radius * .85f;
            pieces[i].rotation = Quaternion.LookRotation(direction);
            sizes[i] = radius * Random.Range(.22f, .38f);
            pieces[i].localScale = Vector3.one * sizes[i];
            velocity[i] = direction * 2.1f + Vector3.up;
            spin[i] = Random.insideUnitSphere * 300f;
            age[i] = 0; floors[i] = floor + .015f;
            renderers[i].SetPropertyBlock(tint);
            pieces[i].gameObject.SetActive(true);
        }
    }
    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < Capacity; i++)
        {
            if (!pieces[i].gameObject.activeSelf) continue;
            age[i] += dt;
            if (age[i] >= .7f) { pieces[i].gameObject.SetActive(false); continue; }
            velocity[i] += Vector3.down * 9.5f * dt;
            var p = pieces[i].position + velocity[i] * dt;
            if (p.y < floors[i]) { p.y = floors[i]; velocity[i] = Vector3.zero; spin[i] = Vector3.zero; }
            pieces[i].position = p; pieces[i].Rotate(spin[i] * dt);
            pieces[i].localScale = Vector3.one * sizes[i] * (1f - Mathf.InverseLerp(.45f, .7f, age[i]));
        }
    }
    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
