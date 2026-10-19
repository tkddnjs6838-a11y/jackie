using UnityEngine;

public sealed class CharacterUpgradePickup : MonoBehaviour
{
    public static int ActiveCount {get;private set;}
    public CharacterGrowth.Kind kind;
    public float lifetime=30f;
    CharacterGrowth player;
    float age,baseY;
    bool collected;
    float collectCooldown;
    const float CollectRadius=.9f;
    const float ChoiceRecoveryDelay=1.5f;
    const float CollectDelay=.75f;
    public static CharacterUpgradePickup Spawn(Vector3 position,CharacterGrowth.Kind kind)
    {
        if(ActiveCount>=2)return null;
        if(!MapNavigation.DropPoint(position,out position))return null;
        var go=new GameObject("CharacterUpgrade_"+kind);go.SetActive(false);
        go.transform.position=position+Vector3.up*.65f;
        var pickup=go.AddComponent<CharacterUpgradePickup>();pickup.kind=kind;go.SetActive(true);return pickup;
    }
    void OnEnable()=>ActiveCount++;
    void OnDisable()=>ActiveCount--;
    void Start()
    {
        player=FindFirstObjectByType<CharacterGrowth>();baseY=transform.position.y;
        // Allow drops at the player's feet after a short spawn delay.
        collectCooldown=CollectDelay;
        PickupCrateVisual.Build(transform,PickupCrateVisual.Category.Growth,new Color(.3f,1,.4f));
    }
    void Update()
    {
        var choices=FindFirstObjectByType<UpgradeSystem>();
        // Space out choice screens without requiring the player to leave and return.
        if(choices!=null && choices.Choosing)
        {
            collectCooldown=ChoiceRecoveryDelay;
            return;
        }
        if(Time.timeScale<=0 || RunStats.Instance==null || !RunStats.Instance.Running || collected)return;
        collectCooldown=Mathf.Max(0,collectCooldown-Time.deltaTime);
        age+=Time.deltaTime;if(age>=lifetime){Destroy(gameObject);return;}
        transform.Rotate(0,60*Time.deltaTime,0);
        var at=transform.position;at.y=baseY+Mathf.Sin(age*3)*.08f;transform.position=at;
        if(player==null)return;
        float distanceSquared=PlayerDistanceSquared();
        if(collectCooldown>0 || distanceSquared>CollectRadius*CollectRadius)return;
        if(!MapNavigation.CanCollect(transform.position,player.transform.position) || choices==null || !choices.TryOffer(player))return;
        collected=true;Destroy(gameObject);
    }
    float PlayerDistanceSquared()
    {
        if(player==null)return float.PositiveInfinity;
        var delta=player.transform.position-transform.position;delta.y=0;
        return delta.sqrMagnitude;
    }
}
