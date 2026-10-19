using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using System.Collections;
using System.Reflection;
using System.IO;
using Object=UnityEngine.Object;
public static class PortalNavigationChecks
{
    static readonly BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Run(){Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Check());}
    static IEnumerator Check()
    {
        const string output="output/portal-navigation/checks.txt";File.WriteAllText(output,"RUNNING\n");
        var map=Object.FindFirstObjectByType<MapSandbox>();while(map.Busy)yield return null;
        var flow=Object.FindFirstObjectByType<GameFlowManager>();if(!RunStats.Instance.Running)flow.startButton.onClick.Invoke();
        var player=Object.FindFirstObjectByType<PlayerController>();player.GetComponent<PlayerHealth>().GrantInvincibility(10000);
        var random=Random.state;
        for(int stage=0;stage<5;stage++)
        {
            if(stage!=map.CurrentMap){map.StartCoroutine((IEnumerator)typeof(MapSandbox).GetMethod("ChangeMap",P).Invoke(map,new object[]{stage}));while(map.Busy)yield return null;}
            var spawner=Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;
            foreach(var e in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))Object.Destroy(e.gameObject);
            player.enabled=false;player.GetComponent<CharacterController>().enabled=false;
            int success=0;var positions=new System.Collections.Generic.List<Vector3>();
            for(int i=0;i<40;i++){if(MapNavigation.PortalPoint(map.Arrival,out var pos,out var face)){success++;positions.Add(pos);}}
            float spread=0;foreach(var a in positions)foreach(var b in positions)spread=Mathf.Max(spread,Vector3.Distance(a,b));
            File.AppendAllText(output,(success==40&&spread>8?"PASS":"FAIL")+": portal map "+stage+" valid="+success+"/40 spread="+spread+"\n");
            if(stage==1)
            {
                int cases=0;
                var path=new NavMeshPath();
                for(int x=-24;x<=24&&cases<8;x+=4)for(int z=-24;z<=24&&cases<8;z+=4)
                {
                    var near=new Vector3(x,0,z);near.y=Terrain.activeTerrain.SampleHeight(near)+Terrain.activeTerrain.transform.position.y;
                    if(!MapNavigation.Sample(near,.5f,out var start)||!MapNavigation.Clear(start,.62f))continue;
                    for(int angle=0;angle<8&&cases<8;angle++)
                    {
                        var to=start+Quaternion.Euler(0,angle*45,0)*Vector3.forward*10;to.y=Terrain.activeTerrain.SampleHeight(to)+Terrain.activeTerrain.transform.position.y;
                        if(!MapNavigation.Sample(to,.5f,out var goal)||!MapNavigation.Clear(goal,.62f)||!NavMesh.Raycast(start,goal,out _,NavMesh.AllAreas)||!NavMesh.CalculatePath(start,goal,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete||path.corners.Length<3)continue;
                        float length=0;for(int c=1;c<path.corners.Length;c++)length+=Vector3.Distance(path.corners[c-1],path.corners[c]);if(length>25)continue;
                        var enemy=Object.Instantiate(spawner.enemyPrefab,start,Quaternion.identity).GetComponent<Enemy>();enemy.enabled=false;
                        player.transform.position=goal;Physics.SyncTransforms();yield return null;int stalled=0;float elapsed=0;
                        for(int step=0;step<40000;step++)
                        {
                            typeof(Enemy).GetField("nextPath",P).SetValue(enemy,0f);
                            var before=enemy.transform.position;var dir=enemy.DirectionTo(goal);
                            float dt=Mathf.Max(Time.deltaTime,.001f);enemy.MoveAvoidingObstacles(dir*enemy.moveSpeed*dt);Physics.SyncTransforms();elapsed+=dt;
                            if(Vector3.Distance(enemy.transform.position,goal)<.8f||elapsed>35)break;
                            stalled=(enemy.transform.position-before).sqrMagnitude<.0000001f?stalled+1:0;if(stalled>8)break;
                        }
                        float gap=Vector3.Distance(enemy.transform.position,goal);var dir2=enemy.DirectionTo(goal);object[] args={dir2,.15f,default(RaycastHit)};bool blocked=(bool)typeof(Enemy).GetMethod("FirstBlockingHit",P).Invoke(enemy,args);var hit=(RaycastHit)args[2];
                        File.AppendAllText(output,(gap<.8f?"PASS":"FAIL")+": canyon route "+cases+" start="+start+" goal="+goal+" end="+enemy.transform.position+" gap="+gap+" elapsed="+elapsed+" stalled="+stalled+" blocker="+(hit.collider?hit.collider.name:"none")+" normal="+hit.normal+"\n");
                        Object.Destroy(enemy.gameObject);cases++;yield return null;
                    }
                }
                File.AppendAllText(output,"Canyon routes="+cases+"\n");
            }
            player.GetComponent<CharacterController>().enabled=true;player.enabled=true;yield return null;
        }
        Random.state=random;File.AppendAllText(output,"COMPLETE\n");Debug.Log("PORTAL NAVIGATION CHECKS COMPLETE");
    }
}
