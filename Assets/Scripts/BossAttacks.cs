using System.Collections;
using UnityEngine;

/// <summary>
/// Telegraphed attack patterns for the boss.
///
/// The boss used to be a 12,000 HP wall with no moves: the fight was four measured minutes
/// of holding the same combo on a target that only ever walked at you. Difficulty now comes
/// from reading a wind-up and getting out of the way, which is also the first thing in the
/// game that gives the dash a reason to exist.
///
/// Every attack follows the same contract: a visible wind-up long enough to react to, a
/// single moment where damage lands, then a recovery window where the boss is free to hit.
/// </summary>
[RequireComponent(typeof(Enemy))]
public class BossAttacks : MonoBehaviour
{
    [Header("Slam — used up close")]
    public float slamRange = 7f;
    public float slamRadius = 6f;
    public float slamDamage = 26f;
    public float slamWindUp = 0.85f;
    public float slamRecover = 0.8f;

    [Header("Charge — used at range")]
    public float chargeMinRange = 9f;
    public float chargeWindUp = 0.7f;
    public float chargeSpeed = 16f;
    public float chargeDistance = 15f;
    public float chargeDamage = 30f;
    public float chargeHitRadius = 3.4f;
    public float chargeRecover = 1.0f;

    [Header("Rhythm")]
    // the gap between attacks is the player's window to deal damage, so it is generous
    public float restBetweenAttacks = 2.4f;
    public float restJitter = 1.0f;

    [Header("Tells")]
    public Color windUpColor = new Color(1f, 0.35f, 0.25f, 1f);
    public Color strikeColor = new Color(1f, 0.85f, 0.4f, 1f);

    private Enemy enemy;
    private Transform player;
    private PlayerHealth playerHealth;

    // exposed for the play-mode checks: which pattern ran, and how many times
    [HideInInspector] public string lastPattern = "none";
    [HideInInspector] public int slamCount;
    [HideInInspector] public int chargeCount;

    void Start()
    {
        enemy = GetComponent<Enemy>();
        StartCoroutine(Brain());
    }

    void OnDisable()
    {
        // never leave the boss frozen out of its own movement code
        if (enemy != null) enemy.externalMovement = false;
    }

    IEnumerator Brain()
    {
        while (true)
        {
            yield return new WaitForSeconds(restBetweenAttacks + Random.Range(0f, restJitter));

            if (!Ready()) continue;

            float gap = SurfaceGap();
            if (gap >= chargeMinRange) yield return Charge();
            else yield return Slam();
        }
    }

    bool Ready()
    {
        if (enemy == null) return false;
        if (player == null)
        {
            player = enemy.Player;
            if (player != null) playerHealth = player.GetComponent<PlayerHealth>();
        }
        return player != null && playerHealth != null;
    }

    /// <summary>Distance from the boss's body surface to the player, so its size is accounted for.</summary>
    float SurfaceGap()
    {
        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        return flat.magnitude - enemy.BodyRadiusWorld;
    }

    Vector3 GroundPoint()
    {
        Vector3 p = transform.position;
        var terrain = Terrain.activeTerrain;
        if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
        return p + Vector3.up * 0.08f;
    }

    IEnumerator Slam()
    {
        lastPattern = "slam";
        slamCount++;
        enemy.externalMovement = true;      // plant it: a slam you can walk away from is not a threat

        // the tell is a ring that keeps redrawing on the ground the boss is about to flatten
        float t = 0f;
        while (t < slamWindUp)
        {
            SwordSweep.Spawn(GroundPoint(), transform.forward, slamRadius, 360f, slamRadius * 0.88f,
                             windUpColor, 0.2f, 1f);
            t += 0.18f;
            yield return new WaitForSeconds(0.18f);
        }

        // impact: one bright ring that reads as the shockwave, and one damage check
        SwordSweep.Spawn(GroundPoint(), transform.forward, slamRadius, 360f, slamRadius * 0.35f,
                         strikeColor, 0.35f, 1.5f);
        HitImpact.Spawn(GroundPoint() + Vector3.up * 0.4f, transform.forward, 2.2f);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.25f, 0.14f);

        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        if (flat.magnitude <= slamRadius) playerHealth.TakeDamage(slamDamage);

        yield return new WaitForSeconds(slamRecover);
        enemy.externalMovement = false;
    }

    IEnumerator Charge()
    {
        lastPattern = "charge";
        chargeCount++;
        enemy.externalMovement = true;

        // aim once, at the start: the charge is dodgeable precisely because it does not track
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
        dir.Normalize();
        transform.forward = dir;

        float t = 0f;
        while (t < chargeWindUp)
        {
            // a narrow lane drawn down the path it is about to take
            SwordSweep.Spawn(GroundPoint(), dir, chargeDistance, 22f, 1.5f, windUpColor, 0.2f, 1f);
            t += 0.18f;
            yield return new WaitForSeconds(0.18f);
        }

        float travelled = 0f;
        bool connected = false;
        while (travelled < chargeDistance)
        {
            float step = chargeSpeed * Time.deltaTime;
            transform.position += dir * step;
            travelled += step;

            if (!connected)
            {
                Vector3 flat = player.position - transform.position;
                flat.y = 0f;
                if (flat.magnitude <= chargeHitRadius + enemy.BodyRadiusWorld)
                {
                    connected = true;
                    playerHealth.TakeDamage(chargeDamage);
                    HitImpact.Spawn(player.position + Vector3.up * 0.7f, dir, 1.6f);
                    if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.2f, 0.12f);
                }
            }
            yield return null;
        }

        SwordSweep.Spawn(GroundPoint(), dir, 4f, 360f, 2.5f, strikeColor, 0.3f, 1.4f);
        yield return new WaitForSeconds(chargeRecover);
        enemy.externalMovement = false;
    }
}
