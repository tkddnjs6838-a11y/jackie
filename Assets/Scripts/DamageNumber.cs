using UnityEngine;

public class DamageNumber : MonoBehaviour
{
    static readonly Unity.Profiling.ProfilerMarker CostMarker = new Unity.Profiling.ProfilerMarker("Combat.DamageNumber");

    public float floatSpeed = 1.2f;
    public float lifetime = 0.7f;

    private float timer;
    private TextMesh textMesh;
    private Color startColor;

    void Awake()
    {
        textMesh = GetComponent<TextMesh>();
        startColor = textMesh.color;
    }

    void Update()
    {
        timer += Time.deltaTime;
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;

        float t = timer / lifetime;
        Color c = startColor;
        c.a = Mathf.Lerp(1f, 0f, t);
        textMesh.color = c;

        if (timer >= lifetime)
            Destroy(gameObject);
    }

    public static void Create(Vector3 worldPos, string text, Color color)
    {
        using (CostMarker.Auto())
        {
            GameObject go = new GameObject("DamageNumber");
            go.transform.position = worldPos;

            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.color = color;
            tm.fontSize = 64;
            tm.characterSize = 0.08f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;

            go.AddComponent<DamageNumber>();
        }
    }
}
