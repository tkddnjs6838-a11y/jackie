using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ChargerNavigationChecks
{
    static ChargerNavigationChecks(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="output/gun-effects/charger-navigation.txt";
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !File.Exists(request))return;
        File.Delete(request);
        Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Run());
    }
    static IEnumerator Run()
    {
        const string report="output/gun-effects/charger-navigation-results.txt";
        File.WriteAllText(report,"RUNNING\n");
        var sandbox=Object.FindFirstObjectByType<MapSandbox>();
        while(sandbox.Busy)yield return null;
        Object.FindFirstObjectByType<GameFlowManager>().startButton.onClick.Invoke();
        var spawner=Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;
        foreach(int map in new[]{1,3,4})
        {
        sandbox.TravelTo(map);while(sandbox.Busy)yield return null;
        var player=Object.FindFirstObjectByType<PlayerController>();
        var controller=player.GetComponent<CharacterController>();
        player.enabled=false;controller.enabled=false;
        var original=player.transform.position;
        var path=new NavMeshPath();Vector3 start=default,goal=default;bool found=false;
        // Locate a real crater structure which blocks a straight line but has a ground route around it.
        for(int x=-24;x<=24 && !found;x+=2)for(int z=-24;z<=24 && !found;z+=2)
        {
            var near=new Vector3(x,0,z);near.y=Terrain.activeTerrain.SampleHeight(near)+Terrain.activeTerrain.transform.position.y;
            if(!MapNavigation.Sample(near,.5f,out var a) || !MapNavigation.Clear(a,.7f))continue;
            for(int i=0;i<8;i++)
            {
                var b=a+Quaternion.Euler(0,i*45,0)*Vector3.forward*8;
                b.y=Terrain.activeTerrain.SampleHeight(b)+Terrain.activeTerrain.transform.position.y;
                if(!MapNavigation.Sample(b,.5f,out b) || !MapNavigation.Clear(b,.7f) || Mathf.Abs(a.y-b.y)>1)continue;
                if(!NavMesh.Raycast(a,b,out _,NavMesh.AllAreas) || !NavMesh.CalculatePath(a,b,NavMesh.AllAreas,path) || path.status!=NavMeshPathStatus.PathComplete || path.corners.Length<3)continue;
                float length=0;var corners=path.corners;for(int j=1;j<corners.Length;j++)length+=Vector3.Distance(corners[j-1],corners[j]);
                if(length>15)continue;
                start=a;goal=b;found=true;break;
            }
        }
        if(!found)throw new Exception("No crater obstacle test route found");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnemyCharger.prefab");
        var instance=Object.Instantiate(prefab,start,Quaternion.identity);
        var charger=instance.GetComponent<EnemyCharger>();charger.restBetweenCharges=100;
        typeof(EnemyCharger).GetField("timer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(charger,100f);
        player.transform.position=goal;
        float end=Time.time+12;float gap=99;bool reached=false;
        while(Time.time<end)
        {
            var delta=instance.transform.position-goal;delta.y=0;gap=delta.magnitude;
            if(Mathf.Abs(gap-charger.preferredDistance)<2.5f && !NavMesh.Raycast(instance.transform.position,goal,out _,NavMesh.AllAreas)){reached=true;break;}yield return null;
        }
        File.AppendAllText(report,$"{(reached?"PASS":"FAIL")}: charger repositions around structure on map {map}; start={start}, goal={goal}, end={instance.transform.position}, gap={gap}\n");
        if(!reached)
        {
            var e=instance.GetComponent<Enemy>();var direction=e.DirectionTo(goal);
            object[] args={direction,.1f,default(RaycastHit)};
            bool blocked=(bool)typeof(Enemy).GetMethod("FirstBlockingHit",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(e,args);
            var hit=(RaycastHit)args[2];
            MapNavigation.MovePoint(instance.transform.position,direction*.1f,out var next);
            var cf=(Vector3[])typeof(Enemy).GetField("chaseCorners",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(e);
            File.AppendAllText(report,$"next={next} corners={string.Join(",",cf)}\n");
            File.AppendAllText(report,$"blocked={blocked}, obstacle={hit.collider}, normal={hit.normal}, dir={direction}, scale={instance.transform.lossyScale}\n");
        }
        if(reached)
        {
            var near=Vector3.MoveTowards(instance.transform.position,goal,2f);
            if(!MapNavigation.Sample(near,1,out near))throw new Exception("Missing close-range test point");
            player.transform.position=near;end=Time.time+12;bool escaped=false;
            while(Time.time<end)
            {
                var distance=instance.transform.position-near;distance.y=0;
                if(distance.magnitude>6.5f && !NavMesh.Raycast(instance.transform.position,near,out _,NavMesh.AllAreas)){escaped=true;break;}
                yield return null;
            }
            File.AppendAllText(report,$"{(escaped?"PASS":"FAIL")}: close-range retreat on map {map}\n");
            reached=escaped;
        }
        Object.Destroy(instance);player.transform.position=original;controller.enabled=true;player.enabled=true;
        if(!reached)throw new Exception("Charger stuck on obstacle route");
        }
        spawner.enabled=true;
        File.AppendAllText(report,"COMPLETE\n");Debug.Log("CHARGER NAVIGATION CHECKS COMPLETE");
    }
}
