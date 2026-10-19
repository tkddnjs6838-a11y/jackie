using UnityEngine;

// Authored in Blender; each finger retains its own joint rotations for later weapon poses.
public class WeaponHandGrip : MonoBehaviour
{
    [System.Serializable]
    public struct FingerPose { public Transform bone; public Quaternion rotation; }
    public FingerPose[] fingers;
    public Vector3 swordLocalPosition;
    public bool useSolvedPosition;

    public void Apply(Transform sword)
    {
        if(fingers!=null)
            foreach(var finger in fingers)
                if(finger.bone!=null) finger.bone.localRotation=finger.rotation;
        if(sword!=null && !useSolvedPosition) sword.localPosition=swordLocalPosition;
    }
}
