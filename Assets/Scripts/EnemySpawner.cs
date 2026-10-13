using UnityEngine;
using UnityEngine.UI;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public GameObject bossPrefab;
    public Transform player;
    public float spawnRadius = 15f;
    public float initialInterval = 1.4f;
    public float minInterval = 0.5f;
    public float intervalDecayPerMinute = 0.2f;
    public int killsToSpawnBoss = 40;
    public float bossEarliestSeconds = 60f;
    public float bossWarningSeconds = 3f;
    public float bossClearRadius = 2.5f;
    public Text killCountText;

    [System.Serializable]
    public class Variant
    {
        public GameObject prefab;
        public float weight = 1f;
        // seconds into the run before this one starts showing up, so the opening
        // stays readable and the specials arrive as an escalation
        public float unlockAfterSeconds = 0f;
    }

    [Header("Variants")]
    public Variant[] variants;

    [Header("Charger arrival")]
    [Tooltip("Guarantees one charger early so the player can learn its attack pattern.")]
    public float firstChargerAtSeconds = 25f;

    [Header("Elites")]
    // The swarm has no targets in it, so every so often one spawn is promoted: tougher,
    // gold-ringed, and it pays out an upgrade when it dies. Interval-based rather than a
    // flat chance, so the player can feel one coming.
    public float eliteInterval = 45f;
    public float eliteJitter = 12f;
    public float eliteFirstAt = 45f;
    public int maxAliveElites = 2;

    [Header("Late-run escalation")]
    // past this many kills the swarm starts hitting harder, so a long run keeps
    // getting more dangerous instead of settling into a safe routine
    public int damageRampStartKills = 300;
    public float damagePerHundredKills = 0.30f;
    public float maxDamageMultiplier = 3f;

    [Header("Swarm")]
    public int enemiesPerSpawn = 1;
    // hard ceiling so the swarm cannot tank the framerate; spawning pauses at the cap
    public int maxAliveEnemies = 35;

    private float timer;
    private float elapsed;
    private int killCount;

    private bool firstChargerSpawned;
    private bool bossSpawned;
    private BossArrivalWarning bossWarning;
    private Vector3 bossArrivalPosition;
    private float bossWarningRemaining;
    public bool BossWarningActive => bossWarning != null;
    public float BossWarningRemaining => bossWarningRemaining;
    public Vector3 BossArrivalPosition => bossArrivalPosition;
    private float nextEliteAt = -1f;

    void Start()
    {
        UpdateKillCountText();

    }

    void OnEnable()
    {
        Enemy.OnEnemyDied += HandleEnemyDied;
    }

    void OnDisable()
    {
        Enemy.OnEnemyDied -= HandleEnemyDied;
        CancelBossWarning();
    }

    void HandleEnemyDied(Enemy enemy)
    {
        if (enemy.isBoss) return;

        killCount++;
        UpdateKillCountText();

        TrySpawnBoss();
    }

    void TrySpawnBoss()
    {
        if(RunStats.Instance==null || !RunStats.Instance.Running || Time.timeScale<=0 || bossSpawned || BossWarningActive
            || elapsed<bossEarliestSeconds || killCount<killsToSpawnBoss || bossPrefab==null)return;
        if(!TryFindSpawnPoint(out bossArrivalPosition,bossClearRadius))return;
        if(bossWarningSeconds<=0){Instantiate(bossPrefab,bossArrivalPosition,Quaternion.identity);bossSpawned=true;return;}
        bossWarningRemaining=bossWarningSeconds;
        bossWarning=BossArrivalWarning.Create(bossArrivalPosition,bossClearRadius);
        bossWarning.SetRemaining(bossWarningRemaining);
    }
    void TickBossWarning(float delta)
    {
        if(!BossWarningActive)return;
        if(RunStats.Instance==null || !RunStats.Instance.Running){CancelBossWarning();return;}
        if(Time.timeScale<=0)return;
        bossWarningRemaining=Mathf.Max(0,bossWarningRemaining-delta);
        bossWarning.SetRemaining(bossWarningRemaining);
        if(bossWarningRemaining>0)return;
        var offset=player!=null?player.position-bossArrivalPosition:Vector3.zero;offset.y=0;
        // If the player or a prop occupies the marked spot, start a fresh warning elsewhere.
        bool clear=player!=null && offset.sqrMagnitude>(bossClearRadius+1f)*(bossClearRadius+1f)
            && !IsSpawnBlocked(bossArrivalPosition,bossClearRadius);
        if(clear && bossPrefab!=null){Instantiate(bossPrefab,bossArrivalPosition,Quaternion.identity);bossSpawned=true;}
        CancelBossWarning();
    }
    void CancelBossWarning()
    {
        if(bossWarning!=null)bossWarning.Dismiss();
        bossWarning=null;bossWarningRemaining=0;
    }

    void UpdateKillCountText()
    {
        if (killCountText != null)
            killCountText.text = "Kills: " + killCount;
    }

    void Update()
    {
        TickBossWarning(Time.deltaTime);
        if (Time.timeScale <= 0f || RunStats.Instance == null || !RunStats.Instance.Running) return;
        elapsed += Time.deltaTime;
        timer += Time.deltaTime;

        float currentInterval = CurrentSpawnInterval;

        if (timer >= currentInterval)
        {
            timer = 0f;
            TrySpawnBoss();

            int dynamicAliveCap = CurrentAliveCap;
            int waveCount = CurrentWaveCount;
            int alive = Enemy.AliveRegularCount;

            if (!firstChargerSpawned && elapsed >= firstChargerAtSeconds && alive < dynamicAliveCap)
            {
                GameObject chargerPrefab = FindChargerPrefab();
                firstChargerSpawned = SpawnEnemy(chargerPrefab);
            }

            for (int i = 0; i < waveCount && Enemy.AliveRegularCount < dynamicAliveCap; i++)
                SpawnEnemy(PickPrefab());
        }

        PromoteDueElite();
    }

    /// <summary>
    /// Contact-damage scaling applied to monsters spawned from here. Flat 1x until the
    /// ramp threshold, then climbs with the kill count up to the cap.
    /// </summary>
    public float CurrentDamageMultiplier()
    {
        if (killCount <= damageRampStartKills) return 1f;
        float hundreds = (killCount - damageRampStartKills) / 100f;
        return Mathf.Min(maxDamageMultiplier, 1f + hundreds * damagePerHundredKills);
    }

    public float CurrentSpawnInterval => Mathf.Max(.1f, Mathf.Max(minInterval, initialInterval - elapsed / 60f * intervalDecayPerMinute));
    public int CurrentAliveCap => Mathf.Min(maxAliveEnemies, 18 + Mathf.FloorToInt(elapsed / 60f) * 6);
    public int CurrentWaveCount => Mathf.Min(3, enemiesPerSpawn + Mathf.FloorToInt(elapsed / 90f));

    public int KillCount { get { return killCount; } }

    /// <summary>
    /// Promotes one live monster into an elite when the schedule says so.
    ///
    /// This runs on the clock and picks from monsters already on the field, rather than
    /// tagging a fresh spawn: spawning stops once the alive cap is reached, so a player who
    /// kills slowly would never have seen an elite at all.
    /// </summary>
    void PromoteDueElite()
    {
        if (nextEliteAt < 0f) nextEliteAt = eliteFirstAt;
        if (elapsed < nextEliteAt) return;

        int aliveElites = 0;
        foreach (var e in Enemy.ActiveEnemies)
            if (e != null && !e.isBoss && e.GetComponent<EliteMarker>() != null) aliveElites++;
        if (aliveElites >= maxAliveElites) return;

        // pick the candidate furthest along the list that is still plain, preferring one
        // that is not already on top of the player
        Enemy best = null;
        float bestScore = -1f;
        foreach (var e in Enemy.ActiveEnemies)
        {
            if (e == null || e.isBoss) continue;
            if (e.GetComponent<EliteMarker>() != null) continue;
            float score = player != null
                ? Vector3.Distance(e.transform.position, player.position)
                : 0f;
            if (score > bestScore) { bestScore = score; best = e; }
        }
        if (best == null) return;

        nextEliteAt = elapsed + eliteInterval + Random.Range(0f, eliteJitter);
        best.gameObject.AddComponent<EliteMarker>().Promote();
    }

    public float NextEliteAt { get { return nextEliteAt; } }

    /// <summary>
    /// Weighted pick among the variants that have unlocked by now; falls back to the
    /// plain monster so the spawner still works with no variants configured.
    /// </summary>
    GameObject PickPrefab()
    {
        if (variants == null || variants.Length == 0) return enemyPrefab;

        float total = 0f;
        foreach (var v in variants)
        {
            if (v == null || v.prefab == null) continue;
            if (elapsed < v.unlockAfterSeconds) continue;
            total += Mathf.Max(0f, v.weight);
        }
        if (total <= 0f) return enemyPrefab;

        float roll = Random.value * total;
        float acc = 0f;
        foreach (var v in variants)
        {
            if (v == null || v.prefab == null) continue;
            if (elapsed < v.unlockAfterSeconds) continue;
            acc += Mathf.Max(0f, v.weight);
            if (roll <= acc) return v.prefab;
        }
        return enemyPrefab;
    }

    bool SpawnEnemy(GameObject prefab)
    {
        if (prefab == null || player == null) return false;

        if(!TryFindSpawnPoint(out var spawnPosition,.62f))return false;

        var spawned = (GameObject)Instantiate(prefab, spawnPosition, Quaternion.identity);
        var enemy = spawned.GetComponent<Enemy>();
        if (enemy != null && !enemy.isBoss)
        {
            enemy.contactDamage *= CurrentDamageMultiplier();
        }
        return true;
    }


    bool TryFindSpawnPoint(out Vector3 position,float radius)
    {
        position=Vector3.zero;if(player==null)return false;
        for(int attempt=0;attempt<16;attempt++)
        {
            Vector2 circle=Random.insideUnitCircle.normalized*spawnRadius;
            position=player.position+new Vector3(circle.x,0,circle.y);position.y=0;
            var terrain=Terrain.activeTerrain;
            if(terrain!=null)position.y=terrain.SampleHeight(position)+terrain.transform.position.y;
            if(!IsSpawnBlocked(position,radius))return true;
        }
        return false;
    }

    bool IsSpawnBlocked(Vector3 position,float radius=.62f)
    {
        Collider[] overlaps = Physics.OverlapSphere(position + Vector3.up * 0.65f, radius,
                                                     ~0, QueryTriggerInteraction.Ignore);
        foreach (var col in overlaps)
        {
            if (col == null || col is TerrainCollider) continue;
            if (player != null && (col.transform == player || col.transform.IsChildOf(player))) continue;
            if (col.GetComponentInParent<Enemy>() != null) continue;
            return true;
        }
        return false;
    }


    GameObject FindChargerPrefab()
    {
        if (variants == null) return null;

        foreach (var variant in variants)
        {
            if (variant == null || variant.prefab == null) continue;
            if (variant.prefab.GetComponent<EnemyCharger>() != null)
                return variant.prefab;
        }

        return null;
    }
}
