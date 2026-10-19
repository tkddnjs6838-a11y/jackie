using UnityEngine;
using UnityEngine.InputSystem;

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
    readonly GameObject[] guns=new GameObject[3];
    readonly bool[] owned=new bool[3];
    PlayerGunCombat gunCombat;
    Kind? pendingEquip;
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
        if(first && !pendingEquip.HasValue)RequestEquip(kind);
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
        if(GetComponent<CombatHudView>()==null)gameObject.AddComponent<CombatHudView>();
        Refresh();
        if(WeaponTestSettings.AllGunsAtStart || MapSandboxSettings.Enabled)
        {
            if(MapSandboxSettings.Enabled)CollectAxe();
            CollectGun(Kind.Shotgun);CollectGun(Kind.MachineGun);CollectGun(Kind.LaserGun);
            TryEquip(Kind.Shotgun);
        }
    }
    void Update()
    {
        if (RunStats.Instance==null || !RunStats.Instance.Running) { pendingEquip=null; return; }
        if (Time.timeScale <= 0f) return;
        TryApplyPendingEquip();
        if (Keyboard.current == null) return;
        if (Keyboard.current.digit1Key.wasPressedThisFrame) RequestEquip(Kind.Sword);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) RequestEquip(Kind.Axe);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) RequestEquip(Kind.Shotgun);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) RequestEquip(Kind.MachineGun);
        if (Keyboard.current.digit5Key.wasPressedThisFrame) RequestEquip(Kind.LaserGun);
    }
    // A requested swap waits for the current swing; the latest valid request wins.
    public bool RequestEquip(Kind kind)
    {
        if (!Owns(kind)) return false;
        if (kind==Equipped) { pendingEquip=null; return true; }
        if (TryEquip(kind)) return true;
        pendingEquip=kind;
        return true;
    }
    public bool TryApplyPendingEquip()
    {
        if (!pendingEquip.HasValue || Time.timeScale<=0 || RunStats.Instance==null || !RunStats.Instance.Running) return false;
        return TryEquip(pendingEquip.Value);
    }
    public bool TryEquip(Kind kind)
    {
        if (kind<Kind.Sword || kind>Kind.LaserGun)return false;
        if (attack == null || attack.AttackInProgress || (kind == Kind.Axe && (!HasAxe || axe == null))) return false;
        if(kind>=Kind.Shotgun && (!Owns(kind) || guns[(int)kind-2]==null))return false;
        if(Equipped!=kind && gunCombat!=null)gunCombat.CancelAction();
        pendingEquip=null; Equipped = kind; Refresh(); return true;
    }
    public bool CollectAxe()
    {
        if (HasAxe || axe == null) return false;
        HasAxe = true;
        if (!pendingEquip.HasValue) RequestEquip(Kind.Axe);
        return true;
    }
    void Refresh()
    {
        bool isAxe = Equipped == Kind.Axe;
        if (axe != null) axe.SetActive(isAxe);
        if (swordRenderers != null) foreach (var renderer in swordRenderers) if(renderer != null) renderer.enabled = Equipped==Kind.Sword;
        for(int i=0;i<guns.Length;i++)if(guns[i]!=null)guns[i].SetActive((int)Equipped==i+2);

    }
}
