using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerAutoAttack))]
[RequireComponent(typeof(CharacterGrowth))]
public sealed class PlayerWeapons : MonoBehaviour
{
    public enum Kind { Sword, Axe, Shotgun, MachineGun, LaserGun }
    public Kind Equipped { get; private set; }
    public bool HasAxe { get; private set; }
    public float DamageScale => Equipped == Kind.Axe ? 2.1f : 1f;
    public float CooldownScale => Equipped == Kind.Axe ? 1.85f : 1f;
    public float RangeScale => Equipped == Kind.Axe ? 1.08f : 1f;
    public float ArcBonus => Equipped == Kind.Axe ? 60f : 0f;
    PlayerAutoAttack attack;
    Renderer[] swordRenderers;
    GameObject axe;
    Text label;
    readonly GameObject[] guns=new GameObject[3];
    readonly bool[] owned=new bool[3];
    PlayerGunCombat gunCombat;
    int hudState=int.MinValue;
    public bool IsGun => Equipped>=Kind.Shotgun;
    public Transform GunModel => IsGun && guns[(int)Equipped-2]!=null ? guns[(int)Equipped-2].transform : null;
    public Transform GunMuzzle => GunModel!=null ? GunModel.Find("Muzzle") : null;
    public bool Owns(Kind kind)=>kind==Kind.Sword || (kind==Kind.Axe?HasAxe:kind>=Kind.Shotgun && kind<=Kind.LaserGun && owned[(int)kind-2]);
    public bool CollectGun(Kind kind)
    {
        int i=(int)kind-2;if(i<0 || i>=guns.Length || guns[i]==null || gunCombat==null)return false;
        bool first=!owned[i];
        if(!gunCombat.AddAmmo(kind,first))return false;
        owned[i]=true;
        if(first && !attack.AttackInProgress)TryEquip(kind);
        return true;
    }

    void Awake() { attack = GetComponent<PlayerAutoAttack>(); if(GetComponent<CharacterGrowth>()==null)gameObject.AddComponent<CharacterGrowth>(); }
    void Start()
    {
        var sword = attack.swordModel;
        if (sword != null)
        {
            swordRenderers = sword.GetComponentsInChildren<Renderer>(true);
            var prefab = Resources.Load<GameObject>("Weapons/Axe");
            if (prefab != null)
            {
                axe = Instantiate(prefab, sword, false);
                // The imported sword's scale is model-specific. Author the axe in metres.
                Vector3 scale = sword.lossyScale;
                const float axeSize = .68f;
                axe.transform.localScale = new Vector3(axeSize / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                    axeSize / Mathf.Max(.001f, Mathf.Abs(scale.y)), axeSize / Mathf.Max(.001f, Mathf.Abs(scale.z)));
                // Align the thicker axe grip with the palm, independent of imported sword units.
                axe.transform.localPosition = new Vector3(0, (.075f - .12f * axeSize) / Mathf.Max(.001f, Mathf.Abs(scale.y)), 0);
                axe.SetActive(false);
            }
        }
        if(sword!=null)
        {
            string[] names={"Pistol","MachineGun","LaserGun"};
            for(int i=0;i<3;i++)
            {
                var prefab=Resources.Load<GameObject>("Weapons/Guns/"+names[i]);if(prefab==null)continue;
                guns[i]=Instantiate(prefab,sword,false);var scale=sword.lossyScale;
                float size=(i==0?1.05f:i==1?.95f:.88f)*1.25f;
                guns[i].transform.localScale=new Vector3(size/Mathf.Max(.001f,Mathf.Abs(scale.x)),size/Mathf.Max(.001f,Mathf.Abs(scale.y)),size/Mathf.Max(.001f,Mathf.Abs(scale.z)));
                guns[i].SetActive(false);
            }
        }
        gunCombat=GetComponent<PlayerGunCombat>();if(gunCombat==null)gunCombat=gameObject.AddComponent<PlayerGunCombat>();
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            var go = new GameObject("WeaponHUD", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(canvas.transform, false);
            label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 19; label.alignment = TextAnchor.LowerRight; label.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-24, -65); rect.sizeDelta = new Vector2(550, 90);
        }
        Refresh();
        if(WeaponTestSettings.AllGunsAtStart)
        {
            CollectGun(Kind.Shotgun);CollectGun(Kind.MachineGun);CollectGun(Kind.LaserGun);
            TryEquip(Kind.Shotgun);
        }
    }
    void Update()
    {
        if (label != null) label.enabled = RunStats.Instance != null && RunStats.Instance.Running;
        if(label!=null && IsGun && gunCombat!=null)
        {
            int state=(int)Equipped*1000000+gunCombat.Ammo*1000+gunCombat.Reserve*4+(gunCombat.IsReloading?2:gunCombat.IsCharging?1:0);
            if(state!=hudState){hudState=state;label.text=Equipped+"  "+gunCombat.Ammo+" / "+gunCombat.Reserve+(gunCombat.IsReloading?"  RELOADING":gunCombat.IsCharging?"  CHARGING":"")+"\nLMB Fire   R Reload   1–5 Weapon";}
        }
        if (Time.timeScale <= 0f || RunStats.Instance==null || !RunStats.Instance.Running || Keyboard.current == null) return;
        if (Keyboard.current.digit1Key.wasPressedThisFrame) TryEquip(Kind.Sword);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) TryEquip(Kind.Axe);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) TryEquip(Kind.Shotgun);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) TryEquip(Kind.MachineGun);
        if (Keyboard.current.digit5Key.wasPressedThisFrame) TryEquip(Kind.LaserGun);
    }
    public bool TryEquip(Kind kind)
    {
        if (kind<Kind.Sword || kind>Kind.LaserGun)return false;
        if (attack == null || attack.AttackInProgress || (kind == Kind.Axe && (!HasAxe || axe == null))) return false;
        if(kind>=Kind.Shotgun && (!Owns(kind) || guns[(int)kind-2]==null))return false;
        if(Equipped!=kind && gunCombat!=null)gunCombat.CancelAction();
        Equipped = kind; Refresh(); return true;
    }
    public bool CollectAxe()
    {
        if (HasAxe || axe == null || attack.AttackInProgress) return false;
        HasAxe = true; return TryEquip(Kind.Axe);
    }
    void Refresh()
    {
        hudState=int.MinValue;
        bool isAxe = Equipped == Kind.Axe;
        if (axe != null) axe.SetActive(isAxe);
        if (swordRenderers != null) foreach (var renderer in swordRenderers) if(renderer != null) renderer.enabled = Equipped==Kind.Sword;
        for(int i=0;i<guns.Length;i++)if(guns[i]!=null)guns[i].SetActive((int)Equipped==i+2);
        if (label != null)
        {
            label.text = (isAxe ? "AXE  /  WIDE & HEAVY" : "SWORD  /  QUICK SLASH") + "\n1 Sword  2 Axe  3 Shotgun  4 Machine  5 Laser" + (HasAxe ? "" : " (locked)");
            label.color = isAxe ? new Color(1f,.72f,.3f) : Color.white;
        }
    }
    void OnDestroy() { if (label != null) Destroy(label.gameObject); }
}
