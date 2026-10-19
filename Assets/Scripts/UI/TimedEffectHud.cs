using UnityEngine;
using UnityEngine.UI;

// Read-only presentation of the existing single active timed effect.
public sealed class TimedEffectHud:MonoBehaviour
{
    PowerUpEffects fx;GameObject root,card;Text title,detail,seconds;Image bar;int version=-1;float noticeUntil,endUntil;bool wasActive;string notice;
    void Start()
    {
        fx=GetComponent<PowerUpEffects>();
        root=new GameObject("TimedEffectHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=41;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        var panel=ToyMenuLayout.Panel(root.transform,"ActiveEffect",Vector2.zero,new Vector2(360,98));card=panel.gameObject;
        var r=panel.rectTransform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.anchoredPosition=new Vector2(194,-220);r.gameObject.AddComponent<UiSafePanel>();
        title=ToyMenuLayout.Label(r,"EffectName","",18,new Vector2(-40,26),new Vector2(250,30),Color.white);
        detail=ToyMenuLayout.Label(r,"EffectDescription","",15,new Vector2(-20,-1),new Vector2(300,26),Color.white);
        seconds=ToyMenuLayout.Label(r,"TimeLeft","",18,new Vector2(142,25),new Vector2(66,30),Color.white);
        var track=ToyMenuLayout.Panel(r,"DurationTrack",new Vector2(0,-29),new Vector2(324,7),false).rectTransform;
        var fill=new GameObject("Remaining",typeof(RectTransform),typeof(Image));fill.transform.SetParent(track,false);bar=fill.GetComponent<Image>();bar.raycastTarget=false;
        var br=bar.rectTransform;br.anchorMin=Vector2.zero;br.anchorMax=Vector2.one;br.offsetMin=br.offsetMax=Vector2.zero;card.SetActive(false);
    }
    void Update()
    {
        if(fx==null||card==null)return;
        bool running=RunStats.Instance!=null&&RunStats.Instance.Running;
        if(!running){card.SetActive(false);wasActive=false;return;}
        if(fx.Active)
        {
            if(version!=fx.ActivationVersion){notice=wasActive?"효과 교체 · ":"획득! · ";version=fx.ActivationVersion;noticeUntil=Time.time+1.2f;}
            wasActive=true;card.SetActive(Time.timeScale>0);
            var color=PowerUpEffects.ColorOf(fx.ActiveKind);title.color=bar.color=color;
            title.text=(Time.time<noticeUntil?notice:"")+PowerUpEffects.DisplayName(fx.ActiveKind);
            detail.text=fx.Description(fx.ActiveKind);seconds.text=fx.Remaining.ToString("0.0")+"초";
            bar.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(fx.Remaining/Mathf.Max(.01f,fx.DurationOf(fx.ActiveKind))),1);
        }
        else
        {
            if(wasActive){endUntil=Time.time+1.2f;wasActive=false;title.text="특수효과 종료";detail.text="기본 상태로 돌아왔습니다";seconds.text="";bar.rectTransform.anchorMax=new Vector2(0,1);}
            card.SetActive(Time.time<endUntil && Time.timeScale>0);
        }
    }
    void OnDisable(){if(card!=null)card.SetActive(false);}
    void OnDestroy(){if(root!=null)Destroy(root);}
}
