using UnityEngine;

/// <summary>
/// Decides what dying monsters leave behind. Kept separate from Enemy so drop rules can
/// change without touching combat code.
/// </summary>
public class PickupDropper : MonoBehaviour
{
    [Header("Normal monsters")]
    public float dropChance = 0.05f;
    public float healAmount = 20f;
    // kills come in bursts, so a plain percentage floods the field; cap what can sit out
    public int maxActiveOrbs = 4;

    [Header("Power-ups")]
    // Rare on purpose: the point of a timed buff is that it changes the next few seconds,
    // which stops being true if one is always on the floor.
    public float powerUpChance = 0.012f;
    public int maxActivePowerUps = 1;
    public bool elitesAlwaysDropPowerUp = true;

    [Header("Late-run scarcity")]
    // a flat percentage means drops pile up as kill rate climbs, and the run gets easier
    // the longer it goes. Taper the rate as the kill count rises instead.
    public int killsForFullTaper = 250;
    public float lateDropChanceMultiplier = 0.35f;
    public int lateMaxActiveOrbs = 2;

    [Header("Boss")]
    public int bossDropCount = 4;
    public float bossHealAmount = 30f;
    public float bossSpreadRadius = 2.5f;

    [Header("Safety net")]
    // if the player is desperate, guarantee a drop so a run cannot become unwinnable
    public bool guaranteeWhenLow = true;
    public float lowHealthFraction = 0.25f;
    public float guaranteedDropCooldown = 14f;

    private PlayerHealth playerHealth;
    private float lastGuaranteedDrop = -999f;
    private int kills;

    public int KillCount { get { return kills; } }

    void Awake()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerHealth = player.GetComponent<PlayerHealth>();
    }

    void OnEnable()
    {
        Enemy.OnEnemyDied += HandleEnemyDied;
    }

    void OnDisable()
    {
        Enemy.OnEnemyDied -= HandleEnemyDied;
    }

    void HandleEnemyDied(Enemy enemy)
    {
        Vector3 at = enemy.transform.position;

        if (!enemy.isBoss) kills++;

        // an elite is a fight, so it always leaves something behind, ignoring the taper
        if (!enemy.isBoss && enemy.GetComponent<EliteMarker>() != null)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 spread = Random.insideUnitCircle * 1.2f;
                HealthPickup.Spawn(at + new Vector3(spread.x, 0f, spread.y), healAmount);
            }
            if (elitesAlwaysDropPowerUp)
                PowerUp.Spawn(at + Vector3.up * 0.2f, PowerUp.RandomKind());
            return;
        }

        if (enemy.isBoss)
        {
            for (int i = 0; i < bossDropCount; i++)
            {
                Vector2 offset = Random.insideUnitCircle * bossSpreadRadius;
                HealthPickup.Spawn(at + new Vector3(offset.x, 0f, offset.y), bossHealAmount);
            }
            return;
        }

        float taper = killsForFullTaper > 0
            ? Mathf.Clamp01(KillCount / (float)killsForFullTaper)
            : 0f;
        int cap = Mathf.RoundToInt(Mathf.Lerp(maxActiveOrbs, lateMaxActiveOrbs, taper));
        float chance = dropChance * Mathf.Lerp(1f, lateDropChanceMultiplier, taper);

        bool roomOnField = HealthPickup.ActiveCount < cap;

        if (PowerUp.ActiveCount < maxActivePowerUps && Random.value < powerUpChance)
        {
            PowerUp.Spawn(at + Vector3.up * 0.2f, PowerUp.RandomKind());
            return;
        }

        if (roomOnField && Random.value < chance)
        {
            HealthPickup.Spawn(at, healAmount);
            return;
        }

        if (!guaranteeWhenLow || playerHealth == null) return;
        // the low-health net stays, otherwise late runs become unrecoverable rather than hard
        // the safety net ignores the cap only if the field is completely empty
        if (!roomOnField) return;
        if (playerHealth.maxHealth <= 0f) return;

        float fraction = playerHealth.currentHealth / playerHealth.maxHealth;
        if (fraction > lowHealthFraction) return;
        if (Time.time - lastGuaranteedDrop < guaranteedDropCooldown) return;

        lastGuaranteedDrop = Time.time;
        HealthPickup.Spawn(at, healAmount);
    }
}
