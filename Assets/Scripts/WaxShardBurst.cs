using UnityEngine;

/// <summary>
/// Throws a few lightweight wax-shell chips on every hit.
/// Each chip moves without physics collisions, keeping the effect cheap for WebGL.
/// </summary>
public class WaxShardBurst : MonoBehaviour
{
    public int chipsPerHit = 5;
    public int chipsOnDeath = 10;
    public float chipLifetime = 0.65f;
    public float launchSpeed = 3.2f;

    private Renderer sourceRenderer;
    private CapsuleCollider capsule;

    void Awake()
    {
        sourceRenderer = GetComponentInChildren<Renderer>();
        capsule = GetComponent<CapsuleCollider>();
    }

    public void Burst(Color shellColor, bool lethal)
    {
        int count = lethal ? chipsOnDeath : chipsPerHit;
        float radius = capsule != null ? capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) : 0.5f;
        float height = capsule != null ? capsule.height * transform.lossyScale.y : 1.5f;
        Vector3 center = capsule != null ? transform.TransformPoint(capsule.center) : transform.position + Vector3.up * (height * 0.5f);

        Vector3 away = transform.position - (Camera.main != null ? Camera.main.transform.position : transform.position - transform.forward);
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = transform.forward;
        away.Normalize();

        for (int i = 0; i < count; i++)
        {
            Vector3 spawn = center + Random.insideUnitSphere * radius * 0.55f;
            spawn.y = Mathf.Max(spawn.y, transform.position.y + height * 0.25f);

            var chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chip.name = "WaxShellChip";
            chip.transform.position = spawn;
            chip.transform.rotation = Random.rotation;
            float size = Random.Range(radius * 0.10f, radius * 0.19f);
            chip.transform.localScale = new Vector3(size, size * Random.Range(0.18f, 0.35f), size * Random.Range(0.55f, 1f));

            var col = chip.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var renderer = chip.GetComponent<Renderer>();
            Material material = null;
            if (sourceRenderer != null && sourceRenderer.sharedMaterial != null)
            {
                material = new Material(sourceRenderer.sharedMaterial);
                ApplyColor(material, shellColor);
                renderer.sharedMaterial = material;
            }

            Vector3 spread = away * Random.Range(0.5f, 1.25f)
                           + Random.insideUnitSphere * 0.85f
                           + Vector3.up * Random.Range(0.65f, 1.25f);
            var piece = chip.AddComponent<WaxShardPiece>();
            piece.Begin(spread.normalized * launchSpeed * Random.Range(0.75f, 1.25f),
                        Random.insideUnitSphere * 520f, chipLifetime, material);
        }
    }

    static void ApplyColor(Material material, Color color)
    {
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("baseColorFactor")) material.SetColor("baseColorFactor", color);
    }
}

public class WaxShardPiece : MonoBehaviour
{
    private Vector3 velocity;
    private Vector3 spin;
    private float lifetime;
    private float age;
    private Material ownedMaterial;

    public void Begin(Vector3 initialVelocity, Vector3 degreesPerSecond, float seconds, Material material)
    {
        velocity = initialVelocity;
        spin = degreesPerSecond;
        lifetime = seconds;
        ownedMaterial = material;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        velocity += Vector3.down * 9.5f * dt;
        transform.position += velocity * dt;
        transform.Rotate(spin * dt, Space.Self);

        if (age >= lifetime)
            Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (ownedMaterial != null)
            Destroy(ownedMaterial);
    }
}
