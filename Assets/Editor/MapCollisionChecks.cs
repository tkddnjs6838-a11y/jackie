using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class MapCollisionChecks
{
    static string report;
    static MapCollisionChecks(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string file="output/gun-effects/collision-check.txt";
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !File.Exists(file))return;
        File.Delete(file);Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Run());
    }
    static void Check(bool value,string message)
    {
        report+=(value?"PASS: ":"FAIL: ")+message+"\n";File.WriteAllText("output/gun-effects/collision-checks.txt",report);
        if(!value)throw new Exception(message);
    }
    static IEnumerator Run()
    {
        UnityEngine.Random.InitState(876);
        report="";var sandbox=Object.FindFirstObjectByType<MapSandbox>();
        while(sandbox.Busy)yield return null;
        var flow=Object.FindFirstObjectByType<GameFlowManager>();flow.startButton.onClick.Invoke();
        var player=Object.FindFirstObjectByType<PlayerWeapons>();var spawner=Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;
        var spawn=typeof(EnemySpawner).GetMethod("TryFindSpawnPoint",BindingFlags.NonPublic|BindingFlags.Instance);
        for(int map=0;map<5;map++)
        {
            if(map!=sandbox.CurrentMap){Check(sandbox.TravelTo(map),"map travel accepted");while(sandbox.Busy)yield return null;}
            Check(MapNavigation.Current!=null,"navigation surface loaded: "+MapSandbox.Names[map]);
            int accepted=0;Vector3 enemyStart=Vector3.zero;
            for(int i=0;i<40;i++)
            {
                object[] args={Vector3.zero,.62f};
                if(!(bool)spawn.Invoke(spawner,args))continue;
                accepted++;var point=(Vector3)args[0];enemyStart=point;
                Check(MapNavigation.Sample(point,.2f,out _) && MapNavigation.Clear(point,.62f) && MapNavigation.Reachable(point),"spawn reachable/clear "+map+":"+i);
            }
            Check(accepted>0,"map has valid spawn positions");
            var terrain=Terrain.activeTerrain;var data=terrain.terrainData;int cliffs=0;
            for(int x=1;x<10;x++)for(int z=1;z<10;z++)
            {
                float u=x/10f,v=z/10f;if(data.GetSteepness(u,v)<45)continue;
                var near=terrain.transform.position+new Vector3(u*data.size.x,0,v*data.size.z);near.y=terrain.SampleHeight(near)+terrain.transform.position.y;
                if(!MapNavigation.DropPoint(near,out var floor))continue;
                Check(MapNavigation.Reachable(floor) && MapNavigation.Clear(floor,.32f),"cliff drop relocated to reachable clear floor");
                cliffs++;if(cliffs>=4)break;
            }
            var start=player.transform.position;
            if(MapNavigation.Sample(start,3,out var navStart))
            {
                int blocked=0;
                for(int i=0;i<24;i++)
                {
                    var delta=Quaternion.Euler(0,i*15,0)*Vector3.forward*20;
                    if(!NavMesh.Raycast(navStart,navStart+delta,out var edge,NavMesh.AllAreas))continue;
                    Check(MapNavigation.MovePoint(navStart,delta,out var end) && (!NavMesh.Raycast(navStart,end,out var stop,NavMesh.AllAreas) || Vector3.Distance(stop.position,end)<.01f),"movement stops before wall/cliff edge");blocked++;
                }
                Check(blocked>0,"tested real blocked directions");
            }
            var walker=Object.Instantiate(spawner.enemyPrefab,enemyStart,Quaternion.identity);
            yield return new WaitForSeconds(3);
            var enemyDebug=walker.GetComponent<Enemy>();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var desired=(Vector3)typeof(Enemy).GetMethod("ChaseDirection",flags).Invoke(enemyDebug,new object[]{player.transform.position-walker.transform.position});
            object[] castArgs={desired,.2f,default(RaycastHit)};
            bool blockedDebug=(bool)typeof(Enemy).GetMethod("FirstBlockingHit",flags).Invoke(enemyDebug,castArgs);
            var obstacle=(RaycastHit)castArgs[2];
            File.AppendAllText("output/gun-effects/nav-debug.txt", $"map={map} start={enemyStart} end={walker.transform.position} player={player.transform.position} corners={typeof(Enemy).GetField("cornerCount",flags).GetValue(enemyDebug)} dir={desired} blocked={blockedDebug} obstacle={obstacle.collider} hit={obstacle.distance}\n");
            Check(Vector3.Distance(walker.transform.position,enemyStart)>1f,"enemy follows path on map "+map);
            Check(MapNavigation.Sample(walker.transform.position,.3f,out _),"enemy remains on walkable floor");
            Object.Destroy(walker);
            yield return null;
        }
        sandbox.TravelTo(0);while(sandbox.Busy)yield return null;
        // Same crowded physics query, comparing old allocation-heavy API with replacement.
        var enemies=new System.Collections.Generic.List<GameObject>();
        for(int i=0;i<35;i++)enemies.Add(Object.Instantiate(spawner.enemyPrefab,player.transform.position+new Vector3((i%7)*.1f,0,(i/7)*.1f),Quaternion.identity));
        yield return null;Physics.SyncTransforms();
        var a=player.transform.position+Vector3.up*.5f-Vector3.forward*3;var b=a+Vector3.up;var buffer=new RaycastHit[128];
        Physics.CapsuleCastNonAlloc(a,b,.5f,Vector3.forward,buffer,6);
        Check(Physics.CapsuleCastNonAlloc(a,b,.5f,Vector3.forward,buffer,6)>0,"benchmark query hits crowded colliders");
        var first=Physics.CapsuleCastAll(a,b,.5f,Vector3.forward,6);
        var second=Physics.CapsuleCastAll(a,b,.5f,Vector3.forward,6);
        Check(first.Length>0 && !ReferenceEquals(first,second),"old crowded query creates separate hit arrays");
        var saved=buffer;
        for(int i=0;i<500;i++)Physics.CapsuleCastNonAlloc(a,b,.5f,Vector3.forward,buffer,6);
        Check(ReferenceEquals(saved,buffer),"500 crowded queries reuse one hit buffer");
        foreach(var enemy in enemies)Object.Destroy(enemy);
        yield return null;
        // A drop requested from a solid wall must be relocated before its visual is created.
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=player.transform.position+Vector3.right*3+Vector3.up*.6f;wall.transform.localScale=new Vector3(2,3,2);
        Physics.SyncTransforms();
        Check(MapNavigation.DropPoint(wall.transform.position,out var safeDrop) && MapNavigation.Clear(safeDrop,.32f),"wall drop relocates outside collider");
        HealthPickup.Spawn(wall.transform.position,20);
        var orb=Object.FindFirstObjectByType<HealthPickup>();
        Check(orb!=null && !wall.GetComponent<Collider>().bounds.Contains(orb.transform.position),"health drop is not embedded in wall");
        if(orb!=null)Object.Destroy(orb.gameObject);
        Object.Destroy(wall);
        yield return null;
        yield return new WaitForSeconds(.3f);
        var safePlayer=player.transform.position;
        wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=safePlayer+Vector3.right*2+Vector3.up*.5f;wall.transform.localScale=new Vector3(1,3,1);
        var controller=player.GetComponent<CharacterController>();controller.enabled=false;player.transform.position=wall.transform.position;controller.enabled=true;
        Physics.SyncTransforms();yield return new WaitForSeconds(.8f);
        Check(!wall.GetComponent<Collider>().bounds.Contains(player.transform.position),"embedded player leaves solid wall");
        Object.Destroy(wall);
        spawner.enabled=true;report+="COMPLETE\n";File.WriteAllText("output/gun-effects/collision-checks.txt",report);Debug.Log("MAP COLLISION CHECKS COMPLETE");
    }
}
