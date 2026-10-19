using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A single anticipation -> snap -> settle, never a looping shake or a sliced text mesh.
public sealed class AugmentHoverCrack : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public AugmentCrackGraphic graphic;
    public AugmentArtFracture fracture;
    RectTransform visual;
    Image halo;
    Vector3 rest;
    Color haloColor;
    bool hovered;
    float elapsed,visibility;
    readonly System.Random motionRandom=new System.Random(System.Guid.NewGuid().GetHashCode());
    float snapAt=.16f,frequency=30,decay=10,tilt=4,force=1;
    float Range(float a,float b)=>(float)(a+(b-a)*motionRandom.NextDouble());
    public void Bind(RectTransform target,Image glow)
    {visual=target;halo=glow;rest=target.localPosition;}
    public void OnPointerEnter(PointerEventData e)
    {
        if(hovered)return;
        hovered=true;elapsed=0;
        snapAt=Range(.11f,.20f);frequency=Range(26,39);decay=Range(9,14);
        tilt=Range(3,6)*(motionRandom.Next(2)==0?-1:1);force=Range(.85f,1.2f);
        if(graphic!=null)graphic.NewPattern();
        if(fracture!=null)fracture.NewPattern();
        if(halo!=null)haloColor=halo.color;
    }
    public void OnPointerExit(PointerEventData e){hovered=false;}
    void Update()
    {
        if(hovered)elapsed+=Time.unscaledDeltaTime;
        visibility=Mathf.MoveTowards(visibility,hovered?1:0,Time.unscaledDeltaTime*8);
        Render(elapsed,visibility);
    }
    void Render(float time,float weight)
    {
        if(visual==null)return;
        float press=time<snapAt?Mathf.Pow(Mathf.Clamp01(time/snapAt),.7f):0;
        float after=Mathf.Max(0,time-snapAt);
        float recoil=time>=snapAt?Mathf.Exp(-after*decay)*Mathf.Sin(after*frequency)*force:0;
        float lift=Mathf.SmoothStep(0,1,Mathf.Clamp01(time/.3f));
        visual.localScale=new Vector3(1-.10f*press+.065f*lift+.075f*recoil,1+.045f*press+.065f*lift+.075f*recoil,1)*weight+Vector3.one*(1-weight);
        visual.localRotation=Quaternion.Euler(0,0,(-tilt*press+tilt*recoil)*weight);
        visual.localPosition=rest+Vector3.up*(10*lift+7*recoil)*weight;
        if(graphic!=null)graphic.SetState(after,time>=snapAt?weight:0);
        if(fracture!=null)fracture.SetMotion(after,time>=snapAt?weight:0);
        if(halo!=null){var c=hovered?haloColor:halo.color;c.a=Mathf.Lerp(.13f,.55f+Mathf.Max(0,recoil)*.3f,weight);halo.color=c;}
    }
    void OnDisable()
    {
        hovered=false;elapsed=visibility=0;
        if(visual!=null){visual.localScale=Vector3.one;visual.localRotation=Quaternion.identity;visual.localPosition=rest;}
        if(graphic!=null)graphic.SetState(0,0);
        if(fracture!=null)fracture.SetAmount(0);
        if(halo!=null){var c=halo.color;c.a=.13f;halo.color=c;}
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class AugmentCrackGraphic : MaskableGraphic
{
    sealed class Fracture { public Vector2[] points; public float delay,width; }
    struct Chip { public Vector2 origin,velocity; public float delay,size,spin; }
    readonly System.Collections.Generic.List<Fracture> lines=new System.Collections.Generic.List<Fracture>();
    readonly System.Collections.Generic.List<Chip> chips=new System.Collections.Generic.List<Chip>();
    // UI randomness must not change enemy spawns, drops or upgrade choices.
    readonly System.Random random=new System.Random();
    float age,weight;
    float Range(float a,float b)=>(float)(a+(b-a)*random.NextDouble());
    Vector2 Clamp(Vector2 p)=>new Vector2(Mathf.Clamp(p.x,-117,117),Mathf.Clamp(p.y,14,160));
    public void NewPattern()
    {
        lines.Clear();chips.Clear();age=weight=0;
        Vector2 center=new Vector2(Range(-30,30),Range(55,110));
        int first=random.Next(4),count=random.Next(3,5);
        for(int j=0;j<count;j++)
        {
            int edge=(first+j)%4;Vector2 origin;
            switch(edge)
            {
                case 0:origin=new Vector2(-117,Range(35,145));break;
                case 1:origin=new Vector2(Range(-100,100),160);break;
                case 2:origin=new Vector2(117,Range(35,145));break;
                default:origin=new Vector2(Range(-100,100),14);break;
            }
            float delay=j*.055f;var delta=center-origin;var side=new Vector2(-delta.y,delta.x).normalized;
            var path=new Vector2[6];path[0]=origin;path[5]=center;
            for(int i=1;i<5;i++)path[i]=Clamp(Vector2.Lerp(origin,center,i/5f)+side*Range(-13,13));
            lines.Add(new Fracture{points=path,delay=delay,width=Range(5,7)});
            for(int branch=0;branch<2;branch++)
            {
                int at=branch+2;float direction=branch==0?1:-1;var start=path[at];
                var middle=Clamp(start+side*direction*Range(14,26)+delta.normalized*Range(-8,8));
                var end=Clamp(middle+side*direction*Range(10,22)+delta.normalized*Range(-10,10));
                lines.Add(new Fracture{points=new[]{start,middle,end},delay=delay+.035f+branch*.025f,width=Range(2.5f,4)});
            }
            for(int k=0;k<4;k++)
            {
                var outward=(origin-center).normalized;
                var velocity=Quaternion.Euler(0,0,Range(-65,65))*outward*Range(30,62);
                chips.Add(new Chip{origin=origin,velocity=velocity,delay=delay,size=Range(3.5f,7.5f),spin=Range(-180,180)});
            }
        }
        SetVerticesDirty();
    }
    public void SetState(float time,float visibility){age=time;weight=visibility;SetVerticesDirty();}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();if(weight<=0)return;
        float fade=Mathf.Lerp(1,.85f,Mathf.Clamp01((age-.3f)/.35f))*weight;
        foreach(var chip in chips)
        {
            float travel=(age-chip.delay)/.5f;if(travel<0 || travel>=1)continue;
            var center=chip.origin+chip.velocity*travel+Vector2.down*(30*travel*travel);
            float size=chip.size*(1-travel*.4f);var c=color;c.a=(1-travel)*weight*.95f;
            var rotation=Quaternion.Euler(0,0,chip.spin*travel);
            int n=vh.currentVertCount;
            vh.AddVert(center+(Vector2)(rotation*new Vector3(-size,0,0)),c,Vector2.zero);
            vh.AddVert(center+(Vector2)(rotation*new Vector3(size*.6f,size,0)),c,Vector2.zero);
            vh.AddVert(center+(Vector2)(rotation*new Vector3(size,-size*.7f,0)),c,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);
        }
    }
    static void Stroke(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c)
    {
        var n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int k=vh.currentVertCount;
        vh.AddVert(a-n,c,Vector2.zero);vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
    }
}
