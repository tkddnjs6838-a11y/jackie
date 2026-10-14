using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class WaterMovementChecks
{
    static string report;
    static WaterMovementChecks(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="output/gun-effects/water-check.txt";
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !File.Exists(request))return;
        File.Delete(request);Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Run());
    }
    static void Check(bool value,string message)
    {
        report+=(value?"PASS: ":"FAIL: ")+message+"\n";File.WriteAllText("output/gun-effects/water-checks.txt",report);
        if(!value)throw new Exception(message);
    }
    static bool Pair(WaterFloat water,out Vector3 wet,out Vector3 dry)
    {
        wet=dry=Vector3.zero;var terrain=Terrain.activeTerrain;
        for(float x=-28;x<=28;x+=2)for(float z=-28;z<=28;z+=2)
        {
            var p=new Vector3(x,water.NavigationHeight,z);
            if(terrain.SampleHeight(p)+terrain.transform.position.y>water.NavigationHeight-(water.gameObject.scene.name.Contains("Meadow")?.9f:.25f))continue;
            if(!MapNavigation.Sample(p,.3f,out var w) || !MapNavigation.Clear(w,.6f))continue;
            for(int i=0;i<16;i++)
            {
                var q=w+Quaternion.Euler(0,i*22.5f,0)*Vector3.forward*6;
                q.y=terrain.SampleHeight(q)+terrain.transform.position.y;
                if(q.y<water.Waterline+.2f || !MapNavigation.Sample(q,.5f,out var d) || !MapNavigation.Clear(d,.6f))continue;
                if(NavMesh.Raycast(w,d,out _,NavMesh.AllAreas))continue;
                wet=w;dry=d;return true;
            }
        }
        return false;
    }
    static IEnumerator Run()
    {
        report="";var sandbox=Object.FindFirstObjectByType<MapSandbox>();while(sandbox.Busy)yield return null;
        Object.FindFirstObjectByType<GameFlowManager>().startButton.onClick.Invoke();
        var spawner=Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;
        var player=Object.FindFirstObjectByType<PlayerWeapons>();var controller=player.GetComponent<CharacterController>();
        var movement=player.GetComponent<PlayerController>();movement.enabled=false;
        foreach(int map in new[]{0,2})
        {
            if(sandbox.CurrentMap!=map){sandbox.TravelTo(map);while(sandbox.Busy)yield return null;}
            var water=Object.FindFirstObjectByType<WaterFloat>();Check(water!=null,"water volume loaded "+map);
            Check(Pair(water,out var wet,out var dry),"connected deep-water / shore pair "+map);
            Check(MapNavigation.Sample(new Vector3(wet.x,water.Waterline-.2f,wet.z),.3f,out _),"floating height resolves onto water navigation plane");
            controller.enabled=false;player.transform.position=dry+Vector3.up*.1f;
            var enemy=Object.Instantiate(spawner.enemyPrefab,wet,Quaternion.identity);
            yield return new WaitForSeconds(.3f);
            var before=enemy.transform.position;
            yield return new WaitForSeconds(1);
            var moved=enemy.transform.position-before;moved.y=0;
            Check(moved.magnitude>.8f,"floating enemy continues moving "+map);
            float deadline=Time.time+8;
            while(Vector2.Distance(new Vector2(enemy.transform.position.x,enemy.transform.position.z),new Vector2(dry.x,dry.z))>1.4f && Time.time<deadline)yield return null;
            Check(Vector2.Distance(new Vector2(enemy.transform.position.x,enemy.transform.position.z),new Vector2(dry.x,dry.z))<1.4f,"enemy exits water onto shore "+map);
            player.transform.position=wet+Vector3.up*.1f;
            deadline=Time.time+8;
            while(Vector2.Distance(new Vector2(enemy.transform.position.x,enemy.transform.position.z),new Vector2(wet.x,wet.z))>1.4f && Time.time<deadline)yield return null;
            Check(Vector2.Distance(new Vector2(enemy.transform.position.x,enemy.transform.position.z),new Vector2(wet.x,wet.z))<1.4f,"enemy follows player back into water "+map);
            Check(enemy.transform.position.y>=Terrain.activeTerrain.SampleHeight(enemy.transform.position)+Terrain.activeTerrain.transform.position.y-.1f,"returning enemy remains above riverbed "+map);
            Object.Destroy(enemy);
        }
        controller.enabled=true;movement.enabled=true;sandbox.TravelTo(0);while(sandbox.Busy)yield return null;spawner.enabled=true;
        report+="COMPLETE\n";File.WriteAllText("output/gun-effects/water-checks.txt",report);Debug.Log("WATER MOVEMENT CHECKS COMPLETE");
    }
}
