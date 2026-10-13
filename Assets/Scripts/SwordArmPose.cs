using UnityEngine;

// A one-handed, chest-height cut sized to this character's short arms and large head.
// Solve the arm and wrist together; the sword never rotates independently in the palm.
public static class SwordArmPose
{
    public static void Apply(Animator animator, Transform root, Transform sword, Vector3 grip, float phase, bool attacking, bool axe = false, bool gun = false, float recoil = 0f, Vector3 gunDirection = default)
    {
        if (animator == null || !animator.isHuman || sword == null) return;
        var upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        var lower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (upper == null || lower == null || hand == null) return;

        var motion=axe ? AxeSwingMotion.Evaluate(attacking ? phase : 0f) : SwordSwingMotion.Evaluate(attacking ? phase : 0f);
        Vector3 wrist=gun ? new Vector3(.46f,-.48f,.57f-recoil*.10f) : motion.wrist;
        float yaw=motion.yaw*Mathf.Deg2Rad, pitch=motion.pitch*Mathf.Deg2Rad;
        Vector3 blade=new Vector3(Mathf.Sin(yaw)*Mathf.Cos(pitch),Mathf.Sin(pitch),Mathf.Cos(yaw)*Mathf.Cos(pitch));

        float a = Vector3.Distance(upper.position, lower.position);
        float b = Vector3.Distance(lower.position, hand.position);
        float reach = a + b;
        if (a < 0.0001f || b < 0.0001f) return;
        Vector3 target = upper.position + root.rotation * wrist * reach;
        Vector3 direction = (target-upper.position).normalized;
        float distance = Mathf.Clamp(Vector3.Distance(upper.position,target), Mathf.Abs(a-b)+0.0001f, reach*0.98f);
        target = upper.position + direction*distance;
        Vector3 pole = root.rotation * new Vector3(1f,-0.2f,-0.35f);
        Vector3 bend = Vector3.ProjectOnPlane(pole,direction).normalized;
        float along = (a*a-b*b+distance*distance)/(2f*distance);
        float height = Mathf.Sqrt(Mathf.Max(0f,a*a-along*along));
        Vector3 elbow = upper.position + direction*along + bend*height;
        upper.rotation = Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
        lower.rotation = Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;

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
    readonly Transform[] bones = new Transform[4];
    readonly Quaternion[] original = new Quaternion[4];
    bool applied;

    public void Restore()
    {
        if (!applied) return;
        for(int i=0;i<bones.Length;i++)
            if(bones[i]!=null) bones[i].localRotation=original[i];
        applied=false;
    }

    public void Apply(Animator animator, Transform root, float phase, bool attacking, bool axe = false)
    {
        if(!attacking || animator==null || !animator.isHuman) return;
        bones[0]=animator.GetBoneTransform(HumanBodyBones.Spine);
        bones[1]=animator.GetBoneTransform(HumanBodyBones.Chest);
        bones[2]=animator.GetBoneTransform(HumanBodyBones.Head);
        bones[3]=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        for(int i=0;i<bones.Length;i++)
            if(bones[i]!=null) original[i]=bones[i].localRotation;
        applied=true;
        // The torso leads slightly, then settles while the hand completes recovery.
        float lead=0.025f*Mathf.Sin(Mathf.Clamp01(phase)*Mathf.PI);
        var motion=axe ? AxeSwingMotion.Evaluate(phase+lead) : SwordSwingMotion.Evaluate(phase+lead);
        float twist=motion.twist;
        float lean=motion.lean;
        Rotate(bones[0],root,twist*0.45f,lean*0.45f);
        Rotate(bones[1],root,twist*0.55f,lean*0.55f);
        // Keep the eyes on the target while the chest leads the cut.
        Rotate(bones[2],root,-twist*0.65f,-lean*0.65f);
        if(bones[3]!=null)
            bones[3].rotation=Quaternion.AngleAxis(-twist*0.4f,root.forward)*bones[3].rotation;
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
    static readonly Vector3[] Wrist={
        new Vector3(0.24f,-0.67f,0.45f),new Vector3(0.58f,-0.26f,0.52f),
        new Vector3(0.08f,-0.28f,0.88f),new Vector3(-0.32f,-0.39f,0.78f),
        new Vector3(-0.16f,-0.62f,0.66f),new Vector3(0.19f,-0.69f,0.47f),
        new Vector3(0.24f,-0.67f,0.45f)};
    static readonly float[] Yaw={20f,61f,0f,-60f,-43f,14f,20f};
    static readonly float[] Pitch={52f,44f,-2f,-18f,-9f,35f,52f};
    static readonly float[] Roll={0f,-16f,0f,18f,12f,3f,0f};
    static readonly float[] Twist={0f,-14f,10f,21f,12f,2f,0f};
    static readonly float[] Lean={0f,-2f,2f,3f,2f,-0.5f,0f};

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
