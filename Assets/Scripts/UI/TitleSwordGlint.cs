using UnityEngine;
using UnityEngine.UI;

// Unscaled-time input from TitleArtworkMotion keeps this looping while the menu pauses gameplay.
public sealed class TitleSwordGlint : MaskableGraphic
{
    const float Sweep=1.05f, Flash=.60f, Period=2.10f;
    // Pixel coordinates traced along the LEFT opaque edge of Hero.png (1671 x 941).
    static readonly Vector2[] BladeEdge={
        new Vector2(1305,525),new Vector2(1322,500),new Vector2(1349,460),
        new Vector2(1375,420),new Vector2(1402,380),new Vector2(1429,340),
        new Vector2(1456,300),new Vector2(1482,260),new Vector2(1509,220),
        new Vector2(1536,180),new Vector2(1572,140),new Vector2(1619,100),new Vector2(1629,94)
    };
    static readonly Vector2[] UpperBarrel={new Vector2(850,577),new Vector2(792,511),new Vector2(734,444)};
    int gunBarrel;
    public void SetGunBarrel(int barrel){gunBarrel=barrel;SetVerticesDirty();}
    float age=-1;
    public void SetTime(float t){age=t;SetVerticesDirty();}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();if(age<0)return;
        float cycle=Mathf.Repeat(age,Period);if(cycle>=Sweep+Flash)return;
        var r=rectTransform.rect;
        Vector2 direction;
        Vector2 tip=EdgePoint(r,1,out direction);
        float positionScale=r.height/720f;
        float size=positionScale*(gunBarrel==0?4f:2.2f);
        if(cycle<Sweep)
        {
            float t=cycle/Sweep;
            var at=EdgePoint(r,t,out direction);
            // Lift the highlight slightly outside the left cutting edge.
            if(gunBarrel==0)at+=new Vector2(-direction.y,direction.x)*(2f*positionScale);
            float alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.10f));
            // A narrow streak runs along the cutting edge, not across the blade face.
            float taper=Mathf.Lerp(1,.45f,Mathf.InverseLerp(.85f,1,t));
            Diamond(vh,at,direction,32*size*taper,9*size,new Color(1,.88f,.58f,alpha*.9f));
            Diamond(vh,at,direction,23*size*taper,4.5f*size,new Color(1,1,.97f,alpha));
        }
        else
        {
            float t=(cycle-Sweep)/Flash;
            float alpha=1-Mathf.SmoothStep(0,1,t);
            float burst=1+.65f*Mathf.Sin(Mathf.PI*t);
            Diamond(vh,tip,Vector2.up,32*burst*size,12*size,new Color(1,.76f,.28f,alpha*.7f));
            Diamond(vh,tip,Vector2.up,32*burst*size,7*size,new Color(1,1,.96f,alpha));
            Diamond(vh,tip,Vector2.right,28*burst*size,7*size,new Color(1,1,.96f,alpha));
            Diamond(vh,tip,new Vector2(.7071f,.7071f),12*burst*size,2.5f*size,new Color(1,.9f,.65f,alpha*.8f));
        }
    }
    Vector2 EdgePoint(Rect rect,float progress,out Vector2 direction)
    {
        var path=gunBarrel==0?BladeEdge:UpperBarrel;
        float total=0;
        for(int i=1;i<path.Length;i++)total+=Vector2.Distance(path[i-1],path[i]);
        float remaining=Mathf.Clamp01(progress)*total;
        for(int i=1;i<path.Length;i++)
        {
            Vector2 a=path[i-1],b=path[i];float length=Vector2.Distance(a,b);
            if(remaining<=length||i==path.Length-1)
            {
                Vector2 point=Vector2.Lerp(a,b,Mathf.Clamp01(remaining/length));
                direction=new Vector2((b.x-a.x)*rect.width/1671f,-(b.y-a.y)*rect.height/941f).normalized;
                return new Vector2(rect.xMin+point.x/1671f*rect.width,rect.yMax-point.y/941f*rect.height);
            }
            remaining-=length;
        }
        direction=Vector2.up;return rect.center;
    }
    static void Diamond(VertexHelper vh,Vector2 at,Vector2 direction,float length,float width,Color color)
    {
        var side=new Vector2(-direction.y,direction.x);int i=vh.currentVertCount;
        vh.AddVert(at,color,Vector2.zero);var edge=color;edge.a=0;
        vh.AddVert(at+direction*length,edge,Vector2.zero);vh.AddVert(at+side*width,edge,Vector2.zero);
        vh.AddVert(at-direction*length,edge,Vector2.zero);vh.AddVert(at-side*width,edge,Vector2.zero);
        for(int n=0;n<4;n++)vh.AddTriangle(i,i+1+n,i+1+(n+1)%4);
    }
}
