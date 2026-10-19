using UnityEngine;
using UnityEngine.UI;

public sealed class RunResultView : MonoBehaviour
{
    Text timeValue,killsValue,comboValue;
    Button retry;
    static readonly Color Ink=new Color(.94f,.98f,1f);
    static readonly Color Muted=new Color(.60f,.78f,.88f);
    public static Button Build(GameObject panel,bool victory)
    {
        if(panel==null)return null;
        foreach(Transform child in panel.transform)child.gameObject.SetActive(false);
        var shade=panel.GetComponent<Image>();if(shade!=null){shade.sprite=null;shade.color=new Color(.025f,.045f,.055f,.75f);}
        var view=panel.AddComponent<RunResultView>();
        var card=Rect(panel.transform,"ResultCard",new Vector2(0,-14),new Vector2(540,360));
        card.gameObject.AddComponent<UiPop>().hoverScale=false;
        var surface=card.gameObject.AddComponent<Image>();ToyHudStyle.Frame(surface);
        if(!victory)
        {
            var cracks=Rect(card,"CrackReveal",Vector2.zero,new Vector2(540,360));
            cracks.gameObject.AddComponent<ResultCrackGraphic>().raycastTarget=false;
        }
        var font=Resources.Load<Font>("UI/AugmentKorean");
        var badge=Rect(card,"WeaponBadge",new Vector2(0,184),new Vector2(100,100));
        var badgeImage=badge.gameObject.AddComponent<Image>();ToyHudStyle.Frame(badgeImage,true,victory);
        var clipping=Surface(badge,"IconClip",Vector2.zero,new Vector2(84,84),Color.white,true);
        clipping.gameObject.AddComponent<Mask>().showMaskGraphic=false;
        var art=Surface(clipping.transform,"Sword",Vector2.zero,new Vector2(92,92),Color.white);
        art.sprite=victory?ToyHudStyle.Map(4):ToyHudStyle.Weapon(0);art.type=Image.Type.Simple;art.preserveAspect=true;
        var stamp=Surface(card,"Status",new Vector2(163,141),new Vector2(130,28),new Color(.06f,.24f,.39f));
        Label(stamp.transform,font,"Label",victory?"05 / 05  CLEAR":"도전 종료",12,Vector2.zero,new Vector2(122,24),Ink);
        Label(card,font,"Title",victory?"전부 뿌쉈다!":"GAME OVER",40,new Vector2(0,103),new Vector2(460,60),victory?Ink:new Color(1f,.24f,.29f));
        if(victory)Label(card,font,"Subtitle","초원부터 화산까지, 완주 성공",15,new Vector2(0,63),new Vector2(460,28),Muted);
        var labels=new[]{"플레이 시간","몬스터 처치","최고 콤보"};var values=new Text[3];
        var colors=new[]{new Color(.05f,.17f,.29f),new Color(.05f,.17f,.29f),new Color(.05f,.17f,.29f)};
        for(int i=0;i<3;i++)
        {
            var tile=Surface(card,"Record"+i,new Vector2((i-1)*160,-9),new Vector2(148,94),colors[i]);
            Label(tile.transform,font,"Name",labels[i],12,new Vector2(0,24),new Vector2(138,23),Muted);
            values[i]=Label(tile.transform,font,"Value","—",30,new Vector2(0,-13),new Vector2(138,46),Ink);
        }
        view.timeValue=values[0];view.killsValue=values[1];view.comboValue=values[2];
        var buttonRect=Rect(card,"Retry",new Vector2(0,-104),new Vector2(292,58));
        var buttonImage=buttonRect.gameObject.AddComponent<Image>();if(victory)ToyHudStyle.Frame(buttonImage,false,true);
        else
        {
            buttonImage.sprite=GameUiStyle.Rounded;buttonImage.type=Image.Type.Sliced;buttonImage.color=new Color(.32f,.69f,.86f);
            var inset=Surface(buttonRect,"Inset",Vector2.zero,new Vector2(288,54),new Color(.05f,.12f,.21f));
            inset.raycastTarget=false;
            buttonRect.gameObject.AddComponent<ResultButtonFeedback>().surface=inset;
        }
        buttonImage.raycastTarget=true;
        var button=buttonRect.gameObject.AddComponent<Button>();button.targetGraphic=buttonImage;buttonRect.gameObject.AddComponent<UiPressFeedback>();button.onClick.AddListener(()=>GameAudio.Play(GameAudio.Sfx.UiClick));
        var tint=button.colors;tint.highlightedColor=Color.white;tint.pressedColor=new Color(.85f,.85f,.85f);button.colors=tint;
        Label(buttonRect,font,"Label","다시 도전",20,new Vector2(-12,0),new Vector2(240,48),victory?new Color(.16f,.12f,.04f):Ink);
        Label(buttonRect,font,"Arrow","→",25,new Vector2(108,0),new Vector2(32,40),victory?new Color(.28f,.19f,.03f):new Color(.53f,.84f,1f));
        Label(card,font,"Footer","처음부터 새롭게 시작",11,new Vector2(0,-150),new Vector2(300,22),Muted);
        view.retry=button;
        return button;
    }
    void OnEnable()
    {
        if(retry!=null)retry.Select();
        if(timeValue==null || RunStats.Instance==null)return;
        var stats=RunStats.Instance;timeValue.text=RunStats.FormatTime(stats.SurvivalTime);killsValue.text=stats.Kills.ToString();comboValue.text="×"+stats.BestComboThisRun;
    }
    static Image Surface(Transform parent,string name,Vector2 position,Vector2 size,Color color,bool circle=false)
    {
        var r=Rect(parent,name,position,size);var image=r.gameObject.AddComponent<Image>();image.sprite=circle?GameUiStyle.Circle:GameUiStyle.Rounded;image.type=circle?Image.Type.Simple:Image.Type.Sliced;image.pixelsPerUnitMultiplier=1.5f;image.color=color;image.raycastTarget=false;return image;
    }
    static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;return r;
    }
    static Text Label(Transform parent,Font font,string name,string content,int size,Vector2 position,Vector2 bounds,Color color)
    {
        var r=Rect(parent,name,position,bounds);var text=r.gameObject.AddComponent<Text>();text.font=font;text.text=content;text.fontSize=size;text.color=color;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;
    }
}
