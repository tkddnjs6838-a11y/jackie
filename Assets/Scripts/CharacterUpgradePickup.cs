using UnityEngine;
using UnityEngine.Rendering;

public sealed class CharacterUpgradePickup : MonoBehaviour
{
    public static int ActiveCount {get;private set;}
    public CharacterGrowth.Kind kind;
    public float lifetime=30f;
    CharacterGrowth player;
    float age,baseY;
    bool collected;
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
        PickupCrateVisual.Build(transform,PickupCrateVisual.Category.Growth,new Color(.3f,1,.4f));
    }
    void Update()
    {
        if(Time.timeScale<=0 || RunStats.Instance==null || !RunStats.Instance.Running || collected)return;
        age+=Time.deltaTime;if(age>=lifetime){Destroy(gameObject);return;}
        transform.Rotate(0,60*Time.deltaTime,0);
        var at=transform.position;at.y=baseY+Mathf.Sin(age*3)*.08f;transform.position=at;
        if(player==null)return;
        var delta=player.transform.position-at;delta.y=0;
        if(delta.sqrMagnitude>1.3f*1.3f)return;
        var choices=FindFirstObjectByType<UpgradeSystem>();
        if(choices==null || !choices.TryOffer(player))return;
        collected=true;Destroy(gameObject);
    }
}
