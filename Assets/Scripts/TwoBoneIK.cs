using UnityEngine;

// Analytic two-bone IK (shoulder-elbow-wrist or hip-knee-ankle). Bends the middle joint toward `pole`
// and leaves the end bone's own rotation to the caller.
public static class TwoBoneIK
{
    public static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
    {
        float a = Vector3.Distance(upper.position, lower.position);
        float b = Vector3.Distance(lower.position, end.position);
        if (a < 0.0001f || b < 0.0001f) return;
        Vector3 direction = (target - upper.position).normalized;
        float distance = Mathf.Clamp(Vector3.Distance(upper.position, target), Mathf.Abs(a - b) + 0.0001f, (a + b) * 0.98f);
        target = upper.position + direction * distance;
        Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
        float along = (a * a - b * b + distance * distance) / (2f * distance);
        float height = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
        Vector3 joint = upper.position + direction * along + bend * height;
        upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, joint - upper.position) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(end.position - lower.position, target - lower.position) * lower.rotation;
    }
}
