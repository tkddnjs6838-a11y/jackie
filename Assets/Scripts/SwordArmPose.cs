using UnityEngine;

// A one-handed, chest-height cut sized to this character's short arms and large head.
// Solve the arm and wrist together; the sword never rotates independently in the palm.
public static class SwordArmPose
{
    public static void Apply(Animator animator, Transform root, Transform sword, Vector3 grip, float phase, bool attacking, bool axe = false, bool gun = false, float recoil = 0f, Vector3 gunDirection = default)
    {
        if (animator == null || sword == null) return;
        var upper = ToyCharacterRig.Bone(animator,HumanBodyBones.RightUpperArm);
        var lower = ToyCharacterRig.Bone(animator,HumanBodyBones.RightLowerArm);
        var hand = ToyCharacterRig.Bone(animator,HumanBodyBones.RightHand);
        if (upper == null || lower == null || hand == null) return;

        var motion=axe ? AxeSwingMotion.Evaluate(attacking ? phase : 0f) : SwordSwingMotion.Evaluate(attacking ? phase : 0f);
        Vector3 wrist=gun ? new Vector3(.46f,-.48f,.57f-recoil*.10f) : motion.wrist;
        float yaw=motion.yaw*Mathf.Deg2Rad, pitch=motion.pitch*Mathf.Deg2Rad;
        Vector3 blade=new Vector3(Mathf.Sin(yaw)*Mathf.Cos(pitch),Mathf.Sin(pitch),Mathf.Cos(yaw)*Mathf.Cos(pitch));

        float reach = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
        var chest=ToyCharacterRig.Bone(animator,HumanBodyBones.Chest);
        bool toy=animator.GetComponent<ToyCharacterRig>()!=null && chest!=null;
        Quaternion armFrame=toy && !gun ? chest.rotation : root.rotation;
        if(toy && !gun && axe)
        {
            // Keep the short arm outside the rounded belly throughout torso rotation.
            // Crossing the cut is carried by the blade, not by dragging the fist through the ribs.
            wrist.x=Mathf.Max(.40f,wrist.x);
            wrist.y=Mathf.Max(-.48f,wrist.y);
            wrist.z=Mathf.Max(.78f,wrist.z);
            wrist=Vector3.ClampMagnitude(wrist,.94f);
        }
        TwoBoneIK.Solve(upper, lower, hand, upper.position + armFrame * wrist * reach, armFrame * new Vector3(1f,-0.2f,-0.15f));

        Vector3 bladeWorld = root.rotation * blade.normalized;
        Vector3 face = Vector3.ProjectOnPlane(root.up,bladeWorld).normalized;
        Quaternion weaponRotation = Quaternion.AngleAxis(motion.roll,bladeWorld)*Quaternion.LookRotation(face,bladeWorld);
        if(gun)weaponRotation=Quaternion.LookRotation(gunDirection.sqrMagnitude>.01f?gunDirection:root.forward,root.up)*Quaternion.Euler(-recoil*12f,0,0);
        Quaternion gripRotation = Quaternion.Euler(grip);
        hand.rotation = weaponRotation * Quaternion.Inverse(gripRotation);
        sword.localRotation = gripRotation;
        // The model origin is at the pommel. Put the middle of its handle in the palm,
        // rather than attaching that pommel at the wrist joint.
        float scale = Mathf.Abs(root.lossyScale.y) / 0.72f;
        sword.position = hand.position - bladeWorld * (0.075f * scale);
    }

}

// Restore only our additive offsets before the Animator evaluates the next frame.
// This preserves walking animation and prevents twists accumulating while paused.
public sealed class SwordBodyPose
{
    static readonly HumanBodyBones[] Touched={HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Head,HumanBodyBones.LeftUpperArm,HumanBodyBones.Hips,
        HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,HumanBodyBones.LeftLowerArm};
    readonly Transform[] bones = new Transform[Touched.Length];
    readonly Quaternion[] original = new Quaternion[Touched.Length];
    Vector3 hipsPosition;
    bool applied;

    public void Restore()
    {
        if (!applied) return;
        for(int i=0;i<bones.Length;i++)
            if(bones[i]!=null) bones[i].localRotation=original[i];
        if(bones[4]!=null) bones[4].localPosition=hipsPosition;
        applied=false;
    }

    public void Apply(Animator animator, Transform root, float phase, bool attacking, bool axe = false)
    {
        if(!attacking || animator==null) return;
        for(int i=0;i<bones.Length;i++)
        {
            bones[i]=ToyCharacterRig.Bone(animator,Touched[i]);
            if(bones[i]!=null) original[i]=bones[i].localRotation;
        }
        Transform spine=bones[0],chest=bones[1],head=bones[2],leftArm=bones[3],hips=bones[4];
        if(hips!=null) hipsPosition=hips.localPosition;
        applied=true;
        // The torso leads slightly, then settles while the hand completes recovery.
        float lead=0.025f*Mathf.Sin(Mathf.Clamp01(phase)*Mathf.PI);
        var motion=axe ? AxeSwingMotion.Evaluate(phase+lead) : SwordSwingMotion.Evaluate(phase+lead);
        bool toy=animator.GetComponent<ToyCharacterRig>()!=null;
        float twist=motion.twist*(toy ? (axe ? 1.8f : 1f) : 1f);
        float lean=motion.lean;
        float effort=Mathf.Sin(Mathf.Clamp01(phase)*Mathf.PI);
        ShiftWeight(hips,root,twist,lean,toy,effort,toy && !axe);
        // the hips already turn into the cut, so the spine and chest add less than before
        Rotate(spine,root,twist*0.25f,lean*0.40f);
        Rotate(chest,root,twist*0.45f,lean*0.60f);
        // Keep the eyes on the target while the chest leads the cut.
        Rotate(head,root,-twist*0.65f,-lean*0.65f);
        if(leftArm!=null)
        {
            if(toy && chest!=null && bones[11]!=null)
            {
                var hand=ToyCharacterRig.Bone(animator,HumanBodyBones.LeftHand);
                if(hand!=null)
                {
                    float reach=Vector3.Distance(leftArm.position,bones[11].position)+Vector3.Distance(bones[11].position,hand.position);
                    var offset=new Vector3(-.55f,-.68f,.28f+effort*.16f);
                    var upperRest=leftArm.localRotation;var lowerRest=bones[11].localRotation;
                    TwoBoneIK.Solve(leftArm,bones[11],hand,leftArm.position+chest.rotation*offset*reach,chest.rotation*new Vector3(-1,-.2f,-.15f));
                    leftArm.localRotation=Quaternion.Slerp(upperRest,leftArm.localRotation,effort);
                    bones[11].localRotation=Quaternion.Slerp(lowerRest,bones[11].localRotation,effort);
                }
            }
            else leftArm.rotation=Quaternion.AngleAxis(-twist*.4f,root.forward)*leftArm.rotation;
        }
    }

    // Body weight behind the cut: hips draw back on the wind-up, then turn, sink and drive forward
    // through contact. The feet stay where the walk or idle put them; the knees take the drop.
    void ShiftWeight(Transform hips,Transform root,float twist,float lean,bool toy,float effort,bool gentle=false)
    {
        Transform legL=bones[5],kneeL=bones[6],footL=bones[7],legR=bones[8],kneeR=bones[9],footR=bones[10];
        if(hips==null || legL==null || kneeL==null || footL==null || legR==null || kneeR==null || footR==null) return;
        Vector3 plantL=footL.position,plantR=footR.position;Quaternion turnL=footL.rotation,turnR=footR.rotation;
        float leg=Vector3.Distance(legL.position,kneeL.position)+Vector3.Distance(kneeL.position,footL.position);
        float weight=Mathf.Clamp(lean/(toy ? 9f : 3f),-0.6f,1f);
        hips.position+=root.forward*((gentle ? .10f : toy ? .18f : .08f)*leg*weight)
            +Vector3.down*(leg*(gentle ? .065f*effort : toy ? .16f*effort : .10f*Mathf.Max(0f,weight)));
        if(toy && !gentle) hips.position+=root.right*(leg*twist/38f*-.09f);
        hips.rotation=Quaternion.AngleAxis(twist*(toy ? .55f : .3f),root.up)*hips.rotation;
        TwoBoneIK.Solve(legL,kneeL,footL,plantL,root.forward);
        TwoBoneIK.Solve(legR,kneeR,footR,plantR,root.forward);
        footL.rotation=turnL;footR.rotation=turnR;
    }

    static void Rotate(Transform bone,Transform root,float yaw,float pitch)
    {
        if(bone!=null)
            bone.rotation=Quaternion.AngleAxis(yaw,root.up)*Quaternion.AngleAxis(pitch,root.right)*bone.rotation;
    }
}

// Continuous, time-aware curves carry velocity through the cut and follow-through.
// Only the ready-pose endpoints have zero velocity; intermediate poses are not stops.
public static class SwordSwingMotion
{
    public const float WindupEnd=0.16f;
    public const float CutEnd=0.40f;
    public const float ContactPhase=0.28f;
    public struct Sample
    {
        public Vector3 wrist;
        public float yaw,pitch,roll,twist,lean;
    }
    static readonly float[] Times={0f,0.16f,0.28f,0.40f,0.56f,0.78f,1f};
    // Hand stays on the sword side of the belly; the blade completes the diagonal cut.
    static readonly Vector3[] Wrist={
        new Vector3(.45f,-.65f,.35f),new Vector3(.65f,-.25f,.25f),
        new Vector3(.45f,-.35f,.70f),new Vector3(.25f,-.50f,.64f),
        new Vector3(.28f,-.60f,.54f),new Vector3(.40f,-.65f,.38f),
        new Vector3(.45f,-.65f,.35f)};
    static readonly float[] Yaw={24f,66f,0f,-48f,-32f,15f,24f};
    static readonly float[] Pitch={30f,48f,0f,-24f,-15f,22f,30f};
    static readonly float[] Roll={0f,-8f,0f,8f,6f,1f,0f};
    static readonly float[] Twist={0f,-12f,5f,14f,8f,1f,0f};
    static readonly float[] Lean={0f,-4f,5f,6f,3f,0f,0f};

    public static Sample Evaluate(float phase)
    {
        phase=Mathf.Clamp01(phase);
        int i=0;
        while(i<Times.Length-2 && phase>Times[i+1]) i++;
        float span=Times[i+1]-Times[i], t=(phase-Times[i])/span;
        return new Sample {
            wrist=Hermite(Wrist[i],Wrist[i+1],Tangent(Wrist,i)*span,Tangent(Wrist,i+1)*span,t),
            yaw=Curve(Yaw,i,span,t),pitch=Curve(Pitch,i,span,t),roll=Curve(Roll,i,span,t),
            twist=Curve(Twist,i,span,t),lean=Curve(Lean,i,span,t)
        };
    }
    static Vector3 Tangent(Vector3[] values,int i) => i==0 || i==values.Length-1 ? Vector3.zero : (values[i+1]-values[i-1])/(Times[i+1]-Times[i-1]);
    static float Tangent(float[] values,int i) => i==0 || i==values.Length-1 ? 0f : (values[i+1]-values[i-1])/(Times[i+1]-Times[i-1]);
    static float Curve(float[] values,int i,float span,float t)
    {
        float t2=t*t,t3=t2*t;
        return (2*t3-3*t2+1)*values[i]+(t3-2*t2+t)*Tangent(values,i)*span
            +(-2*t3+3*t2)*values[i+1]+(t3-t2)*Tangent(values,i+1)*span;
    }
    static Vector3 Hermite(Vector3 a,Vector3 b,Vector3 ta,Vector3 tb,float t)
    {
        float t2=t*t,t3=t2*t;
        return (2*t3-3*t2+1)*a+(t3-2*t2+t)*ta+(-2*t3+3*t2)*b+(t3-t2)*tb;
    }
}
