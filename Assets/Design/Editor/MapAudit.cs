using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Walks every design map with a CharacterController the size of the player and reports where it
// cannot go, and where it can go but cannot come back from (stuck). Grid of 1.5 m cells over the
// ±31 m play area; each pair of neighbouring cells is tested by actually moving the controller
// across, so slopes, step heights, props and narrow gaps all count.
// Output: output/design/maps/audit_<map>.png (one pixel block per cell) and audit.txt.
public static class MapAudit
{
    const float Step = 1.5f, Extent = 31f;
    const float Waterline = 2.4f, PlayerDepth = 0.65f; // must match WaterFloat
    static readonly string[] Maps = { "Map01_Meadow", "Map02_Canyon", "Map03_WaxLake", "Map04_CrystalPlateau", "Map05_Crater" };

    static Terrain terrain;
    static CharacterController walker;
    static bool hasWater;
    static readonly Dictionary<long, bool> passCache = new Dictionary<long, bool>();
    static int n;

    [MenuItem("Tools/Design/Audit Map Traversal")]
    public static void Run()
    {
        if (!MapBuilder.LeaveDesignScenes()) return;
        n = Mathf.FloorToInt(Extent * 2f / Step) + 1;
        var report = new StringBuilder();
        foreach (var map in Maps) report.Append(AuditOne(map));
        File.WriteAllText("output/design/maps/audit.txt", report.ToString());
        Debug.Log("[MapAudit]\n" + report);
        PlaytestBuilder.OpenMeadow();
    }

    static string AuditOne(string map)
    {
        var scene = EditorSceneManager.OpenScene($"Assets/Design/Scenes/{map}.unity", OpenSceneMode.Single);
        terrain = Terrain.activeTerrain;
        passCache.Clear();
        Vector3 start = Vector3.zero;
        GameObject floor = null;
        hasWater = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            var marker = root.transform.Find("Markers/Marker_Start");
            if (marker != null) start = marker.position;
            if (root.transform.Find("Zone_WaterVolume") != null)
            {
                // stands in for WaterFloat's raft, which only exists in play mode
                hasWater = true;
                floor = new GameObject("AuditWaterFloor");
                floor.AddComponent<BoxCollider>().size = new Vector3(96f, 0.2f, 96f);
                floor.transform.position = new Vector3(0f, Waterline - PlayerDepth - 0.1f, 0f);
            }
        }

        var go = new GameObject("AuditWalker");
        go.transform.localScale = Vector3.one * 0.72f; // same as the game's Player
        walker = go.AddComponent<CharacterController>();
        walker.height = 2f;
        walker.radius = 0.5f;
        walker.stepOffset = 0.3f;
        walker.slopeLimit = 45f;
        walker.skinWidth = 0.02f;
        walker.center = Vector3.zero;
        Physics.SyncTransforms();

        var standable = new bool[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                standable[i, j] = CanStand(Cell(i, j));

        int si = Mathf.Clamp(Mathf.RoundToInt((start.x + Extent) / Step), 0, n - 1);
        int sj = Mathf.Clamp(Mathf.RoundToInt((start.z + Extent) / Step), 0, n - 1);
        var reach = Flood(si, sj, standable, forward: true);
        var back = Flood(si, sj, standable, forward: false);

        // colour map: green ok, blue ok in water, red stuck (reachable, no way back),
        // yellow flat ground that cannot be reached, grey walls / steep ground, black inside a prop
        // (steep cells are "not standable" too, so grey and black both mean "can't stand here")
        const int px = 8;
        var tex = new Texture2D(n * px, n * px, TextureFormat.RGB24, false);
        int stuck = 0, flatUnreached = 0, ok = 0;
        var stuckAt = new List<string>();
        var flatAt = new List<string>();
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                var c = Cell(i, j);
                float h = Ground(c);
                Color col;
                if (!standable[i, j]) col = Slope(c) > 1f ? new Color(0.55f, 0.55f, 0.55f) : new Color(0.1f, 0.1f, 0.1f);
                else if (reach[i, j] && back[i, j]) { col = hasWater && h < Waterline ? new Color(0.35f, 0.6f, 1f) : new Color(0.3f, 0.8f, 0.35f); ok++; }
                else if (reach[i, j]) { col = new Color(0.95f, 0.15f, 0.15f); stuck++; stuckAt.Add($"({c.x:F0},{c.z:F0})"); }
                else if (Slope(c) < 0.35f) { col = new Color(1f, 0.85f, 0.1f); flatUnreached++; flatAt.Add($"({c.x:F0},{c.z:F0})"); }
                else col = new Color(0.55f, 0.55f, 0.55f);
                if (i == si && j == sj) col = Color.white;
                for (int y = 0; y < px; y++)
                    for (int x = 0; x < px; x++)
                        tex.SetPixel(i * px + x, j * px + y, col);
            }
        tex.Apply();
        File.WriteAllBytes($"output/design/maps/audit_{map}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
        if (floor != null) Object.DestroyImmediate(floor);

        return $"{map}: ok {ok}, stuck {stuck}, flat-but-unreachable {flatUnreached}\n" +
               (stuck > 0 ? "  stuck at " + string.Join(" ", stuckAt) + "\n" : "") +
               (flatUnreached > 0 ? "  unreachable flat at " + string.Join(" ", flatAt) + "\n" : "");
    }

    static bool[,] Flood(int si, int sj, bool[,] standable, bool forward)
    {
        var seen = new bool[n, n];
        if (!standable[si, sj]) return seen;
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(si, sj));
        seen[si, sj] = true;
        var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        while (queue.Count > 0)
        {
            var a = queue.Dequeue();
            foreach (var d in dirs)
            {
                var b = a + d;
                if (b.x < 0 || b.y < 0 || b.x >= n || b.y >= n || seen[b.x, b.y] || !standable[b.x, b.y]) continue;
                bool pass = forward ? CanPass(a, b) : CanPass(b, a);
                if (!pass) continue;
                seen[b.x, b.y] = true;
                queue.Enqueue(b);
            }
        }
        return seen;
    }

    static Vector3 Cell(int i, int j) => new Vector3(-Extent + i * Step, 0f, -Extent + j * Step);
    static Vector3 Cell(Vector2Int c) => Cell(c.x, c.y);

    static float Ground(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

    static float Slope(Vector3 p)
    {
        const float e = 0.75f;
        float dx = Ground(p + Vector3.right * e) - Ground(p - Vector3.right * e);
        float dz = Ground(p + Vector3.forward * e) - Ground(p - Vector3.forward * e);
        return new Vector2(dx, dz).magnitude / (2f * e);
    }

    static void Place(Vector3 p)
    {
        walker.enabled = false;
        walker.transform.position = new Vector3(p.x, Mathf.Max(Ground(p), Waterline - PlayerDepth) + 0.8f, p.z);
        walker.enabled = true;
        Physics.SyncTransforms();
        walker.Move(Vector3.down * 1.5f);
    }

    // can the controller stand here: ground no steeper than its 45° slope limit (or water to float on),
    // and not inside a prop? Without the slope rule the test would teleport onto cliff faces and
    // "climb" walls the real player cannot.
    static bool CanStand(Vector3 p)
    {
        if (Slope(p) > 1f && !(hasWater && Ground(p) < Waterline - PlayerDepth)) return false;
        Place(p);
        var b = walker.bounds;
        float r = walker.radius * 0.72f;
        var hits = Physics.OverlapCapsule(b.center + Vector3.up * (b.extents.y - r), b.center - Vector3.up * (b.extents.y - r - 0.05f), r * 0.95f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            if (h != walker && !(h is TerrainCollider) && h.name != "AuditWaterFloor") return false;
        return true;
    }

    // walk from cell a toward cell b; true when the controller actually gets there
    static bool CanPass(Vector2Int a, Vector2Int b)
    {
        long key = ((long)(a.x * n + a.y) << 20) | (long)(b.x * n + b.y);
        if (passCache.TryGetValue(key, out bool cached)) return cached;
        Vector3 from = Cell(a), to = Cell(b);
        Vector3 dir = (to - from).normalized;
        Place(from);
        Vector3 origin = walker.transform.position;
        for (int k = 0; k < 22; k++)
            walker.Move(dir * 0.1f + Vector3.down * 0.12f);
        Vector3 moved = walker.transform.position - origin;
        moved.y = 0f;
        bool ok = Vector3.Dot(moved, dir) >= Step - 0.25f;
        passCache[key] = ok;
        return ok;
    }
}
