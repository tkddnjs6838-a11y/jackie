using UnityEngine;

/// <summary>
/// Supplies the builtin primitive meshes to the runtime effects.
/// GameObject.CreatePrimitive cannot be used in the player: it always attaches a
/// collider, and the collider classes are stripped out of the build, which logs
/// "Can't add component because class 'SphereCollider' doesn't exist" every time.
/// The meshes come from a Resources prefab, which stripping leaves alone.
/// </summary>
public static class FxShapes
{
    private static Mesh sphere;
    private static Mesh cylinder;
    private static bool loaded;

    public static Mesh Mesh(PrimitiveType type)
    {
        Load();
        switch (type)
        {
            case PrimitiveType.Cylinder: return cylinder;
            default: return sphere;
        }
    }

    public static Mesh Sphere { get { Load(); return sphere; } }
    public static Mesh Cylinder { get { Load(); return cylinder; } }

    static void Load()
    {
        if (loaded) return;
        loaded = true;

        var prefab = Resources.Load<GameObject>("FxShapes");
        if (prefab == null)
        {
            Debug.LogWarning("FxShapes: Resources/FxShapes prefab is missing; effects will have no mesh.");
            return;
        }

        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.gameObject.name == "Sphere") sphere = mf.sharedMesh;
            else if (mf.gameObject.name == "Cylinder") cylinder = mf.sharedMesh;
        }
    }
}
