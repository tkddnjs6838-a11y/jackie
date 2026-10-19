using UnityEngine;

// The attack coroutine owns this pose clock, so the strike cannot drift away from damage/VFX.
[DefaultExecutionOrder(100)]
public sealed class BossAttackMotion : MonoBehaviour
{
    public Transform[] bones;
    public Transform visual;
    public AnimationClip authoredIdle, authoredWalk, authoredSlam, authoredWings;
    public AnimationClip authoredThrow, authoredPour;
    public bool applyAuthoredSlamCorrection = true;
    public float authoredStrideLength = 4f;
    public bool HasAuthoredMotion => authoredSlam != null;
    float idleClock;
    void Sample(AnimationClip clip,float normalized)
    {
        if(clip!=null && visual!=null)clip.SampleAnimation(visual.gameObject,Mathf.Clamp01(normalized)*clip.length);
    }
    Vector3 visualPosition, visualScale;
    bool visualCached;
    Vector3[] positions;
    Quaternion[] rotations;
    Vector3 previous;
    float step;
    Enemy enemy;
    public string Phase { get; private set; } = "Idle";
    public float Progress { get; private set; }
    public int ImpactCount { get; private set; }
    void Awake() { Cache(); }
    void Cache()
    {
        if (!visualCached && visual != null) { visualPosition=visual.localPosition; visualScale=visual.localScale; visualCached=true; }
        if (positions != null || bones == null) return;
        positions = new Vector3[bones.Length]; rotations = new Quaternion[bones.Length];
        for(int i=0;i<bones.Length;i++){positions[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;}
        enemy=GetComponent<Enemy>(); previous=transform.position;
    }
    Transform Bone(string name){foreach(var b in bones)if(b.name==name)return b;return null;}
    void Turn(string name,float x=0,float y=0,float z=0)
    {
        var b=Bone(name);if(b!=null)b.localRotation=Quaternion.Euler(x,y,z);
    }
    void ResetPose()
    {
        Cache();
        if(visualCached && visual!=null){visual.localPosition=visualPosition;visual.localScale=visualScale;}
        if(positions==null)return;
        for(int i=0;i<bones.Length;i++){bones[i].localPosition=positions[i];bones[i].localRotation=rotations[i];}
    }
    static float Ease(float t)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
    public void Pose(string phase,float t)
    {
        if(bones==null || bones.Length==0)return;
        ResetPose();Phase=phase;Progress=Mathf.Clamp01(t);
        if(phase=="HopTell" || phase=="HopAir" || phase=="HopRecover")
        {
            Sample(authoredIdle,0);
            float compress=phase=="HopTell"?Ease(Progress):phase=="HopRecover"?1-Ease(Progress):0;
            float airborne=phase=="HopAir"?Mathf.Sin(Progress*Mathf.PI):0;
            if(visual!=null)
            {
                visual.localScale=Vector3.Scale(visualScale,new Vector3(1+.12f*compress,1-.2f*compress,1+.12f*compress));
                visual.position+=Vector3.up*(airborne*3.2f);
            }
            foreach(var side in new[]{"L","R"})
            {
                var thigh=Bone("Thigh."+side);var shin=Bone("Shin."+side);
                if(thigh!=null)thigh.localRotation*=Quaternion.Euler(-25*airborne,0,0);
                if(shin!=null)shin.localRotation*=Quaternion.Euler(45*airborne,0,0);
            }
            return;
        }
        if(HasAuthoredMotion)
        {
            if(phase=="LavaThrowTell")Sample(authoredThrow,Progress*.6f);
            else if(phase=="LavaThrowRecover")Sample(authoredThrow,Mathf.Lerp(.6f,1,Progress));
            else if(phase=="LavaPourTell")Sample(authoredPour,Progress*.55f);
            else if(phase=="LavaPourHold")Sample(authoredPour,.55f);
            else if(phase=="LavaPourRecover")Sample(authoredPour,Mathf.Lerp(.55f,1,Progress));
            else if(phase=="SlamTell" || phase=="SlamRecover")
            {
                float clipTime=phase=="SlamTell"?Mathf.Lerp(0,.65f,Progress):Mathf.Lerp(.65f,1,Progress);
                Sample(authoredSlam,clipTime);
                float contact=(applyAuthoredSlamCorrection?1f:0f)*Ease(Mathf.InverseLerp(.52f,.65f,clipTime))*(1-Ease(Mathf.InverseLerp(.70f,1,clipTime)));
                var authoredPelvis=Bone("Pelvis");if(authoredPelvis!=null)authoredPelvis.position-=transform.up*(.8f*contact);
                var spine=Bone("Spine");if(spine!=null)spine.rotation=Quaternion.AngleAxis(20*contact,transform.right)*spine.rotation;
                foreach(var side in new[]{"L","R"})
                {
                    var thigh=Bone("Thigh."+side);var shin=Bone("Shin."+side);
                    if(thigh!=null)thigh.rotation=Quaternion.AngleAxis(-40*contact,transform.right)*thigh.rotation;
                    if(shin!=null)shin.rotation=Quaternion.AngleAxis(70*contact,transform.right)*shin.rotation;
                }
            }
            else if(phase=="ChargeTell")Sample(authoredWings,Progress*.45f);
            else if(phase=="Charge")Sample(authoredWings,.45f);
            else if(phase=="ChargeRecover")Sample(authoredWings,Mathf.Lerp(.45f,1,Progress));
            return;
        }
        float lift=0,hit=0,lean=0;
        if(phase=="SlamTell") {lift=Ease(t/.58f); if(t>.76f){float down=Mathf.Pow(Mathf.InverseLerp(.76f,1,t),2);lift=1-down;hit=down;} }
        else if(phase=="SlamRecover")hit=1-Ease(Mathf.InverseLerp(.14f,1,t));
        else if(phase=="ChargeTell")lean=Ease(t)*18;
        else if(phase=="Charge")lean=25;
        else if(phase=="ChargeRecover")lean=25*(1-Ease(t));
        Turn("Chest",-12*lift+55*hit+lean);
        Turn("Head",7*lift-25*hit-lean*.5f);
        var pelvis=Bone("Pelvis");if(pelvis!=null)pelvis.localPosition+=Vector3.down*(1.1f*hit+.12f*lean/25);
        foreach(var side in new[]{"L","R"})
        {
            float sign=side=="L"?1:-1;
            Turn("Arm."+side,-85*lift-65*hit+lean*.5f,0,sign*(8+12*lift));
            Turn("Forearm."+side,-18*lift-12*hit);
            Turn("Thigh."+side,-65*hit);Turn("Shin."+side,105*hit);
        }
    }
    public void MarkImpact(){ImpactCount++;}
    public void Release(){ResetPose();if(HasAuthoredMotion)Sample(authoredIdle,0);Phase="Idle";Progress=0;previous=transform.position;}
    public Vector3 FistPoint(string side)
    {
        var b=Bone("Hand."+side);return b==null?transform.position:b.position;
    }
    void LateUpdate()
    {
        if(enemy==null || enemy.IsDying || Time.deltaTime<=0)return;
        float distance=Vector3.Distance(previous,transform.position);previous=transform.position;
        if(enemy.externalMovement)return;
        ResetPose();Phase="Idle";
        if(HasAuthoredMotion)
        {
            idleClock+=Time.deltaTime;step+=distance/Mathf.Max(.1f,authoredStrideLength);
            Sample(distance>.0001f?authoredWalk:authoredIdle,distance>.0001f?Mathf.Repeat(step,1):Mathf.Repeat(idleClock/2,1));
            return;
        }
        if(distance<.0001f)return;
        step+=distance*1.6f;
        float swing=Mathf.Sin(step)*13;
        Turn("Thigh.L",swing);Turn("Thigh.R",-swing);
        Turn("Shin.L",Mathf.Max(0,-swing));Turn("Shin.R",Mathf.Max(0,swing));
        Turn("Arm.L",-swing*.55f,0,8);Turn("Arm.R",swing*.55f,0,-8);
        Turn("Chest",0,Mathf.Sin(step)*2);
    }
    void OnDisable(){Release();}
}
