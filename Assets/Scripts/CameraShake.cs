using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    private Vector3 originalLocalPos;
    private float shakeTimer;
    private float shakeMagnitude;

    void Awake()
    {
        Instance = this;
        originalLocalPos = transform.localPosition;
    }

    void LateUpdate()
    {
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            Vector3 offset = Random.insideUnitSphere * shakeMagnitude;
            offset.z = 0f;
            transform.localPosition = originalLocalPos + offset;

            if (shakeTimer <= 0f)
                transform.localPosition = originalLocalPos;
        }
    }

    public void Shake(float duration, float magnitude)
    {
        if (duration > shakeTimer)
        {
            shakeTimer = duration;
            shakeMagnitude = magnitude;
        }
    }
}
