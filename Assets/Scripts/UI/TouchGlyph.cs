using UnityEngine;
using UnityEngine.UI;

// Crisp monochrome touch symbols, independent of font glyph availability.
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
    void Arc(float radius, float start, float end)
    {
        for(int i=0;i<32;i++)
        {
            float a=Mathf.Lerp(start,end,i/32f)*Mathf.Deg2Rad,b=Mathf.Lerp(start,end,(i+1)/32f)*Mathf.Deg2Rad;
            Line(new Vector2(.5f+Mathf.Cos(a)*radius,.5f+Mathf.Sin(a)*radius),new Vector2(.5f+Mathf.Cos(b)*radius,.5f+Mathf.Sin(b)*radius),.025f);
        }
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();mesh=vh;
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
