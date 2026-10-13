using UnityEngine;

public sealed class CharacterUpgradePickup : MonoBehaviour
{
    public static int ActiveCount {get;private set;}
    public CharacterGrowth.Kind kind;
    public float lifetime=30f;
    CharacterGrowth player;
    float age,baseY;
    bool collected;
    bool readyToCollect;
    const float CollectRadius=.9f;
    const float RearmRadius=1.5f;
    const float CollectDelay=.75f;
    public static CharacterUpgradePickup Spawn(Vector3 position,CharacterGrowth.Kind kind)
    {
        if(ActiveCount>=2)return null;
        if(Terrain.activeTerrain!=null)position.y=Terrain.activeTerrain.SampleHeight(position)+Terrain.activeTerrain.transform.position.y;
        var go=new GameObject("CharacterUpgrade_"+kind);go.SetActive(false);
        go.transform.position=position+Vector3.up*.65f;
        var pickup=go.AddComponent<CharacterUpgradePickup>();pickup.kind=kind;go.SetActive(true);return pickup;
    }
    void OnEnable()=>ActiveCount++;
    void OnDisable()=>ActiveCount--;
    void Start()
    {
        player=FindFirstObjectByType<CharacterGrowth>();baseY=transform.position.y;
        // A crate dropped under the player must be approached again, not consumed on spawn.
        readyToCollect=PlayerDistanceSquared()>RearmRadius*RearmRadius;
        PickupCrateVisual.Build(transform,PickupCrateVisual.Category.Growth,new Color(.3f,1,.4f));
    }
    void Update()
    {
        var choices=FindFirstObjectByType<UpgradeSystem>();
        // Nearby crates must not queue another interruption when the current choice closes.
        if(choices!=null && choices.Choosing && PlayerDistanceSquared()<=RearmRadius*RearmRadius)
            readyToCollect=false;
        if(Time.timeScale<=0 || RunStats.Instance==null || !RunStats.Instance.Running || collected)return;
        age+=Time.deltaTime;if(age>=lifetime){Destroy(gameObject);return;}
        transform.Rotate(0,60*Time.deltaTime,0);
        var at=transform.position;at.y=baseY+Mathf.Sin(age*3)*.08f;transform.position=at;
        if(player==null)return;
        float distanceSquared=PlayerDistanceSquared();
        if(distanceSquared>RearmRadius*RearmRadius)readyToCollect=true;
        if(!readyToCollect || age<CollectDelay || distanceSquared>CollectRadius*CollectRadius)return;
        if(choices==null || !choices.TryOffer(player))return;
        collected=true;Destroy(gameObject);
    }
    float PlayerDistanceSquared()
    {
        if(player==null)return float.PositiveInfinity;
        var delta=player.transform.position-transform.position;delta.y=0;
        return delta.sqrMagnitude;
    }
}
