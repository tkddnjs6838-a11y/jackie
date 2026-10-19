using UnityEngine;
using UnityEngine.UI;

// A presentation-only HUD. All values come from the existing combat systems.
public sealed class CombatHudView : MonoBehaviour
{
    PlayerHealth health; PlayerDash dash; PlayerWeapons weapons; PlayerGunCombat gun;
    GameObject root; Canvas canvas; Font font; Sprite rounded;
    Text hpText,timeText,killsText,ammoText,weaponText,stateText,dashText,weaponTypeText;
    Image hpFill,dashFill,actionFill; Image weaponIcon;
    Text pickupNotice;
    readonly Image[] slotFaces=new Image[5],slotRims=new Image[5],slotIcons=new Image[5];
    readonly Text[] slotNumbers=new Text[5],slotNames=new Text[5]; Image noticeBackground; AmmoFullNotice ammoNotice;
    readonly Sprite[] portraits=new Sprite[5];
    readonly Sprite[] croppedGunPortraits=new Sprite[2];
    readonly Image[] shells=new Image[30];
    public GameObject Root=>root;
    public Text AmmoLabel=>ammoText;
    public Text StateLabel=>stateText;
    public int LitAmmoIndicators {get;private set;}
    public int VisibleAmmoIndicators {get;private set;}
    public Canvas HudCanvas=>canvas;
    static readonly string[] WeaponNames={"칼","도끼","샷건","기관총","레이저총"};
    static readonly Color Dark=new Color(.045f,.07f,.10f,.94f), Border=new Color(.25f,.72f,1f,.65f);
    void Start()
    {
        health=GetComponent<PlayerHealth>();dash=GetComponent<PlayerDash>();weapons=GetComponent<PlayerWeapons>();gun=GetComponent<PlayerGunCombat>();
        font=Resources.Load<Font>("UI/AugmentKorean");rounded=MakeRounded();
        root=new GameObject("CombatHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=40;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        var hp=Panel("Health",new Vector2(1,1),new Vector2(-220,-42),new Vector2(400,48));
        var heart=Box(hp,"Heart",new Vector2(-174,0),new Vector2(45,42),Color.white);heart.sprite=GameUiArt.Icon(CharacterGrowth.Kind.Vitality);heart.type=Image.Type.Simple;heart.preserveAspect=true;
        Bar(hp,"HealthTrack",new Vector2(-35,0),new Vector2(230,18),out hpFill,GameUiStyle.Pink);
        hpText=Label(hp,"HealthValue","",21,new Vector2(143,0),new Vector2(110,35));
        var dp=Panel("Dash",new Vector2(1,1),new Vector2(-165,-94),new Vector2(290,35));
        dashText=Label(dp,"DashLabel","Space  대시",14,new Vector2(-91,0),new Vector2(100,30));
        Bar(dp,"DashTrack",new Vector2(48,0),new Vector2(173,10),out dashFill,GameUiStyle.Blue);
        var clock=Panel("Timer",new Vector2(.5f,1),new Vector2(0,-42),new Vector2(168,48));
        Icon(clock,"Clock",CombatHudIcon.Kind.Clock,new Vector2(-58,0),new Vector2(25,25),Color.white);
        timeText=Label(clock,"Time","00:00",26,new Vector2(18,0),new Vector2(110,42));
        var kills=Panel("Kills",new Vector2(0,1),new Vector2(100,-42),new Vector2(160,48));
        Icon(kills,"Skull",CombatHudIcon.Kind.Skull,new Vector2(-54,0),new Vector2(26,28),new Color(.77f,.79f,.78f));
        killsText=Label(kills,"Count","처치 0",23,new Vector2(18,0),new Vector2(106,38));
        if(FindFirstObjectByType<MapSandbox>()!=null && !MapSandboxSettings.Enabled)kills.gameObject.SetActive(false);
        var weaponPanel=Box(root.transform,"Weapon",new Vector2(MobileControls.Enabled?-450:-168,158),new Vector2(274,96),Color.white);
        var wp=weaponPanel.rectTransform;wp.anchorMin=wp.anchorMax=new Vector2(1,0);
        if(MobileControls.Enabled){wp.localScale=Vector3.one*.82f;wp.anchoredPosition+=Vector2.left*36;}
        wp.gameObject.AddComponent<UiSafePanel>();ToyHudStyle.Frame(weaponPanel);
        noticeBackground=Box(wp,"PickupNotice",new Vector2(-8,84),new Vector2(290,30),new Color(.035f,.05f,.075f,.94f));
        pickupNotice=Label(noticeBackground.transform,"Message","탄약 가득참 · 더 주울 수 없어요",13,Vector2.zero,new Vector2(282,28));
        pickupNotice.color=new Color(1f,.88f,.5f);noticeBackground.gameObject.SetActive(false);
        var badge=Box(wp,"WeaponBadge",new Vector2(-98,7),new Vector2(104,104),Color.white);ToyHudStyle.Frame(badge,true);
        weaponIcon=Box(wp,"WeaponPortrait",new Vector2(-98,7),new Vector2(98,98),Color.white);
        weaponIcon.type=Image.Type.Simple;weaponIcon.preserveAspect=true;
        var portraitNames=new[]{"Sword","Axe","Shotgun","MachineGun","LaserGun"};
        for(int i=0;i<5;i++)
        {
            // Slots 4 and 5 use portraits rendered from the actual gun prefabs.
            portraits[i]=i>=3?Resources.Load<Sprite>("UI/WeaponPortraits/"+portraitNames[i]):ToyHudStyle.Weapon(i);
            if(portraits[i]==null)portraits[i]=Resources.Load<Sprite>("UI/WeaponPortraits/"+portraitNames[i]);
            if(i>=3 && portraits[i]!=null)
            {
                var texture=portraits[i].texture;
                var crop=i==3?new Rect(118,35,292,185):new Rect(134,48,254,157);
                croppedGunPortraits[i-3]=Sprite.Create(texture,crop,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                croppedGunPortraits[i-3].name=portraitNames[i]+"_GameModel";
                portraits[i]=croppedGunPortraits[i-3];
            }
            var tile=Box(wp,"WeaponSlot"+(i+1),new Vector2(-112+i*56,-90),new Vector2(50,56),new Color(.06f,.12f,.20f));
            var shadow=tile.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.45f);shadow.effectDistance=new Vector2(0,-3);
            slotRims[i]=tile;
            slotFaces[i]=Box(tile.transform,"Face",new Vector2(0,1),new Vector2(44,49),new Color(.08f,.16f,.27f));
            slotFaces[i].gameObject.AddComponent<UiSurfaceGradient>();
            slotIcons[i]=Box(tile.transform,"Icon",new Vector2(0,6),new Vector2(i>=3?49:43,40),Color.white);
            slotIcons[i].sprite=portraits[i];slotIcons[i].type=Image.Type.Simple;slotIcons[i].preserveAspect=true;
            slotNumbers[i]=Label(tile.transform,"Key",(i+1).ToString(),14,new Vector2(0,-20),new Vector2(24,20));slotNumbers[i].fontStyle=FontStyle.Bold;
            slotNames[i]=Label(tile.transform,"Name",WeaponNames[i],11,new Vector2(0,-40),new Vector2(56,19));
        }
        weaponText=Label(wp,"WeaponName","칼",18,new Vector2(7,25),new Vector2(100,28));weaponText.color=Color.white;weaponText.alignment=TextAnchor.MiddleLeft;weaponText.fontStyle=FontStyle.Bold;
        weaponTypeText=Label(wp,"WeaponType","근접 무기",12,new Vector2(21,1),new Vector2(128,20));weaponTypeText.alignment=TextAnchor.MiddleLeft;weaponTypeText.color=new Color(.78f,.87f,.97f);
        ammoText=Label(wp,"Ammo","",24,new Vector2(83,24),new Vector2(84,30));ammoText.alignment=TextAnchor.MiddleRight;
        stateText=Label(wp,"State","",11,new Vector2(41,-32),new Vector2(174,20));stateText.color=new Color(.78f,.87f,.97f);
        stateText.resizeTextForBestFit=true;stateText.resizeTextMinSize=9;stateText.resizeTextMaxSize=11;stateText.horizontalOverflow=HorizontalWrapMode.Wrap;
        Bar(wp,"ActionProgress",new Vector2(36,-15),new Vector2(172,3),out actionFill,GameUiStyle.Blue);actionFill.transform.parent.gameObject.SetActive(false);
        for(int i=0;i<shells.Length;i++)
        {
            shells[i]=Box(wp,"Round"+i,Vector2.zero,new Vector2(13,22),new Color(.95f,.2f,.17f));
            Box(shells[i].transform,"Cap",new Vector2(0,5),new Vector2(11,4),new Color(1,.75f,.24f));
        }
        // Retain the existing combo and timed-effect displays; remove superseded readouts.
        foreach(var old in FindObjectsByType<HealthBarUI>(FindObjectsSortMode.None))old.enabled=false;
        var oldHealth=GameObject.Find("HealthBarRoot");if(oldHealth!=null)oldHealth.SetActive(false);
        var run=FindFirstObjectByType<RunHud>();
        if(run!=null){if(run.timeText!=null)run.timeText.gameObject.SetActive(false);if(run.dashLabel!=null)run.dashLabel.gameObject.SetActive(false);}
        var oldDash=GameObject.Find("DashBar");if(oldDash!=null)oldDash.SetActive(false);
        var spawner=FindFirstObjectByType<EnemySpawner>();if(spawner!=null && spawner.killCountText!=null)spawner.killCountText.gameObject.SetActive(false);
        if(!MobileControls.Enabled)
        {
            hp.localScale=Vector3.one*.84f;hp.anchoredPosition=new Vector2(-188,-35);
            dp.localScale=Vector3.one*.88f;dp.anchoredPosition=new Vector2(-148,-77);
            clock.localScale=Vector3.one*.88f;clock.anchoredPosition=new Vector2(0,-35);
            wp.localScale=Vector3.one*.84f;wp.anchoredPosition=new Vector2(-143,134);
        }
        Refresh();
    }
    float nextRefresh,displayedHealth=-1;
    void LateUpdate()
    {
        if(hpFill!=null && root.activeSelf)
        {
            float target=Mathf.Clamp01(health.currentHealth/Mathf.Max(1,health.maxHealth));
            displayedHealth=displayedHealth<0?target:Mathf.MoveTowards(displayedHealth,target,Time.unscaledDeltaTime*1.8f);
            SetFill(hpFill,displayedHealth);
            SetFill(dashFill,dash.CooldownFraction);
            dashFill.color=dash.IsReady?Color.Lerp(GameUiStyle.Blue,Color.white,.16f+.10f*Mathf.Sin(Time.unscaledTime*3)):GameUiStyle.Blue;
        }
        if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.1f;Refresh();
    }
    public void Refresh()
    {
        if(root==null)return;
        var stats=RunStats.Instance;bool running=stats!=null && stats.Running;
        root.SetActive(running);if(!running)return;
        hpText.text=Mathf.CeilToInt(health.currentHealth)+" <size=14>/ "+Mathf.RoundToInt(health.maxHealth)+"</size>";
        hpFill.color=health.currentHealth/Mathf.Max(1,health.maxHealth)<.25f?new Color(1f,.18f,.38f):GameUiStyle.Pink;
        SetFill(dashFill,dash.CooldownFraction);dashText.text=dash.IsReady?(MobileControls.Enabled?"대시":"Space  대시"):"대시 충전";
        timeText.text=RunStats.FormatTime(stats.SurvivalTime);killsText.text="<size=13>처치</size>  "+stats.Kills;
        var kind=weapons.Equipped;weaponIcon.sprite=portraits[(int)kind];weaponIcon.enabled=weaponIcon.sprite!=null;
        weaponText.text=WeaponNames[(int)kind];
        for(int i=0;i<5;i++)
        {
            bool owned=weapons.Owns((PlayerWeapons.Kind)i),selected=i==(int)kind;
            slotRims[i].color=selected?new Color(1,.76f,.16f):new Color(.16f,.25f,.36f);
            slotFaces[i].color=selected?new Color(1,.78f,.22f):new Color(.055f,.11f,.20f);
            slotIcons[i].color=owned?Color.white:new Color(.14f,.18f,.24f,.35f);
            slotNumbers[i].color=selected?new Color(.08f,.09f,.12f):owned?Color.white:new Color(.43f,.49f,.56f);
            slotNumbers[i].gameObject.SetActive(!MobileControls.Enabled);
            slotNames[i].gameObject.SetActive(owned);
            slotNames[i].color=selected?new Color(1,.84f,.34f):owned?new Color(.86f,.91f,.96f):new Color(.43f,.49f,.56f);
            slotRims[i].rectTransform.localScale=Vector3.one*(selected?1.06f:1f);
        }
        if(ammoNotice==null)ammoNotice=GetComponent<AmmoFullNotice>();
        bool noticeVisible=ammoNotice!=null && ammoNotice.Visible;
        noticeBackground.gameObject.SetActive(noticeVisible);
        if(noticeVisible){var c=pickupNotice.color;c.a=ammoNotice.Opacity;pickupNotice.color=c;}

        bool ranged=weapons.IsGun;weaponTypeText.gameObject.SetActive(!ranged);weaponIcon.rectTransform.sizeDelta=new Vector2(98,98);VisibleAmmoIndicators=ranged?PlayerGunCombat.Capacity(kind):0;LitAmmoIndicators=ranged?gun.Ammo:0;
        ammoText.text=MapSandboxSettings.Enabled?"∞":ranged?gun.Ammo+" <size=13>/ "+gun.Reserve+"</size>":"";
        ammoText.color=ranged && gun.Ammo==0?new Color(.72f,.27f,.20f):Color.white;
        stateText.text=MapSandboxSettings.Enabled && ranged?"무한 탄약 · 재장전 없음":!ranged?(MobileControls.Enabled?"공격 버튼 길게 누르기":"좌클릭 공격"):gun.IsReloading?"재장전 중":gun.IsCharging?"충전 중":gun.Ammo==0 && gun.Reserve==0?"탄약 없음 · 무기 교체":(MobileControls.Enabled?"재장전":"R  재장전");
        if(!ranged && GetComponent<CharacterGrowth>().DashStrikeReady)stateText.text="대시 일격 준비 · 다음 공격 강화";
        actionFill.transform.parent.gameObject.SetActive(gun.IsReloading || gun.IsCharging);
        SetFill(actionFill,gun.IsReloading?gun.ReloadFraction:gun.IsCharging?gun.ChargeFraction:0);
        for(int i=0;i<shells.Length;i++)
        {
            bool visible=i<VisibleAmmoIndicators;shells[i].gameObject.SetActive(visible);if(!visible)continue;
            bool machine=kind==PlayerWeapons.Kind.MachineGun;
            var r=shells[i].rectTransform;r.anchoredPosition=machine?new Vector2(-35+(i%15)*11,4-(i/15)*10):new Vector2(-27+i*24,-1);r.sizeDelta=machine?new Vector2(7,7):new Vector2(11,15);
            shells[i].color=i<gun.Ammo?(kind==PlayerWeapons.Kind.LaserGun?new Color(.22f,.84f,1):new Color(1,.72f,.04f)):new Color(.15f,.23f,.32f);
            var cap=shells[i].transform.GetChild(0).GetComponent<Image>();cap.gameObject.SetActive(!machine);cap.color=i<gun.Ammo?new Color(1,.92f,.53f):new Color(.28f,.31f,.36f);
        }
    }
    static void SetFill(Image image,float value){image.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(value),1);}
    RectTransform Panel(string name,Vector2 anchor,Vector2 at,Vector2 size)
    {
        var edge=Box(root.transform,name,at,size,Border);var r=edge.rectTransform;r.anchorMin=r.anchorMax=anchor;
        if(MobileControls.Enabled){r.localScale=Vector3.one*.82f;if(anchor.x==1)r.anchoredPosition+=Vector2.left*36;else if(anchor.x==0)r.anchoredPosition+=Vector2.right*36;}
        r.gameObject.AddComponent<UiSafePanel>();ToyHudStyle.Frame(edge);return r;
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
        fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;fill.rectTransform.sizeDelta=Vector2.zero;fill.gameObject.AddComponent<UiSurfaceGradient>();
    }
    Text Label(Transform parent,string name,string text,int fontSize,Vector2 at,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=at;r.sizeDelta=size;
        var t=go.GetComponent<Text>();t.font=font;t.fontSize=fontSize;t.fontStyle=FontStyle.Normal;t.verticalOverflow=VerticalWrapMode.Overflow;t.horizontalOverflow=HorizontalWrapMode.Overflow;t.color=new Color(.94f,.97f,1f);t.alignment=TextAnchor.MiddleCenter;t.text=text;t.raycastTarget=false;return t;
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
    void OnDestroy(){foreach(var sprite in croppedGunPortraits)if(sprite!=null)Destroy(sprite);if(root!=null)Destroy(root);if(rounded!=null){Destroy(rounded.texture);Destroy(rounded);}}
}
