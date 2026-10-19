using UnityEngine;

// A brief, camera-facing contact star. Spawn only after a melee hit is confirmed.
public sealed class MeleeImpactBurst : MonoBehaviour
{
    Mesh mesh;
    Material material;
    float age;
    const float Duration=.18f;
    float size;
    public static void Spawn(Vector3 contact,float strength)
    {
        var go=new GameObject("MeleeContactBurst");
        var view=Camera.main;
        go.transform.position=contact+(view!=null?(view.transform.position-contact).normalized*.22f:Vector3.zero);
        if(view!=null)go.transform.rotation=view.transform.rotation;
        var burst=go.AddComponent<MeleeImpactBurst>();burst.Build(strength);
    }
    void Build(float strength)
    {
        size=.85f*Mathf.Clamp(strength,1f,2f);
        mesh=new Mesh{name="ContactStar"};
        var vertices=new Vector3[24];var triangles=new int[24];
        for(int i=0;i<8;i++)
        {
            float angle=(i*45+22)*Mathf.Deg2Rad;
            Vector3 direction=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
            Vector3 side=new Vector3(-direction.y,direction.x,0);
            vertices[i*3]=direction*.08f-side*.085f;
            vertices[i*3+1]=direction*(i%2==0?1f:.65f);
            vertices[i*3+2]=direction*.08f+side*.085f;
            triangles[i*3]=i*3;triangles[i*3+1]=i*3+1;triangles[i*3+2]=i*3+2;
        }
        mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var source=Resources.Load<Material>("AdditiveFX");
        material=source!=null?new Material(source):new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetFloat("_Cull",0);
        material.SetFloat("_SrcBlend",1);
        var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows=false;
        Apply();
    }
    int bornFrame;
    void Awake(){bornFrame=Time.frameCount;}
    void Update()
    {
        if(Time.frameCount==bornFrame)return;
        if(Time.timeScale<=0)return;
        age+=Time.unscaledDeltaTime;Apply();
        if(age>=Duration)Destroy(gameObject);
    }
    void Apply()
    {
        float t=Mathf.Clamp01(age/Duration);
        transform.localScale=Vector3.one*size*Mathf.Lerp(.55f,1.15f,t);
        Color color=new Color(3f,2.4f,.8f,1f)*(1-t);
        material.color=color;
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
    }
    void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
}
