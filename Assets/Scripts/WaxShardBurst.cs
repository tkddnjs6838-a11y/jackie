using UnityEngine;

// Thin curved shell fragments, with a pale broken interior and the enemy's outer color.
public class WaxShardBurst : MonoBehaviour
{
    static readonly Unity.Profiling.ProfilerMarker CostMarker = new Unity.Profiling.ProfilerMarker("Combat.Shards");

    public int chipsPerHit = 3;
    public int chipsOnDeath = 16;
    public float chipLifetime = 1.2f;
    public float launchSpeed = 2.1f;

    void Start() { if (Application.isMobilePlatform) MobileWaxShards.Prepare(); }

    public void Burst(Color shellColor, bool lethal)
    {
        using (CostMarker.Auto())
        {
            Renderer source = null;
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (r.name != "WaxSurfaceCracks" && (r is MeshRenderer || r is SkinnedMeshRenderer) &&
                    (source == null || r.bounds.size.sqrMagnitude > source.bounds.size.sqrMagnitude)) source = r;
            if (source == null || source.sharedMaterial == null) return;
            var bounds = source.bounds;
            float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
            Vector3 center = bounds.center + Vector3.up * bounds.extents.y * 0.18f;
            int count = lethal ? chipsOnDeath : chipsPerHit;
            if (count <= 0) return;
            if (Application.isMobilePlatform)
            {
                MobileWaxShards.Emit(source.sharedMaterial, shellColor, center, radius, bounds.min.y, lethal);
                return;
            }
            var outer = new Material(source.sharedMaterial);
            var inner = new Material(source.sharedMaterial);
            Configure(outer, shellColor);
            Configure(inner, Color.Lerp(shellColor, new Color(0.92f,0.86f,0.69f),0.78f));
            var materials = new WaxShardMaterials(outer, inner);
            var player = GameObject.FindGameObjectWithTag("Player");
            Vector3 contact = player != null ? (player.transform.position - center).normalized : -transform.forward;
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = lethal ? Random.onUnitSphere : (contact + Random.insideUnitSphere * 0.4f).normalized;
                direction.y = Mathf.Abs(direction.y) * 0.7f;
                direction.Normalize();
                var go = new GameObject("WaxShellChip");
                go.transform.position = center + direction * radius * 0.88f;
                go.transform.rotation = Quaternion.LookRotation(direction);
                float size = radius * (lethal ? Random.Range(0.28f,0.48f) : Random.Range(0.10f,0.18f));
                var mesh = MakeShell(size, radius);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials.Values;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                Vector3 velocity = direction * launchSpeed * Random.Range(0.65f,1.1f) + Vector3.up * Random.Range(1f,2f);
                go.AddComponent<WaxShardPiece>().Begin(velocity,Random.insideUnitSphere*360f,
                    chipLifetime*(lethal ? 1.5f : 1f), materials, mesh, bounds.min.y);
            }
        }
    }
    static void Configure(Material material, Color color)
    {
        foreach (var key in new[] { "_BaseMap", "_MainTex", "baseColorTexture" })
            if (material.HasProperty(key)) material.SetTexture(key,Texture2D.whiteTexture);
        foreach (var key in new[] { "_BaseColor", "_Color", "baseColorFactor" })
            if (material.HasProperty(key)) material.SetColor(key,color);
    }
    internal static Mesh MakeShell(float size, float radius)
    {
        const int sides = 5;
        var vertices = new Vector3[(sides+1)*2];
        float thickness = size * 0.12f;
        vertices[0]=Vector3.zero; vertices[sides+1]=Vector3.back*thickness;
        for(int i=0;i<sides;i++)
        {
            float angle=i*Mathf.PI*2f/sides;
            float r=size*Random.Range(0.7f,1f);
            var p=new Vector3(Mathf.Cos(angle)*r,Mathf.Sin(angle)*r,-r*r/Mathf.Max(2*radius,0.001f));
            vertices[i+1]=p; vertices[sides+2+i]=p-Vector3.forward*thickness;
        }
        var outside=new System.Collections.Generic.List<int>();
        var inside=new System.Collections.Generic.List<int>();
        for(int i=0;i<sides;i++)
        {
            int a=i+1,b=(i+1)%sides+1,c=a+sides+1,d=b+sides+1;
            outside.AddRange(new[]{0,a,b});
            inside.AddRange(new[]{sides+1,d,c,a,c,b,b,c,d});
        }
        var mesh=new Mesh { name="CurvedWaxShell", vertices=vertices, subMeshCount=2 };
        mesh.SetTriangles(outside,0); mesh.SetTriangles(inside,1);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }
}

[ExecuteAlways]
public class WaxShardPiece : MonoBehaviour
{
    Vector3 velocity, spin;
    float lifetime, age, floor;
    WaxShardMaterials materials;
    Mesh mesh;
    bool settled;
    public void Begin(Vector3 v,Vector3 s,float seconds,WaxShardMaterials shared,Mesh m,float floorY)
    { velocity=v; spin=s; lifetime=seconds; materials=shared; materials.Retain(); mesh=m; floor=floorY+0.015f; }
    void Update()
    {
        if (!Application.isPlaying) return;
        float dt=Time.deltaTime;
        age+=dt;
        if(!settled)
        {
            velocity+=Vector3.down*9.5f*dt;
            transform.position+=velocity*dt;
            transform.Rotate(spin*dt,Space.Self);
            if(transform.position.y<floor)
            {
                var p=transform.position; p.y=floor; transform.position=p;
                velocity=new Vector3(velocity.x*0.35f,Mathf.Abs(velocity.y)*0.2f,velocity.z*0.35f);
                spin*=0.4f;
                if(velocity.y<0.5f) settled=true;
            }
        }
        transform.localScale=Vector3.one*(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(lifetime*0.75f,lifetime,age)));
        if(age>=lifetime) Destroy(gameObject);
    }
    static void Release(Object resource)
    {
        if(resource==null) return;
        if(Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
    }
    void OnDestroy()
    {
        materials?.Release(); Release(mesh);
    }
}

// A burst owns one material pair. Destroy it only after its last fragment disappears.
public sealed class WaxShardMaterials
{
    public Material[] Values { get; }
    int users;
    public WaxShardMaterials(Material outer, Material inner) { Values = new[] { outer, inner }; }
    public void Retain() { users++; }
    public void Release()
    {
        if (--users != 0) return;
        foreach (var material in Values)
        {
            if (Application.isPlaying) Object.Destroy(material);
            else Object.DestroyImmediate(material);
        }
    }
}
