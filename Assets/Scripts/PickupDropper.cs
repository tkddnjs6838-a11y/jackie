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
    private bool introducedGrowth;
    private int lastGrowthDropKill;
    [Header("Augment drought protection")]
    [Min(1)] public int maxKillsWithoutGrowth = 25;

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

        if(!enemy.isBoss)DropReward(at,enemy.GetComponent<EliteMarker>()!=null);

        // an elite is a fight, so it always leaves something behind, ignoring the taper
        if (!enemy.isBoss && enemy.GetComponent<EliteMarker>() != null)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 spread = Random.insideUnitCircle * 1.2f;
                HealthPickup.Spawn(at + new Vector3(spread.x, 0f, spread.y), healAmount);
            }
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
    // Each victim rolls once for weapon / growth / timed rewards. Health remains a separate safety drop.
    void DropReward(Vector3 at,bool elite)
    {
        var weapons=playerHealth!=null?playerHealth.GetComponent<PlayerWeapons>():null;
        if(weapons==null)return;
        var growth=weapons.GetComponent<CharacterGrowth>();
        if(kills>=3 && !weapons.HasAxe && WeaponPickup.ActiveCount==0 && WeaponPickup.Spawn(at))return;
        if(kills>=5 && !introducedGrowth && CharacterUpgradePickup.ActiveCount<2 && growth!=null
            && TryDropGrowth(at,growth))
        {introducedGrowth=true;return;}
        if(kills>=8 && !weapons.Owns(PlayerWeapons.Kind.Shotgun) && GunPickup.ActiveCount==0 && GunPickup.Spawn(at,PlayerWeapons.Kind.Shotgun))return;
        if(elite && elitesAlwaysDropPowerUp && PowerUp.ActiveCount<maxActivePowerUps)
        {PowerUp.Spawn(at,PowerUp.RandomKind());return;}
        // Only guarantee another box when none is waiting to be collected.
        // Preserve one category reward per victim and elite timed-reward priority.
        if(introducedGrowth && kills-lastGrowthDropKill>=Mathf.Max(1,maxKillsWithoutGrowth)
            && CharacterUpgradePickup.ActiveCount==0 && TryDropGrowth(at,growth))return;
        float roll=Random.value;
        if(roll<.10f)
        {
            if(kills>8 && GunPickup.ActiveCount<2)GunPickup.Spawn(at,SelectGun(Random.value));
        }
        else if(roll<.18f)
        {
            if(kills>=5)TryDropGrowth(at,growth);
        }
        else if(roll<.18f+Mathf.Clamp01(powerUpChance) && PowerUp.ActiveCount<maxActivePowerUps)
            PowerUp.Spawn(at,PowerUp.RandomKind());
    }
    bool TryDropGrowth(Vector3 at,CharacterGrowth growth)
    {
        if(growth==null || CharacterUpgradePickup.ActiveCount>=2 || !growth.TryRandomAvailable(out var kind))return false;
        if(CharacterUpgradePickup.Spawn(at,kind)==null)return false;
        lastGrowthDropKill=kills;
        return true;
    }
    public static PlayerWeapons.Kind SelectGun(float roll)=>roll<.65f?PlayerWeapons.Kind.Shotgun:roll<.94f?PlayerWeapons.Kind.MachineGun:PlayerWeapons.Kind.LaserGun;

}
