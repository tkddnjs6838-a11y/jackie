using UnityEngine;
using UnityEngine.Rendering;

// Simple category markers; all parts share one material and one mesh per crate.
public sealed class PickupCrateVisual:MonoBehaviour
{
    public enum Category { Weapon, Growth, Timed }
    Mesh mesh;
    public static void Build(Transform parent,Category category,Color accent)
    {
        var go=new GameObject("CategoryCrate_"+category);go.transform.SetParent(parent,false);
        var view=go.AddComponent<PickupCrateVisual>();view.Create(category,accent);
    }
    void Create(Category category,Color accent)
    {
        mesh=new Mesh{name="PickupCrateMesh"};
        mesh.vertices=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
        mesh.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,3,7,6,3,6,2,0,1,5,0,5,4};mesh.RecalculateNormals();
        Color body=category==Category.Weapon?new Color(.48f,.25f,.06f):category==Category.Growth?new Color(.06f,.32f,.15f):new Color(.32f,.08f,.5f);
        var size=category==Category.Weapon?new Vector3(.75f,.3f,.42f):new Vector3(.48f,.48f,.48f);
        Part("Body",Vector3.zero,size,body);
        Part("Lid",new Vector3(0,size.y*.5f,0),new Vector3(size.x+.025f,.055f,size.z+.025f),accent);
        for(int side=-1;side<=1;side+=2)
        {
            float z=side*(size.z*.5f+.015f);
            if(category==Category.Weapon)
            {
                for(int i=-1;i<=1;i++)Part("AmmoMark",new Vector3(i*.13f,0,z),new Vector3(.055f,.18f,.025f),accent);
            }
            else
            {
                Part(category==Category.Growth?"PlusVertical":"ClockMinute",new Vector3(0,category==Category.Growth?0:.06f,z),new Vector3(.06f,category==Category.Growth?.3f:.18f,.025f),accent);
                Part(category==Category.Growth?"PlusHorizontal":"ClockHour",new Vector3(category==Category.Growth?0:.07f,0,z),new Vector3(category==Category.Growth?.3f:.18f,.06f,.025f),accent);
            }
        }
    }
    void Part(string name,Vector3 at,Vector3 size,Color color)
    {
        var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=at;go.transform.localScale=size;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=Resources.Load<Material>("Weapons/Guns/GunCeramic");
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);renderer.SetPropertyBlock(block);
    }
    void OnDestroy(){if(mesh!=null)Destroy(mesh);}
}
