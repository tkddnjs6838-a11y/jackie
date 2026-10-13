using UnityEngine;

// An overhead lift and accelerating forward downward chop, with contact during the cut,
// followed by a short low follow-through and recovery. Uses the existing hand IK.
public static class AxeSwingMotion
{
    public const float ContactPhase = .44f;
    static SwordSwingMotion.Sample Pose(Vector3 wrist,float yaw,float pitch,float roll,float twist,float lean)
        => new SwordSwingMotion.Sample{wrist=wrist,yaw=yaw,pitch=pitch,roll=roll,twist=twist,lean=lean};
    public static SwordSwingMotion.Sample Evaluate(float phase)
    {
        phase=Mathf.Clamp01(phase);
        var idle=Pose(new Vector3(.46f,-.48f,.30f),55,-50,-8,0,0);
        var raised=Pose(new Vector3(.72f,.64f,-.04f),15,86,90,0,-8);
        var follow=Pose(new Vector3(.55f,-.22f,.68f),15,-15,90,0,14);
        if(phase<.28f)return Blend(idle,raised,Mathf.SmoothStep(0,1,phase/.28f));
        // No keyframe stop at contact: the blade carries speed into follow-through.
        if(phase<.50f)return Blend(raised,follow,Mathf.SmoothStep(0,1,(phase-.28f)/.22f));
        if(phase<.62f)return Blend(follow,Pose(new Vector3(.52f,-.30f,.60f),15,-12,90,0,6),(phase-.50f)/.12f);
        return Blend(Pose(new Vector3(.52f,-.30f,.60f),15,-12,90,0,6),idle,Mathf.SmoothStep(0,1,(phase-.62f)/.38f));
    }
    static SwordSwingMotion.Sample Blend(SwordSwingMotion.Sample a,SwordSwingMotion.Sample b,float t)
        => Pose(Vector3.Lerp(a.wrist,b.wrist,t),Mathf.Lerp(a.yaw,b.yaw,t),Mathf.Lerp(a.pitch,b.pitch,t),Mathf.Lerp(a.roll,b.roll,t),Mathf.Lerp(a.twist,b.twist,t),Mathf.Lerp(a.lean,b.lean,t));
}
