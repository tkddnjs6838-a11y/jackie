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
    Text status,stageTitle,stageNumber;
    Image killProgress,stageIcon;
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
    // Compatibility for the touch HUD: portals now activate by walking through.
    public bool CanInteractWithPortal=>false;
    public bool EnterPortal(int index)
    {
        if(Busy || player==null || Time.unscaledTime<readyAt || Time.timeScale<=0 || RunStats.Instance==null || !RunStats.Instance.Running)return false;
        if(index<0 || index>=Maps.Length || (!TestMode && (!portalUnlocked || index!=Mathf.Min(CurrentMap+1,4))))return false;
        if((player.transform.position-PortalPositions[index]).sqrMagnitude>2.3f*2.3f)return false;
        if(!TestMode && CurrentMap==4){flow.CompleteStages();return true;}
        return TravelTo(index);
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
        string hint="포탈 안으로 이동 · F1~F5 바로 이동 · F6 시작 지점";
        status.text="테스트 · 무적 / 무한 탄약   |   "+(CurrentMap+1)+". "+Names[CurrentMap]+"\n1 칼  2 도끼  3 샷건  4 기관총  5 레이저\n"+hint;
        if(stageTitle!=null){stageTitle.text=Names[CurrentMap];stageNumber.text=$"STAGE {CurrentMap+1:00}";stageIcon.sprite=ToyHudStyle.Map(CurrentMap);}
        if(!TestMode)status.text=(portalUnlocked?(CurrentMap==4?"포탈을 통과하면 모험 완료":"포탈을 통과해 다음 스테이지로"):spawner!=null?spawner.ObjectiveStatus:"몬스터를 처치하세요");
        if(killProgress!=null && spawner!=null)killProgress.rectTransform.anchorMax=new Vector2(Mathf.Clamp01((float)spawner.KillCount/Mathf.Max(1,spawner.killsToSpawnBoss)),1);
        if(Time.timeScale<=0 || Time.unscaledTime<readyAt)return;
        var k=Keyboard.current;
        if(!TestMode || k==null)return;
        for(int i=0;i<5;i++)if(k[(Key)((int)Key.F1+i)].wasPressedThisFrame){TravelTo(i);return;}
        if(k.f6Key.wasPressedThisFrame)PlacePlayer();
    }
    IEnumerator ChangeMap(int index)
    {
        Busy=true;
        foreach(var guide in FindObjectsByType<ObjectiveGuide>(FindObjectsSortMode.None))guide.ResetGuide();
        player.GetComponent<PlayerAutoAttack>()?.EndImpactSlowdown();
        player.GetComponent<PlayerDash>()?.CancelMotion();
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
            MapMood.ApplyIn(environment); // the player's theme; the scene itself bakes the bright look
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
        var forward=new Vector3(-Arrival.x,0,-Arrival.z).normalized;
        if(forward.sqrMagnitude<.01f)forward=Vector3.forward;
        for(int i=0;i<5;i++)
        {
            if(!TestMode && i!=Mathf.Min(CurrentMap+1,4))continue;
            var direction=Quaternion.Euler(0,TestMode?(i-2)*33:0,0)*forward;
            var position=Arrival+direction*5;
            if(Physics.Raycast(position+Vector3.up*50,Vector3.down,out var hit,100,~(1<<2),QueryTriggerInteraction.Ignore))position.y=hit.point.y+.12f;
            else if(Terrain.activeTerrain!=null)position.y=Terrain.activeTerrain.SampleHeight(position)+Terrain.activeTerrain.transform.position.y+.12f;
            if(!TestMode)
            {
                if(!MapNavigation.PortalPoint(Arrival,out position,out direction))
                    throw new System.InvalidOperationException("No reachable portal clearance on "+Maps[CurrentMap]);
            }
            PortalPositions[i]=position;
            var ring=new GameObject("Portal_"+(i+1));ring.layer=2;ring.transform.SetParent(portals.transform);ring.transform.position=position;
            if(!TestMode)foreach(var guide in FindObjectsByType<ObjectiveGuide>(FindObjectsSortMode.None))guide.SetStageTarget(ring.transform,CurrentMap==4);
            ring.AddComponent<DestinationPortal>().Build(i,direction,!TestMode && CurrentMap==4);
        }
    }

    void CreateHud()
    {
        hud=new GameObject("SandboxStatus",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(hud,gameObject.scene);
        var canvas=hud.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=350;
        var scaler=hud.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
        var panel=new GameObject("Panel",typeof(RectTransform),typeof(Image));panel.transform.SetParent(hud.transform,false);
        var r=panel.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,0);r.pivot=new Vector2(.5f,0);r.anchoredPosition=new Vector2(0,12);r.sizeDelta=TestMode?new Vector2(580,82):new Vector2(244,100);
        var bg=panel.GetComponent<Image>();bg.color=new Color(.02f,.03f,.05f,.85f);bg.raycastTarget=false;
        var label=new GameObject("Status",typeof(RectTransform),typeof(Text));label.transform.SetParent(panel.transform,false);
        r=label.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
        status=label.GetComponent<Text>();status.font=Resources.Load<Font>("UI/AugmentKorean");status.fontSize=16;status.alignment=TextAnchor.MiddleCenter;status.color=new Color(.84f,.89f,.94f);status.raycastTarget=false;
        if(!TestMode)
        {
            var panelRect=panel.GetComponent<RectTransform>();panelRect.anchorMin=panelRect.anchorMax=new Vector2(0,1);panelRect.pivot=new Vector2(0,1);panelRect.anchoredPosition=new Vector2(20,-26);
            if(MobileControls.Enabled){panelRect.localScale=Vector3.one*.8f;panelRect.anchoredPosition=new Vector2(64,-26);}
            if(!MobileControls.Enabled)panelRect.localScale=Vector3.one*.88f;
            panel.AddComponent<UiSafePanel>();status.fontSize=13;status.resizeTextForBestFit=true;status.resizeTextMinSize=11;status.resizeTextMaxSize=13;
            ToyHudStyle.Frame(bg);status.transform.SetAsLastSibling();
            status.color=new Color(.87f,.94f,1);status.alignment=TextAnchor.MiddleLeft;
            r.anchorMin=new Vector2(0,0);r.anchorMax=new Vector2(1,0);
            r.offsetMin=new Vector2(94,26);r.offsetMax=new Vector2(-12,53);
            status.resizeTextMinSize=10;status.resizeTextMaxSize=13;
            var tab=ToyHudStyle.Piece(panel.transform,"StageTab",new Vector2(-48,47),new Vector2(102,25),Color.white);ToyHudStyle.Frame(tab,false,true);
            stageNumber=GameUiStyle.Label(tab.transform,"Number","STAGE 01",status.font,14,new Color(.04f,.10f,.18f),Vector2.zero,new Vector2(98,24),true);
            stageIcon=ToyHudStyle.Piece(panel.transform,"MapIcon",new Vector2(-72,-1),new Vector2(80,70),Color.white);stageIcon.type=Image.Type.Simple;stageIcon.preserveAspect=true;stageIcon.sprite=ToyHudStyle.Map(0);
            stageTitle=GameUiStyle.Label(panel.transform,"StageTitle","",status.font,24,Color.white,new Vector2(44,18),new Vector2(144,32),true);stageTitle.alignment=TextAnchor.MiddleLeft;
            var track=new GameObject("KillProgress",typeof(RectTransform),typeof(Image));track.transform.SetParent(panel.transform,false);
            var trackRect=track.GetComponent<RectTransform>();trackRect.anchorMin=new Vector2(0,0);trackRect.anchorMax=new Vector2(1,0);trackRect.offsetMin=new Vector2(16,13);trackRect.offsetMax=new Vector2(-16,21);
            GameUiStyle.Apply(track.GetComponent<Image>(),new Color(.015f,.06f,.13f));track.GetComponent<Image>().raycastTarget=false;track.GetComponent<Image>().pixelsPerUnitMultiplier=12;
            var edge=track.AddComponent<Outline>();edge.effectColor=GameUiStyle.Blue;edge.effectDistance=new Vector2(1,-1);
            var fill=new GameObject("Fill",typeof(RectTransform),typeof(Image));fill.transform.SetParent(track.transform,false);
            killProgress=fill.GetComponent<Image>();GameUiStyle.Apply(killProgress,GameUiStyle.Blue);killProgress.raycastTarget=false;killProgress.pixelsPerUnitMultiplier=12;
            killProgress.rectTransform.anchorMin=Vector2.zero;killProgress.rectTransform.anchorMax=Vector2.one;killProgress.rectTransform.offsetMin=killProgress.rectTransform.offsetMax=Vector2.zero;

        }
    }
    void OnDestroy(){if(hud!=null)Destroy(hud);if(portals!=null)Destroy(portals);}
}
