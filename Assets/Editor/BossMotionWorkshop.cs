using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;

public static class BossMotionWorkshop
{
    [Serializable] public class BoneData {public string name,parent; public float[] position;}
    [Serializable] public class WeightData {public int[] indices;public float[] values;}
    [Serializable] public class RigData {public float bottom;public BoneData[] bones;public WeightData[] weights;}
    public static string Install()
    {
        var data=JsonUtility.FromJson<RigData>(File.ReadAllText("output/boss-motion/rig-data.json"));
        const string path="Assets/Prefabs/BossEnemy.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            if(root.GetComponent<BossAttackMotion>()!=null)return "Already installed";
            var source=root.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh=UnityEngine.Object.Instantiate(source.sharedMesh);mesh.name="BossCombatSkin";
            var vs=mesh.vertices;var normals=mesh.normals;
            var matrix=root.transform.worldToLocalMatrix*source.transform.localToWorldMatrix;
            for(int i=0;i<vs.Length;i++){vs[i]=matrix.MultiplyPoint3x4(vs[i])-Vector3.up*data.bottom;if(normals.Length==vs.Length)normals[i]=matrix.inverse.transpose.MultiplyVector(normals[i]).normalized;}
            mesh.vertices=vs;if(normals.Length==vs.Length)mesh.normals=normals;
            var visual=new GameObject("BossCombatModel").transform;visual.SetParent(root.transform,false);
            var bones=new Transform[data.bones.Length];var byName=new Dictionary<string,Transform>();
            for(int i=0;i<bones.Length;i++)
            {
                var d=data.bones[i];var b=new GameObject(d.name).transform;
                b.SetParent(visual,false);b.localPosition=new Vector3(d.position[0],d.position[1],d.position[2]);
                if(!string.IsNullOrEmpty(d.parent))b.SetParent(byName[d.parent],true);
                bones[i]=b;byName[d.name]=b;
            }
            var weights=new BoneWeight[vs.Length];var binds=new Matrix4x4[bones.Length];
            for(int i=0;i<bones.Length;i++)binds[i]=bones[i].worldToLocalMatrix*visual.localToWorldMatrix;
            for(int i=0;i<weights.Length;i++){var w=data.weights[i];weights[i]=new BoneWeight{boneIndex0=w.indices[0],boneIndex1=w.indices[1],boneIndex2=w.indices[2],boneIndex3=w.indices[3],weight0=w.values[0],weight1=w.values[1],weight2=w.values[2],weight3=w.values[3]};}
            mesh.boneWeights=weights;mesh.bindposes=binds;mesh.RecalculateBounds();mesh.RecalculateTangents();
            const string meshPath="Assets/Models/BossCombatSkin.asset";AssetDatabase.CreateAsset(mesh,meshPath);
            var skin=visual.gameObject.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=bones;skin.rootBone=bones[0];skin.sharedMaterials=source.sharedMaterials;
            skin.localBounds=new Bounds(new Vector3(0,3,0),new Vector3(12,12,12));skin.updateWhenOffscreen=true;
            var motion=root.AddComponent<BossAttackMotion>();motion.bones=bones;motion.visual=visual;
            var waddle=root.GetComponent<CreatureWaddle>();if(waddle!=null)UnityEngine.Object.DestroyImmediate(waddle);
            var lod=root.GetComponent<EnemyMeshLod>();if(lod!=null)UnityEngine.Object.DestroyImmediate(lod);
            var old=root.GetComponentInChildren<Animator>();if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();return "Installed 15-bone rig, original UV/material and boss gameplay components retained";
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
