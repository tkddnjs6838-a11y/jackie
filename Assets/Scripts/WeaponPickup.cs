using UnityEngine;

// First weapon crate: no physics body, limited lifetime, scaled-time animation.
public sealed class WeaponPickup : MonoBehaviour
{
    public static int ActiveCount { get; private set; }
    public float lifetime = 35f;
    PlayerWeapons player;
    float age, baseY;
    bool collected;
    void OnEnable() { ActiveCount++; }
    void OnDisable() { ActiveCount--; }
    void Start()
    {
        player = FindFirstObjectByType<PlayerWeapons>(); baseY = transform.position.y;
    }
    void Update()
    {
        if (Time.timeScale <= 0f || RunStats.Instance==null || !RunStats.Instance.Running || collected) return;
        age += Time.deltaTime;
        if (age >= lifetime || (player != null && player.HasAxe)) { Destroy(gameObject); return; }
        transform.position = new Vector3(transform.position.x, baseY + Mathf.Sin(age * 2.5f) * .08f, transform.position.z);
        transform.Rotate(0, 35f * Time.deltaTime, 0);
        if (player == null) return;
        var distance = player.transform.position - transform.position; distance.y = 0;
        if (distance.sqrMagnitude <= 1.3f * 1.3f && player.CollectAxe())
        {
            collected = true; GameAudio.Play(GameAudio.Sfx.Pickup); Destroy(gameObject);
        }
    }
    public static bool Spawn(Vector3 at)
    {
        var prefab = Resources.Load<GameObject>("Weapons/AxeCrate");
        if (prefab == null) return false;
        if (Terrain.activeTerrain != null) at.y = Terrain.activeTerrain.SampleHeight(at) + Terrain.activeTerrain.transform.position.y;
        Instantiate(prefab, at + Vector3.up * .35f, Quaternion.identity); return true;
    }
}
