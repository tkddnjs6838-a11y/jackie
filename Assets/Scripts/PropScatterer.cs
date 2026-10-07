using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scatters the map decoration fresh at the start of every run, so no two runs have the
/// same terrain to fight in. Colliders are built here too, since the props only exist at
/// runtime now — a prop the player can walk through is worse than no prop.
/// </summary>
public class PropScatterer : MonoBehaviour
{
    [System.Serializable]
    public class PropKind
    {
        public GameObject prefab;
        public float weight = 1f;
        public float minScale = 1f;
        public float maxScale = 2f;
        public bool standingCollider = false;      // capsule on the trunk instead of a sphere
        public float trunkRadiusFactor = 0.12f;
    }

    public PropKind[] kinds;

    [Header("Area")]
    public int propCount = 70;
    public float extent = 29f;
    public float riverHalfWidth = 8.5f;
    public float minSpacing = 3.1f;
    public float clearAroundSpawn = 7f;
    public float clearAroundGoal = 6f;
    public int maxAttempts = 1600;

    private bool goalCleared;

    void Start()
    {
        Scatter();
    }

    void LateUpdate()
    {
        // the goal portal picks its own random spot in Start too, and script order is not
        // guaranteed, so clear the props around it once both have run
        if (goalCleared) return;
        goalCleared = true;
        ClearAroundGoal();
    }

    void Scatter()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        if (kinds == null || kinds.Length == 0) return;

        float totalWeight = 0f;
        foreach (var k in kinds) if (k != null && k.prefab != null) totalWeight += Mathf.Max(0f, k.weight);
        if (totalWeight <= 0f) return;

        var terrain = Terrain.activeTerrain;
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        Vector3 spawn = playerObj != null ? playerObj.transform.position : Vector3.zero;

        var placed = new List<Vector3>();
        for (int attempt = 0; attempt < maxAttempts && placed.Count < propCount; attempt++)
        {
            float x = Random.Range(-extent, extent);
            float z = Random.Range(-extent, extent);
            if (Mathf.Abs(x) < riverHalfWidth) continue;

            Vector3 p = new Vector3(x, 0f, z);
            if (Vector3.Distance(new Vector3(spawn.x, 0f, spawn.z), p) < clearAroundSpawn) continue;

            bool crowded = false;
            for (int i = 0; i < placed.Count; i++)
                if (Vector3.Distance(placed[i], p) < minSpacing) { crowded = true; break; }
            if (crowded) continue;

            var kind = PickKind(totalWeight);
            if (kind == null) continue;

            float y = terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : spawn.y;
            var go = (GameObject)Instantiate(kind.prefab, new Vector3(x, y, z),
                                             Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            go.transform.localScale = Vector3.one * Random.Range(kind.minScale, kind.maxScale);
            go.name = kind.prefab.name + "_" + placed.Count;

            AddCollider(go.transform, kind);
            placed.Add(p);
        }
    }

    PropKind PickKind(float totalWeight)
    {
        float roll = Random.value * totalWeight;
        float acc = 0f;
        foreach (var k in kinds)
        {
            if (k == null || k.prefab == null) continue;
            acc += Mathf.Max(0f, k.weight);
            if (roll <= acc) return k;
        }
        return null;
    }

    void AddCollider(Transform t, PropKind kind)
    {
        var rend = t.GetComponentInChildren<Renderer>();
        if (rend == null) return;

        float s = Mathf.Max(0.0001f, t.localScale.x);
        Vector3 world = rend.bounds.size;
        float localHeight = world.y / s;
        float localWidth = Mathf.Max(world.x, world.z) / s;

        if (kind.standingCollider)
        {
            var cap = t.gameObject.AddComponent<CapsuleCollider>();
            cap.direction = 1;
            cap.height = localHeight;
            cap.radius = Mathf.Max(0.12f, localWidth * kind.trunkRadiusFactor);
            cap.center = new Vector3(0f, localHeight * 0.5f, 0f);
        }
        else
        {
            var sph = t.gameObject.AddComponent<SphereCollider>();
            sph.radius = Mathf.Max(localWidth, localHeight) * 0.42f;
            sph.center = new Vector3(0f, localHeight * 0.42f, 0f);
        }
    }

    void ClearAroundGoal()
    {
        var goal = GameObject.Find("WinZone");
        if (goal == null) return;

        Vector3 g = new Vector3(goal.transform.position.x, 0f, goal.transform.position.z);
        int removed = 0;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            Vector3 c = new Vector3(child.position.x, 0f, child.position.z);
            if (Vector3.Distance(c, g) < clearAroundGoal) { Destroy(child.gameObject); removed++; }
        }
        if (removed > 0) Debug.Log("PropScatterer: cleared " + removed + " prop(s) around the goal portal.");
    }
}
