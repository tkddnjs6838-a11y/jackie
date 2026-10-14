using System.Collections.Generic;
using UnityEngine;

// Makes everything that wades into the water float half submerged, with a slow bob.
// Sits on a trigger volume covering everything below the waterline (land is above it).
//  - Monsters: pathfinding uses a baked water plane; LateUpdate keeps their middle
//    on the waterline. Taller monsters (the boss) keep wading on the riverbed.
//  - Player: a small hidden raft follows under the player's feet while it is in the water,
//    so the CharacterController stands on it. It is only player-sized, because the monsters'
//    movement casts treat every collider as a wall and a map-wide floor stopped them at the bank.
[RequireComponent(typeof(Rigidbody))]
public class WaterFloat : MonoBehaviour
{
    [SerializeField] float waterline = 2.4f;
    [SerializeField] float playerDepth = 0.65f; // feet below the waterline: waist-deep for the 0.72-scale player
    [SerializeField] float amplitude = 0.06f;
    [SerializeField] float period = 2.4f;

    // monster -> height from its pivot (feet) to the middle of its body
    readonly Dictionary<Enemy, float> floating = new Dictionary<Enemy, float>();
    readonly List<Enemy> gone = new List<Enemy>();

    static readonly HashSet<WaterFloat> volumes=new HashSet<WaterFloat>();
    public float NavigationHeight => waterline-playerDepth;
    public float Waterline => waterline;
    public Bounds VolumeBounds => GetComponent<BoxCollider>().bounds;
    void OnEnable(){volumes.Add(this);}
    void OnDisable(){volumes.Remove(this);}
    // Only project underwater points in this water volume; dry land retains strict
    // NavMesh sampling so a nearby cliff cannot become a shortcut.
    public static bool NavigationPoint(Vector3 position,out Vector3 projected)
    {
        projected=position;
        foreach(var water in volumes)
        {
            if(water==null)continue;
            var bounds=water.VolumeBounds;
            if(position.x<bounds.min.x || position.x>bounds.max.x || position.z<bounds.min.z || position.z>bounds.max.z || position.y>water.waterline+.5f)continue;
            var terrain=Terrain.activeTerrain;
            if(terrain==null)continue;
            float ground=terrain.SampleHeight(position)+terrain.transform.position.y;
            if(ground>=water.NavigationHeight)continue;
            projected.y=water.NavigationHeight;return true;
        }
        return false;
    }
    CharacterController swimmer;
    Transform raft;

    void Awake()
    {
        GetComponent<Rigidbody>().isKinematic = true;
        var go = new GameObject("PlayerRaft", typeof(BoxCollider), typeof(Rigidbody));
        go.layer = gameObject.layer;
        go.GetComponent<Rigidbody>().isKinematic = true;
        go.GetComponent<BoxCollider>().size = new Vector3(0.6f, 0.2f, 0.6f); // inside the player's 0.36m radius, so monsters still reach its body
        raft = go.transform;
        raft.SetParent(transform, false);
        go.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        var enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            if (!floating.ContainsKey(enemy)) floating.Add(enemy, MiddleHeight(enemy));
        }
        else if (other is CharacterController controller)
        {
            swimmer = controller;
            raft.gameObject.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        var enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            floating.Remove(enemy);
        }
        else if (other == swimmer)
        {
            swimmer = null;
            raft.gameObject.SetActive(false);
        }
    }

    void LateUpdate()
    {
        float bob = Mathf.Sin(Time.time * (2f * Mathf.PI / period)) * amplitude;

        foreach (var pair in floating)
        {
            if (pair.Key == null)
            {
                gone.Add(pair.Key);
                continue;
            }
            var t = pair.Key.transform;
            var p = t.position;
            if(p.y>waterline+.05f)continue; // Do not pull bridge walkers down through their deck.
            float floatY = waterline - pair.Value + bob;
            var terrain=Terrain.activeTerrain;
            float ground=terrain!=null?terrain.SampleHeight(p)+terrain.transform.position.y:p.y;
            // Keep large creatures wading, while small ones float. Navigation uses
            // the shared water plane independently of this visual/pivot height.
            p.y=Mathf.Max(ground,floatY);
            t.position=p;
        }
        foreach (var dead in gone) floating.Remove(dead);
        gone.Clear();

        if (swimmer != null)
        {
            var p = swimmer.transform.position;
            raft.position = new Vector3(p.x, waterline - playerDepth - 0.1f + bob, p.z);
        }
    }

    static float MiddleHeight(Enemy enemy)
    {
        Bounds bounds = default;
        bool any = false;
        foreach (var r in enemy.GetComponentsInChildren<Renderer>())
        {
            if (r is LineRenderer || r is ParticleSystemRenderer || !r.enabled) continue;
            if (any) bounds.Encapsulate(r.bounds);
            else { bounds = r.bounds; any = true; }
        }
        return any ? bounds.center.y - enemy.transform.position.y : 0.5f;
    }
}
