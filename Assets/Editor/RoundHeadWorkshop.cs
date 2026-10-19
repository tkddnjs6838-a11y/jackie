using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class RoundHeadWorkshop
{
    // The source FBX uses Z-up mesh coordinates. Restrict this correction to the head
    // envelope; retain a short blend at the neck and preserve the eyes and crown.
    public static Mesh Correct(Mesh source, Transform[] bones)
    {
        int head=Array.FindIndex(bones,b=>b.name=="Head");
        if(head<0)throw new InvalidOperationException("Head bone not found");
        var mesh=Object.Instantiate(source);mesh.name="PlayerHandRig_RoundHead";
        var vertices=mesh.vertices;var weights=mesh.boneWeights;int changed=0;
        for(int i=0;i<vertices.Length;i++)
        {
            var v=vertices[i];if(v.z<.00485f)continue;
            var old=weights[i];
            float radial=new Vector2(v.x,v.y).magnitude;
            float mask=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.00205f,.00265f,radial));
            float vertical=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.00485f,.00535f,v.z));
            float blend=mask*vertical;
            if(v.z>=.00635f)blend=1;
            if(v.z<.00665f)
            {
                var direction=new Vector3(v.x/.0021f,v.y/.0021f,(v.z-.00665f)/.00194f).normalized;
                var sphere=new Vector3(direction.x*.0021f,direction.y*.0021f,.00665f+direction.z*.00194f);
                vertices[i]=Vector3.Lerp(v,sphere,blend*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.00605f,.00665f,v.z))));
            }
            if(blend<=0)continue;
            var values=new float[bones.Length];
            values[old.boneIndex0]+=old.weight0*(1-blend);values[old.boneIndex1]+=old.weight1*(1-blend);
            values[old.boneIndex2]+=old.weight2*(1-blend);values[old.boneIndex3]+=old.weight3*(1-blend);values[head]+=blend;
            var ids=new int[values.Length];for(int j=0;j<ids.Length;j++)ids[j]=j;
            Array.Sort(ids,(a,b)=>values[b].CompareTo(values[a]));
            float total=values[ids[0]]+values[ids[1]]+values[ids[2]]+values[ids[3]];
            weights[i]=new BoneWeight{boneIndex0=ids[0],weight0=values[ids[0]]/total,boneIndex1=ids[1],weight1=values[ids[1]]/total,boneIndex2=ids[2],weight2=values[ids[2]]/total,boneIndex3=ids[3],weight3=values[ids[3]]/total};changed++;
        }
        mesh.vertices=vertices;mesh.boneWeights=weights;mesh.RecalculateNormals();
        var originalVertices=source.vertices;var originalNormals=source.normals;var newNormals=mesh.normals;
        for(int i=0;i<vertices.Length;i++)if((vertices[i]-originalVertices[i]).sqrMagnitude<1e-16f)newNormals[i]=originalNormals[i];
        mesh.normals=newNormals;mesh.RecalculateBounds();Debug.Log("Round head weights processed: "+changed);return mesh;
    }
    public static void Preview(bool corrected,string prefix,float phase=0,bool walking=false)
    {
        var source=GameObject.FindGameObjectWithTag("Player");var preview=new PreviewRenderUtility();Mesh candidate=null;
        try
        {
            var clone=Object.Instantiate(source);clone.transform.position=Vector3.zero;clone.transform.rotation=Quaternion.identity;
            preview.AddSingleGO(clone);foreach(var c in clone.GetComponentsInChildren<MonoBehaviour>())c.enabled=false;
            foreach(var camera in clone.GetComponentsInChildren<Camera>())camera.enabled=false;
            var skin=clone.GetComponentInChildren<SkinnedMeshRenderer>();
            if(corrected){candidate=Correct(skin.sharedMesh,skin.bones);skin.sharedMesh=candidate;}
            var animator=clone.GetComponentInChildren<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();animator.SetFloat("AttackSpeed",1);animator.SetFloat("Speed",walking?6:0);animator.Play(walking?"Base Layer.Walk":"Base Layer.Idle",0,phase);animator.Update(0);
            var attack=clone.GetComponent<PlayerAutoAttack>();attack.ApplySwordPose(phase,phase>0 && !walking);
            var mesh=new Mesh();skin.BakeMesh(mesh);var go=new GameObject("Baked");go.transform.SetParent(skin.transform,false);
            var scale=skin.transform.lossyScale;go.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;
            preview.camera.orthographic=true;preview.camera.orthographicSize=.43f;
            preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=50;
            preview.camera.backgroundColor=new Color(.24f,.31f,.38f);preview.camera.clearFlags=CameraClearFlags.SolidColor;
            preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-35,0);
            preview.lights[1].intensity=.8f;preview.lights[1].transform.rotation=Quaternion.Euler(20,145,0);preview.ambientColor=Color.gray;
            var head=animator.GetBoneTransform(HumanBodyBones.Head);var target=head.position+Vector3.up*.18f;
            for(int view=0;view<3;view++)
            {
                var direction=view==0?Vector3.forward:view==1?Vector3.back:Vector3.right;
                preview.camera.transform.position=target+direction*3;preview.camera.transform.LookAt(target);
                preview.BeginStaticPreview(new Rect(0,0,640,640));preview.Render(true);var image=preview.EndStaticPreview();
                File.WriteAllBytes(prefix+"-"+view+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
            }
            Object.DestroyImmediate(mesh);
        }
        finally{preview.Cleanup();if(candidate!=null)Object.DestroyImmediate(candidate);}
    }
}
