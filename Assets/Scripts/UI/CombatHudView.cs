using UnityEngine;
using UnityEngine.UI;

// A presentation-only HUD. All values come from the existing combat systems.
public sealed class CombatHudView : MonoBehaviour
{
    PlayerHealth health; PlayerDash dash; PlayerWeapons weapons; PlayerGunCombat gun;
    GameObject root; Canvas canvas; Font font; Sprite rounded;
    Text hpText,timeText,killsText,ammoText,weaponText,stateText,dashText;
    Image hpFill,dashFill,actionFill; Image weaponIcon;
    readonly Sprite[] portraits=new Sprite[5];
    readonly Image[] shells=new Image[30];
    public GameObject Root=>root;
    public Text AmmoLabel=>ammoText;
    public Text StateLabel=>stateText;
    public int LitAmmoIndicators {get;private set;}
    public int VisibleAmmoIndicators {get;private set;}
    public Canvas HudCanvas=>canvas;
    static readonly string[] WeaponNames={"칼","도끼","샷건","기관총","레이저총"};
    static readonly Color Dark=new Color(.045f,.07f,.10f,.94f), Border=new Color(.32f,.34f,.36f,.40f);
    void Start()
    {
        health=GetComponent<PlayerHealth>();dash=GetComponent<PlayerDash>();weapons=GetComponent<PlayerWeapons>();gun=GetComponent<PlayerGunCombat>();
        font=Resources.Load<Font>("UI/AugmentKorean");rounded=MakeRounded();
        root=new GameObject("CombatHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=40;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        var hp=Panel("Health",new Vector2(1,1),new Vector2(-220,-42),new Vector2(400,48));
        Icon(hp,"Heart",CombatHudIcon.Kind.Heart,new Vector2(-174,0),new Vector2(30,30),new Color(.68f,.72f,.69f));
        Bar(hp,"HealthTrack",new Vector2(-35,0),new Vector2(230,9),out hpFill,new Color(.47f,.61f,.53f));
        hpText=Label(hp,"HealthValue","",21,new Vector2(143,0),new Vector2(110,35));
        var dp=Panel("Dash",new Vector2(1,1),new Vector2(-165,-94),new Vector2(290,35));
        dashText=Label(dp,"DashLabel","Space  대시",14,new Vector2(-91,0),new Vector2(100,30));
        Bar(dp,"DashTrack",new Vector2(48,0),new Vector2(173,5),out dashFill,new Color(.51f,.60f,.66f));
        var clock=Panel("Timer",new Vector2(.5f,1),new Vector2(0,-42),new Vector2(168,48));
        Icon(clock,"Clock",CombatHudIcon.Kind.Clock,new Vector2(-58,0),new Vector2(25,25),Color.white);
        timeText=Label(clock,"Time","00:00",26,new Vector2(18,0),new Vector2(110,42));
        var kills=Panel("Kills",new Vector2(0,1),new Vector2(100,-42),new Vector2(160,48));
        Icon(kills,"Skull",CombatHudIcon.Kind.Skull,new Vector2(-54,0),new Vector2(26,28),new Color(.77f,.79f,.78f));
        killsText=Label(kills,"Count","처치 0",23,new Vector2(18,0),new Vector2(106,38));
        var wp=Panel("Weapon",new Vector2(1,0),new Vector2(-192,MobileControls.Enabled?442:84),new Vector2(344,128));
        weaponIcon=Box(wp,"WeaponPortrait",new Vector2(-95,12),new Vector2(138,72),Color.white);
        weaponIcon.type=Image.Type.Simple;weaponIcon.preserveAspect=true;
        var portraitNames=new[]{"Sword","Axe","Shotgun","MachineGun","LaserGun"};
        for(int i=0;i<5;i++)portraits[i]=Resources.Load<Sprite>("UI/WeaponPortraits/"+portraitNames[i]);
        weaponText=Label(wp,"WeaponName","칼",16,new Vector2(-95,-38),new Vector2(130,30));
        Box(wp,"Divider",new Vector2(-18,0),new Vector2(1,98),new Color(.4f,.5f,.6f,.5f));
        ammoText=Label(wp,"Ammo","",33,new Vector2(73,28),new Vector2(166,46));
        stateText=Label(wp,"State","",14,new Vector2(78,-42),new Vector2(178,28));
        Bar(wp,"ActionProgress",new Vector2(80,-58),new Vector2(150,3),out actionFill,new Color(.65f,.69f,.71f));
        for(int i=0;i<shells.Length;i++)
        {
            shells[i]=Box(wp,"Round"+i,Vector2.zero,new Vector2(13,22),new Color(.95f,.2f,.17f));
            Box(shells[i].transform,"Cap",new Vector2(0,8),new Vector2(13,5),new Color(1,.75f,.24f));
        }
        // Retain the existing combo and timed-effect displays; remove superseded readouts.
        foreach(var old in FindObjectsByType<HealthBarUI>(FindObjectsSortMode.None))old.enabled=false;
        var oldHealth=GameObject.Find("HealthBarRoot");if(oldHealth!=null)oldHealth.SetActive(false);
        var run=FindFirstObjectByType<RunHud>();
        if(run!=null){if(run.timeText!=null)run.timeText.gameObject.SetActive(false);if(run.dashLabel!=null)run.dashLabel.gameObject.SetActive(false);}
        var oldDash=GameObject.Find("DashBar");if(oldDash!=null)oldDash.SetActive(false);
        var spawner=FindFirstObjectByType<EnemySpawner>();if(spawner!=null && spawner.killCountText!=null)spawner.killCountText.gameObject.SetActive(false);
        Refresh();
    }
    float nextRefresh;
    void LateUpdate(){if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.1f;Refresh();}
    public void Refresh()
    {
        if(root==null)return;
        var stats=RunStats.Instance;bool running=stats!=null && stats.Running;
        root.SetActive(running);if(!running)return;
        hpText.text=Mathf.CeilToInt(health.currentHealth)+" <size=14>/ "+Mathf.RoundToInt(health.maxHealth)+"</size>";
        SetFill(hpFill,health.currentHealth/Mathf.Max(1,health.maxHealth));hpFill.color=health.currentHealth/Mathf.Max(1,health.maxHealth)<.25f?new Color(.72f,.36f,.34f):new Color(.47f,.61f,.53f);
        SetFill(dashFill,dash.CooldownFraction);dashText.text=dash.IsReady?(MobileControls.Enabled?"대시":"Space  대시"):"대시 충전";
        timeText.text=RunStats.FormatTime(stats.SurvivalTime);killsText.text="<size=13>처치</size>  "+stats.Kills;
        var kind=weapons.Equipped;weaponIcon.sprite=portraits[(int)kind];weaponIcon.enabled=weaponIcon.sprite!=null;
        weaponText.text=WeaponNames[(int)kind];
        bool ranged=weapons.IsGun;VisibleAmmoIndicators=ranged?PlayerGunCombat.Capacity(kind):0;LitAmmoIndicators=ranged?gun.Ammo:0;
        ammoText.text=MapSandboxSettings.Enabled?"∞":ranged?gun.Ammo+" <size=22>/ "+gun.Reserve+"</size>":"∞";
        ammoText.color=ranged && gun.Ammo==0?new Color(.79f,.51f,.46f):Color.white;
        stateText.text=MapSandboxSettings.Enabled && ranged?"무한 탄약 · 재장전 없음":!ranged?(MobileControls.Enabled?"공격 버튼 길게 누르기":"좌클릭 공격 · 우클릭 강공격"):gun.IsReloading?"재장전 중":gun.IsCharging?"충전 중":gun.Ammo==0 && gun.Reserve==0?"탄약 없음 · 무기 교체":(MobileControls.Enabled?"재장전":"R  재장전");
        SetFill(actionFill,gun.IsReloading?gun.ReloadFraction:gun.IsCharging?gun.ChargeFraction:0);
        for(int i=0;i<shells.Length;i++)
        {
            bool visible=i<VisibleAmmoIndicators;shells[i].gameObject.SetActive(visible);if(!visible)continue;
            bool machine=kind==PlayerWeapons.Kind.MachineGun;
            var r=shells[i].rectTransform;r.anchoredPosition=machine?new Vector2(7+(i%15)*10,-7-(i/15)*12):new Vector2(50+i*24,-8);r.sizeDelta=machine?new Vector2(7,9):new Vector2(13,23);
            shells[i].color=i<gun.Ammo?(kind==PlayerWeapons.Kind.LaserGun?new Color(.50f,.66f,.67f):new Color(.61f,.49f,.45f)):new Color(.19f,.23f,.28f);
            var cap=shells[i].transform.GetChild(0).GetComponent<Image>();cap.gameObject.SetActive(!machine);cap.color=i<gun.Ammo?new Color(.73f,.68f,.53f):new Color(.28f,.31f,.36f);
        }
    }
    static void SetFill(Image image,float value){image.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(value),1);}
    RectTransform Panel(string name,Vector2 anchor,Vector2 at,Vector2 size)
    {
        var edge=Box(root.transform,name,at,size,Border);var r=edge.rectTransform;r.anchorMin=r.anchorMax=anchor;
        if(MobileControls.Enabled){r.localScale=Vector3.one*.82f;if(anchor.x==1)r.anchoredPosition+=Vector2.left*36;else if(anchor.x==0)r.anchoredPosition+=Vector2.right*36;}
        GameUiStyle.Apply(edge,Border);
        var inner=Box(r,"Inner",Vector2.zero,size-new Vector2(2,2),Dark);GameUiStyle.Apply(inner,GameUiStyle.Ink);GameUiStyle.Accent(r);return r;
    }
    Image Box(Transform parent,string name,Vector2 at,Vector2 size,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=at;r.sizeDelta=size;
        var im=go.GetComponent<Image>();im.sprite=rounded;im.type=Image.Type.Sliced;im.color=color;im.raycastTarget=false;return im;
    }
    void Bar(Transform parent,string name,Vector2 at,Vector2 size,out Image fill,Color color)
    {
        var track=Box(parent,name,at,size,new Color(.1f,.15f,.2f));fill=Box(track.transform,"Fill",Vector2.zero,size,color);
        fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;fill.rectTransform.sizeDelta=Vector2.zero;
    }
    Text Label(Transform parent,string name,string text,int fontSize,Vector2 at,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=at;r.sizeDelta=size;
        var t=go.GetComponent<Text>();t.font=font;t.fontSize=fontSize;t.fontStyle=FontStyle.Normal;t.verticalOverflow=VerticalWrapMode.Overflow;t.horizontalOverflow=HorizontalWrapMode.Overflow;t.color=new Color(.88f,.89f,.87f);t.alignment=TextAnchor.MiddleCenter;t.text=text;t.raycastTarget=false;return t;
    }
    CombatHudIcon Icon(Transform parent,string name,CombatHudIcon.Kind kind,Vector2 at,Vector2 size,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(CombatHudIcon));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=at;r.sizeDelta=size;
        var icon=go.GetComponent<CombatHudIcon>();icon.color=color;icon.raycastTarget=false;icon.SetKind(kind);return icon;
    }
    static Sprite MakeRounded()
    {
        const int s=64;var t=new Texture2D(s,s,TextureFormat.RGBA32,false);t.filterMode=FilterMode.Bilinear;
        var pixels=new Color32[s*s];for(int y=0;y<s;y++)for(int x=0;x<s;x++){float dx=x-Mathf.Clamp(x,14,s-15),dy=y-Mathf.Clamp(y,14,s-15);pixels[y*s+x]=new Color(1,1,1,Mathf.Clamp01(14-Mathf.Sqrt(dx*dx+dy*dy)));}t.SetPixels32(pixels);t.Apply();
        return Sprite.Create(t,new Rect(0,0,s,s),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));
    }
    void OnDestroy(){if(root!=null)Destroy(root);if(rounded!=null){Destroy(rounded.texture);Destroy(rounded);}}
}
