using UnityEngine;
using UnityEngine.UI;

public sealed class RunResultView : MonoBehaviour
{
    Text timeValue,killsValue,comboValue;
    static readonly Color Paper=new Color(.88f,.87f,.82f);
    static readonly Color Muted=new Color(.53f,.56f,.57f);
    public static Button Build(GameObject panel,bool victory)
    {
        if(panel==null)return null;
        foreach(Transform child in panel.transform)child.gameObject.SetActive(false);
        var shade=panel.GetComponent<Image>();if(shade!=null){shade.sprite=null;shade.color=new Color(.025f,.03f,.035f,.88f);}
        var view=panel.AddComponent<RunResultView>();
        var card=Rect(panel.transform,"ResultCard",Vector2.zero,new Vector2(620,420));
        var surface=card.gameObject.AddComponent<Image>();GameUiStyle.Apply(surface,new Color(.09f,.10f,.11f,.97f));
        var font=Resources.Load<Font>("UI/AugmentKorean");
        Label(card,font,"Caption",victory?"05 / 05     완주 기록":"이번 도전의 기록",13,new Vector2(0,162),new Vector2(520,25),Muted);
        Label(card,font,"Title",victory?"모든 스테이지 클리어":"도전 종료",34,new Vector2(0,110),new Vector2(520,55),Paper);
        Label(card,font,"Subtitle",victory?"초원부터 화산까지, 끝까지 살아남았습니다.":"획득한 경험으로 다시 도전해 보세요.",15,new Vector2(0,64),new Vector2(520,30),Muted);
        var line=Rect(card,"Divider",new Vector2(0,28),new Vector2(520,1));line.gameObject.AddComponent<Image>().color=new Color(.24f,.26f,.27f);
        var labels=new[]{"플레이 시간","몬스터 처치","최고 콤보"};var values=new Text[3];
        for(int i=0;i<3;i++)
        {
            float x=(i-1)*180;
            Label(card,font,"MetricLabel"+i,labels[i],13,new Vector2(x,-10),new Vector2(160,24),Muted,TextAnchor.MiddleCenter);
            values[i]=Label(card,font,"MetricValue"+i,"—",30,new Vector2(x,-49),new Vector2(160,50),Paper,TextAnchor.MiddleCenter);
        }
        view.timeValue=values[0];view.killsValue=values[1];view.comboValue=values[2];
        var buttonRect=Rect(card,"Retry",new Vector2(160,-145),new Vector2(200,48));
        GameUiStyle.Apply(buttonRect.gameObject.AddComponent<Image>(),new Color(.76f,.77f,.73f));
        var button=buttonRect.gameObject.AddComponent<Button>();
        var colors=button.colors;colors.highlightedColor=new Color(1,1,1);colors.pressedColor=new Color(.72f,.75f,.74f);button.colors=colors;
        Label(buttonRect,font,"Label","다시 도전",17,Vector2.zero,new Vector2(180,40),new Color(.12f,.14f,.15f),TextAnchor.MiddleCenter);
        Label(card,font,"Footer",victory?"다음 도전은 새로운 조합으로":"처음부터 다시 시작합니다",12,new Vector2(-105,-145),new Vector2(310,35),Muted);
        return button;
    }
    void OnEnable()
    {
        if(timeValue==null || RunStats.Instance==null)return;
        var stats=RunStats.Instance;timeValue.text=RunStats.FormatTime(stats.SurvivalTime);killsValue.text=stats.Kills.ToString();comboValue.text="×"+stats.BestComboThisRun;
    }
    static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;return r;
    }
    static Text Label(Transform parent,Font font,string name,string content,int size,Vector2 position,Vector2 bounds,Color color,TextAnchor align=TextAnchor.MiddleLeft)
    {
        var r=Rect(parent,name,position,bounds);var text=r.gameObject.AddComponent<Text>();text.font=font;text.text=content;text.fontSize=size;text.color=color;text.alignment=align;text.raycastTarget=false;return text;
    }
}
