using System;
using System.IO;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class LowStepChecks
{
    static LowStepChecks(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string file="output/gun-effects/low-step.txt";
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !File.Exists(file))return;
        File.Delete(file);Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Run());
    }
    static GameObject Box(Transform root,Vector3 position,Vector3 size)
    {
        var go=new GameObject("StepFixture",typeof(BoxCollider));go.transform.SetParent(root,false);
        go.transform.localPosition=position;go.GetComponent<BoxCollider>().size=size;return go;
    }
    static IEnumerator Run()
    {
        const string report="output/gun-effects/low-step-results.txt";
        File.WriteAllText(report,"RUNNING\n");
        var sandbox=Object.FindFirstObjectByType<MapSandbox>();while(sandbox.Busy)yield return null;
        Object.FindFirstObjectByType<GameFlowManager>().startButton.onClick.Invoke();
        var root=new GameObject("LowStepTest");root.transform.position=new Vector3(1000,0,1000);
        Box(root.transform,new Vector3(0,-.1f,0),new Vector3(20,.2f,10));
        var step=Box(root.transform,new Vector3(0,.45f,0),new Vector3(3,.9f,10));
        var surface=root.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;
        surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.overrideVoxelSize=true;surface.voxelSize=.12f;
        Physics.SyncTransforms();surface.BuildNavMesh();
        var a=root.transform.position+Vector3.left*5;var b=root.transform.position+Vector3.right*5;
        var path=new NavMeshPath();bool low=NavMesh.CalculatePath(a,b,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
        File.AppendAllText(report,$"{(low?"PASS":"FAIL")}: 0.9m raised floor connected\n");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab");
        var instance=Object.Instantiate(prefab,a,Quaternion.identity);var enemy=instance.GetComponent<Enemy>();enemy.externalMovement=true;
        float end=Time.time+12;float highest=0;
        while(Time.time<end && Vector3.Distance(instance.transform.position,b)>.5f)
        {
            enemy.MoveAvoidingObstacles(enemy.DirectionTo(b)*3f*Time.deltaTime);
            highest=Mathf.Max(highest,instance.transform.position.y);yield return null;
        }
        bool crossed=Vector3.Distance(instance.transform.position,b)<.5f && highest>.7f;
        File.AppendAllText(report,$"{(crossed?"PASS":"FAIL")}: enemy climbs and crosses step; highest={highest}, end={instance.transform.position}\n");
        Object.Destroy(instance);
        step.transform.localPosition=new Vector3(0,1.25f,0);step.GetComponent<BoxCollider>().size=new Vector3(3,2.5f,10);
        Physics.SyncTransforms();surface.BuildNavMesh();
        bool tall=NavMesh.Raycast(a,b,out _,NavMesh.AllAreas);
        File.AppendAllText(report,$"{(tall?"PASS":"FAIL")}: 2.5m wall stays blocked\n");
        surface.RemoveData();Object.Destroy(root);
        if(!low || !crossed || !tall)throw new Exception("Low step movement check failed");
        File.AppendAllText(report,"COMPLETE\n");
    }
}
