using UnityEngine;

// Recover only genuine static-geometry penetration, never ordinary wall contact.
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerGroundSafety:MonoBehaviour
{
    readonly Collider[] hits=new Collider[64];
    CharacterController body;
    Vector3 lastSafe;
    bool hasSafe;
    float nextCheck,embeddedTime;
    MapNavigation map;
    void Awake(){body=GetComponent<CharacterController>();}
    void LateUpdate()
    {
        if(MapNavigation.Current==null || Time.timeScale<=0 || !body.enabled || Time.time<nextCheck)return;
        nextCheck=Time.time+.15f;
        if(map!=MapNavigation.Current){map=MapNavigation.Current;hasSafe=false;embeddedTime=0;}
        var bounds=body.bounds;
        float radius=body.radius*Mathf.Max(transform.lossyScale.x,transform.lossyScale.z)*.85f;
        var half=Mathf.Max(0,bounds.extents.y-radius);
        int count=Physics.OverlapCapsuleNonAlloc(bounds.center+Vector3.up*half,bounds.center-Vector3.up*half,radius,hits,~(1<<2),QueryTriggerInteraction.Ignore);
        bool embedded=false;
        for(int i=0;i<count;i++)
        {
            var c=hits[i];if(c==body || c is TerrainCollider || c.GetComponentInParent<Enemy>()!=null || c.transform.IsChildOf(transform))continue;
            if(Physics.ComputePenetration(body,transform.position,transform.rotation,c,c.transform.position,c.transform.rotation,out _,out float depth) && depth>.09f){embedded=true;break;}
        }
        if(!embedded)
        {
            embeddedTime=0;
            if(MapNavigation.Sample(transform.position,.65f,out var floor) && MapNavigation.Clear(floor,body.radius,body.height)){lastSafe=floor+Vector3.up*.08f;hasSafe=true;}
            return;
        }
        embeddedTime+=.15f;
        if(embeddedTime<.45f || !hasSafe)return;
        body.enabled=false;transform.position=lastSafe;body.enabled=true;embeddedTime=0;
    }
}
