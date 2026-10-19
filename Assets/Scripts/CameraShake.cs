using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;
    public Vector3 Offset { get; private set; }
    Vector3 originalLocalPos;
    float shakeTimer,shakeMagnitude,shakeDuration;
    void Awake() { Instance=this; originalLocalPos=transform.localPosition; }
    void LateUpdate()
    {
        if(Time.timeScale<=0f) { ResetShake(); return; }
        if(shakeTimer<=0f){Offset=Vector3.zero;return;}
        // Impact slow-motion must not stretch a short jolt into a long vibration.
        shakeTimer=Mathf.Max(0,shakeTimer-Time.unscaledDeltaTime);
        float fade=shakeTimer/Mathf.Max(.001f,shakeDuration);
        Vector3 offset=Random.insideUnitSphere*(shakeMagnitude*fade*fade);
        offset.z=0;
        Offset=offset;
        if(GetComponent<CombatCamera>()==null)transform.localPosition=originalLocalPos+offset;
        if(shakeTimer<=0)shakeMagnitude=0;
    }
    public void Shake(float duration,float magnitude)
    {
        if(duration<=0 || magnitude<=0 || Time.timeScale<=0)return;
        if(shakeTimer<=0)originalLocalPos=transform.localPosition;
        // A weak, longer hit must not swallow a stronger short impact.
        shakeTimer=Mathf.Max(shakeTimer,duration);
        shakeDuration=shakeTimer;
        shakeMagnitude=Mathf.Max(shakeMagnitude,magnitude);
    }
    void ResetShake()
    {
        Offset=Vector3.zero;
        if(shakeTimer>0 && GetComponent<CombatCamera>()==null)transform.localPosition=originalLocalPos;
        shakeTimer=shakeMagnitude=shakeDuration=0;
    }
    void OnDisable(){ResetShake();}
    void OnDestroy(){if(Instance==this)Instance=null;}
}
