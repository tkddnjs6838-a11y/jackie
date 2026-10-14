using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Touch events own their pointer IDs: moving, aiming and firing can happen together.
[DefaultExecutionOrder(-100)]
public sealed class MobileControls : MonoBehaviour
{
    public static MobileControls Instance { get; private set; }
    public static bool Enabled
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool("Wakuball.MobilePreview",false);
#else
            return Application.isMobilePlatform;
#endif
        }
    }
    public static Vector2 Move => Instance!=null ? Instance.move : Vector2.zero;
    public static bool Firing => Instance!=null && Instance.fire;
    public static Vector2 TakeLook(){if(Instance==null)return Vector2.zero;var d=Instance.look;Instance.look=Vector2.zero;return d;}
    public static bool TakeDash(){if(Instance==null)return false;bool value=Instance.dash;Instance.dash=false;return value;}
    public static bool TakeInteract(){if(Instance==null)return false;bool value=Instance.interact;Instance.interact=false;return value;}
    Vector2 move,look;
    bool fire,dash,interact;
    GameObject root;RectTransform safe;
    PlayerWeapons weapons;
    Text dashLabel;TouchControl[] controls;
    Rect lastSafe;int lastWidth,lastHeight;
    void Awake(){Instance=this;if(Application.isMobilePlatform)Screen.orientation=ScreenOrientation.LandscapeLeft;}
    void Start()
    {
        weapons=GetComponent<PlayerWeapons>();
        root=new GameObject("MobileControls",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=60;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=1;
        safe=new GameObject("SafeArea",typeof(RectTransform)).GetComponent<RectTransform>();safe.SetParent(root.transform,false);
        var lookPad=Control("Look",new Vector2(.5f,.5f),Vector2.zero,Vector2.zero,"");
        var lr=lookPad.GetComponent<RectTransform>();lr.anchorMin=new Vector2(.4f,0);lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;
        lookPad.GetComponent<Image>().color=Color.clear;
        lookPad.OnDragValue=d=>look+=d*180f/Mathf.Max(1,Screen.height);
        var stick=Control("Move",Vector2.zero,new Vector2(145,145),new Vector2(180,180),"이동");
        var knob=new GameObject("StickKnob",typeof(RectTransform),typeof(Image));knob.transform.SetParent(stick.transform,false);
        var knobRect=knob.GetComponent<RectTransform>();knobRect.sizeDelta=new Vector2(60,60);
        var knobImage=knob.GetComponent<Image>();GameUiStyle.Apply(knobImage,new Color(.7f,.75f,.75f,.3f));knobImage.raycastTarget=false;
        stick.Stick=true;stick.OnStick=v=>{move=v;if(knobRect!=null)knobRect.anchoredPosition=v*60;};
        var attack=Control("Attack",new Vector2(1,0),new Vector2(-135,140),new Vector2(128,128),"공격");
        attack.OnHeld=v=>fire=v;attack.OnDragValue=d=>look+=d*180f/Mathf.Max(1,Screen.height);
        var dodge=Control("Dash",new Vector2(1,0),new Vector2(-295,100),new Vector2(100,82),"대시");dodge.OnPress=()=>dash=true;dashLabel=dodge.GetComponentInChildren<Text>();
        Control("Reload",new Vector2(1,0),new Vector2(-130,285),new Vector2(110,65),"재장전").OnPress=()=>GetComponent<PlayerGunCombat>()?.BeginReload();
        Control("Switch",new Vector2(1,0),new Vector2(-275,225),new Vector2(110,65),"무기 변경").OnPress=CycleWeapon;
        Control("Portal",new Vector2(.5f,0),new Vector2(0,70),new Vector2(130,58),"포탈 이동").OnPress=()=>interact=true;
        controls=root.GetComponentsInChildren<TouchControl>(true);
        ApplySafeArea();root.SetActive(false);
    }
    TouchControl Control(string name,Vector2 anchor,Vector2 position,Vector2 size,string label)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(TouchControl));go.transform.SetParent(safe,false);
        var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=anchor;r.anchoredPosition=position;r.sizeDelta=size;
        GameUiStyle.Apply(go.GetComponent<Image>(),new Color(.10f,.12f,.14f,.60f));
        var textGo=new GameObject("Label",typeof(RectTransform),typeof(Text));textGo.transform.SetParent(go.transform,false);
        var tr=textGo.GetComponent<RectTransform>();tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.sizeDelta=Vector2.zero;
        var text=textGo.GetComponent<Text>();text.font=Resources.Load<Font>("UI/AugmentKorean");text.fontSize=20;text.text=label;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(.88f,.90f,.89f);text.raycastTarget=false;
        return go.GetComponent<TouchControl>();
    }
    void Update()
    {
        if(root==null)return;
        bool active=Enabled && Time.timeScale>0 && RunStats.Instance!=null && RunStats.Instance.Running;
        if(!active)Clear();root.SetActive(active);
        if(!active)return;
        if(Screen.safeArea!=lastSafe || Screen.width!=lastWidth || Screen.height!=lastHeight)ApplySafeArea();
        var playerDash=GetComponent<PlayerDash>();dashLabel.text=playerDash.IsReady?"대시":Mathf.CeilToInt((1-playerDash.CooldownFraction)*playerDash.cooldown).ToString();
    }
    void ApplySafeArea()
    {
        lastSafe=Screen.safeArea;lastWidth=Screen.width;lastHeight=Screen.height;
        safe.anchorMin=lastSafe.position/new Vector2(Mathf.Max(1,lastWidth),Mathf.Max(1,lastHeight));
        safe.anchorMax=(lastSafe.position+lastSafe.size)/new Vector2(Mathf.Max(1,lastWidth),Mathf.Max(1,lastHeight));safe.offsetMin=safe.offsetMax=Vector2.zero;
    }
    void CycleWeapon()
    {
        if(weapons==null)weapons=GetComponent<PlayerWeapons>();
        if(weapons==null)return;
        for(int step=1;step<5;step++){var kind=(PlayerWeapons.Kind)(((int)weapons.Equipped+step)%5);if(weapons.Owns(kind) && weapons.TryEquip(kind))return;}
    }
    void Clear(){move=look=Vector2.zero;fire=dash=interact=false;if(controls!=null)foreach(var c in controls)if(c!=null)c.Release();}
    void OnApplicationFocus(bool focus){if(!focus)Clear();}
    void OnApplicationPause(bool paused){if(paused)Clear();}
    void OnDisable(){Clear();if(root!=null)root.SetActive(false);}
    void OnDestroy(){if(Instance==this)Instance=null;if(root!=null)Destroy(root);}
}
