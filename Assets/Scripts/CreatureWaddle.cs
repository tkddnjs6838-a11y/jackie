using UnityEngine;

/// <summary>
/// Body-level walk motion for the round creatures: bounce, side-to-side roll, a forward
/// lean and a squash/stretch, all driven by how fast the creature is actually moving.
/// The generated leg animation barely reads on these stubby-legged meshes, so the sense
/// of walking comes from the body instead.
/// </summary>
public class CreatureWaddle : MonoBehaviour
{
    public Transform body;

    [Header("Cadence")]
    public float stepsPerMeter = 1.6f;
    public float minStepsPerSecond = 1.2f;

    [Header("Amplitude")]
    public float bounceHeight = 0.08f;
    public float rollDegrees = 8f;
    public float leanDegrees = 5f;
    public float squash = 0.05f;

    [Header("Response")]
    public float speedForFullAmplitude = 3f;
    public float amplitudeLerp = 6f;

    [Header("Hit Squish")]
    [Range(0f, 0.7f)] public float hitSquash = 0.42f;
    public float hitSquishDuration = 0.38f;

    

    // Chargers keep their short, cute proportions. Their state is expressed by
    // deforming the whole rendered body instead of relying on a skeletal rig.
    private EnemyCharger charger;
    private Vector3 chargerPoseScale = Vector3.one;
    private Vector3 chargerPoseOffset;
    private Quaternion chargerPoseRotation = Quaternion.identity;
private float hitSquishTime = -1f;
    Vector3 hitDirection;
    float hitIntensity=1f;

    private Vector3 restPosition;
    private Quaternion restRotation;
    private Vector3 restScale;
    private Vector3 lastPosition;
    private float phase;
    private float amplitude;

void Start()
    {
        if (body == null)
        {
            // The visual child, i.e. the first child that carries a renderer.
            foreach (Transform child in transform)
                if (child.GetComponentInChildren<Renderer>() != null) { body = child; break; }
        }
        if (body == null) { enabled = false; return; }

        restPosition = body.localPosition;
        restRotation = body.localRotation;
        restScale = body.localScale;
        lastPosition = transform.position;
        charger = GetComponent<EnemyCharger>();
    }

    public void PunchSquish() { PunchSquish(transform.position-transform.forward,1f); }
    public void PunchSquish(Vector3 contact,float strength)
    {
        hitSquishTime=0f;hitIntensity=Mathf.Clamp(strength,.2f,1.2f);
        Vector3 away=transform.position-contact;away.y=0;
        hitDirection=transform.InverseTransformDirection(away.sqrMagnitude>.001f?away.normalized:transform.forward);
    }


void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;

        float speed = delta.magnitude / dt;
        float target = Mathf.Clamp01(speed / Mathf.Max(0.01f, speedForFullAmplitude));
        amplitude = Mathf.Lerp(amplitude, target, amplitudeLerp * dt);

        // Cadence follows distance covered, so the bounce never slides against the ground.
        float stepsPerSecond = Mathf.Max(minStepsPerSecond, speed * stepsPerMeter);
        phase += stepsPerSecond * Mathf.PI * dt;

        float bounce = Mathf.Abs(Mathf.Sin(phase)) - 0.5f;
        float sway = Mathf.Sin(phase * 0.5f);

        Vector3 stateScale = Vector3.one;
        Vector3 stateOffset = Vector3.zero;
        Quaternion stateRotation = Quaternion.identity;

        if (charger != null)
        {
            float fastPulse = Mathf.Sin(Time.time * 30f);
            float tiredWobble = Mathf.Sin(Time.time * 9f);

            switch (charger.StateName)
            {
                case "WindUp":
                    // Pull back, spread sideways and tremble before the dash.
                    stateScale = new Vector3(1.18f + fastPulse * 0.025f,
                                             0.70f - fastPulse * 0.015f,
                                             0.82f);
                    stateOffset = new Vector3(fastPulse * 0.012f, -0.035f, -0.10f);
                    stateRotation = Quaternion.Euler(0f, 0f, fastPulse * 2.4f);
                    break;

                case "Charge":
                    // Stretch in the local forward direction while keeping the silhouette chunky.
                    stateScale = new Vector3(0.86f, 0.90f, 1.30f);
                    stateOffset = new Vector3(0f, 0.035f, 0.09f);
                    stateRotation = Quaternion.Euler(-7f, 0f, 0f);
                    break;

                case "Recover":
                    // Land as a soft, tired pancake and wobble back into shape.
                    stateScale = new Vector3(1.20f + tiredWobble * 0.04f,
                                             0.76f - Mathf.Abs(tiredWobble) * 0.04f,
                                             1.12f);
                    stateOffset = new Vector3(0f, -0.045f, 0f);
                    stateRotation = Quaternion.Euler(tiredWobble * 3f, 0f, tiredWobble * 9f);
                    break;
            }
        }

        float poseLerp = 1f - Mathf.Exp(-11f * dt);
        chargerPoseScale = Vector3.Lerp(chargerPoseScale, stateScale, poseLerp);
        chargerPoseOffset = Vector3.Lerp(chargerPoseOffset, stateOffset, poseLerp);
        chargerPoseRotation = Quaternion.Slerp(chargerPoseRotation, stateRotation, poseLerp);

        body.localPosition = restPosition
                           + Vector3.up * (bounce * bounceHeight * amplitude)
                           + chargerPoseOffset;
        body.localRotation = restRotation
                           * Quaternion.Euler(leanDegrees * amplitude, 0f, -sway * rollDegrees * amplitude)
                           * chargerPoseRotation;

        float walkScale = Mathf.Max(0.55f, 1f - bounce * squash * amplitude);
        Vector3 finalScale = new Vector3(restScale.x / walkScale,
                                         restScale.y * walkScale,
                                         restScale.z / walkScale);
        finalScale = Vector3.Scale(finalScale, chargerPoseScale);

        // A single sine pulse makes the wax ball spread sideways, flatten, then recover.
        // It is multiplied over the walk and charger poses so every effect can coexist.
        if (hitSquishTime >= 0f)
        {
            hitSquishTime += dt;
            float duration = .3f;
            float progress = Mathf.Clamp01(hitSquishTime / duration);
            float pulse = Mathf.Sin(progress*Mathf.PI*2f)*Mathf.Exp(-3f*progress)*hitIntensity;
            float height = 1f-.18f*pulse;
            float side = 1f/Mathf.Sqrt(height);
            finalScale=Vector3.Scale(finalScale,new Vector3(side,height,side));
            body.localPosition+=hitDirection*(.12f*pulse);
            body.localRotation*=Quaternion.AngleAxis(12f*pulse,Vector3.Cross(Vector3.up,hitDirection));

            if (progress >= 1f)
                hitSquishTime = -1f;
        }

        body.localScale = finalScale;
    }
}
