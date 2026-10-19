using UnityEngine;
using UnityEngine.UI;

// Shared cut-corner treatment for gameplay cards; text remains level and readable.
public static class GameUiStyle
{
    public static readonly Color Ink=new Color(.035f,.075f,.18f,.90f);
    public static readonly Color Gold=new Color(1f,.70f,.20f);
    public static readonly Color Pink=new Color(1f,.29f,.60f);
    public static readonly Color Blue=new Color(.16f,.72f,1f);
    public static readonly Color Purple=new Color(.71f,.32f,1f);
    static Sprite card,rounded,circle,glow,ringGlow;
    public static Sprite Glow=>glow!=null?glow:glow=GlowSprite();
    public static Sprite RingGlow=>ringGlow!=null?ringGlow:ringGlow=GlowSprite(true);
    static Sprite GlowSprite(bool circular=false)
    {
        const int size=128;var t=new Texture2D(size,size,TextureFormat.RGBA32,false){name="SoftUiGlow",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float qx=Mathf.Abs(x-63.5f)-26,qy=Mathf.Abs(y-63.5f)-26;
            float distance=Mathf.Sqrt(Mathf.Max(qx,0)*Mathf.Max(qx,0)+Mathf.Max(qy,0)*Mathf.Max(qy,0))+Mathf.Min(Mathf.Max(qx,qy),0)-24;
            if(circular)distance=Vector2.Distance(new Vector2(x,y),new Vector2(63.5f,63.5f))-50;
            pixels[y*size+x]=new Color(1,1,1,Mathf.Exp(-Mathf.Abs(distance)/4f)*.8f);
        }
        t.SetPixels32(pixels);t.Apply(false,true);
        return Sprite.Create(t,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(52,52,52,52));
    }
    public static Sprite Card=>card!=null?card:card=RoundedSprite("CutCornerUi",64,6,16);
    // Soft 30px corners (9-sliced); Image.pixelsPerUnitMultiplier shrinks them for smaller pieces.
    public static Sprite Rounded=>rounded!=null?rounded:rounded=RoundedSprite("RoundedUi",128,30,40);
    public static Sprite Circle=>circle!=null?circle:circle=RoundedSprite("CircleUi",128,63,0);
    static Sprite RoundedSprite(string name,int size,int radius,int border)
    {
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float dx=x-Mathf.Clamp(x,radius,size-1-radius),dy=y-Mathf.Clamp(y,radius,size-1-radius);
            float edge=radius-Mathf.Sqrt(dx*dx+dy*dy);
            pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(edge));
        }
        texture.SetPixels32(pixels);texture.Apply();
        return Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(border,border,border,border));
    }
    public static Text Label(Transform parent,string name,string content,Font font,int size,Color color,Vector2 at,Vector2 bounds,bool bold)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=at;rect.sizeDelta=bounds;
        var text=go.GetComponent<Text>();text.font=font;text.fontSize=size;text.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;text.color=color;text.alignment=TextAnchor.MiddleCenter;text.text=content;text.raycastTarget=false;
        text.verticalOverflow=VerticalWrapMode.Overflow; // a line taller than its box would otherwise vanish entirely
        return text;
    }
    public static void Apply(Image image,Color color)
    {
        if(image==null)return;image.sprite=Rounded;image.type=Image.Type.Sliced;image.color=color;
    }
    public static void Accent(Transform parent)
    {
        var image=parent.GetComponent<Image>();
        if(image==null)return;
        var shadow=image.GetComponent<Shadow>();
        if(shadow==null)shadow=image.gameObject.AddComponent<Shadow>();
        shadow.effectColor=new Color(.01f,.02f,.08f,.42f);shadow.effectDistance=new Vector2(0,-4);
        var outline=image.GetComponent<Outline>();
        if(outline==null)outline=image.gameObject.AddComponent<Outline>();
        outline.effectColor=new Color(.25f,.72f,1f,.34f);outline.effectDistance=new Vector2(1,-1);
    }
    // Warm field-guide surfaces for the world-facing HUD, without emissive borders.
    public static readonly Color FieldPaper=new Color(.96f,.92f,.80f,.97f);
    public static readonly Color FieldInk=new Color(.15f,.25f,.23f);
    public static readonly Color FieldMuted=new Color(.36f,.43f,.36f);
    public static readonly Color FieldGreen=new Color(.43f,.64f,.40f);
    public static void ApplyFieldPanel(Image image)
    {
        Apply(image,FieldPaper);image.pixelsPerUnitMultiplier=3;
        var shadow=image.gameObject.AddComponent<Shadow>();
        shadow.effectColor=new Color(.08f,.16f,.13f,.28f);shadow.effectDistance=new Vector2(0,-3);
        var outline=image.gameObject.AddComponent<Outline>();
        outline.effectColor=new Color(.35f,.40f,.29f,.65f);outline.effectDistance=new Vector2(1,-1);
    }
    public static void StyleResult(GameObject panel)
    {
        if(panel==null)return;
        foreach(var button in panel.GetComponentsInChildren<Button>(true))
        {
            Apply(button.GetComponent<Image>(),Gold);
            foreach(var text in button.GetComponentsInChildren<Text>(true)){text.color=Ink;text.fontStyle=FontStyle.Bold;}
        }
    }
}
