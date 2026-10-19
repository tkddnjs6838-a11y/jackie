using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using Object=UnityEngine.Object;

public static class ToyCharacterWorkshop
{
    const string Folder="Assets/Models/WakuballB/";
    public static void BuildAndApply()
    {
        var preview=EditorSceneManager.NewPreviewScene();
        var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"WakuballB.prefab"));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(model,preview);
        AnimationClip idle,walk;
        try
        {
            if(!model.GetComponent<ToyCharacterRig>())model.AddComponent<ToyCharacterRig>();
            model.GetComponent<Animator>().enabled=false;
            idle=Bake(model,false);walk=Bake(model,true);
        }
        finally{EditorSceneManager.ClosePreviewScene(preview);}
        var path=Folder+"B_Gameplay.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if(controller==null)
        {
            controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
            controller.AddParameter("AttackSpeed",AnimatorControllerParameterType.Float);
            BlendTree tree;var state=controller.CreateBlendTreeInController("Locomotion",out tree);
            tree.blendParameter="Speed";tree.blendType=BlendTreeType.Simple1D;tree.useAutomaticThresholds=false;tree.AddChild(idle,0);tree.AddChild(walk,4);
            state.writeDefaultValues=true;
        }
        var player=Object.FindFirstObjectByType<PlayerController>();var attack=player.GetComponent<PlayerAutoAttack>();
        var old=player.animator.gameObject;var existing=player.transform.Find("PlayerModel_B");
        if(existing!=null)throw new System.InvalidOperationException("PlayerModel_B already installed; rebake clips separately instead of duplicating it");
        var replacement=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"WakuballB.prefab"),player.gameObject.scene);
        replacement.name="PlayerModel_B";replacement.transform.SetParent(player.transform,false);replacement.transform.localPosition=new Vector3(0,-1,0);replacement.transform.localScale=Vector3.one;
        replacement.AddComponent<ToyCharacterRig>();var animator=replacement.GetComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var hand=ToyCharacterRig.Bone(animator,HumanBodyBones.RightHand);
        var sword=attack.swordModel;sword.SetParent(hand,true);
        var grip=replacement.AddComponent<WeaponHandGrip>();grip.useSolvedPosition=true;grip.fingers=new WeaponHandGrip.FingerPose[0];
        attack.animator=animator;attack.handGrip=grip;player.animator=animator;
        // Rest wrists follow their own new skeleton. Align the weapon's +Y blade to it.
        var upper=ToyCharacterRig.Bone(animator,HumanBodyBones.RightUpperArm);var lower=ToyCharacterRig.Bone(animator,HumanBodyBones.RightLowerArm);
        var motion=SwordSwingMotion.Evaluate(0);float reach=Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position);
        TwoBoneIK.Solve(upper,lower,hand,upper.position+player.transform.rotation*motion.wrist*reach,player.transform.rotation*new Vector3(1,-.2f,-.35f));
        float yaw=motion.yaw*Mathf.Deg2Rad,pitch=motion.pitch*Mathf.Deg2Rad;
        var blade=player.transform.rotation*new Vector3(Mathf.Sin(yaw)*Mathf.Cos(pitch),Mathf.Sin(pitch),Mathf.Cos(yaw)*Mathf.Cos(pitch));
        var weapon=Quaternion.LookRotation(Vector3.ProjectOnPlane(player.transform.up,blade).normalized,blade);
        attack.gripIdleEuler=(Quaternion.Inverse(hand.rotation)*weapon).eulerAngles;
        attack.ApplySwordPose(0,false);
        var oldPose=player.GetComponent<PlayerPose>();if(oldPose)oldPose.enabled=false;
        old.SetActive(false);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        EditorUtility.SetDirty(player);EditorUtility.SetDirty(attack);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(player.gameObject.scene);EditorSceneManager.SaveScene(player.gameObject.scene);
        Debug.Log("B MODEL INSTALLED: generic locomotion + shared melee/gun IK; old visual preserved inactive");
    }
    static AnimationClip Bake(GameObject model,bool walk)
    {
        var animator=model.GetComponent<Animator>();var all=model.GetComponentsInChildren<Transform>();
        var positions=new Vector3[all.Length];var rotations=new Quaternion[all.Length];
        for(int i=0;i<all.Length;i++){positions[i]=all[i].localPosition;rotations[i]=all[i].localRotation;}
        var left=ToyCharacterRig.Bone(animator,HumanBodyBones.LeftFoot);var right=ToyCharacterRig.Bone(animator,HumanBodyBones.RightFoot);
        Vector3 l0=left.position,r0=right.position;Quaternion lq=left.rotation,rq=right.rotation;
        var curves=new AnimationCurve[all.Length,7];for(int i=0;i<all.Length;i++)for(int j=0;j<7;j++)curves[i,j]=new AnimationCurve();
        float duration=walk?.56f:1.6f;const int frames=48;
        for(int frame=0;frame<=frames;frame++)
        {
            for(int i=0;i<all.Length;i++){all[i].localPosition=positions[i];all[i].localRotation=rotations[i];}
            float t=frame/(float)frames,phase=t*Mathf.PI*2,s=Mathf.Sin(phase);
            var hips=ToyCharacterRig.Bone(animator,HumanBodyBones.Hips);
            hips.position+=Vector3.down*(walk?.024f-.009f*Mathf.Cos(phase*2):.018f+.004f*Mathf.Sin(phase));
            hips.rotation=Quaternion.AngleAxis(walk?s*2:0,Vector3.forward)*hips.rotation;
            Rotate(animator,HumanBodyBones.Spine,Vector3.right,walk?4:2);
            Rotate(animator,HumanBodyBones.Chest,Vector3.up,walk?-s*3:0);
            Rotate(animator,HumanBodyBones.Head,Vector3.up,walk?s*2:0);
            Rotate(animator,HumanBodyBones.LeftUpperArm,Vector3.right,-8+(walk?s*7:0));
            Rotate(animator,HumanBodyBones.LeftLowerArm,Vector3.right,-14);
            var lp=l0+Vector3.forward*(walk?s*.115f:.025f)+Vector3.up*(walk?Mathf.Max(0,s)*.045f:0);
            var rp=r0+Vector3.forward*(walk?-s*.115f:-.025f)+Vector3.up*(walk?Mathf.Max(0,-s)*.045f:0);
            TwoBoneIK.Solve(ToyCharacterRig.Bone(animator,HumanBodyBones.LeftUpperLeg),ToyCharacterRig.Bone(animator,HumanBodyBones.LeftLowerLeg),left,lp,Vector3.forward);
            TwoBoneIK.Solve(ToyCharacterRig.Bone(animator,HumanBodyBones.RightUpperLeg),ToyCharacterRig.Bone(animator,HumanBodyBones.RightLowerLeg),right,rp,Vector3.forward);
            left.rotation=lq;right.rotation=rq;
            for(int i=0;i<all.Length;i++)
            {
                var pos=all[i].localPosition;var q=all[i].localRotation;
                float[] values={pos.x,pos.y,pos.z,q.x,q.y,q.z,q.w};
                for(int j=0;j<7;j++)curves[i,j].AddKey(t*duration,values[j]);
            }
        }
        for(int i=0;i<all.Length;i++){all[i].localPosition=positions[i];all[i].localRotation=rotations[i];}
        var clip=new AnimationClip{name=walk?"B_Walk":"B_Ready",frameRate=60};string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
        for(int i=0;i<all.Length;i++)
        {
            if(all[i]==model.transform||all[i].GetComponent<Renderer>()!=null)continue;
            var path=AnimationUtility.CalculateTransformPath(all[i],model.transform);
            for(int j=0;j<7;j++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),properties[j]),curves[i,j]);
        }
        clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.loopBlend=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
        string asset=Folder+clip.name+".anim";var previous=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);
        if(previous!=null){EditorUtility.CopySerialized(clip,previous);Object.DestroyImmediate(clip);EditorUtility.SetDirty(previous);return previous;}
        AssetDatabase.CreateAsset(clip,asset);return clip;
    }
    static void Rotate(Animator a,HumanBodyBones b,Vector3 axis,float angle){var t=ToyCharacterRig.Bone(a,b);if(t)t.rotation=Quaternion.AngleAxis(angle,axis)*t.rotation;}
}
