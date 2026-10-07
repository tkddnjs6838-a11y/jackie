using UnityEngine;

public class PlayerPose : MonoBehaviour
{
    public Transform leftArm;
    public Transform rightArm;
    public Transform leftForeArm;
    public Transform rightForeArm;

    public Vector3 leftArmRestEuler = new Vector3(278f, 351.92f, 359.51f);
    public Vector3 rightArmRestEuler = new Vector3(278f, 8.08f, 0.49f);
    public Vector3 leftForeArmRestEuler = new Vector3(0.75f, 359.53f, 3.89f);
    public Vector3 rightForeArmRestEuler = new Vector3(0.75f, 0.47f, 356.11f);

    void Start()
    {
        if (leftArm != null) leftArm.localEulerAngles = leftArmRestEuler;
        if (rightArm != null) rightArm.localEulerAngles = rightArmRestEuler;
        if (leftForeArm != null) leftForeArm.localEulerAngles = leftForeArmRestEuler;
        if (rightForeArm != null) rightForeArm.localEulerAngles = rightForeArmRestEuler;
    }
}
