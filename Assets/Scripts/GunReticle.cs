using UnityEngine;
using UnityEngine.UI;

// Screen-centre feedback shares the exact camera ray used by PlayerGunCombat.
public sealed class GunReticle:Graphic
{
    PlayerGunCombat gun;PlayerWeapons weapons;
    float shot,hit;
    Canvas hud;
    public bool Visible=>hud!=null && hud.enabled;
    public static GunReticle Create(PlayerGunCombat gun,PlayerWeapons weapons)
    {
        var root=new GameObject("GunAimHUD",typeof(Canvas),typeof(CanvasScaler));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=80;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var go=new GameObject("Reticle",typeof(RectTransform),typeof(CanvasRenderer));go.transform.SetParent(root.transform,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.sizeDelta=new Vector2(160,160);
        var view=go.AddComponent<GunReticle>();view.gun=gun;view.weapons=weapons;view.hud=canvas;view.raycastTarget=false;canvas.enabled=false;return view;
    }
    public void Shot(bool connected){shot=.15f;if(connected)hit=.22f;SetVerticesDirty();}
    void Update()
    {
        if(hud==null)return;
        hud.enabled=gun!=null && gun.isActiveAndEnabled && weapons.IsGun && RunStats.Instance!=null && RunStats.Instance.Running && Time.timeScale>0;
        if(!hud.enabled){shot=hit=0;return;}
        shot=Mathf.Max(0,shot-Time.deltaTime);hit=Mathf.Max(0,hit-Time.deltaTime);SetVerticesDirty();
    }
    static void Quad(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
    {
        var side=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
        int i=vh.currentVertCount;vh.AddVert(a-side,color,Vector2.zero);vh.AddVert(a+side,color,Vector2.zero);vh.AddVert(b+side,color,Vector2.zero);vh.AddVert(b-side,color,Vector2.zero);
        vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
    }
    static void Stroke(VertexHelper vh,Vector2 a,Vector2 b,Color color)
    {Quad(vh,a,b,6,new Color(0,0,0,.85f));Quad(vh,a,b,2.5f,color);}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();if(weapons==null)return;
        Color tint=shot>0?GunEffects.ColorFor(weapons.Equipped):Color.white;
        float gap=(weapons.Equipped==PlayerWeapons.Kind.Shotgun?20:8)+shot/.15f*10;
        for(int i=0;i<4;i++)
        {
            float a=i*Mathf.PI*.5f;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Stroke(vh,dir*gap,dir*(gap+9),tint);
        }
        Quad(vh,new Vector2(-2,0),new Vector2(2,0),6,Color.black);Quad(vh,new Vector2(-1,0),new Vector2(1,0),2,Color.white);
        if(hit>0)for(int i=0;i<4;i++)
        {
            float a=Mathf.PI*.25f+i*Mathf.PI*.5f;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Stroke(vh,dir*21,dir*29,new Color(1,.85f,.35f));
        }
        float progress=gun.IsReloading?gun.ReloadFraction:gun.ChargeFraction;
        if(progress>0)
        {
            int segments=Mathf.CeilToInt(progress*40);Color c=gun.IsReloading?new Color(1,.7f,.2f):new Color(.2f,.9f,1);
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI/20,b=(i+1)*Mathf.PI/20;
                Stroke(vh,new Vector2(Mathf.Sin(a),Mathf.Cos(a))*35,new Vector2(Mathf.Sin(b),Mathf.Cos(b))*35,c);
            }
        }
    }
}
