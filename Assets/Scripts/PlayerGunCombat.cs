using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerWeapons))]
[DefaultExecutionOrder(100)]
public sealed class PlayerGunCombat : MonoBehaviour
{
    readonly int[] magazine=new int[3],reserve=new int[3];
    readonly RaycastHit[] hits=new RaycastHit[128];
    readonly HashSet<Enemy> damaged=new HashSet<Enemy>();
    PlayerWeapons weapons;PlayerAutoAttack attack;
    readonly RaycastHit[] aimHits=new RaycastHit[128];
    public Camera AimCamera;
    public Vector3 LastShotDirection { get; private set; }
    public Vector3 LastShotOrigin { get; private set; }
    GunReticle reticle;
    public float ChargeFraction=>charging?Mathf.Clamp01(charge/.4f):0;
    public float ReloadFraction=>reload>0?1-reload/ReloadTime(weapons.Equipped):0;
    public bool ReticleVisible=>reticle!=null && reticle.Visible;
    public Vector3 AimPoint()
    {
        var camera=AimCamera!=null?AimCamera:Camera.main;
        if(camera==null)return transform.position+Vector3.up*.7f+transform.forward*30;
        var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
        int count=Physics.RaycastNonAlloc(ray,aimHits,60,~0,QueryTriggerInteraction.Ignore);
        float nearest=60;Vector3 point=ray.GetPoint(60);
        for(int i=0;i<count;i++)
        {
            var hit=aimHits[i];if(hit.collider.transform.IsChildOf(transform))continue;
            var enemy=hit.collider.GetComponentInParent<Enemy>();if(enemy!=null && enemy.IsDying)continue;
            if(Vector3.Dot(hit.point-transform.position,camera.transform.forward)<0)continue;
            if(hit.distance<nearest){nearest=hit.distance;point=hit.point;}
        }
        return point;
    }
    float cooldown,reload,charge,recoil,chargeFx;
    bool charging,externalTrigger,pendingShot;
    public bool ReadInput=true;
    public int ShotsFired { get; private set; }
    public int LastHitCount { get; private set; }
    public bool IsReloading=>reload>0;
    public bool IsCharging=>charging;
    int Index=>(int)weapons.Equipped-2;
    public int Ammo=>weapons.IsGun?magazine[Index]:0;
    public int Reserve=>weapons.IsGun?reserve[Index]:0;
    public static int Capacity(PlayerWeapons.Kind kind)=>kind==PlayerWeapons.Kind.Shotgun?2:kind==PlayerWeapons.Kind.MachineGun?30:3;
    static float ReloadTime(PlayerWeapons.Kind kind)=>kind==PlayerWeapons.Kind.Shotgun?1.5f:kind==PlayerWeapons.Kind.MachineGun?1.8f:2.2f;
    public float FireInterval => (weapons.Equipped==PlayerWeapons.Kind.LaserGun?.6f:weapons.Equipped==PlayerWeapons.Kind.MachineGun?.105f:.7f)*(GetComponent<PowerUpEffects>()?.CooldownMultiplier ?? 1f)*(GetComponent<CharacterGrowth>()?.CooldownMultiplier ?? 1f);
    bool Running=>Time.timeScale>0 && RunStats.Instance!=null && RunStats.Instance.Running;
    void Awake(){weapons=GetComponent<PlayerWeapons>();attack=GetComponent<PlayerAutoAttack>();}
    void Start(){reticle=GunReticle.Create(this,weapons);}
    public bool ReserveFull(PlayerWeapons.Kind kind)
    {
        int i=(int)kind-2;
        return i>=0 && i<reserve.Length && reserve[i]>=Capacity(kind)*4;
    }
    public bool AddAmmo(PlayerWeapons.Kind kind,bool first)
    {
        int i=(int)kind-2;
        if(i<0 || i>=magazine.Length)return false;
        int capacity=Capacity(kind),previous=magazine[i]+reserve[i];
        if(first)magazine[i]=capacity;
        reserve[i]=Mathf.Min(capacity*4,reserve[i]+capacity*(kind==PlayerWeapons.Kind.Shotgun?4:2));
        return magazine[i]+reserve[i]>previous;
    }
    public void SetTrigger(bool held)=>externalTrigger=held;
    public void CancelAction(){pendingShot=false;reload=0;charge=0;charging=false;externalTrigger=false;recoil=0;}
    public void BeginReload()
    {
        if(MapSandboxSettings.Enabled || !Running || !weapons.IsGun || reload>0 || magazine[Index]>=Capacity(weapons.Equipped) || reserve[Index]<=0)return;
        charging=false;charge=0;reload=ReloadTime(weapons.Equipped);GameAudio.Play(GameAudio.Sfx.GunReload);
    }
    void Update()
    {
        if(!Running){if(RunStats.Instance==null || !RunStats.Instance.Running)CancelAction();return;}
        cooldown=Mathf.Max(0,cooldown-Time.deltaTime);recoil=Mathf.MoveTowards(recoil,0,Time.deltaTime*(weapons.Equipped==PlayerWeapons.Kind.Shotgun?5f:8f));
        if(!weapons.IsGun)return;
        if(reload>0)
        {
            reload-=Time.deltaTime;
            if(reload<=0){int count=Mathf.Min(Capacity(weapons.Equipped)-magazine[Index],reserve[Index]);magazine[Index]+=count;reserve[Index]-=count;}
            return;
        }
        if(ReadInput && Keyboard.current!=null && Keyboard.current.rKey.wasPressedThisFrame){BeginReload();return;}
        // An exhausted gun must not leave the player stranded after releasing fire.
        if(magazine[Index]==0 && reserve[Index]==0 && cooldown<=0)
        {
            weapons.TryEquip(weapons.HasAxe?PlayerWeapons.Kind.Axe:PlayerWeapons.Kind.Sword);
            return;
        }
        bool held=externalTrigger || (ReadInput && (MobileControls.Enabled ? MobileControls.Firing : Mouse.current!=null && Mouse.current.leftButton.isPressed));
        if(!held){charging=false;charge=0;return;}
        if(cooldown>0)return;
        if(magazine[Index]<=0)
        {
            if(reserve[Index]>0)BeginReload();else weapons.TryEquip(weapons.HasAxe?PlayerWeapons.Kind.Axe:PlayerWeapons.Kind.Sword);
            return;
        }
        if(weapons.Equipped==PlayerWeapons.Kind.LaserGun)
        {
            if(!charging){charging=true;charge=0;GameAudio.Play(GameAudio.Sfx.LaserCharge);}
            charge+=Time.deltaTime/Mathf.Max(.1f,(GetComponent<PowerUpEffects>()?.CooldownMultiplier ?? 1f)*(GetComponent<CharacterGrowth>()?.CooldownMultiplier ?? 1f));chargeFx-=Time.deltaTime;
            if(chargeFx<=0){GunEffects.Charge(weapons.GunMuzzle,charge/.4f);chargeFx=.045f;}
            if(charge<.4f)return;
            charging=false;charge=0;
        }
        pendingShot=true;
    }
    void LateUpdate()
    {
        if(!pendingShot)return;
        pendingShot=false;
        if(Running && weapons.IsGun && !IsReloading)Fire();
    }
    public Vector3 PoseDirection()
    {
        // Near targets must not rotate the wrist backwards/upwards around the muzzle.
        var shoulder=ToyCharacterRig.Bone(attack.animator,HumanBodyBones.RightUpperArm);
        var origin=shoulder!=null?shoulder.position:transform.position+Vector3.up*.7f;
        var local=transform.InverseTransformDirection(AimPoint()-origin);
        float forward=Mathf.Max(1.2f,local.z);
        float yaw=Mathf.Clamp(Mathf.Atan2(local.x,forward)*Mathf.Rad2Deg,-25,25);
        float pitch=Mathf.Clamp(Mathf.Atan2(local.y,forward)*Mathf.Rad2Deg,-25,25);
        return transform.rotation*Quaternion.Euler(-pitch,yaw,0)*Vector3.forward;
    }
    public void ApplyPose()
    {
        SwordArmPose.Apply(attack.animator,transform,attack.swordModel,attack.gripIdleEuler,0,false,false,true,recoil,PoseDirection());
        if(attack.handGrip!=null)attack.handGrip.Apply(attack.swordModel);
        var hand=ToyCharacterRig.Bone(attack.animator,HumanBodyBones.RightHand);
        if(hand!=null && weapons.GunModel!=null)weapons.GunModel.position=hand.position;
    }
    void Fire()
    {
        if(weapons.GunMuzzle==null)return;
        var kind=weapons.Equipped;bool laser=kind==PlayerWeapons.Kind.LaserGun,auto=kind==PlayerWeapons.Kind.MachineGun;
        recoil=laser?1f:auto?.22f:1.8f;
        ApplyPose();var muzzle=weapons.GunMuzzle;
        if(!MapSandboxSettings.Enabled)magazine[Index]--;ShotsFired++;
        cooldown=FireInterval;
        var origin=muzzle.position;var aimPoint=AimPoint();var direction=(aimPoint-origin).normalized;
        bool nearTarget=Vector3.Dot(aimPoint-origin,transform.forward)<.15f;
        if(auto)direction=Quaternion.AngleAxis(Random.Range(-3f,3f),Vector3.up)*direction;
        float range=laser?24f:18f;Vector3 end=origin+direction*range;
        // Check the short shoulder-to-muzzle segment, then trace from the muzzle to the reticle.
        Vector3 shoulder=new Vector3(transform.position.x,origin.y,transform.position.z);
        Vector3 barrel=origin-shoulder;float obstructionDistance=barrel.magnitude;
        int blockers=Physics.RaycastNonAlloc(shoulder,barrel.normalized,aimHits,obstructionDistance,~0,QueryTriggerInteraction.Ignore);
        Vector3 castOrigin=origin;
        bool obstructed=false;
        for(int i=0;i<blockers;i++)
        {
            var hit=aimHits[i];if(hit.collider.transform.IsChildOf(transform))continue;
            if(hit.distance<=obstructionDistance){obstructionDistance=hit.distance;end=hit.point;obstructed=true;}
        }
        if(obstructed){castOrigin=shoulder;direction=barrel.normalized;range=obstructionDistance+.005f;}
        else if(nearTarget){castOrigin=shoulder;direction=(aimPoint-shoulder).normalized;end=castOrigin+direction*range;}
        LastShotOrigin=castOrigin;LastShotDirection=direction;
        if(kind==PlayerWeapons.Kind.Shotgun)
        {
            FireShotgun(castOrigin,direction,obstructed?range:10f);
            FinishShot(kind,muzzle,laser,auto);
            return;
        }
        int count=Physics.RaycastNonAlloc(castOrigin,direction,hits,range,~0,QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits,0,count,HitComparer.Instance);damaged.Clear();LastHitCount=0;
        for(int i=0;i<count;i++)
        {
            var hit=hits[i];if(hit.collider.transform.IsChildOf(transform))continue;
            var enemy=hit.collider.GetComponentInParent<Enemy>();
            if(enemy!=null && enemy.IsDying)continue;
            if(enemy!=null)
            {
                if(!damaged.Add(enemy))continue;
                var buffs=GetComponent<PowerUpEffects>();float damage=(laser?95f:auto?12f:36f)*(GetComponent<CharacterGrowth>()?.DamageMultiplier ?? 1f)*(buffs!=null?buffs.DamageMultiplier:1f);
                enemy.TakeDamage(damage,hit.point,laser?1.25f:auto?.32f:.7f);LastHitCount++;
                GunEffects.Impact(kind,hit.point,hit.normal);
                if(laser)continue;
            }
            else GunEffects.Impact(kind,hit.point,hit.normal);
            end=hit.point;break;
        }
        GunEffects.Beam(kind,origin,end);
        FinishShot(kind,muzzle,laser,auto);
    }
    void FinishShot(PlayerWeapons.Kind kind,Transform muzzle,bool laser,bool auto)
    {
        RunStats.Instance.RegisterClick();if(LastHitCount>0)RunStats.Instance.RegisterHit();
        GunEffects.Muzzle(kind,muzzle);
        if(reticle!=null)reticle.Shot(LastHitCount>0);
        GameAudio.Play(laser?GameAudio.Sfx.LaserShot:auto?GameAudio.Sfx.MachineShot:GameAudio.Sfx.PistolShot);
        if(LastHitCount>0)GameAudio.Play(laser?GameAudio.Sfx.LaserImpact:auto?GameAudio.Sfx.MachineImpact:GameAudio.Sfx.PistolImpact);
        if(CameraShake.Instance!=null)CameraShake.Instance.Shake(laser?.12f:auto?.05f:.12f,laser?.045f:auto?.003f:.035f);
        if(magazine[Index]==0 && reserve[Index]>0)BeginReload();
    }
    struct PelletHit { public float damage,distance; public Vector3 point,normal; }
    readonly Dictionary<Enemy,PelletHit> pelletHits=new Dictionary<Enemy,PelletHit>();
    public static float ShotgunPelletDamage(float distance)=>Mathf.Lerp(15f,4f,Mathf.InverseLerp(2f,10f,distance));
    void FireShotgun(Vector3 origin,Vector3 forward,float range)
    {
        pelletHits.Clear();LastHitCount=0;
        var rotation=Quaternion.LookRotation(forward,Vector3.up);
        // Nine deterministic pellets cover a broad horizontal fan, including its centre.
        for(int pellet=0;pellet<9;pellet++)
        {
            float yaw=(pellet-4)*4.5f;
            float pitch=pellet==4?0:(pellet%2==0?2f:-2f);
            var direction=rotation*Quaternion.Euler(pitch,yaw,0)*Vector3.forward;
            var end=origin+direction*range;
            int count=Physics.RaycastNonAlloc(origin,direction,hits,range,~0,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,0,count,HitComparer.Instance);
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];if(hit.collider.transform.IsChildOf(transform))continue;
                var enemy=hit.collider.GetComponentInParent<Enemy>();
                if(enemy!=null && enemy.IsDying)continue;
                end=hit.point;
                if(enemy!=null)
                {
                    pelletHits.TryGetValue(enemy,out var data);
                    data.damage+=ShotgunPelletDamage(hit.distance);
                    data.distance=hit.distance;data.point=hit.point;data.normal=hit.normal;
                    pelletHits[enemy]=data;
                }
                break; // Each pellet stops at the first surface, including walls.
            }
            if(pellet%2==0)GunEffects.Beam(PlayerWeapons.Kind.Shotgun,origin,end);
        }
        var buffs=GetComponent<PowerUpEffects>();
        foreach(var pair in pelletHits)
        {
            var data=pair.Value;var enemy=pair.Key;
            // Aggregate both damage and impulse: one response per enemy, never per pellet.
            enemy.ApplyKnockback(enemy.transform.position-origin,Mathf.Lerp(11f,4f,data.distance/10f));
            enemy.TakeDamage(data.damage*(GetComponent<CharacterGrowth>()?.DamageMultiplier ?? 1f)*(buffs!=null?buffs.DamageMultiplier:1f),data.point,1.1f);
            GunEffects.Impact(PlayerWeapons.Kind.Shotgun,data.point,data.normal);LastHitCount++;
        }
    }
    sealed class HitComparer:IComparer<RaycastHit>{public static readonly HitComparer Instance=new HitComparer();public int Compare(RaycastHit a,RaycastHit b)=>a.distance.CompareTo(b.distance);}
    void OnDisable()=>CancelAction();
    void OnDestroy(){if(reticle!=null)Destroy(reticle.transform.root.gameObject);}
}
