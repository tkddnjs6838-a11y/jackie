using UnityEngine;

// Stage-specific silhouette; collider, health and rewards stay with the existing boss.
public sealed class LateStageBossLook : MonoBehaviour
{
    Material accent;Mesh crystal;
    public void Configure(int stage)
    {
        var attack=GetComponent<BossAttacks>();
        if(attack!=null)
        {
            attack.stageVariant=stage;
            attack.windUpColor=stage==3?new Color(.65f,.35f,1):new Color(1,.3f,.12f);
            attack.strikeColor=stage==3?new Color(.35f,.9f,1):new Color(1,.8f,.2f);
            // Each strike retains its full tell and recovery, including combo follow-ups.
        }
        var renderers=GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        // Use the rest mesh, not the enlarged animation culling bounds, to place decorations.
        var rig=GetComponent<BossAttackMotion>();
        if(rig!=null && rig.visual!=null)
        {
            var skin=rig.visual.GetComponent<SkinnedMeshRenderer>();
            if(skin!=null && skin.sharedMesh!=null)
            {
                var rest=skin.sharedMesh.bounds;
                bounds=new Bounds(rig.visual.TransformPoint(rest.center),Vector3.Scale(rest.size,rig.visual.lossyScale));
            }
        }
        accent=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        accent.color=stage==3?new Color(.3f,.65f,1):new Color(1,.32f,.04f);
        accent.SetFloat("_Smoothness",.55f);
        crystal=new Mesh();crystal.vertices=new[]{new Vector3(0,1,0),new Vector3(-.5f,0,0),new Vector3(0,0,.5f),new Vector3(.5f,0,0),new Vector3(0,0,-.5f),new Vector3(0,-.3f,0)};
        crystal.triangles=new[]{0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1};crystal.RecalculateNormals();
        int count=stage==3?5:2;
        for(int i=0;i<count;i++)
        {
            var spike=new GameObject(stage==3?"CrystalCrown":"LavaHorn");spike.transform.SetParent(transform,false);
            float x=stage==3?(i-2)*.09f:(i==0?-.20f:.20f);
            var world=bounds.center+transform.right*(bounds.size.x*x)+Vector3.up*bounds.extents.y*.64f;
            spike.transform.position=world;spike.transform.rotation=transform.rotation*Quaternion.Euler(0,0,-x*60);
            float scale=Mathf.Max(.01f,Mathf.Abs(transform.lossyScale.x));
            spike.transform.localScale=new Vector3(bounds.size.x*.11f,bounds.size.y*(stage==3?.16f:.26f),bounds.size.z*.11f)/scale;
            // Crown and horns follow the head through anticipation and recovery.
            var motion=GetComponent<BossAttackMotion>();
            if(motion!=null && motion.bones!=null)
                foreach(var bone in motion.bones)if(bone.name=="Head"){spike.transform.SetParent(bone,true);break;}
            spike.AddComponent<MeshFilter>().sharedMesh=crystal;spike.AddComponent<MeshRenderer>().sharedMaterial=accent;
        }
    }
    void OnDestroy(){if(accent!=null)Destroy(accent);if(crystal!=null)Destroy(crystal);}
}
