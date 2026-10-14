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
    readonly Text[] inventoryRows=new Text[8];
    Text emptyInventory;
    public bool InventoryOpen {get;private set;}
    public GameObject InventoryPanel=>inventoryPanel;
    CursorLockMode inventoryLock;
    bool inventoryCursor;
    readonly List<CharacterGrowth.Kind> offered=new List<CharacterGrowth.Kind>();
    readonly Button[] buttons=new Button[3];
    readonly Text[] labels=new Text[3];
    public bool Choosing { get; private set; }
    public IReadOnlyList<CharacterGrowth.Kind> Offered=>offered;
    public GameObject ChoicePanel=>choicePanel;
    CursorLockMode previousLock;
    bool previousCursor;
    void Awake()=>CancelChoices();
    void OnDestroy(){if(choicePanel!=null)Destroy(choicePanel);if(inventoryPanel!=null)Destroy(inventoryPanel);}
    void Update()
    {
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
            inventoryRows[row].text=growth.OwnedDescription(kind);inventoryRows[row++].gameObject.SetActive(true);
        }
        for(int i=row;i<inventoryRows.Length;i++)inventoryRows[i].gameObject.SetActive(false);
        emptyInventory.gameObject.SetActive(row==0);
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
    void EnsureInventoryUI()
    {
        if(inventoryPanel!=null)return;
        inventoryPanel=new GameObject("OwnedAugments",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(Image));
        var canvas=inventoryPanel.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=190;
        var scaler=inventoryPanel.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        inventoryPanel.GetComponent<Image>().color=new Color(.015f,.025f,.045f,.96f);
        var font=Resources.Load<Font>("UI/AugmentKorean");
        MakeText(inventoryPanel.transform,"Title","보유 증강",font,36,new Vector2(0,290),new Vector2(1100,55));
        MakeText(inventoryPanel.transform,"Hint","이번 판에 획득한 증강 · 일시 효과와 무기별 보정 제외",font,20,new Vector2(0,240),new Vector2(1100,40));
        emptyInventory=MakeText(inventoryPanel.transform,"Empty","아직 획득한 증강이 없습니다.\n증강 상자를 주워 효과를 선택하세요.",font,25,Vector2.zero,new Vector2(1100,150));
        for(int i=0;i<inventoryRows.Length;i++)
        {
            inventoryRows[i]=MakeText(inventoryPanel.transform,"Owned"+i,"",font,22,new Vector2(i%2==0?-285:285,150-(i/2)*115),new Vector2(530,105));
            inventoryRows[i].alignment=TextAnchor.MiddleLeft;
        }
        var close=new GameObject("Close",typeof(RectTransform),typeof(Image),typeof(Button));close.transform.SetParent(inventoryPanel.transform,false);
        var rect=close.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(0,-290);rect.sizeDelta=new Vector2(360,55);
        close.GetComponent<Image>().color=new Color(.12f,.22f,.34f);close.GetComponent<Button>().onClick.AddListener(CloseInventory);
        MakeText(close.transform,"Label","닫기  ·  Tab / Esc",font,22,Vector2.zero,new Vector2(340,50));
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
            buttons[i].gameObject.SetActive(true);
            if(candidates.Count==0){buttons[i].gameObject.SetActive(false);continue;}
            int index=Random.Range(0,candidates.Count);var kind=candidates[index];candidates.RemoveAt(index);offered.Add(kind);
            labels[i].text=growth.ChoiceDescription(kind);
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
        var canvas=choicePanel.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=200;
        var scaler=choicePanel.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        choicePanel.GetComponent<Image>().color=new Color(.015f,.025f,.045f,.94f);
        var font=Resources.Load<Font>("UI/AugmentKorean");
        MakeText(choicePanel.transform,"Title","증강 선택",font,36,new Vector2(0,265),new Vector2(1100,60));
        MakeText(choicePanel.transform,"Hint","상자 1개 · 효과 1개 선택 · 이번 판 동안 유지",font,20,new Vector2(0,212),new Vector2(1100,45));
        for(int i=0;i<3;i++)
        {
            var card=new GameObject("Choice"+i,typeof(RectTransform),typeof(Image),typeof(Button));card.transform.SetParent(choicePanel.transform,false);
            var rect=card.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(350,330);rect.anchoredPosition=new Vector2((i-1)*380,-5);
            GameUiStyle.Apply(card.GetComponent<Image>(),new Color(.06f,.09f,.13f));GameUiStyle.Accent(card.transform);
            var frame=card.AddComponent<Outline>();frame.effectColor=new Color(.35f,.43f,.5f);frame.effectDistance=new Vector2(1,-1);
            buttons[i]=card.GetComponent<Button>();int selected=i;buttons[i].onClick.AddListener(()=>Choose(selected));
            labels[i]=MakeText(card.transform,"Effect","",font,23,Vector2.zero,new Vector2(310,295));
        }
        MakeText(choicePanel.transform,"Footer","원하는 카드를 누르면 전투가 이어집니다",font,20,new Vector2(0,-245),new Vector2(1100,45));
    }
    static Text MakeText(Transform parent,string name,string content,Font font,int size,Vector2 at,Vector2 bounds)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=at;rect.sizeDelta=bounds;
        var text=go.GetComponent<Text>();text.font=font;text.fontSize=size;text.fontStyle=size>=30?FontStyle.Bold:FontStyle.Normal;text.color=size>=30?GameUiStyle.Gold:Color.white;text.alignment=TextAnchor.MiddleCenter;text.text=content;text.raycastTarget=false;
        return text;
    }
}
