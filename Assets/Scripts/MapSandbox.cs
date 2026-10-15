using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Keeps the gameplay scene and player alive while swapping only the design environment.
public sealed class MapSandbox : MonoBehaviour
{
    public static readonly string[] Maps = { "Map01_Meadow", "Map02_Canyon", "Map03_WaxLake", "Map04_CrystalPlateau", "Map05_Crater" };
    public static readonly string[] Names = { "초원", "협곡", "왁스 호수", "수정 고원", "분화구" };
    public int CurrentMap { get; private set; } = -1;
    public bool Busy { get; private set; }
    public Vector3 Arrival { get; private set; }
    public Vector3[] PortalPositions { get; } = new Vector3[5];
    Scene environment;
    PlayerWeapons player;
    GameFlowManager flow;
    EnemySpawner spawner;
    GameObject portals, hud;
    Text status,stageTitle;
    Image[] stagePips=new Image[5];
    Material portalMaterial;
    float readyAt;
    bool TestMode=>MapSandboxSettings.Enabled;
    bool portalUnlocked;
    void OnEnable(){Enemy.OnBossDefeated+=UnlockPortal;}
    void OnDisable(){Enemy.OnBossDefeated-=UnlockPortal;}
    void UnlockPortal(){if(!TestMode){portalUnlocked=true;if(spawner!=null)spawner.enabled=false;if(portals!=null)portals.SetActive(true);}}

    IEnumerator Start()
    {
        flow=GetComponent<GameFlowManager>();
        flow.startButton.interactable=false;
        // PlayerAutoAttack creates PlayerWeapons in Start; Start order is not guaranteed.
        yield return null;
        player=FindFirstObjectByType<PlayerWeapons>();
        if(player==null){Debug.LogError("Cannot load first stage: PlayerWeapons was not initialized.");yield break;}
        spawner=FindFirstObjectByType<EnemySpawner>();
        foreach(var root in gameObject.scene.GetRootGameObjects())
            if(root.name=="MapTerrain" || root.name=="River" || root.name=="MapProps" || root.name=="Directional Light" || root.name=="WinZone")root.SetActive(false);
        CreateHud();
        flow.startButton.interactable=false;
        yield return ChangeMap(0);
        flow.startButton.interactable=CurrentMap==0 && !Busy;
    }
    public bool CanInteractWithPortal
    {
        get
        {
            if(Busy || CurrentMap<0 || player==null || (!TestMode && !portalUnlocked))return false;
            for(int i=0;i<5;i++)if((TestMode || i==Mathf.Min(CurrentMap+1,4)) && (player.transform.position-PortalPositions[i]).sqrMagnitude<2.3f*2.3f)return true;
            return false;
        }
    }
    public bool TravelTo(int index)
    {
        if(Busy || index<0 || index>=Maps.Length || RunStats.Instance==null || !RunStats.Instance.Running || Time.timeScale<=0)return false;
        if(!TestMode && (!portalUnlocked || index!=CurrentMap+1))return false;
        StartCoroutine(ChangeMap(index));return true;
    }
    public void HideHud(){if(hud!=null)hud.SetActive(false);}
    void Update()
    {
        bool running=RunStats.Instance!=null && RunStats.Instance.Running;
        if(status==null)return;
        hud.SetActive(running || Busy);
        if(Busy){status.text="맵 이동 중…";return;}
        if(!running || player==null || CurrentMap<0)return;
        string hint="포탈 근처에서 E 이동 · F1~F5 바로 이동 · F6 시작 지점";
        int nearby=-1;
        for(int i=0;i<5;i++)
            if((TestMode || (portalUnlocked && i==Mathf.Min(CurrentMap+1,4))) && Vector3.Distance(player.transform.position,PortalPositions[i])<2.3f){nearby=i;hint="E  →  "+(i+1)+". "+Names[i];break;}
        status.text="테스트 · 무적 / 무한 탄약   |   "+(CurrentMap+1)+". "+Names[CurrentMap]+"\n1 칼  2 도끼  3 샷건  4 기관총  5 레이저\n"+hint;
        if(stageTitle!=null){stageTitle.text=Busy?"이동 중":$"STAGE {CurrentMap+1:00}  /  {Names[CurrentMap]}";for(int j=0;j<5;j++)stagePips[j].color=j<=CurrentMap?new Color(.59f,.68f,.66f):new Color(.2f,.26f,.32f);}
        if(!TestMode)status.text=(portalUnlocked?(CurrentMap==4?"최종 보스 격파 · 포탈에서 E로 완료":"보스 격파 · 포탈에서 E로 다음 스테이지"):spawner!=null && spawner.KillCount<spawner.killsToSpawnBoss?$"몬스터 처치  {spawner.KillCount} / {spawner.killsToSpawnBoss}  ·  보스 소환 준비":"보스 격파 후 다음 스테이지로 이동");
        if(MobileControls.Enabled)status.text=status.text.Replace("E로", "이동 버튼으로");
        if(Time.timeScale<=0 || Time.unscaledTime<readyAt)return;
        var k=Keyboard.current;
        bool interact=MobileControls.TakeInteract() || (k!=null && k.eKey.wasPressedThisFrame);
        if(nearby>=0 && interact){if(!TestMode && CurrentMap==4)flow.CompleteStages();else TravelTo(nearby);return;}
        if(!TestMode || k==null)return;
        for(int i=0;i<5;i++)if(k[(Key)((int)Key.F1+i)].wasPressedThisFrame){TravelTo(i);return;}
        if(k.f6Key.wasPressedThisFrame)PlacePlayer();
    }
    IEnumerator ChangeMap(int index)
    {
        Busy=true;
        foreach(var guide in FindObjectsByType<ObjectiveGuide>(FindObjectsSortMode.None))guide.ResetGuide();
        float previousScale=Time.timeScale;
        bool spawnEnabled=spawner!=null && spawner.enabled;
        if(spawner!=null)spawner.enabled=false;
        Time.timeScale=0;
        try
        {
            player.GetComponent<PlayerGunCombat>()?.CancelAction();
            string path="Assets/Design/Scenes/"+Maps[index]+".unity";
            if(!Application.CanStreamedLevelBeLoaded(path))throw new System.InvalidOperationException("Map missing from build scenes: "+path);
            // Unload the old environment before loading to also support re-entering the same map.
            if(environment.IsValid() && environment.isLoaded)
            {
                SceneManager.SetActiveScene(gameObject.scene);
                yield return SceneManager.UnloadSceneAsync(environment);
            }
            if(portals!=null)Destroy(portals);
            foreach(var enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))Destroy(enemy.gameObject);
            ClearPickups<WeaponPickup>();ClearPickups<GunPickup>();ClearPickups<CharacterUpgradePickup>();ClearPickups<HealthPickup>();ClearPickups<PowerUp>();
            Enemy.ResetRunState();
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);
            environment=SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(environment);
            Transform marker=null;
            foreach(var root in environment.GetRootGameObjects())
            {
                foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name=="Marker_Start")marker=t;
                foreach(var cam in root.GetComponentsInChildren<Camera>(true))cam.enabled=false;
                foreach(var listener in root.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
            }
            if(marker==null)throw new System.InvalidOperationException("Missing Marker_Start in "+path);
            Arrival=marker.position+Vector3.up*.6f;
            CurrentMap=index;
            portalUnlocked=false;
            if(!TestMode && spawner!=null)spawner.ConfigureStage(index);
            PlacePlayer();
            BuildPortals();
            if(!TestMode)portals.SetActive(false);
            Physics.SyncTransforms();
            readyAt=Time.unscaledTime+.75f;
        }
        finally
        {
            Busy=false;Time.timeScale=previousScale;
            if(!TestMode)spawnEnabled=true;
            if(spawner!=null)spawner.enabled=spawnEnabled;
        }
    }
    static void ClearPickups<T>() where T:Component
    { foreach(var pickup in FindObjectsByType<T>(FindObjectsSortMode.None))Destroy(pickup.gameObject); }
    void PlacePlayer()
    {
        var controller=player.GetComponent<CharacterController>();bool enabled=controller.enabled;controller.enabled=false;
        Vector3 inward=new Vector3(-Arrival.x,0,-Arrival.z).normalized;
        player.transform.SetPositionAndRotation(Arrival,Quaternion.LookRotation(inward.sqrMagnitude>.01f?inward:Vector3.forward));
        controller.enabled=enabled;
    }
    void BuildPortals()
    {
        portals=new GameObject("SandboxPortals");SceneManager.MoveGameObjectToScene(portals,gameObject.scene);
        if(portalMaterial==null)
        {
            var shader=Shader.Find("Universal Render Pipeline/Unlit");
            if(shader==null)shader=Shader.Find("Unlit/Color");
            portalMaterial=new Material(shader);
        }
        var forward=new Vector3(-Arrival.x,0,-Arrival.z).normalized;
        if(forward.sqrMagnitude<.01f)forward=Vector3.forward;
        for(int i=0;i<5;i++)
        {
            if(!TestMode && i!=Mathf.Min(CurrentMap+1,4))continue;
            var direction=Quaternion.Euler(0,TestMode?(i-2)*33:0,0)*forward;
            var position=Arrival+direction*5;
            if(Physics.Raycast(position+Vector3.up*50,Vector3.down,out var hit,100,~(1<<2),QueryTriggerInteraction.Ignore))position.y=hit.point.y+.12f;
            else if(Terrain.activeTerrain!=null)position.y=Terrain.activeTerrain.SampleHeight(position)+Terrain.activeTerrain.transform.position.y+.12f;
            if(!TestMode && MapNavigation.DropPoint(position,out var safePortal))position=safePortal+Vector3.up*.12f;
            PortalPositions[i]=position;
            var ring=new GameObject("Portal_"+(i+1),typeof(LineRenderer));ring.layer=2;ring.transform.SetParent(portals.transform);ring.transform.position=position;
            if(!TestMode)foreach(var guide in FindObjectsByType<ObjectiveGuide>(FindObjectsSortMode.None))guide.SetStageTarget(ring.transform,CurrentMap==4);
            var color=TestMode?Color.HSVToRGB(i/5f,.65f,1):new Color(.59f,.68f,.66f);
            var line=ring.GetComponent<LineRenderer>();line.sharedMaterial=portalMaterial;line.useWorldSpace=false;line.loop=true;line.positionCount=48;
            var tint=new MaterialPropertyBlock();tint.SetColor("_BaseColor",color);tint.SetColor("_Color",color);line.SetPropertyBlock(tint);
            line.startWidth=line.endWidth=.1f;line.startColor=line.endColor=color;
            var side=Vector3.Cross(Vector3.up,direction).normalized;
            for(int j=0;j<48;j++){float a=j*Mathf.PI*2/48;line.SetPosition(j,side*Mathf.Cos(a)*.9f+Vector3.up*(1.25f+Mathf.Sin(a)*1.2f));}
            var board=new GameObject("Destination",typeof(RectTransform),typeof(Canvas));board.transform.SetParent(ring.transform,false);
            var canvas=board.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            board.GetComponent<RectTransform>().sizeDelta=new Vector2(260,75);board.transform.localScale=Vector3.one*.01f;board.transform.localPosition=Vector3.up*2.9f;
            var label=new GameObject("Label",typeof(RectTransform),typeof(Text));label.transform.SetParent(board.transform,false);
            var rt=label.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            var text=label.GetComponent<Text>();text.font=Resources.Load<Font>("UI/AugmentKorean");text.fontSize=24;text.alignment=TextAnchor.MiddleCenter;text.color=color;text.text=(!TestMode && CurrentMap==4)?"완주\n[E] 완료":(i+1)+". "+Names[i]+"\n[E] 이동";text.raycastTarget=false;
        }
    }
    void LateUpdate()
    {
        if(portals==null || Camera.main==null)return;
        foreach(Transform portal in portals.transform)portal.GetChild(0).rotation=Camera.main.transform.rotation;
    }
    void CreateHud()
    {
        hud=new GameObject("SandboxStatus",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(hud,gameObject.scene);
        var canvas=hud.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=350;
        var scaler=hud.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
        var panel=new GameObject("Panel",typeof(RectTransform),typeof(Image));panel.transform.SetParent(hud.transform,false);
        var r=panel.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,0);r.pivot=new Vector2(.5f,0);r.anchoredPosition=new Vector2(0,12);r.sizeDelta=TestMode?new Vector2(580,82):new Vector2(300,80);
        var bg=panel.GetComponent<Image>();bg.color=new Color(.02f,.03f,.05f,.85f);bg.raycastTarget=false;
        var label=new GameObject("Status",typeof(RectTransform),typeof(Text));label.transform.SetParent(panel.transform,false);
        r=label.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
        status=label.GetComponent<Text>();status.font=Resources.Load<Font>("UI/AugmentKorean");status.fontSize=16;status.alignment=TextAnchor.MiddleCenter;status.color=new Color(.84f,.89f,.94f);status.raycastTarget=false;
        if(!TestMode)
        {
            var panelRect=panel.GetComponent<RectTransform>();panelRect.anchorMin=panelRect.anchorMax=new Vector2(0,1);panelRect.pivot=new Vector2(0,1);panelRect.anchoredPosition=new Vector2(20,-82);
            if(MobileControls.Enabled){panelRect.localScale=Vector3.one*.8f;panelRect.anchoredPosition=new Vector2(64,-82);}
            status.fontSize=13;
            GameUiStyle.Apply(bg,GameUiStyle.Ink);GameUiStyle.Accent(panel.transform);
            var border=panel.AddComponent<Outline>();border.effectColor=new Color(.32f,.34f,.36f,.4f);border.effectDistance=new Vector2(1,-1);
            r.anchorMin=new Vector2(0,0);r.anchorMax=new Vector2(1,.48f);
            var title=new GameObject("StageTitle",typeof(RectTransform),typeof(Text));title.transform.SetParent(panel.transform,false);
            var tr=title.GetComponent<RectTransform>();tr.anchorMin=new Vector2(0,.45f);tr.anchorMax=new Vector2(1,.88f);tr.offsetMin=tr.offsetMax=Vector2.zero;
            stageTitle=title.GetComponent<Text>();stageTitle.font=status.font;stageTitle.fontSize=18;stageTitle.fontStyle=FontStyle.Bold;stageTitle.color=new Color(.84f,.86f,.84f);stageTitle.alignment=TextAnchor.MiddleCenter;stageTitle.raycastTarget=false;
            for(int j=0;j<5;j++)
            {
                var pip=new GameObject("StageProgress"+j,typeof(RectTransform),typeof(Image));pip.transform.SetParent(panel.transform,false);
                var pr=pip.GetComponent<RectTransform>();pr.anchorMin=new Vector2(.04f+j*.188f,.91f);pr.anchorMax=new Vector2(.204f+j*.188f,.95f);pr.offsetMin=pr.offsetMax=Vector2.zero;
                stagePips[j]=pip.GetComponent<Image>();stagePips[j].raycastTarget=false;
            }
        }
    }
    void OnDestroy(){if(hud!=null)Destroy(hud);if(portalMaterial!=null)Destroy(portalMaterial);if(portals!=null)Destroy(portals);}
}
