using UnityEngine;
using UnityEngine.Rendering;

// Draws on the actual body mesh, so cracks follow the skin and squash animation.
[DefaultExecutionOrder(100)]
public class WaxCrackVisual : MonoBehaviour
{
    private int visibleStage;
    private Material crackMaterial;
    private Renderer overlay;
    private Transform surface;
    private Matrix4x4 crackSpace;
    private SkinnedMeshRenderer sourceSkin;
    private MeshFilter sourceFilter;
    // Instantiate copies child renderers but not this component's private ownership fields.
    // A new split child must never retain resources that its parent will destroy.
    public void ClearInheritedVisuals()
    {
        if (overlay != null) return;
        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform || (child.name != "WaxSurfaceCracks" && child.name != "WaxDeformedSurface")) continue;
            if (child.name == "WaxDeformedSurface")
            {
                var original = child.parent != null ? child.parent.GetComponent<SkinnedMeshRenderer>() : null;
                if (original != null) original.enabled = true;
            }
            child.gameObject.SetActive(false);
            // Do not destroy shared meshes/materials: they still belong to the source creature.
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }
    }
    void LateUpdate() => SyncSurface();
    public void SyncSurface()
    {
        if (sourceSkin != null && overlay is SkinnedMeshRenderer copy)
        {
            copy.sharedMesh = sourceSkin.sharedMesh;
            for (int i=0;i<sourceSkin.sharedMesh.blendShapeCount;i++)
                copy.SetBlendShapeWeight(i,sourceSkin.GetBlendShapeWeight(i));
        }
        if (sourceFilter != null && overlay != null)
            overlay.GetComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
        if (crackMaterial != null && surface != null)
            crackMaterial.SetMatrix("_WorldToCrack", (surface.localToWorldMatrix * crackSpace).inverse);
    }
    public void SetDamage(float damageFraction)
    {
        visibleStage = damageFraction >= 0.72f ? 3 : damageFraction >= 0.38f ? 2 : damageFraction > 0f ? 1 : 0;
        if (visibleStage == 0) { if (overlay != null) overlay.enabled = false; return; }
        EnsureOverlay();
        if (overlay == null) return;
        overlay.enabled = true;
        crackMaterial.SetFloat("_Damage", Mathf.Clamp01(damageFraction));
    }
    public void ShowCorpseCracks() => SetDamage(1f);

    void EnsureOverlay()
    {
        var dent=GetComponent<WaxDent>();
        if(overlay!=null && dent!=null && dent.SurfaceRenderer!=null && surface!=dent.SurfaceRenderer.transform)
        {
            if(Application.isPlaying) { Destroy(overlay.gameObject); Destroy(crackMaterial); }
            else { DestroyImmediate(overlay.gameObject); DestroyImmediate(crackMaterial); }
            overlay=null;sourceSkin=null;sourceFilter=null;
        }
        if (overlay != null) return;
        Renderer source = null;
        float largest = 0f;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            float size = r.bounds.size.sqrMagnitude;
            if (size > largest) { largest = size; source = r; }
        }
        if(dent!=null && dent.SurfaceRenderer!=null) source=dent.SurfaceRenderer;
        if (source == null) return;
        var template = Resources.Load<Material>("WaxCracks");
        if (template == null) return;
        Mesh mesh = source is SkinnedMeshRenderer sk ? sk.sharedMesh : source.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null) return;
        crackMaterial = new Material(template);
        crackMaterial.SetFloat("_UseRestPosition", dent != null && source == dent.SurfaceRenderer ? 1f : 0f);
        var bounds = source.bounds;
        surface = source.transform;
        float extent = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        crackSpace = surface.worldToLocalMatrix * Matrix4x4.TRS(bounds.center, surface.rotation, Vector3.one * extent);
        LateUpdate();
        var go = new GameObject("WaxSurfaceCracks");
        go.transform.SetParent(source.transform, false);
        if (source is SkinnedMeshRenderer skin)
        {
            sourceSkin = skin;
            var copy = go.AddComponent<SkinnedMeshRenderer>();
            copy.sharedMesh = mesh; copy.bones = skin.bones; copy.rootBone = skin.rootBone;
            copy.localBounds = skin.localBounds; copy.updateWhenOffscreen = skin.updateWhenOffscreen;
            overlay = copy;
        }
        else
        {
            sourceFilter = source.GetComponent<MeshFilter>();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            overlay = go.AddComponent<MeshRenderer>();
        }
        var materials = new Material[mesh.subMeshCount];
        for (int i = 0; i < materials.Length; i++) materials[i] = crackMaterial;
        overlay.sharedMaterials = materials;
        overlay.shadowCastingMode = ShadowCastingMode.Off;
        overlay.receiveShadows = false;
    }
    void OnDestroy()
    {
        if (crackMaterial != null)
        { if (Application.isPlaying) Destroy(crackMaterial); else DestroyImmediate(crackMaterial); }
    }
}
