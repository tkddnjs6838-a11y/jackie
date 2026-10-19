using UnityEngine;
using System.Collections.Generic;

// The new mascot uses its authored proportions instead of humanoid retargeting.
// Shared combat code can still address semantic bones for either rig.
public sealed class ToyCharacterRig : MonoBehaviour
{
    readonly Dictionary<HumanBodyBones,Transform> bones=new Dictionary<HumanBodyBones,Transform>();
    bool initialized;
    void Awake(){Initialize();}
    void Initialize()
    {
        if(initialized)return;initialized=true;
        foreach(var t in GetComponentsInChildren<Transform>(true))
        {
            HumanBodyBones b;
            switch(t.name)
            {
                case "Hips":b=HumanBodyBones.Hips;break;
                case "Spine":b=HumanBodyBones.Spine;break;
                case "Chest":b=HumanBodyBones.Chest;break;
                case "Head":b=HumanBodyBones.Head;break;
                case "LeftArm":b=HumanBodyBones.LeftUpperArm;break;
                case "LeftForeArm":b=HumanBodyBones.LeftLowerArm;break;
                case "LeftHand":b=HumanBodyBones.LeftHand;break;
                case "RightArm":b=HumanBodyBones.RightUpperArm;break;
                case "RightForeArm":b=HumanBodyBones.RightLowerArm;break;
                case "RightHand":b=HumanBodyBones.RightHand;break;
                case "LeftUpLeg":b=HumanBodyBones.LeftUpperLeg;break;
                case "LeftLeg":b=HumanBodyBones.LeftLowerLeg;break;
                case "LeftFoot":b=HumanBodyBones.LeftFoot;break;
                case "RightUpLeg":b=HumanBodyBones.RightUpperLeg;break;
                case "RightLeg":b=HumanBodyBones.RightLowerLeg;break;
                case "RightFoot":b=HumanBodyBones.RightFoot;break;
                default:continue;
            }
            bones[b]=t;
        }
    }
    public static Transform Bone(Animator animator,HumanBodyBones bone)
    {
        if(animator==null)return null;
        if(animator.isHuman)return animator.GetBoneTransform(bone);
        var rig=animator.GetComponent<ToyCharacterRig>();if(rig==null)return null;
        rig.Initialize();return rig.bones.TryGetValue(bone,out var t)?t:null;
    }
}
