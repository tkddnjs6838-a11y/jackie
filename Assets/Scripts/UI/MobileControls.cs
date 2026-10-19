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
            // A saved touch preview must never override desktop keyboard/mouse input.
            var target = UnityEditor.EditorUserBuildSettings.activeBuildTarget;
            bool mobileTarget = target == UnityEditor.BuildTarget.Android || target == UnityEditor.BuildTarget.iOS;
            return mobileTarget && UnityEditor.EditorPrefs.GetBool("Wakuball.MobilePreview",false);
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
    GameObject portalButton;
    MapSandbox map;
    float frameSeconds,worstFrame;int frameCount;
    Text dashLabel;TouchControl[] controls;
    static Sprite disc;
    static Sprite Disc
    {
        get
        {
            if(disc!=null)return disc;
            var t=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color32[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(31.5f-Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))));
            t.SetPixels32(pixels);t.Apply();disc=Sprite.Create(t,new Rect(0,0,64,64),new Vector2(.5f,.5f));return disc;
        }
    }
    Rect lastSafe;int lastWidth,lastHeight;
    void Awake(){Instance=this;if(Application.isMobilePlatform)Screen.orientation=ScreenOrientation.LandscapeLeft; if(Application.isMobilePlatform)Application.targetFrameRate=60;}
    void Start()
    {
        weapons=GetComponent<PlayerWeapons>();map=FindFirstObjectByType<MapSandbox>();
        root=new GameObject("MobileControls",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=60;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=1;
        safe=new GameObject("SafeArea",typeof(RectTransform)).GetComponent<RectTransform>();safe.SetParent(root.transform,false);
        var lookPad=Control("Look",new Vector2(.5f,.5f),Vector2.zero,Vector2.zero,"");
        var lr=lookPad.GetComponent<RectTransform>();lr.anchorMin=new Vector2(.4f,0);lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;
        lookPad.GetComponent<Image>().color=Color.clear;
        lookPad.OnDragValue=d=>look+=d*180f/Mathf.Max(1,Screen.height);
        var stick=Control("Move",Vector2.zero,new Vector2(145,145),new Vector2(164,164),"");
        var knob=new GameObject("StickKnob",typeof(RectTransform),typeof(Image));knob.transform.SetParent(stick.transform,false);
        var knobRect=knob.GetComponent<RectTransform>();knobRect.sizeDelta=new Vector2(60,60);
        var knobImage=knob.GetComponent<Image>();knobImage.sprite=Disc;knobImage.color=new Color(.82f,.95f,1f,.95f);knob.AddComponent<UiSurfaceGradient>();var knobEdge=knob.AddComponent<Outline>();knobEdge.effectColor=new Color(.9f,1,1,.9f);knobEdge.effectDistance=new Vector2(2,-2);knobImage.raycastTarget=false;
        stick.Stick=true;stick.OnStick=v=>{move=v;if(knobRect!=null)knobRect.anchoredPosition=v*60;};
        var attack=Control("Attack",new Vector2(1,0),new Vector2(-120,145),new Vector2(126,126),"공격");
        attack.OnHeld=v=>fire=v;attack.OnDragValue=d=>look+=d*180f/Mathf.Max(1,Screen.height);
        var dodge=Control("Dash",new Vector2(1,0),new Vector2(-268,105),new Vector2(78,78),"대시");dodge.OnPress=()=>dash=true;dashLabel=dodge.GetComponentInChildren<Text>();
        Control("Reload",new Vector2(1,0),new Vector2(-113,302),new Vector2(70,70),"재장전").OnPress=()=>GetComponent<PlayerGunCombat>()?.BeginReload();
        Control("Switch",new Vector2(1,0),new Vector2(-246,245),new Vector2(70,70),"교체").OnPress=CycleWeapon;
        var portal=Control("Portal",new Vector2(.5f,0),new Vector2(0,65),new Vector2(110,46),"포탈 이동");portal.OnPress=()=>interact=true;portalButton=portal.gameObject;
        controls=root.GetComponentsInChildren<TouchControl>(true);
        ApplySafeArea();root.SetActive(false);
    }
    TouchControl Control(string name,Vector2 anchor,Vector2 position,Vector2 size,string label)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(TouchControl));go.transform.SetParent(safe,false);
        var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=anchor;r.anchoredPosition=position;r.sizeDelta=size;
        var background=go.GetComponent<Image>();
        GameUiStyle.Apply(background,new Color(.055f,.07f,.085f,.36f));
        if(name!="Look" && name!="Portal") {background.sprite=Disc;background.type=Image.Type.Simple;}
        if(name!="Look" && name!="Move" && name!="Portal")
        {
            var icon=new GameObject("Glyph",typeof(RectTransform),typeof(CanvasRenderer),typeof(TouchGlyph));icon.transform.SetParent(go.transform,false);
            icon.GetComponent<RectTransform>().sizeDelta=size*.46f;
            var glyph=icon.GetComponent<TouchGlyph>();glyph.Symbol=name;glyph.color=new Color(.90f,.93f,.94f,.9f);glyph.raycastTarget=false;
        }
        if(name=="Move")
        {
            background.color=new Color(.18f,.47f,.55f,.24f);
            var ring=new GameObject("JoystickGlow",typeof(RectTransform),typeof(Image));ring.transform.SetParent(go.transform,false);
            ring.GetComponent<RectTransform>().sizeDelta=size+new Vector2(30,30);
            var ri=ring.GetComponent<Image>();ri.sprite=GameUiStyle.RingGlow;ri.color=new Color(.65f,1,1,.9f);ri.raycastTarget=false;
        }
        if(name!="Look" && name!="Move")
        {
            background.color=name=="Attack"?GameUiStyle.Gold:name=="Dash"?GameUiStyle.Blue:new Color(.12f,.27f,.45f,.9f);
            background.gameObject.AddComponent<UiSurfaceGradient>();
            var edge=go.AddComponent<Outline>();edge.effectColor=name=="Attack"?new Color(1,.88f,.4f,.8f):new Color(.42f,.88f,1f,.65f);edge.effectDistance=new Vector2(2,-2);
            var depth=go.AddComponent<Shadow>();depth.effectColor=new Color(.01f,.03f,.08f,.55f);depth.effectDistance=new Vector2(0,-5);
            go.AddComponent<UiPressFeedback>();
            var halo=new GameObject("Halo",typeof(RectTransform),typeof(Image));halo.transform.SetParent(go.transform,false);
            var hr=halo.GetComponent<RectTransform>();hr.sizeDelta=size+new Vector2(26,26);
            var hi=halo.GetComponent<Image>();hi.sprite=name=="Portal"?GameUiStyle.Glow:GameUiStyle.RingGlow;hi.type=name=="Portal"?Image.Type.Sliced:Image.Type.Simple;hi.color=name=="Attack"?new Color(1,.72f,.15f,.9f):new Color(.15f,.85f,1f,.7f);hi.raycastTarget=false;halo.transform.SetAsFirstSibling();
            if(name=="Attack")
            {
                var glyph=go.transform.Find("Glyph");if(glyph!=null)glyph.gameObject.SetActive(false);
                var art=new GameObject("SwordArt",typeof(RectTransform),typeof(Image));art.transform.SetParent(go.transform,false);
                art.GetComponent<RectTransform>().sizeDelta=size*.80f;
                var ai=art.GetComponent<Image>();ai.sprite=GameUiArt.Icon(CharacterGrowth.Kind.Power);ai.preserveAspect=true;ai.raycastTarget=false;
            }
        }
        var textGo=new GameObject("Label",typeof(RectTransform),typeof(Text));textGo.transform.SetParent(go.transform,false);
        var tr=textGo.GetComponent<RectTransform>();tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.sizeDelta=Vector2.zero;
        if(name!="Portal" && name!="Look" && name!="Move"){tr.anchorMin=tr.anchorMax=new Vector2(.5f,0);tr.anchoredPosition=new Vector2(0,-17);tr.sizeDelta=new Vector2(110,30);}
        var text=textGo.GetComponent<Text>();text.font=Resources.Load<Font>("UI/AugmentKorean");text.fontSize=21;text.verticalOverflow=VerticalWrapMode.Overflow;text.horizontalOverflow=HorizontalWrapMode.Overflow;text.text=label;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(.88f,.90f,.89f);text.raycastTarget=false;
        return go.GetComponent<TouchControl>();
    }
    void Update()
    {
        if(root==null)return;
        bool active=Enabled && Time.timeScale>0 && RunStats.Instance!=null && RunStats.Instance.Running;
        if(!active)Clear();root.SetActive(active);
        if(!active)return;
        portalButton.SetActive(map!=null && map.CanInteractWithPortal);
        if(Application.isMobilePlatform)
        {
            frameSeconds+=Time.unscaledDeltaTime;frameCount++;worstFrame=Mathf.Max(worstFrame,Time.unscaledDeltaTime);
            if(frameSeconds>=10){Debug.Log($"[MobileFrames] avg={frameCount/frameSeconds:F1} fps worst={worstFrame*1000:F1}ms");frameSeconds=worstFrame=0;frameCount=0;}
        }
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
