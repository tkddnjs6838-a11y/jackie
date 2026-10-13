using UnityEngine;

public sealed class GunPickup:MonoBehaviour
{
    public static int ActiveCount {get;private set;}
    public PlayerWeapons.Kind kind;
    float age,baseY;
    PlayerWeapons player;
    void OnEnable()=>ActiveCount++;
    void OnDisable()=>ActiveCount--;
    void Start(){baseY=transform.position.y;player=FindFirstObjectByType<PlayerWeapons>();}
    void Update()
    {
        if(Time.timeScale<=0 || RunStats.Instance==null || !RunStats.Instance.Running)return;age+=Time.deltaTime;
        if(age>35){Destroy(gameObject);return;}
        transform.position=new Vector3(transform.position.x,baseY+Mathf.Sin(age*2.5f)*.08f,transform.position.z);transform.Rotate(0,45*Time.deltaTime,0);
        if(player==null)return;var d=player.transform.position-transform.position;d.y=0;
        if(d.sqrMagnitude<1.7f && player.CollectGun(kind)){GameAudio.Play(GameAudio.Sfx.Pickup);Destroy(gameObject);}
    }
    public static bool Spawn(Vector3 at,PlayerWeapons.Kind kind)
    {
        if(ActiveCount>=2)return false;
        var prefab=Resources.Load<GameObject>("Weapons/Guns/"+(kind==PlayerWeapons.Kind.Shotgun?"Pistol":kind.ToString()));if(prefab==null)return false;
        if(Terrain.activeTerrain!=null)at.y=Terrain.activeTerrain.SampleHeight(at)+Terrain.activeTerrain.transform.position.y;
        var go=new GameObject("WeaponCrate_"+kind);go.transform.position=at+Vector3.up*.55f;
        PickupCrateVisual.Build(go.transform,PickupCrateVisual.Category.Weapon,GunEffects.ColorFor(kind));
        var model=Instantiate(prefab,go.transform,false);model.transform.localPosition=Vector3.up*.35f;model.transform.localRotation=Quaternion.Euler(0,90,0);model.transform.localScale=Vector3.one*.6f;
        go.AddComponent<GunPickup>().kind=kind;return true;
    }
}
