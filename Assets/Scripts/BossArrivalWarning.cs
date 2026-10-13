using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

// Presentation only: EnemySpawner owns the countdown and the actual spawn.
public sealed class BossArrivalWarning : MonoBehaviour
{
    GameObject hud;
    Text label;
    LineRenderer ring;
    Material ringMaterial;
    public static BossArrivalWarning Create(Vector3 position,float radius)
    {
        var root=new GameObject("BossArrivalWarning");root.transform.position=position;
        var view=root.AddComponent<BossArrivalWarning>();
        view.ring=root.AddComponent<LineRenderer>();view.ringMaterial=new Material(Resources.Load<Material>("AdditiveFX"));
        view.ringMaterial.SetFloat("_Blend",0);
        view.ringMaterial.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        view.ring.sharedMaterial=view.ringMaterial;
        view.ring.useWorldSpace=true;view.ring.loop=true;view.ring.positionCount=64;view.ring.widthMultiplier=.12f;
        view.ring.shadowCastingMode=ShadowCastingMode.Off;view.ring.receiveShadows=false;
        for(int i=0;i<64;i++)
        {
            float angle=i*Mathf.PI*2/64;var point=position+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
            if(Terrain.activeTerrain!=null)point.y=Terrain.activeTerrain.SampleHeight(point)+Terrain.activeTerrain.transform.position.y;
            point.y+=.08f;view.ring.SetPosition(i,point);
        }
        view.hud=new GameObject("BossArrivalHUD",typeof(Canvas),typeof(CanvasScaler));
        var canvas=view.hud.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=85;
        var scaler=view.hud.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        var banner=new GameObject("Warning",typeof(RectTransform),typeof(Image));banner.transform.SetParent(view.hud.transform,false);
        var rect=banner.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-80);rect.sizeDelta=new Vector2(600,90);
        banner.GetComponent<Image>().color=new Color(.1f,.015f,.01f,.9f);banner.GetComponent<Image>().raycastTarget=false;
        var text=new GameObject("Label",typeof(RectTransform),typeof(Text));text.transform.SetParent(banner.transform,false);
        var textRect=text.GetComponent<RectTransform>();textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;textRect.offsetMin=new Vector2(8,4);textRect.offsetMax=new Vector2(-8,-4);
        view.label=text.GetComponent<Text>();view.label.font=Resources.Load<Font>("UI/AugmentKorean");view.label.fontSize=26;view.label.alignment=TextAnchor.MiddleCenter;view.label.color=new Color(1,.55f,.25f);view.label.raycastTarget=false;
        return view;
    }
    public void SetRemaining(float seconds)
    {
        label.text=$"보스 등장까지 {Mathf.CeilToInt(seconds)}초\n붉은 원에서 떨어지세요";
        float glow=.65f+.35f*Mathf.Sin(seconds*10);ringMaterial.SetColor("_BaseColor",new Color(glow,.04f,.015f,1));
    }
    public void Dismiss(){if(hud!=null)hud.SetActive(false);gameObject.SetActive(false);Destroy(gameObject);}
    void OnDestroy(){if(hud!=null)Destroy(hud);if(ringMaterial!=null)Destroy(ringMaterial);}
}
