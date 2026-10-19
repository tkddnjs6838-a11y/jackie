using UnityEngine;
using UnityEngine.UI;

// Presentation only: all copy and values still come from DescribeChoice.
public sealed class AugmentCard
{
    public readonly Button Button;
    readonly GameObject slot;
    readonly AugmentCrackGraphic cracks;
    readonly Image frame,glow,pill,art,tag;
    readonly Text title,description,value,level;
    readonly Image[] pips=new Image[CharacterGrowth.MaxLevel];
    const float Width=250,Height=350;
    static readonly Color Ink=new Color(.94f,.98f,1f),Muted=new Color(.65f,.79f,.89f);
    public AugmentCard(Transform parent,Font font,int index,Vector2 at)
    {
        slot=Node("Choice"+index,parent,at,new Vector2(Width,Height)).gameObject;
        var pop=slot.AddComponent<UiPop>();pop.delay=index*.07f;pop.hoverScale=false;
        var visual=Node("HoverVisual",slot.transform,Vector2.zero,new Vector2(Width,Height));
        glow=Panel("NeonHalo",visual,GameUiStyle.Glow,Color.white,Vector2.zero,new Vector2(Width+8,Height+8));
        var rim=Panel("WhiteRim",visual,GameUiStyle.Rounded,Color.white,Vector2.zero,new Vector2(Width,Height));ToyHudStyle.Frame(rim);
        frame=Panel("Card",visual,GameUiStyle.Rounded,Color.white,Vector2.zero,new Vector2(Width-12,Height-12));
        frame.raycastTarget=true;frame.gameObject.AddComponent<UiSurfaceGradient>();
        Button=frame.gameObject.AddComponent<Button>();Button.transition=Selectable.Transition.None;
        frame.gameObject.AddComponent<UiPressFeedback>();
        var core=Panel("ArtWell",frame.transform,GameUiStyle.Rounded,new Color(.04f,.08f,.22f,.42f),new Vector2(0,66),new Vector2(Width-15,202));
        core.gameObject.AddComponent<UiSurfaceGradient>();
        art=Panel("Illustration",frame.transform,null,Color.white,new Vector2(0,76),new Vector2(202,172));
        art.type=Image.Type.Simple;art.preserveAspect=true;
        Panel("Body",frame.transform,GameUiStyle.Rounded,new Color(.05f,.15f,.26f),new Vector2(0,-83),new Vector2(Width-14,171));
        title=GameUiStyle.Label(frame.transform,"Title","",font,22,Ink,new Vector2(0,-24),new Vector2(Width-22,42),true);
        title.resizeTextForBestFit=true;title.resizeTextMinSize=18;title.resizeTextMaxSize=22;
        description=GameUiStyle.Label(frame.transform,"Description","",font,15,Muted,new Vector2(0,-65),new Vector2(Width-24,40),false);
        pill=Panel("ValuePill",frame.transform,GameUiStyle.Rounded,Color.white,new Vector2(0,-112),new Vector2(Width-38,46));
        value=GameUiStyle.Label(pill.transform,"Value","",font,22,Ink,Vector2.zero,new Vector2(Width-42,52),true);
        for(int i=0;i<pips.Length;i++)pips[i]=Panel("Level"+i,frame.transform,GameUiStyle.Circle,Color.white,new Vector2(0,-151),Vector2.one*9);
        tag=Panel("LevelTag",frame.transform,GameUiStyle.Rounded,new Color(.03f,.09f,.23f),new Vector2(0,175),new Vector2(96,34));
        var edge=tag.gameObject.AddComponent<Outline>();edge.effectColor=Color.white;edge.effectDistance=new Vector2(1,-1);
        level=GameUiStyle.Label(tag.transform,"Label","",font,18,Color.white,Vector2.zero,new Vector2(90,30),true);
        cracks=Node("HoverCracks",frame.transform,Vector2.zero,new Vector2(Width-12,Height-12)).gameObject.AddComponent<AugmentCrackGraphic>();
        cracks.raycastTarget=false;
        var hover=frame.gameObject.AddComponent<AugmentHoverCrack>();hover.graphic=cracks;hover.fracture=art.gameObject.AddComponent<AugmentArtFracture>();
        // Small static highlights provide shine without particle systems or extra canvases.
        GameUiStyle.Label(frame.transform,"SparkleLeft","✦",font,24,new Color(1,1,1,.28f),new Vector2(-94,105),new Vector2(32,32),false);
        GameUiStyle.Label(frame.transform,"SparkleRight","✦",font,17,new Color(1,1,1,.28f),new Vector2(93,50),new Vector2(26,26),false);
        hover.Bind(visual,glow);
    }
    public void Show(CharacterGrowth growth,CharacterGrowth.Kind kind)
    {
        var copy=growth.DescribeChoice(kind);var color=GameUiArt.Accent(kind);
        cracks.color=Color.Lerp(color,Color.white,.65f);
        frame.color=Color.Lerp(new Color(.035f,.095f,.20f),color,.20f);glow.color=new Color(color.r,color.g,color.b,.13f);pill.color=Color.Lerp(new Color(.04f,.12f,.22f),color,.25f);tag.color=Color.Lerp(new Color(.03f,.09f,.23f),color,.35f);
        title.text=copy.title;description.text=copy.description;art.sprite=GameUiArt.Icon(kind);art.enabled=art.sprite!=null;
        bool multiline=copy.value.Contains("\n");value.fontSize=multiline?17:22;
        value.text=copy.value.Replace("→","<color=#"+ColorUtility.ToHtmlStringRGB(Color.Lerp(color,Color.white,.35f))+">→</color>");
        int current=growth.Level(kind),limit=CharacterGrowth.Limit(kind);level.text="Lv. "+(current+1);
        for(int i=0;i<pips.Length;i++)
        {
            pips[i].gameObject.SetActive(i<limit);pips[i].rectTransform.anchoredPosition=new Vector2((i-(limit-1)*.5f)*18,-151);
            pips[i].rectTransform.sizeDelta=Vector2.one*(i==current?12:8);
            pips[i].color=i<=current?color:new Color(.77f,.82f,.9f);
        }
        slot.SetActive(true);
    }
    public void Hide()=>slot.SetActive(false);
    static RectTransform Node(string name,Transform parent,Vector2 at,Vector2 size)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=at;r.sizeDelta=size;return r;
    }
    static Image Panel(string name,Transform parent,Sprite sprite,Color color,Vector2 at,Vector2 size)
    {
        var image=Node(name,parent,at,size).gameObject.AddComponent<Image>();image.sprite=sprite;image.color=color;image.raycastTarget=false;
        image.type=sprite!=null && sprite.border!=Vector4.zero?Image.Type.Sliced:Image.Type.Simple;return image;
    }
}
