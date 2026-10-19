using UnityEngine;
using UnityEngine.UI;

// Crisp monochrome UI symbols (touch buttons, augment badges), independent of font glyph availability.
public sealed class TouchGlyph : MaskableGraphic
{
    public string Symbol;
    VertexHelper mesh;
    void Line(Vector2 a, Vector2 b, float width = .045f)
    {
        var n = new Vector2(-(b-a).y, (b-a).x).normalized * width * .5f;
        var r=rectTransform.rect; int first=mesh.currentVertCount;
        foreach(var p in new[]{a+n,b+n,b-n,a-n})
            mesh.AddVert(new Vector3(r.xMin+p.x*r.width,r.yMin+p.y*r.height),color,Vector2.zero);
        mesh.AddTriangle(first,first+1,first+2);mesh.AddTriangle(first,first+2,first+3);
    }
    void Arc(float radius, float start, float end, float width = .025f, float cx = .5f, float cy = .5f)
    {
        for(int i=0;i<32;i++)
        {
            float a=Mathf.Lerp(start,end,i/32f)*Mathf.Deg2Rad,b=Mathf.Lerp(start,end,(i+1)/32f)*Mathf.Deg2Rad;
            Line(new Vector2(cx+Mathf.Cos(a)*radius,cy+Mathf.Sin(a)*radius),new Vector2(cx+Mathf.Cos(b)*radius,cy+Mathf.Sin(b)*radius),width);
        }
    }
    void Path(float width, params float[] xy)
    {
        for(int i=0;i+3<xy.Length;i+=2)Line(new Vector2(xy[i],xy[i+1]),new Vector2(xy[i+2],xy[i+3]),width);
    }
    // Augment card badges, one per CharacterGrowth.Kind name; strokes are bolder than the touch symbols.
    bool Augment()
    {
        const float w=.085f;
        switch(Symbol)
        {
            case "Power": Path(w,.24f,.24f,.8f,.8f);Path(w,.18f,.46f,.46f,.18f);Path(w,.33f,.33f,.14f,.14f);return true;
            case "Speed": Path(w,.5f,.24f,.8f,.5f,.5f,.76f);Path(w,.14f,.62f,.4f,.62f);Path(w,.08f,.5f,.36f,.5f);Path(w,.14f,.38f,.4f,.38f);return true;
            case "Vitality": Arc(.15f,0,180,w,.355f,.58f);Arc(.15f,0,180,w,.645f,.58f);Path(w,.21f,.58f,.5f,.2f,.79f,.58f);return true;
            case "QuickHands": Path(w,.62f,.9f,.3f,.48f,.56f,.48f,.38f,.1f);return true;
            case "LongReach": Path(w,.16f,.5f,.82f,.5f);Path(w,.64f,.68f,.82f,.5f,.64f,.32f);Path(w,.16f,.3f,.16f,.7f);return true;
            case "WideSwing": Arc(.46f,25,155,w,.5f,.22f);Path(w,.08f,.41f,.5f,.22f,.92f,.41f);return true;
            case "BloodEdge": Arc(.22f,150,390,w,.5f,.38f);Path(w,.31f,.49f,.5f,.88f,.69f,.49f);return true;
            case "SecondWind": Arc(.34f,0,360,w);Path(w,.5f,.74f,.5f,.5f,.68f,.5f);return true;
            default: return false;
        }
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();mesh=vh;
        if(Augment())return;
        if(Symbol=="Attack")
        {
            Arc(.28f,0,360);
            Line(new Vector2(.5f,.08f),new Vector2(.5f,.32f));Line(new Vector2(.5f,.68f),new Vector2(.5f,.92f));
            Line(new Vector2(.08f,.5f),new Vector2(.32f,.5f));Line(new Vector2(.68f,.5f),new Vector2(.92f,.5f));return;
        }
        if(Symbol=="Dash")
        {
            for(int i=0;i<2;i++){float x=.18f+i*.35f;Line(new Vector2(x,.23f),new Vector2(x+.24f,.5f));Line(new Vector2(x+.24f,.5f),new Vector2(x,.77f));}return;
        }
        if(Symbol=="Reload")
        {
            Arc(.32f,45,335);Line(new Vector2(.72f,.72f),new Vector2(.73f,.94f));Line(new Vector2(.72f,.72f),new Vector2(.94f,.72f));return;
        }
        if(Symbol=="Switch")
        {
            Line(new Vector2(.15f,.68f),new Vector2(.85f,.68f));Line(new Vector2(.65f,.88f),new Vector2(.85f,.68f));
            Line(new Vector2(.15f,.32f),new Vector2(.85f,.32f));Line(new Vector2(.35f,.12f),new Vector2(.15f,.32f));return;
        }
        Arc(.32f,0,360);
    }
}
