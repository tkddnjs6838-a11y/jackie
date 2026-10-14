using UnityEngine;

/// <summary>
/// Splits into smaller copies when killed. Aimed squarely at the habit of swinging the
/// 160-degree arc into a clump: clearing a pack of these leaves more enemies than you
/// started with, so they are better picked off at the edge of the crowd.
/// </summary>
[RequireComponent(typeof(Enemy))]
public class EnemySplitter : MonoBehaviour
{
    static readonly Unity.Profiling.ProfilerMarker CostMarker = new Unity.Profiling.ProfilerMarker("Combat.Split");

    public int generation = 0;
    public int maxGenerations = 2;
    public int childCount = 2;

    [Header("Child scaling")]
    public float childScaleFactor = 0.62f;
    public float childHealthFactor = 0.45f;
    public float childSpeedFactor = 1.25f;
    public float childDamageFactor = 0.6f;
    public float spreadRadius = 1.1f;

    private Enemy enemy;
    private bool split;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    void OnEnable()
    {
        Enemy.OnEnemyDied += HandleEnemyDied;
    }

    void OnDisable()
    {
        Enemy.OnEnemyDied -= HandleEnemyDied;
    }

    void HandleEnemyDied(Enemy died)
    {
        if (died != enemy || split) return;
        split = true;
        if (generation >= maxGenerations) return;

        var spawner = FindFirstObjectByType<EnemySpawner>();
        for (int i = 0; i < childCount; i++)
        {
            if (spawner != null && Enemy.AliveRegularCount >= spawner.maxAliveEnemies) break;
            SpawnChild(i);
        }
    }

    void SpawnChild(int index)
    {
        using (CostMarker.Auto())
        {
            float angle = (index / (float)Mathf.Max(1, childCount)) * Mathf.PI * 2f + Random.value;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spreadRadius;
            Vector3 at = transform.position + offset;

            if(!MapNavigation.SpawnPoint(at,.35f,out at))return;

            // clone this creature rather than a prefab, so a second-generation child
            // inherits whatever the first generation already had
            var child = Instantiate(gameObject, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            child.name = gameObject.name + "_split" + (generation + 1);
            child.transform.localScale = transform.localScale * childScaleFactor;

            var childEnemy = child.GetComponent<Enemy>();
            childEnemy.maxHealth = Mathf.Max(6f, enemy.maxHealth * childHealthFactor);
            childEnemy.moveSpeed = enemy.moveSpeed * childSpeedFactor;
            childEnemy.contactDamage = Mathf.Max(4f, enemy.contactDamage * childDamageFactor);

            childEnemy.randomizeColor = false;
            childEnemy.variantColor = Color.Lerp(enemy.variantColor, Color.white,
                                                 0.22f * (generation + 1));
            childEnemy.RefreshConfiguredStatsAndColor();
            childEnemy.contactGap = enemy.contactGap * childScaleFactor;

            var childSplitter = child.GetComponent<EnemySplitter>();
            childSplitter.generation = generation + 1;

            // a fresh clone should not think it has already split
            childSplitter.split = false;
        }
    }
}
