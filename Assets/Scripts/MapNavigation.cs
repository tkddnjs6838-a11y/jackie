using UnityEngine;
using UnityEngine.AI;

// One baked walkable surface per design map, shared by spawn, drops and movement.
public sealed class MapNavigation : MonoBehaviour
{
    public const float StepHeight=1f;
    public const float WalkableNormalY=0.574f; // cos(55 degrees), matches baked agent settings.
    public static MapNavigation Current { get; private set; }
    static NavMeshPath reachPath;
    static readonly Collider[] overlaps=new Collider[128];
    Transform player;
    void OnEnable(){Current=this;if(reachPath==null)reachPath=new NavMeshPath();}
    void OnDisable(){if(Current==this)Current=null;}
    Transform Player
    {
        get { if(player==null){var p=GameObject.FindGameObjectWithTag("Player");if(p!=null)player=p.transform;}return player; }
    }
    public static bool Sample(Vector3 near,float distance,out Vector3 point)
    {
        point=near;
        if(Current==null)return false;
        if(WaterFloat.NavigationPoint(near,out var waterPoint))near=waterPoint;
        if(!NavMesh.SamplePosition(near,out var hit,distance,NavMesh.AllAreas))return false;
        point=hit.position;return true;
    }
    public static bool Clear(Vector3 point,float radius,float height=1.3f)
    {
        int count=Physics.OverlapCapsuleNonAlloc(point+Vector3.up*(radius+.08f),point+Vector3.up*Mathf.Max(radius+.08f,height-radius),radius,overlaps,~(1<<2),QueryTriggerInteraction.Ignore);
        if(count==overlaps.Length)return false;
        for(int i=0;i<count;i++)
        {
            var c=overlaps[i];
            if(c is TerrainCollider || c is CharacterController || c.GetComponentInParent<Enemy>()!=null)continue;
            return false;
        }
        return true;
    }
    public static bool Reachable(Vector3 point)
    {
        if(Current==null)return true;
        var p=Current.Player;
        return p!=null && Sample(p.position,3,out var from) && NavMesh.CalculatePath(from,point,NavMesh.AllAreas,reachPath) && reachPath.status==NavMeshPathStatus.PathComplete;
    }
    public static bool SpawnPoint(Vector3 near,float radius,out Vector3 point)
    {
        point=near;
        if(Current!=null)return Sample(near,2,out point) && Clear(point,radius) && Reachable(point);
        var terrain=Terrain.activeTerrain;
        if(terrain!=null)
        {
            var local=near-terrain.transform.position;var size=terrain.terrainData.size;
            if(local.x<0 || local.z<0 || local.x>size.x || local.z>size.z)return false;
            if(terrain.terrainData.GetSteepness(local.x/size.x,local.z/size.z)>40)return false;
            point.y=terrain.SampleHeight(point)+terrain.transform.position.y;
        }
        return Clear(point,radius);
    }
    public static bool DropPoint(Vector3 near,out Vector3 point)
    {
        point=near;
        if(Current==null)return SpawnPoint(near,.3f,out point);
        // Prefer the death location, then search toward the player's reachable floor.
        var p=Current.Player;if(p==null)return false;
        for(int i=0;i<=12;i++)
        {
            var candidate=Vector3.Lerp(near,p.position,i/12f);
            if(Sample(candidate,2.5f,out point) && Clear(point,.32f) && Reachable(point))return true;
        }
        return false;
    }
    public static bool MovePoint(Vector3 from,Vector3 delta,out Vector3 point)
    {
        point=from;
        if(!Sample(from,.85f,out var start))return false;
        var target=start+delta;
        // Ray endpoints must lie on the sloped navigation surface, not on a
        // horizontal plane through the previous foot position.
        bool onSurface=Sample(target,.6f,out var projected);
        if(onSurface)target=projected;
        // NavMesh.Raycast stops at cliffs and walls instead of lifting onto their tops.
        if(NavMesh.Raycast(start,target,out var edge,NavMesh.AllAreas))target=edge.position;
        else if(!onSurface)return false;
        if(!Sample(target,.6f,out point))return false;
        return true;
    }
    public static bool CanCollect(Vector3 from,Vector3 playerPosition)
    {
        if(Mathf.Abs(from.y-playerPosition.y)>2.2f)return false;
        return Current==null || (Sample(from,1.5f,out var a) && Sample(playerPosition,2,out var b) && !NavMesh.Raycast(a,b,out _,NavMesh.AllAreas));
    }
}
