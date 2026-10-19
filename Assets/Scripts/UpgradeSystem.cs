using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Only a collected augment crate can open this screen; kills never open it.
public sealed class UpgradeSystem : MonoBehaviour
{
    public GameObject panel; // Legacy scene overlay, kept hidden for scene compatibility.
    GameObject choicePanel;
    CharacterGrowth recipient;
    GameObject inventoryPanel;
    readonly Text[] inventoryRows=new Text[CharacterGrowth.KindCount];
    readonly Image[] inventoryIcons=new Image[CharacterGrowth.KindCount];
    Text emptyInventory;
    public bool InventoryOpen {get;private set;}
    public GameObject InventoryPanel=>inventoryPanel;
    CursorLockMode inventoryLock;
    bool inventoryCursor;
    readonly List<CharacterGrowth.Kind> offered=new List<CharacterGrowth.Kind>();
    readonly AugmentCard[] cards=new AugmentCard[3];
    RectTransform choiceContent;
    public bool Choosing { get; private set; }
    public IReadOnlyList<CharacterGrowth.Kind> Offered=>offered;
    public GameObject ChoicePanel=>choicePanel;
    CursorLockMode previousLock;
    bool previousCursor;
    void Awake()=>CancelChoices();
    void OnDestroy(){if(choicePanel!=null)Destroy(choicePanel);if(inventoryPanel!=null)Destroy(inventoryPanel);}
    void Update()
    {
        // short or very wide screens: shrink the whole choice layout until it fits
        if(Choosing && choiceContent!=null)
        {
            var area=((RectTransform)choicePanel.transform).rect.size;
            var safe=Screen.safeArea;
            var screen=new Vector2(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height));
            choiceContent.anchoredPosition=new Vector2((safe.center.x/screen.x-.5f)*area.x,(safe.center.y/screen.y-.5f)*area.y);
            choiceContent.localScale=Vector3.one*Mathf.Min(1f,area.x*(safe.width/screen.x)/880f,area.y*(safe.height/screen.y)/540f);
        }
        if(Keyboard.current==null)return;
        if(Keyboard.current.tabKey.wasPressedThisFrame)ToggleInventory();
        else if(InventoryOpen && Keyboard.current.escapeKey.wasPressedThisFrame)CloseInventory();
    }
    public bool ToggleInventory()
    {
        if(InventoryOpen){CloseInventory();return true;}
        if(Choosing || InventoryOpen || Time.timeScale<=0 || RunStats.Instance==null || !RunStats.Instance.Running)return false;
        var growth=FindFirstObjectByType<CharacterGrowth>();
        if(growth==null || growth.GetComponent<PlayerHealth>()==null || growth.GetComponent<PlayerHealth>().IsDead)return false;
        EnsureInventoryUI();int row=0;
        foreach(CharacterGrowth.Kind kind in System.Enum.GetValues(typeof(CharacterGrowth.Kind)))
        {
            if(growth.Level(kind)==0)continue;
            inventoryRows[row].text=growth.OwnedDescription(kind);inventoryIcons[row].sprite=GameUiArt.Icon(kind);inventoryRows[row++].transform.parent.gameObject.SetActive(true);
        }
        for(int i=row;i<inventoryRows.Length;i++)inventoryRows[i].transform.parent.gameObject.SetActive(false);
        emptyInventory.gameObject.SetActive(row==0);
        LayoutInventory(row);
        inventoryLock=Cursor.lockState;inventoryCursor=Cursor.visible;
        InventoryOpen=true;inventoryPanel.SetActive(true);Time.timeScale=0;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return true;
    }
    public void CloseInventory()
    {
        if(!InventoryOpen)return;
        InventoryOpen=false;if(inventoryPanel!=null)inventoryPanel.SetActive(false);
        if(RunStats.Instance!=null && RunStats.Instance.Running)
        {
            Time.timeScale=1;Cursor.lockState=inventoryLock;Cursor.visible=inventoryCursor;
        }
    }
    void LayoutInventory(int count)
    {
        int rows=Mathf.Max(1,(count+1)/2);float height=206+rows*80,top=height*.5f;
        var root=inventoryPanel.transform;
        ((RectTransform)root.Find("InventoryCard")).sizeDelta=new Vector2(920,height);
        ((RectTransform)root.Find("Title")).anchoredPosition=new Vector2(0,top-54);
        ((RectTransform)root.Find("Hint")).anchoredPosition=new Vector2(0,top-92);
        ((RectTransform)root.Find("Close")).anchoredPosition=new Vector2(0,-top+53);
        for(int i=0;i<count;i++)((RectTransform)inventoryRows[i].transform.parent).anchoredPosition=new Vector2(count==1?0:i%2==0?-219:219,top-162-(i/2)*80);
    }
    void EnsureInventoryUI()
    {
        if(inventoryPanel!=null)return;
        inventoryPanel=new GameObject("OwnedAugments",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(Image));
        var canvas=inventoryPanel.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=390;
        var scaler=inventoryPanel.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        inventoryPanel.GetComponent<Image>().color=new Color(.01f,.025f,.05f,.86f);
        var font=Resources.Load<Font>("UI/AugmentKorean");
        ToyMenuLayout.Panel(inventoryPanel.transform,"InventoryCard",Vector2.zero,new Vector2(920,590));
        ToyMenuLayout.Label(inventoryPanel.transform,"Title","보유 증강",32,new Vector2(0,241),new Vector2(830,48),ToyMenuLayout.White);
        ToyMenuLayout.Label(inventoryPanel.transform,"Hint","이번 판에서 고른 효과",15,new Vector2(0,203),new Vector2(830,30),ToyMenuLayout.Muted);
        emptyInventory=ToyMenuLayout.Label(inventoryPanel.transform,"Empty","아직 획득한 증강이 없어요.\n상자를 발견하면 효과를 골라보세요!",22,Vector2.zero,new Vector2(800,110),ToyMenuLayout.Muted);
        for(int i=0;i<inventoryRows.Length;i++)
        {
            var row=ToyMenuLayout.Panel(inventoryPanel.transform,"Owned"+i,new Vector2(i%2==0?-219:219,133-(i/2)*80),new Vector2(418,74),false).transform;
            var icon=ToyMenuLayout.Rect(row,"Icon",new Vector2(-166,0),new Vector2(68,68)).gameObject.AddComponent<Image>();icon.raycastTarget=false;icon.preserveAspect=true;inventoryIcons[i]=icon;
            inventoryRows[i]=ToyMenuLayout.Label(row,"Description","",15,new Vector2(37,0),new Vector2(316,66),ToyMenuLayout.White,TextAnchor.MiddleLeft);
        }
        var close=ToyMenuLayout.Rect(inventoryPanel.transform,"Close",new Vector2(0,-242),new Vector2(300,50)).gameObject.AddComponent<Button>();
        ToyMenuLayout.ButtonStyle(close,"닫기  ·  Tab / Esc",true,new Vector2(0,-242),new Vector2(300,50));
        close.onClick.AddListener(()=>GameAudio.Play(GameAudio.Sfx.UiClick));close.onClick.AddListener(CloseInventory);
    }

    public bool TryOffer(CharacterGrowth growth)
    {
        if(Choosing || InventoryOpen || Time.timeScale<=0 || RunStats.Instance==null || !RunStats.Instance.Running || growth==null)return false;
        var health=growth.GetComponent<PlayerHealth>();
        if(health==null || health.IsDead)return false;
        var candidates=new List<CharacterGrowth.Kind>();
        foreach(CharacterGrowth.Kind kind in System.Enum.GetValues(typeof(CharacterGrowth.Kind)))
            if(growth.CanCollect(kind))candidates.Add(kind);
        if(candidates.Count==0)return false;
        EnsureUI();offered.Clear();recipient=growth;
        for(int i=0;i<3;i++)
        {
            if(candidates.Count==0){cards[i].Hide();continue;}
            int index=Random.Range(0,candidates.Count);var kind=candidates[index];candidates.RemoveAt(index);offered.Add(kind);
            cards[i].Show(growth,kind);
        }
        previousLock=Cursor.lockState;previousCursor=Cursor.visible;
        Choosing=true;choicePanel.SetActive(true);Time.timeScale=0;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        GameAudio.Play(GameAudio.Sfx.UpgradeOpen);
        return true;
    }
    public bool Choose(int index)
    {
        if(!Choosing || index<0 || index>=offered.Count || recipient==null || RunStats.Instance==null || !RunStats.Instance.Running)return false;
        if(!recipient.TryApplyChoice(offered[index]))return false;
        CancelChoices();
        // Resume normal combat even if the crate was touched during a brief hit stop.
        Time.timeScale=1;Cursor.lockState=previousLock;Cursor.visible=previousCursor;
        return true;
    }
    public void CancelChoices()
    {
        Choosing=false;recipient=null;offered.Clear();
        InventoryOpen=false;if(inventoryPanel!=null)inventoryPanel.SetActive(false);
        if(panel!=null)panel.SetActive(false);
        if(choicePanel!=null)choicePanel.SetActive(false);
        // GameFlowManager owns time and cursor when the run ends.
    }
    void EnsureUI()
    {
        if(choicePanel!=null)return;
        choicePanel=new GameObject("AugmentChoice",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(Image));
        // above the stage HUD (350) so nothing sits on top of the cards
        var canvas=choicePanel.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=400;
        var scaler=choicePanel.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        choicePanel.GetComponent<Image>().color=new Color(.025f,.045f,.12f,.38f);
        var font=Resources.Load<Font>("UI/AugmentKorean");
        choiceContent=new GameObject("Content",typeof(RectTransform)).GetComponent<RectTransform>();
        choiceContent.SetParent(choicePanel.transform,false);choiceContent.sizeDelta=new Vector2(1280,720);
        var heading=GameUiStyle.Label(choiceContent,"Title","증강 선택",font,46,Color.white,new Vector2(0,238),new Vector2(1100,90),true);
        var outline=heading.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.1f,.13f,.28f);outline.effectDistance=new Vector2(2.5f,-2.5f);
        var drop=heading.gameObject.AddComponent<Shadow>();drop.effectColor=new Color(0,0,0,.35f);drop.effectDistance=new Vector2(0,-5);
        GameUiStyle.Label(choiceContent,"Hint","상자 1개 · 효과 1개 선택 · 이번 판 동안 유지",font,19,new Color(1,1,1,.8f),new Vector2(0,191),new Vector2(1100,40),false);
        for(int i=0;i<3;i++)
        {
            cards[i]=new AugmentCard(choiceContent,font,i,new Vector2((i-1)*278,-22));
            int selected=i;cards[i].Button.onClick.AddListener(()=>Choose(selected));
        }
    }
    static Text MakeText(Transform parent,string name,string content,Font font,int size,Vector2 at,Vector2 bounds)
        =>GameUiStyle.Label(parent,name,content,font,size,size>=30?GameUiStyle.Gold:Color.white,at,bounds,size>=30);
}
