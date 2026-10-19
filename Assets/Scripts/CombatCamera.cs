using UnityEngine;

// Free-look stays owned by PlayerController; this only frames combat and avoids scenery.
[DefaultExecutionOrder(300)]
public sealed class CombatCamera : MonoBehaviour
{
    public Vector3 offset=new Vector3(.35f,2.65f,-4.6f);
    readonly RaycastHit[] obstacles=new RaycastHit[64];
    Transform player; CameraShake shake; float distance;
    void Awake(){player=GetComponentInParent<PlayerController>().transform;shake=GetComponent<CameraShake>();distance=offset.magnitude;}
    void LateUpdate()
    {
        if(player==null)return;
        Vector3 pivot=player.position+Vector3.up*1.2f;
        Vector3 delta=player.TransformPoint(offset)-pivot;
        float desired=delta.magnitude, allowed=desired;
        int count=Physics.SphereCastNonAlloc(pivot,.18f,delta/desired,obstacles,desired,~(1<<2),QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++)
        {
            var c=obstacles[i].collider;
            if(c.transform.IsChildOf(player) || c.GetComponentInParent<Enemy>()!=null)continue;
            // Invisible movement boundaries must not push the view into the player.
            if(c.name.StartsWith("Boundary"))
            {
                var boundaryRenderer=c.GetComponent<Renderer>();
                if(boundaryRenderer==null || !boundaryRenderer.enabled)continue;
            }
            allowed=Mathf.Min(allowed,Mathf.Max(.25f,obstacles[i].distance-.08f));
        }
        distance=allowed<distance?allowed:Mathf.Lerp(distance,allowed,1-Mathf.Exp(-10*Time.unscaledDeltaTime));
        transform.position=pivot+delta.normalized*distance;
        if(shake!=null)transform.localPosition+=shake.Offset;
    }
}
