using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Object=UnityEngine.Object;

// Builds the toy locomotion clips (ToyIdle, ToyWalk) from the original FBX takes.
// Each frame of the take is posed on a temporary copy of the player; the hips drop a little, the torso leans in,
// the legs and the free (left) arm are solved toward a combat-ready stance with two-bone IK, and the result is
// read back as humanoid muscle curves. The game plays plain clips; the weapon arm is posed at runtime (SwordArmPose).
public static class SoftWalkWorkshop
{
    // All distances are fractions of the leg or arm length, in the character's frame (x right, y up, z forward).
    const float IdleCrouch = .09f, WalkCrouch = .05f;                   // hips drop: knees soften (the walk stays light)
    const float IdleLean = 3f, WalkLean = 4f;                           // degrees the spine and chest tip forward
    static readonly Vector3 IdleLeftFoot = new Vector3(-.12f, 0f, .32f), IdleRightFoot = new Vector3(.12f, 0f, -.18f); // stance: left foot leads
    const float WalkStride = .82f, WalkLift = .85f;                     // shorter, lighter steps than the take
    static readonly Vector3 LeftHand = new Vector3(-.40f, -.68f, .42f); // left hand: in front of the hip, clear of the body
    static readonly Vector3 ElbowPole = new Vector3(-.8f, -.2f, -.9f);  // elbows bend out and back, not stiff at the side
    const float ArmSwing = .20f, TorsoTwist = 7f;                       // walk: small counter-swing of arm, chest and hips
    // The arms root right under the head, so raised collarbones read as a shrug: both sit low, level and a little forward
    // (the take has them uneven, -0.84 / -1.31).
    const float ShoulderDown = -1.6f, ShoulderForward = -.2f;
    const float SampleRate = 30f;

    static AnimationClip Source(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));

    public static void Build()
    {
        Bake("Assets/Animations/IdleAnim.fbx","Assets/Animations/ToyIdle.anim",false);
        Bake("Assets/Animations/WalkAnim.fbx","Assets/Animations/ToyWalk.anim",true);
        AssetDatabase.SaveAssets();
    }

    static void Bake(string sourcePath,string path,bool walk)
    {
        var source=Source(sourcePath);
        var player=GameObject.FindGameObjectWithTag("Player").transform.Find("PlayerModel_HandRig").gameObject;
        var main=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var temp=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Additive);
        try
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(temp);
            var model=Object.Instantiate(player);model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);model.transform.localScale=Vector3.one;
            var rig=new Rig(model.GetComponent<Animator>());
            using var handler=new HumanPoseHandler(rig.animator.avatar,model.transform);
            int frames=Mathf.Max(2,Mathf.RoundToInt(source.length*SampleRate)+1);
            var pose=new HumanPose();
            // ground: the lowest each ankle gets over the take, so planted feet stay planted
            float groundL=float.MaxValue,groundR=float.MaxValue;
            for(int f=0;f<frames;f++){Sample(source,f*source.length/(frames-1),handler,rig,ref pose);groundL=Mathf.Min(groundL,rig.footL.position.y);groundR=Mathf.Min(groundR,rig.footR.position.y);}
            var muscles=new AnimationCurve[HumanTrait.MuscleCount];for(int i=0;i<muscles.Length;i++)muscles[i]=new AnimationCurve();
            var root=new AnimationCurve[7];for(int i=0;i<root.Length;i++)root[i]=new AnimationCurve();
            for(int f=0;f<frames;f++)
            {
                float time=f*source.length/(frames-1);
                Sample(source,time,handler,rig,ref pose);
                RelaxShoulders(handler,ref pose);
                Restance(rig,walk,time,groundL,groundR);
                handler.GetHumanPose(ref pose);
                for(int i=0;i<muscles.Length;i++)muscles[i].AddKey(time,pose.muscles[i]);
                Vector3 t=pose.bodyPosition/rig.animator.humanScale;Quaternion q=pose.bodyRotation;
                root[0].AddKey(time,t.x);root[1].AddKey(time,t.y);root[2].AddKey(time,t.z);root[3].AddKey(time,q.x);root[4].AddKey(time,q.y);root[5].AddKey(time,q.z);root[6].AddKey(time,q.w);
            }
            var clip=Object.Instantiate(source);clip.name=walk?"ToyWalk":"ToyIdle";
            for(int i=0;i<muscles.Length;i++)Write(clip,HumanTrait.MuscleName[i],muscles[i]);
            string[] rootNames={"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"};
            for(int i=0;i<root.Length;i++)Write(clip,rootNames[i],root[i]);
            var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(old==null)AssetDatabase.CreateAsset(clip,path);else{EditorUtility.CopySerialized(clip,old);Object.DestroyImmediate(clip);EditorUtility.SetDirty(old);}
        }
        finally
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(main);
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(temp,true);
        }
    }

    sealed class Rig
    {
        public readonly Animator animator;
        public readonly Transform hips,spine,chest,upperChest,armL,elbowL,handL,legL,kneeL,footL,legR,kneeR,footR;
        public readonly float legLength,armLength;
        public Rig(Animator a)
        {
            animator=a;Transform B(HumanBodyBones b)=>a.GetBoneTransform(b);
            hips=B(HumanBodyBones.Hips);spine=B(HumanBodyBones.Spine);chest=B(HumanBodyBones.Chest);upperChest=B(HumanBodyBones.UpperChest);
            armL=B(HumanBodyBones.LeftUpperArm);elbowL=B(HumanBodyBones.LeftLowerArm);handL=B(HumanBodyBones.LeftHand);
            legL=B(HumanBodyBones.LeftUpperLeg);kneeL=B(HumanBodyBones.LeftLowerLeg);footL=B(HumanBodyBones.LeftFoot);
            legR=B(HumanBodyBones.RightUpperLeg);kneeR=B(HumanBodyBones.RightLowerLeg);footR=B(HumanBodyBones.RightFoot);
            legLength=Vector3.Distance(legL.position,kneeL.position)+Vector3.Distance(kneeL.position,footL.position);
            armLength=Vector3.Distance(armL.position,elbowL.position)+Vector3.Distance(elbowL.position,handL.position);
        }
    }

    // Poses the rig from the take's muscle and root curves at `time`.
    static void Sample(AnimationClip clip,float time,HumanPoseHandler handler,Rig rig,ref HumanPose pose)
    {
        handler.GetHumanPose(ref pose);
        Vector3 t=Vector3.zero;var q=new float[4];
        foreach(var binding in AnimationUtility.GetCurveBindings(clip))
        {
            string n=binding.propertyName;float v=AnimationUtility.GetEditorCurve(clip,binding).Evaluate(time);
            int i=System.Array.IndexOf(HumanTrait.MuscleName,n);
            if(i>=0)pose.muscles[i]=v;
            else if(n=="RootT.x")t.x=v;else if(n=="RootT.y")t.y=v;else if(n=="RootT.z")t.z=v;
            else if(n=="RootQ.x")q[0]=v;else if(n=="RootQ.y")q[1]=v;else if(n=="RootQ.z")q[2]=v;else if(n=="RootQ.w")q[3]=v;
        }
        pose.bodyPosition=t*rig.animator.humanScale;pose.bodyRotation=new Quaternion(q[0],q[1],q[2],q[3]);
        handler.SetHumanPose(ref pose);
    }

    static void RelaxShoulders(HumanPoseHandler handler,ref HumanPose pose)
    {
        handler.GetHumanPose(ref pose);
        foreach(var side in new[]{"Left","Right"})
        {
            pose.muscles[System.Array.IndexOf(HumanTrait.MuscleName,side+" Shoulder Down-Up")]=ShoulderDown;
            pose.muscles[System.Array.IndexOf(HumanTrait.MuscleName,side+" Shoulder Front-Back")]=ShoulderForward;
        }
        handler.SetHumanPose(ref pose);
    }

    // Moves the sampled take toward the combat-ready stance (see the constants above).
    static void Restance(Rig rig,bool walk,float time,float groundL,float groundR)
    {
        Vector3 right=Vector3.right,forward=Vector3.forward;
        Vector3 footL0=rig.footL.position,footR0=rig.footR.position;Quaternion footLRot=rig.footL.rotation,footRRot=rig.footR.rotation;
        Quaternion handLLocal=rig.handL.localRotation;
        Vector3 hips0=rig.hips.position;
        // walk phase from the feet: +1 left foot ahead, -1 right foot ahead
        float stride=Mathf.Clamp((footL0.z-footR0.z)/(rig.legLength*.6f),-1f,1f);

        rig.hips.position+=Vector3.down*(walk?WalkCrouch:IdleCrouch)*rig.legLength;
        if(walk)rig.hips.rotation=Quaternion.AngleAxis(stride*TorsoTwist*.5f,Vector3.up)*rig.hips.rotation;
        foreach(var bone in new[]{rig.spine,rig.chest})
            bone.rotation=Quaternion.AngleAxis((walk?WalkLean:IdleLean)*.5f,right)*bone.rotation;
        if(walk)rig.chest.rotation=Quaternion.AngleAxis(-stride*TorsoTwist,Vector3.up)*rig.chest.rotation;

        Vector3 FootTarget(Vector3 foot0,float ground,Vector3 stance)
        {
            if(!walk)return new Vector3(foot0.x,ground,foot0.z)+stance*rig.legLength;
            Vector3 offset=foot0-hips0;
            return new Vector3(hips0.x+offset.x,ground+(foot0.y-ground)*WalkLift,hips0.z+offset.z*WalkStride);
        }
        Vector3 knee=forward+right*.15f;
        TwoBoneIK.Solve(rig.legL,rig.kneeL,rig.footL,FootTarget(footL0,groundL,IdleLeftFoot),forward-right*.15f);
        TwoBoneIK.Solve(rig.legR,rig.kneeR,rig.footR,FootTarget(footR0,groundR,IdleRightFoot),knee);
        rig.footL.rotation=footLRot;rig.footR.rotation=footRRot;

        Vector3 hand=LeftHand;
        if(walk){hand.z-=stride*ArmSwing;hand.y+=Mathf.Abs(stride)*ArmSwing*.25f;}
        else hand.y+=.015f*Mathf.Sin(time*Mathf.PI*2f/3f);   // breathing
        TwoBoneIK.Solve(rig.armL,rig.elbowL,rig.handL,rig.armL.position+hand*rig.armLength,ElbowPole);
        rig.handL.localRotation=handLLocal;
    }

    static void Write(AnimationClip clip,string property,AnimationCurve curve)
    {
        for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property),curve);
    }

    public static void Capture(bool revised,string prefix,bool weapon=false,bool sequence=false)
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play mode required.");
        Directory.CreateDirectory(prefix);
        var player=Object.FindFirstObjectByType<PlayerWeapons>();var attack=player.GetComponent<PlayerAutoAttack>();var controller=player.GetComponent<PlayerController>();var animator=player.GetComponentInChildren<Animator>();
        var oldController=animator.runtimeAnimatorController;var overrideController=new AnimatorOverrideController(oldController);
        var pairs=new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip,AnimationClip>>();overrideController.GetOverrides(pairs);
        for(int i=0;i<pairs.Count;i++){var key=pairs[i].Key;var path=AssetDatabase.GetAssetPath(key);if(path.Contains("Walk"))pairs[i]=new System.Collections.Generic.KeyValuePair<AnimationClip,AnimationClip>(key,revised?AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/ToyWalk.anim"):Source("Assets/Animations/WalkAnim.fbx"));if(path.Contains("Idle"))pairs[i]=new System.Collections.Generic.KeyValuePair<AnimationClip,AnimationClip>(key,revised?AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/ToyIdle.anim"):Source("Assets/Animations/IdleAnim.fbx"));}
        overrideController.ApplyOverrides(pairs);animator.runtimeAnimatorController=overrideController;
        bool oldAttack=attack.enabled,oldMove=controller.enabled,oldSword=attack.swordModel.gameObject.activeSelf;float oldTime=Time.timeScale;
        attack.enabled=false;controller.enabled=false;attack.swordModel.gameObject.SetActive(weapon);Time.timeScale=0;
        var go=new GameObject("WalkReviewCamera");var cam=go.AddComponent<Camera>();cam.enabled=false;cam.fieldOfView=36;cam.nearClipPlane=.1f;cam.farClipPlane=150;cam.clearFlags=CameraClearFlags.Skybox;
        var rt=RenderTexture.GetTemporary(512,512,24);var oldRT=RenderTexture.active;cam.targetTexture=rt;var tex=new Texture2D(512,512,TextureFormat.RGB24,false);
        try
        {
            for(int frame=0;frame<(sequence?80:24);frame++)
            {
                attack.ResetSwordPose();animator.Rebind();animator.SetFloat("AttackSpeed",1);animator.SetFloat("Speed",6);animator.Play("Base Layer.Walk",0,frame/24f);animator.SetLayerWeight(1,0);animator.Update(0);
                bool walking=!sequence || (frame>=20 && frame<52);
                string motion=walking?"Walk":"Idle";
                var sampled=revised?AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Toy"+motion+".anim"):Source("Assets/Animations/"+motion+"Anim.fbx");
                sampled.SampleAnimation(animator.gameObject,sequence?(walking?(frame-20)*.05f*3f:frame*.05f):frame/24f*sampled.length);
                if(weapon)attack.ApplySwordPose(sequence && frame>=52 && frame<62?(frame-52)/9f:0,sequence && frame>=52 && frame<62);
                var skins=animator.GetComponentsInChildren<SkinnedMeshRenderer>();
                var baked=new System.Collections.Generic.List<GameObject>();
                var meshes=new System.Collections.Generic.List<Mesh>();
                foreach(var skin in skins){var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);var copy=new GameObject("BakedReview");copy.transform.SetParent(skin.transform,false);copy.transform.localScale=new Vector3(1/skin.transform.lossyScale.x,1/skin.transform.lossyScale.y,1/skin.transform.lossyScale.z);copy.AddComponent<MeshFilter>().sharedMesh=mesh;copy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;baked.Add(copy);}
                for(int view=0;view<3;view++)
                {
                    var direction=view==0?player.transform.forward:view==1?player.transform.right:-player.transform.forward;
                    cam.transform.position=player.transform.position+direction*3.1f+Vector3.up*.32f;cam.transform.LookAt(player.transform.position+Vector3.up*.05f);
                    cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,512,512),0,0);tex.Apply();File.WriteAllBytes(prefix+"/"+view+"-"+frame.ToString("00")+".png",tex.EncodeToPNG());
                }
                foreach(var skin in skins)skin.enabled=true;foreach(var copy in baked)Object.DestroyImmediate(copy);foreach(var mesh in meshes)Object.DestroyImmediate(mesh);
            }
        }
        finally
        {
            animator.runtimeAnimatorController=oldController;animator.Rebind();attack.swordModel.gameObject.SetActive(oldSword);attack.enabled=oldAttack;controller.enabled=oldMove;Time.timeScale=oldTime;
            cam.targetTexture=null;RenderTexture.active=oldRT;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);Object.DestroyImmediate(go);Object.DestroyImmediate(overrideController);
        }
    }
    // Walk plays at 3x: ~4.4 short steps a second. This tiny stride cannot keep pace with 6 m/s anyway, so it reads as a quick scurry.
    public static void Apply()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Apply in edit mode.");
        var a=GameObject.FindGameObjectWithTag("Player").GetComponentInChildren<Animator>();var c=(AnimatorController)a.runtimeAnimatorController;
        Undo.RecordObject(c,"Soften character locomotion");
        foreach(var child in c.layers[0].stateMachine.states){var state=child.state;if(state.name=="Idle" || state.name=="Walk"){Undo.RecordObject(state,"Soften locomotion state");state.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Toy"+state.name+".anim");if(state.name=="Walk")state.speed=3f;EditorUtility.SetDirty(state);}}
        EditorUtility.SetDirty(c);AssetDatabase.SaveAssets();
    }
}
