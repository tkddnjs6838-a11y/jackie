using UnityEngine;

// Bake the animated source without changing its skin binding; deform a separate visible surface.
[DefaultExecutionOrder(50)]
public class WaxDent : MonoBehaviour
{
    SkinnedMeshRenderer skin;
    Mesh mesh, baked, sourceMesh;
    Renderer surface;
    Vector3 impactLocal, normalLocal;
    float radius, age, residual, peak, sampleBudget;
    bool frozen;
    static readonly Unity.Profiling.ProfilerMarker SampleMarker = new Unity.Profiling.ProfilerMarker("WaxDent.Sample");
    public Renderer SurfaceRenderer => surface;
    public float CurrentWeight { get; private set; }
    MaterialPropertyBlock tint;
    System.Collections.Generic.List<Vector3> vertices;
    readonly System.Collections.Generic.List<Vector3> normals = new System.Collections.Generic.List<Vector3>();

    public bool Punch(Vector3 contact,float damage,bool lethal,float intensity=1f)
    {
        bool initialized = false;
        if(skin==null || mesh==null || baked==null || surface==null)
        {
            foreach(var candidate in GetComponentsInChildren<SkinnedMeshRenderer>())
                if(candidate.name!="WaxSurfaceCracks" && (skin==null || candidate.bounds.size.sqrMagnitude>skin.bounds.size.sqrMagnitude)) skin=candidate;
            if(skin==null) return false;
            mesh=new Mesh { name="WaxDeformedSurface", indexFormat=skin.sharedMesh.indexFormat };mesh.MarkDynamic();
            baked=new Mesh();
            skin.BakeMesh(baked);
            CopyTopology();
            var go=new GameObject("WaxDeformedSurface");go.layer=skin.gameObject.layer;go.transform.SetParent(skin.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            surface=go.AddComponent<MeshRenderer>();surface.sharedMaterials=skin.sharedMaterials;
            initialized = true;
        }
        var bounds=skin.bounds;
        radius=Mathf.Min(bounds.extents.x,bounds.extents.z);
        Vector3 center=bounds.center+Vector3.up*bounds.extents.y*0.18f;
        Vector3 normal=contact-center;
        if(normal.sqrMagnitude<0.0001f) normal=-transform.forward;
        normal.Normalize();
        normalLocal=Quaternion.Inverse(skin.transform.rotation)*normal;
        impactLocal=Quaternion.Inverse(skin.transform.rotation)*(center+normal*radius*0.9f-skin.transform.position);
        peak=(lethal ? 0.42f : 0.30f)*Mathf.Clamp(intensity,.1f,1.5f);
        residual=lethal ? 0.8f : Mathf.InverseLerp(0.35f,1f,damage)*0.30f;
        age=0f;sampleBudget=0f;frozen=false;
        SampleSurface(0f, initialized);
        skin.enabled=false;
        return true;
    }
    public void Sample(float elapsed) => SampleSurface(elapsed, false);

    void SampleSurface(float elapsed, bool poseReady)
    {
        if(skin==null || mesh==null || baked==null || surface==null) return;
        using (SampleMarker.Auto())
        {
            float strength;
            if(elapsed<0.045f) strength=Mathf.SmoothStep(0f,1f,elapsed/0.045f);
            else
            {
                float t=Mathf.Clamp01((elapsed-0.045f)/0.32f);
                strength=Mathf.Lerp(1f,residual,Mathf.SmoothStep(0f,1f,t));
                strength+=Mathf.Sin(t*Mathf.PI*2f)*(1f-t)*0.07f;
            }
            CurrentWeight=strength*100f;
            if(tint==null) tint=new MaterialPropertyBlock();
            if(vertices==null) vertices=new System.Collections.Generic.List<Vector3>();
            if (!poseReady) skin.BakeMesh(baked);
            if(sourceMesh!=skin.sharedMesh) CopyTopology();
            baked.GetVertices(vertices);
            // Keep the crack pattern attached to material points as the surface dents.
            mesh.SetUVs(3,vertices);
            float patch=Mathf.Max(radius*0.78f,0.001f);
            float inversePatchSquared=1f/(patch*patch);
            for(int i=0; strength > 0f && i<vertices.Count;i++)
            {
                var offset=vertices[i]-impactLocal;
                float distanceSquared=offset.sqrMagnitude*inversePatchSquared;
                if(distanceSquared>=2.25f) continue;
                float distance=Mathf.Sqrt(distanceSquared);
                float dent=Mathf.Exp(-distanceSquared*4.5f);
                var tangent=Vector3.ProjectOnPlane(offset,normalLocal);
                float rimDistance=(distance-0.8f)*3.5f;
                float rim=Mathf.Exp(-rimDistance*rimDistance);
                var delta=(-normalLocal*(radius*peak*dent)+tangent.normalized*(radius*0.075f*rim))*strength;
                vertices[i]=vertices[i]+delta;
            }
            mesh.SetVertices(vertices);
            // At impact onset the deformation is exactly zero. Preserve the baked
            // normals instead of recomputing identical normals across the whole mesh.
            if (strength <= 0f) { baked.GetNormals(normals); mesh.SetNormals(normals); }
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var scale=skin.transform.lossyScale;
            surface.transform.localScale=new Vector3(1f/scale.x,1f/scale.y,1f/scale.z);
            SyncTint();
        }
    }
    // A LOD switch changes vertex count and UV layout. Keep the same visible mesh object
    // so the crack overlay continues sharing it, but replace all topology atomically.
    void CopyTopology()
    {
        mesh.Clear();
        mesh.indexFormat=skin.sharedMesh.indexFormat;
        mesh.vertices=baked.vertices;
        mesh.uv=baked.uv;
        mesh.tangents=baked.tangents;
        mesh.subMeshCount=baked.subMeshCount;
        for(int sub=0;sub<baked.subMeshCount;sub++) mesh.SetTriangles(baked.GetTriangles(sub),sub);
        sourceMesh=skin.sharedMesh;
    }
    void SyncTint()
    {
        if(tint==null) tint=new MaterialPropertyBlock();
        skin.GetPropertyBlock(tint);surface.SetPropertyBlock(tint);
    }
    void LateUpdate()
    {
        AdvanceSurface(Time.deltaTime, surface!=null && surface.isVisible);
    }
    // Sample the initial impact at up to 60 Hz, then skin deformation at 30 Hz.
    // Movement still runs every frame; hit-flash tint must not wait for a mesh update.
    void AdvanceSurface(float deltaTime, bool visible)
    {
        if(deltaTime<=0f || frozen || surface==null) return;
        age+=deltaTime;
        float interval=age<=0.08f ? 1f/60f : 1f/30f;
        sampleBudget=Mathf.Min(sampleBudget+deltaTime, interval);
        if(!visible) return;
        SyncTint();
        if(sampleBudget+0.000001f>=interval)
        {
            Sample(age);
            sampleBudget=0f;
        }
    }
    // Called only after the death squash and animation have stopped.
    public void FreezeSurface()
    {
        Sample(Mathf.Max(age,0.365f));
        frozen=true;
    }
    void OnDestroy()
    {
        if(skin!=null) skin.enabled=true;
        if(mesh!=null) { if(Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        if(baked!=null) { if(Application.isPlaying) Destroy(baked); else DestroyImmediate(baked); }
    }
}
