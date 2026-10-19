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

    public int stageVariant;
    [HideInInspector] public int bounceLandings;
    private Enemy enemy;
    private bool crystalGuardNext;
    private bool lavaPourNext;
    private BossAttackMotion motion;
    private Transform player;
    private PlayerHealth playerHealth;

    // exposed for the play-mode checks: which pattern ran, and how many times
    [HideInInspector] public string lastPattern = "none";
    [HideInInspector] public int slamCount;
    [HideInInspector] public int chargeCount;

    void OnEnable()
    {
        enemy = GetComponent<Enemy>();
        motion = GetComponent<BossAttackMotion>();
        StartCoroutine(Brain());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        var crystals=GetComponent<CrystalBossCrystals>();
        if(crystals!=null)crystals.Cancel();
        var lava=GetComponent<CraterBossAttacks>();
        if(lava!=null)lava.Cancel();
        if (enemy != null) enemy.externalMovement = false;
        if(motion!=null)motion.Release();
    }

    IEnumerator Brain()
    {
        while (true)
        {
            yield return new WaitForSeconds(restBetweenAttacks + Random.Range(0f, restJitter));

            if (!Ready()) continue;

            float gap = SurfaceGap();
            if(stageVariant==4)
            {
                var lava=GetComponent<CraterBossAttacks>();
                if(lava!=null && lava.isActiveAndEnabled)
                {
                    lastPattern=lavaPourNext?"lava flow":"lava throw";
                    if(lavaPourNext)yield return lava.Pour();else yield return lava.Throw();
                    lavaPourNext=!lavaPourNext;
                }
                else {yield return Charge();if(Ready())yield return Slam();}
            }
            else if(stageVariant==3)
            {
                var crystals=GetComponent<CrystalBossCrystals>();
                if(crystals!=null && crystals.isActiveAndEnabled)
                {
                    lastPattern=crystalGuardNext?"crystal guard":"crystal volley";
                    if(crystalGuardNext)yield return crystals.Defend();
                    else yield return crystals.Volley();
                    crystalGuardNext=!crystalGuardNext;
                    if(Ready() && SurfaceGap()<=slamRange)yield return Slam();
                }
                else
                {
                    yield return Slam();
                    if(Ready())yield return Slam();
                }
            }
            else if(stageVariant==0) yield return Slam();
            else if(stageVariant==1) yield return Charge();
            else if(stageVariant==2)
            {
                yield return BounceCombo();
            }
            else if (gap >= chargeMinRange) yield return Charge();
            else yield return Slam();
        }
    }

    bool Ready()
    {
        if (RunStats.Instance == null || !RunStats.Instance.Running) return false;
        if (enemy == null || enemy.IsDying) return false;
        if (player == null)
        {
            player = enemy.Player;
            if (player != null) playerHealth = player.GetComponent<PlayerHealth>();
        }
        return player != null && playerHealth != null && !playerHealth.IsDead;
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
        if (terrain != null && MapNavigation.Current==null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
        return p + Vector3.up * 0.08f;
    }

    // Wax lake: lock each landing spot before take-off, then leave a long punish window.
    IEnumerator BounceCombo()
    {
        lastPattern = "wax bounce";
        enemy.externalMovement = true;
        for (int hop = 0; hop < 3; hop++)
        {
            if (!Ready()) { Release(); yield break; }
            Vector3 start = transform.position;
            Vector3 offset = player.position - start; offset.y = 0;
            Vector3 target = start + Vector3.ClampMagnitude(offset, 7f);
            if (MapNavigation.Current != null)
            {
                Vector3 from, to;
                UnityEngine.AI.NavMeshHit edge;
                if (!MapNavigation.Sample(start, 2f, out from) || !MapNavigation.Sample(target, 2f, out to)) target = start;
                else
                {
                    if (UnityEngine.AI.NavMesh.Raycast(from, to, out edge, UnityEngine.AI.NavMesh.AllAreas))
                        to = Vector3.MoveTowards(edge.position, from, enemy.BodyRadiusWorld + .3f);
                    target = MapNavigation.Clear(to, enemy.BodyRadiusWorld) ? to : start;
                }
            }
            else if (Terrain.activeTerrain != null)
                target.y = Terrain.activeTerrain.SampleHeight(target) + Terrain.activeTerrain.transform.position.y;
            Vector3 facing = target - start; facing.y = 0;
            if (facing.sqrMagnitude > .01f) transform.forward = facing.normalized;
            float radius = Mathf.Min(slamRadius, 4f);
            float tell = hop == 0 ? .85f : .6f;
            float t = 0, nextTell = 0;
            while (t < tell)
            {
                if (!Ready()) { Release(); yield break; }
                if (Time.timeScale <= 0) { yield return null; continue; }
                t += Time.deltaTime;
                if (motion != null) motion.Pose("HopTell", t / tell);
                if (t >= nextTell)
                {
                    SwordSweep.Spawn(target + Vector3.up * .1f, transform.forward, radius, 360f, radius * .9f, windUpColor, .24f, 1f);
                    nextTell = t + .18f;
                }
                yield return null;
            }
            t = 0;
            while (t < .75f)
            {
                if (!Ready()) { Release(); yield break; }
                if (Time.timeScale <= 0) { yield return null; continue; }
                t = Mathf.Min(.75f, t + Time.deltaTime);
                Vector3 desired = Vector3.Lerp(start, target, t / .75f);
                enemy.MoveAvoidingObstacles(desired - transform.position);
                if (motion != null) motion.Pose("HopAir", t / .75f);
                yield return null;
            }
            if (!Ready()) { Release(); yield break; }
            while (Time.timeScale <= 0) yield return null;
            if (!Ready()) { Release(); yield break; }
            if (motion != null) { motion.Pose("HopRecover", 0); motion.MarkImpact(); }
            bounceLandings++;
            GameAudio.Play(GameAudio.Sfx.AxeHit);
            var ground = GroundPoint();
            SwordSweep.Spawn(ground, transform.forward, radius, 360f, radius * .3f, strikeColor, .35f, 1.3f);
            HitImpact.Spawn(ground + Vector3.up * .15f, Vector3.up, 1.5f);
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(.16f, .09f);
            Vector3 distance = player.position - transform.position; distance.y = 0;
            if (distance.magnitude <= radius) playerHealth.TakeDamage(slamDamage);
            yield return Recover("HopRecover", hop == 2 ? 1.6f : .28f);
        }
        Release();
    }

    IEnumerator Slam()
    {
        lastPattern = "slam";
        slamCount++;
        enemy.externalMovement = true;      // plant it: a slam you can walk away from is not a threat

        float t = 0f, nextTell = 0f;
        while (t < slamWindUp)
        {
            if (!Ready()) { Release(); yield break; }
            if(Time.timeScale<=0){yield return null;continue;}
            t=Mathf.Min(slamWindUp,t+Time.deltaTime);
            if(motion!=null)motion.Pose("SlamTell",t/Mathf.Max(.001f,slamWindUp));
            if(t>=nextTell){SwordSweep.Spawn(GroundPoint(),transform.forward,slamRadius,360f,slamRadius*.88f,windUpColor,.2f,1f);nextTell=t+.18f;}
            if(t<slamWindUp)yield return null;
        }

        if (!Ready()) { Release(); yield break; }
        while (Time.timeScale <= 0f) yield return null;
        if (!Ready()) { Release(); yield break; }

        if(motion!=null){motion.Pose("SlamTell",1);motion.MarkImpact();}
        GameAudio.Play(GameAudio.Sfx.AxeHit);
        // impact: one bright ring that reads as the shockwave, and one damage check
        SwordSweep.Spawn(GroundPoint(), transform.forward, slamRadius, 360f, slamRadius * 0.35f,
                         strikeColor, 0.35f, 1.5f);
        if(motion!=null)
        {
            foreach(var side in new[]{"L","R"})
            {
                var contact=motion.FistPoint(side);contact.y=GroundPoint().y+.15f;
                HitImpact.Spawn(contact,Vector3.up,1.25f);
            }
        }
        else HitImpact.Spawn(GroundPoint()+Vector3.up*.4f,transform.forward,2.2f);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.25f, 0.14f);

        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        if (flat.magnitude <= slamRadius) playerHealth.TakeDamage(slamDamage);

        yield return Recover("SlamRecover",slamRecover);
        Release();
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

        float t=0f, nextTell=0f;
        while(t<chargeWindUp)
        {
            if(!Ready()){Release();yield break;}
            if(Time.timeScale<=0){yield return null;continue;}
            t=Mathf.Min(chargeWindUp,t+Time.deltaTime);
            if(motion!=null)motion.Pose("ChargeTell",t/Mathf.Max(.001f,chargeWindUp));
            if(t>=nextTell){SwordSweep.Spawn(GroundPoint(),dir,chargeDistance,22f,1.5f,windUpColor,.2f,1f);nextTell=t+.18f;}
            if(t<chargeWindUp)yield return null;
        }

        float travelled = 0f;
        bool connected = false;
        while (travelled < chargeDistance)
        {
            if (!Ready()) { Release(); yield break; }
            // Coroutines yielding null still run while the choice/inventory screen pauses time.
            if (Time.timeScale <= 0f) { yield return null; continue; }
            if(motion!=null)motion.Pose("Charge",travelled/Mathf.Max(.001f,chargeDistance));
            float step = Mathf.Min(chargeSpeed * PowerUpEffects.EnemySpeedScale * Time.deltaTime,
                                   chargeDistance - travelled);
            Vector3 before=transform.position;
            enemy.MoveAvoidingObstacles(dir * step);
            Vector3 movement=transform.position-before;movement.y=0;
            travelled += movement.magnitude;

            if (!connected)
            {
                Vector3 relative=player.position-before;relative.y=0;
                float along=movement.sqrMagnitude>.00001f?Mathf.Clamp01(Vector3.Dot(relative,movement)/movement.sqrMagnitude):0;
                Vector3 flat=relative-movement*along;
                if (flat.magnitude <= chargeHitRadius + enemy.BodyRadiusWorld)
                {
                    connected = true;
                    if(motion!=null)motion.MarkImpact();
                    GameAudio.Play(GameAudio.Sfx.Hit);
                    playerHealth.TakeDamage(chargeDamage);
                    HitImpact.Spawn(player.position + Vector3.up * 0.7f, dir, 1.6f);
                    if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.2f, 0.12f);
                }
            }
            if(step>.001f && movement.magnitude<step*.1f)break;
            yield return null;
        }

        yield return Recover("ChargeRecover",chargeRecover);
        Release();
    }
    void Release()
    {
        if(enemy!=null)enemy.externalMovement=false;
        if(motion!=null)motion.Release();
    }
    IEnumerator Recover(string phase,float duration)
    {
        float t=0;
        while(t<duration)
        {
            if(!Ready()){Release();yield break;}
            if(Time.timeScale<=0){yield return null;continue;}
            if(motion!=null)motion.Pose(phase,t/Mathf.Max(.001f,duration));
            yield return null;t+=Time.deltaTime;
        }
    }

}
