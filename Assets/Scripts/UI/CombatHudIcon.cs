using UnityEngine;
using UnityEngine.UI;

// Small code-native HUD symbols; no extra camera or per-frame render texture.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CombatHudIcon : MaskableGraphic
{
    public enum Kind { Heart,Clock,Skull,Sword,Axe,Shotgun,MachineGun,LaserGun }
    Kind kind;
    public void SetKind(Kind value){if(kind==value)return;kind=value;SetVerticesDirty();}
    VertexHelper mesh;
    void Poly(Color ink,params Vector2[] points)
    {
        int first=mesh.currentVertCount;var r=rectTransform.rect;
        foreach(var p in points)mesh.AddVert(new Vector3(r.xMin+p.x*r.width,r.yMin+p.y*r.height),ink,Vector2.zero);
        for(int i=1;i<points.Length-1;i++)mesh.AddTriangle(first,first+i+1,first+i);
    }
    void Rect(Color c,float x,float y,float w,float h)=>Poly(c,new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h));
    void Disc(Color c,float x,float y,float radius)
    {var points=new Vector2[24];for(int i=0;i<24;i++){float a=i*Mathf.PI*2/24;points[i]=new Vector2(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius);}Poly(c,points);}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();mesh=vh;var silver=new Color(.68f,.77f,.83f);var dark=new Color(.08f,.12f,.16f);var wood=new Color(.48f,.22f,.12f);
        if(kind==Kind.Heart){Disc(color,.3f,.66f,.27f);Disc(color,.7f,.66f,.27f);Poly(color,new Vector2(.04f,.59f),new Vector2(.5f,.04f),new Vector2(.96f,.59f));return;}
        if(kind==Kind.Clock){Disc(color,.5f,.5f,.46f);Disc(dark,.5f,.5f,.34f);Rect(color,.46f,.46f,.09f,.3f);Rect(color,.5f,.43f,.22f,.08f);return;}
        if(kind==Kind.Skull){Disc(color,.5f,.6f,.43f);Rect(color,.24f,.12f,.52f,.42f);Disc(dark,.33f,.62f,.11f);Disc(dark,.67f,.62f,.11f);Rect(dark,.43f,.27f,.06f,.12f);Rect(dark,.55f,.27f,.06f,.12f);return;}
        if(kind==Kind.Sword){Poly(silver,new Vector2(.4f,.34f),new Vector2(.68f,.96f),new Vector2(.75f,.98f),new Vector2(.76f,.84f),new Vector2(.5f,.29f));Poly(wood,new Vector2(.26f,.06f),new Vector2(.36f,.02f),new Vector2(.5f,.31f),new Vector2(.4f,.36f));Poly(silver,new Vector2(.29f,.39f),new Vector2(.61f,.22f),new Vector2(.65f,.32f),new Vector2(.33f,.48f));return;}
        if(kind==Kind.Axe){Poly(wood,new Vector2(.23f,.02f),new Vector2(.3f,0),new Vector2(.68f,.91f),new Vector2(.61f,.95f));Poly(silver,new Vector2(.45f,.92f),new Vector2(.91f,.83f),new Vector2(.75f,.48f),new Vector2(.51f,.65f));return;}
        if(kind==Kind.Shotgun){Poly(wood,new Vector2(.04f,.18f),new Vector2(.27f,.34f),new Vector2(.35f,.49f),new Vector2(.06f,.5f));Rect(dark,.25f,.39f,.61f,.29f);Rect(silver,.35f,.62f,.61f,.09f);Rect(new Color(.4f,.48f,.55f),.35f,.48f,.61f,.1f);Rect(wood,.47f,.34f,.23f,.14f);Rect(silver,.27f,.4f,.12f,.21f);return;}
        if(kind==Kind.MachineGun){Rect(dark,.06f,.36f,.19f,.22f);Rect(silver,.22f,.4f,.48f,.31f);Rect(dark,.65f,.47f,.31f,.17f);Rect(wood,.48f,.13f,.13f,.29f);Rect(dark,.29f,.15f,.09f,.27f);for(int i=0;i<4;i++)Rect(dark,.38f+i*.065f,.61f,.03f,.06f);return;}
        Rect(silver,.12f,.39f,.45f,.34f);Rect(dark,.2f,.15f,.12f,.29f);Rect(silver,.54f,.65f,.41f,.11f);Rect(silver,.54f,.34f,.41f,.1f);Rect(Color.cyan,.47f,.48f,.32f,.08f);Disc(Color.cyan,.4f,.57f,.08f);
    }
}
