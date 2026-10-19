using UnityEngine;
using UnityEngine.UI;

// Shared presentation; existing controls retain their events and saved preferences.
public static class ToyMenuLayout
{
    public static readonly Color White=new Color(.94f,.98f,1),Muted=new Color(.60f,.78f,.88f);
    public static RectTransform Rect(Transform parent,string name,Vector2 at,Vector2 size)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);Place(r,at,size);return r;
    }
    public static void Place(RectTransform r,Vector2 at,Vector2 size)
    {r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=at;r.sizeDelta=size;r.localScale=Vector3.one;}
    public static Image Panel(Transform parent,string name,Vector2 at,Vector2 size,bool frame=true)
    {
        var r=Rect(parent,name,at,size);var im=r.gameObject.AddComponent<Image>();
        if(frame)ToyHudStyle.Frame(im);else{im.sprite=GameUiStyle.Rounded;im.type=Image.Type.Sliced;im.color=new Color(.05f,.17f,.29f);im.raycastTarget=false;}
        return im;
    }
    public static Text Label(Transform parent,string name,string value,int size,Vector2 at,Vector2 bounds,Color color,TextAnchor align=TextAnchor.MiddleCenter)
    {
        var t=Rect(parent,name,at,bounds).gameObject.AddComponent<Text>();t.font=Resources.Load<Font>("UI/AugmentKorean");t.text=value;t.fontSize=size;t.color=color;t.alignment=align;t.raycastTarget=false;return t;
    }
    public static void ButtonStyle(Button b,string text,bool gold,Vector2 at,Vector2 size)
    {
        foreach(Transform c in b.transform)c.gameObject.SetActive(false);
        Place((RectTransform)b.transform,at,size);var im=b.GetComponent<Image>();if(im==null)im=b.gameObject.AddComponent<Image>();
        ToyHudStyle.Frame(im,false,gold);im.raycastTarget=true;b.targetGraphic=im;
        if(b.GetComponent<UiPressFeedback>()==null)b.gameObject.AddComponent<UiPressFeedback>();
        Label(b.transform,"UnifiedLabel",text,22,Vector2.zero,size-new Vector2(16,8),gold?new Color(.16f,.12f,.04f):White);
    }
    public static void Build(StartScreenView view)
    {
        // Preserve the approved title artwork and slash-shaped start button from the prefab.
        var overlay=view.settingsPanel.transform;
        // Move controls out before hiding the old layout. No listeners are replaced.
        var card=Panel(overlay,"SettingsCard",Vector2.zero,new Vector2(650,580)).rectTransform;
        view.volumeSlider.transform.SetParent(card,false);view.volumeLabel.transform.SetParent(card,false);view.closeSettingsButton.transform.SetParent(card,false);
        foreach(Transform c in overlay)if(c!=card)c.gameObject.SetActive(false);
        var shade=view.settingsPanel.GetComponent<Image>();if(shade!=null)shade.color=new Color(.01f,.025f,.05f,.82f);
        Label(card,"Heading","설정",32,new Vector2(0,234),new Vector2(560,50),White);
        Label(card,"AudioTitle","전체 음량",17,new Vector2(-185,169),new Vector2(180,32),Muted,TextAnchor.MiddleLeft);
        Place(view.volumeLabel.rectTransform,new Vector2(223,169),new Vector2(100,32));view.volumeLabel.color=White;view.volumeLabel.fontSize=20;
        var slider=view.volumeSlider;Place((RectTransform)slider.transform,new Vector2(0,126),new Vector2(540,38));
        var track=slider.transform.Find("Track") as RectTransform;if(track!=null){Place(track,Vector2.zero,new Vector2(540,12));track.GetComponent<Image>().color=new Color(.025f,.08f,.15f);}
        var fill=slider.fillRect;var area=(RectTransform)fill.parent;Place(area,Vector2.zero,new Vector2(520,12));fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;fill.GetComponent<Image>().color=new Color(.10f,.68f,.98f);
        var handleArea=Rect(slider.transform,"HandleArea",Vector2.zero,new Vector2(520,34));slider.handleRect.SetParent(handleArea,false);slider.handleRect.sizeDelta=new Vector2(26,26);slider.handleRect.anchoredPosition=Vector2.zero;slider.handleRect.GetComponent<Image>().color=new Color(1,.80f,.24f);slider.handleRect.GetComponent<Image>().sprite=GameUiStyle.Circle;slider.handleRect.GetComponent<Image>().type=Image.Type.Simple;
        slider.SetDirection(Slider.Direction.LeftToRight,true);float volume=slider.value;slider.SetValueWithoutNotify(volume>.5f?0:1);slider.SetValueWithoutNotify(volume);
        Label(card,"ControlHeading","그림으로 보는 조작법",17,new Vector2(0,70),new Vector2(540,28),Muted);
        string[] actions={"이동","대시","공격 / 사격","무기 교체","재장전","보유 증강"};
        for(int i=0;i<6;i++)
        {
            var row=Panel(card,"Control"+i,new Vector2(i%2==0?-141:141,18-(i/2)*62),new Vector2(264,56),false).transform;
            Label(row,"Action",actions[i],15,new Vector2(58,0),new Vector2(138,40),White);
            if(i==0)
            {
                Keycap(row,"W",new Vector2(-64,12),new Vector2(24,23));
                Keycap(row,"A",new Vector2(-90,-12),new Vector2(24,23));
                Keycap(row,"S",new Vector2(-64,-12),new Vector2(24,23));
                Keycap(row,"D",new Vector2(-38,-12),new Vector2(24,23));
            }
            else if(i==1)Keycap(row,"SPACE",new Vector2(-66,0),new Vector2(94,28));
            else if(i==2){MouseDiagram(row,new Vector2(-66,0),true);}
            else if(i==3)for(int k=0;k<5;k++)Keycap(row,(k+1).ToString(),new Vector2(-110+k*22,0),new Vector2(20,28));
            else Keycap(row,i==4?"R":"TAB",new Vector2(-66,0),new Vector2(i==4?40:66,32));
        }
        ButtonStyle(view.closeSettingsButton,"돌아가기  ·  Esc",true,new Vector2(0,-230),new Vector2(280,52));
    }
    static void Keycap(Transform parent,string key,Vector2 at,Vector2 size)
    {
        var rim=Panel(parent,"Key_"+key,at,size,false);rim.color=new Color(.39f,.54f,.66f);
        var face=Panel(rim.transform,"Face",new Vector2(0,2),size-new Vector2(3,4),false);face.color=new Color(.86f,.93f,.98f);
        Label(face.transform,"Glyph",key,key.Length>1?11:13,Vector2.zero,size,new Color(.06f,.12f,.19f));
    }
    static void MouseDiagram(Transform parent,Vector2 at,bool left)
    {
        var body=Panel(parent,left?"LeftMouse":"RightMouse",at,new Vector2(31,43),false);body.color=new Color(.76f,.85f,.92f);
        var selected=Panel(body.transform,"PressedButton",new Vector2(left?-7:7,9),new Vector2(12,19),false);selected.color=new Color(1,.78f,.16f);
        var wheel=Panel(body.transform,"Wheel",new Vector2(0,9),new Vector2(3,8),false);wheel.color=new Color(.10f,.18f,.25f);
        Label(body.transform,"Caption",left?"좌":"우",10,new Vector2(0,-10),new Vector2(25,16),new Color(.08f,.14f,.20f));
    }

}
