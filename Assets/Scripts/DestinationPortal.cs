using UnityEngine;
using UnityEngine.UI;

// Presentation only. MapSandbox owns unlock rules, distance checks and travel input.
public sealed class DestinationPortal : MonoBehaviour
{
    Material windowMaterial,rimMaterial;
    Mesh rimMesh;
    Transform board;
    PlayerWeapons player;MapSandbox map;Vector3 previous;bool havePrevious;
    public int Destination {get;private set;}
    public bool HasVista {get;private set;}
    public void Build(int index,Vector3 direction,bool finish)
    {
        player=FindFirstObjectByType<PlayerWeapons>();map=FindFirstObjectByType<MapSandbox>();
        Destination=index;transform.rotation=Quaternion.LookRotation(direction,Vector3.up);
        var tint=finish?new Color(1,.68f,.1f):GameUiStyle.Blue;
        var vista=Resources.Load<PortalVista>("Portals/"+MapSandbox.Maps[index]);
        HasVista=!finish && vista!=null && vista.panorama!=null;
        var window=GameObject.CreatePrimitive(PrimitiveType.Quad);window.name="DestinationView";window.layer=2;window.transform.SetParent(transform,false);
        Destroy(window.GetComponent<Collider>());window.transform.localPosition=Vector3.up*1.25f;window.transform.localScale=new Vector3(1.8f,2.4f,1);
        windowMaterial=new Material(Resources.Load<Shader>("Portals/DestinationWindow"));windowMaterial.SetColor("_Tint",tint);
        windowMaterial.SetFloat("_Completion",finish?1:0);windowMaterial.SetFloat("_HasVista",HasVista?1:0);
        if(HasVista){windowMaterial.SetTexture("_Vista",vista.panorama);windowMaterial.SetVector("_Forward",vista.forward);}
        window.GetComponent<Renderer>().sharedMaterial=windowMaterial;
        var rim=new GameObject("MoldedRim",typeof(MeshFilter),typeof(MeshRenderer));rim.layer=2;rim.transform.SetParent(transform,false);rim.transform.localPosition=Vector3.up*1.25f;
        rimMesh=MakeRim();rim.GetComponent<MeshFilter>().sharedMesh=rimMesh;
        rimMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));rimMaterial.SetColor("_BaseColor",finish?new Color(.68f,.32f,.015f):new Color(.025f,.24f,.48f));rimMaterial.SetFloat("_Smoothness",.75f);rimMaterial.SetFloat("_Metallic",.25f);rimMaterial.EnableKeyword("_EMISSION");rimMaterial.SetColor("_EmissionColor",tint*.12f);rim.GetComponent<MeshRenderer>().sharedMaterial=rimMaterial;
        var go=new GameObject("Destination",typeof(RectTransform),typeof(Canvas));board=go.transform;board.SetParent(transform,false);board.localPosition=Vector3.up*2.85f;board.localScale=Vector3.one*.008f;
        var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(260,66);go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        var bg=ToyHudStyle.Piece(board,"Plaque",Vector2.zero,new Vector2(260,66),Color.white);ToyHudStyle.Frame(bg);
        var font=Resources.Load<Font>("UI/AugmentKorean");
        GameUiStyle.Label(bg.transform,"Title",finish?"모험 완료":$"STAGE {index+1:00}  ·  {MapSandbox.Names[index]}",font,19,Color.white,new Vector2(0,12),new Vector2(240,28),true);
        GameUiStyle.Label(bg.transform,"Hint",finish?"통과하면 모험 완료":"안으로 걸어가면 이동",font,14,new Color(.65f,.9f,1),new Vector2(0,-16),new Vector2(244,22),false);
    }
    void OnEnable(){havePrevious=false;}
    void LateUpdate()
    {
        var cam=Camera.main;if(board!=null && cam!=null)board.rotation=cam.transform.rotation;
        if(player==null || map==null)return;
        var current=transform.InverseTransformPoint(player.transform.position+Vector3.up*.9f);
        if(havePrevious && Time.timeScale>0 && !map.Busy && CrossesOpening(previous,current))map.EnterPortal(Destination);
        previous=current;havePrevious=true;
    }
    public static bool CrossesOpening(Vector3 from,Vector3 to)
    {
        float dz=to.z-from.z;if(Mathf.Abs(dz)<.0001f || from.z*to.z>0)return false;
        var hit=Vector3.Lerp(from,to,Mathf.Clamp01(-from.z/dz));
        float x=hit.x/.82f,y=(hit.y-1.25f)/1.10f;return x*x+y*y<=1;
    }
    static Mesh MakeRim()
    {
        const int n=80,t=10;var v=new Vector3[(n+1)*(t+1)];var uv=new Vector2[v.Length];var tri=new int[n*t*6];int p=0;
        for(int i=0;i<=n;i++)for(int j=0;j<=t;j++)
        {
            float a=i*Mathf.PI*2/n,b=j*Mathf.PI*2/t;var normal=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
            int k=i*(t+1)+j;v[k]=new Vector3(.94f*Mathf.Cos(a),1.24f*Mathf.Sin(a),0)+normal*(Mathf.Cos(b)*.065f)+Vector3.forward*(Mathf.Sin(b)*.065f);uv[k]=new Vector2((float)i/n,(float)j/t);
            if(i<n && j<t){tri[p++]=k;tri[p++]=k+t+1;tri[p++]=k+1;tri[p++]=k+1;tri[p++]=k+t+1;tri[p++]=k+t+2;}
        }
        var mesh=new Mesh{name="PortalMoldedRing"};mesh.vertices=v;mesh.uv=uv;mesh.triangles=tri;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    void OnDestroy(){if(windowMaterial!=null)Destroy(windowMaterial);if(rimMaterial!=null)Destroy(rimMaterial);if(rimMesh!=null)Destroy(rimMesh);}
}
