using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAutoAttack : MonoBehaviour
{
    public float manualAttackRange = 3f;
    public float manualAttackCooldown = 0.5f;
    
    [Header("Mouse Combo")]
    public float comboWindow = 0.8f;
    // How long a click is remembered when it lands during the cooldown. Without this the
    // press is read only on frames the attack is already available, so every click that
    // arrives a fraction early is thrown away and the attack feels like it ignores you.
    public float inputBufferSeconds = 0.22f;
    public float strongDamageMultiplier = 1.35f;
    public float strongRangeMultiplier = 1.12f;
    // The strong swing has to be worse than the light one at sustained damage, or there is no
    // reason to press left click. That cost is paid in damage rather than a long cooldown:
    // at 1.9x the swing animation ended 0.45s before the attack was available again, and
    // that dead gap felt like the game stuttering.
    public float strongCooldownMultiplier = 1.5f;
    public float strongAngleBonus = 30f;
    public float finisherDamageMultiplier = 2.4f;
    public float finisherRangeMultiplier = 1.3f;
    public float finisherAngle = 220f;
public float manualAttackDamage = 30f;
    // hold the right button to keep swinging at the cooldown rate
    public bool holdToAttack = true;
    // health returned per connecting swing, granted by an upgrade
    public float lifestealPerHit = 0f;
    // Hard ceiling on healing per second. Lifesteal is per swing and attack speed is
    // itself upgradeable, so the two multiplied: at 4.8 swings/s a +6 leech healed
    // 28 HP/s and outpaced eight enemies, making the player unkillable while attacking.
    public float lifestealPerSecondCap = 5f;

    public Animator animator;

    public Transform swingBone;
    public float swingDuration = 0.35f;
    public Vector3 swingEulerDelta = new Vector3(0f, 0f, -100f);

    [Header("Impact timing")]
    // Measured off the live Animator (not the raw clip — humanoid retargeting shifts
    // the timing): in the SpinningSlash state the blade descends fastest across the
    // front at normalizedTime 0.65, and the state lasts 0.50s, so contact is 0.325s.
    public float impactDelay = 0.325f;

    [Header("Hit arc")]
    // Total cone in front of the player that a swing sweeps through. Every enemy
    // inside it is hit by the same swing; anything outside is ignored.
    public float attackAngle = 160f;
    public bool hitEveryTargetInArc = true;
    // chest height of the 1.34m player — the arc has to read as a sword swing,
    // not as a ring hovering over its head
    public float sweepEffectHeight = 0.75f;
    public float impactSourceHeight = 0.7f;
    // fraction of the range where the arc band starts — keeps it a blade crescent
    // rather than a filled pie, whatever the range is set to
    public float sweepInnerRatio = 0.64f;

    [Header("Impact feedback")]
    public float impactShakeDuration = 0.12f;
    public float impactShakeMagnitude = 0.09f;
    public float missShakeDuration = 0.05f;
    public float missShakeMagnitude = 0.02f;
    public float impactEffectSize = 1f;

    public bool useHitStop = true;
    public float hitStopDuration = 0.04f;
    public float hitStopScale = 0.2f;

    [Header("Sword grip")]
    // The blade's offset inside the hand cannot be a single fixed value: the idle
    // pose and the slash hold the wrist differently once the clip is retargeted, so
    // one offset that looks right standing still points the blade backwards mid-swing.
    // These two are blended by how much of the swing is playing.
    public Transform swordModel;
    public Vector3 gripIdleEuler = new Vector3(59.03f, 7.22f, 180.80f);
    public Vector3 gripSwingEuler = Vector3.zero;
    public float gripBlendIn = 0.06f;
    public float gripBlendOut = 0.12f;
    public float swingStateDuration = 0.5f;

    [Header("Attack layer")]
    // The upper-body attack layer sits at weight 0 so walking and idling drive the
    // whole body; it is only raised while a swing is playing.
    public bool driveAttackLayerWeight = true;
    public int attackLayerIndex = 1;

    private float manualCooldownTimer;
    private Vector3 swingRestEuler;
    private Coroutine swingRoutine;
    private float gripWeight;
    private float swingHold;
    private float lifestealWindowStart;
    
    private int comboStep;
    private float comboTimer;
    private float bufferTimer;
    private bool bufferedStrong;
private float lifestealSpentThisSecond;void Start()
    {
        if (GetComponent<OffscreenEnemyIndicator>() == null)
            gameObject.AddComponent<OffscreenEnemyIndicator>();

        if (swingBone != null)
            swingRestEuler = swingBone.localEulerAngles;

        if (swordModel == null && animator != null)
        {
            foreach (var t in animator.GetComponentsInChildren<Transform>(true))
                if (t.name == "SwordModel") { swordModel = t; break; }
        }
    }

    // runs after the Animator has written the bones, so this wins over the clip
    void LateUpdate()
    {
        if (swordModel == null) return;

        if (swingHold > 0f)
        {
            swingHold -= Time.deltaTime;
            gripWeight = Mathf.MoveTowards(gripWeight, 1f, Time.deltaTime / Mathf.Max(0.0001f, gripBlendIn));
        }
        else
        {
            gripWeight = Mathf.MoveTowards(gripWeight, 0f, Time.deltaTime / Mathf.Max(0.0001f, gripBlendOut));
        }

        swordModel.localRotation = Quaternion.Slerp(Quaternion.Euler(gripIdleEuler),
                                                    Quaternion.Euler(gripSwingEuler), gripWeight);
    }

void Update()
    {
        if (driveAttackLayerWeight && animator != null && animator.layerCount > attackLayerIndex)
            animator.SetLayerWeight(attackLayerIndex, gripWeight);

        manualCooldownTimer -= Time.deltaTime;
        comboTimer -= Time.deltaTime;
        if (comboTimer <= 0f) comboStep = 0;

        var mouse = Mouse.current;
        if (mouse == null) return;

        // input is read every frame, cooldown or not, and the most recent click waits its turn
        if (mouse.leftButton.wasPressedThisFrame)
        {
            bufferedStrong = false;
            bufferTimer = inputBufferSeconds;
        }
        else if (mouse.rightButton.wasPressedThisFrame)
        {
            bufferedStrong = true;
            bufferTimer = inputBufferSeconds;
        }

        if (bufferTimer > 0f) bufferTimer -= Time.deltaTime;
        if (manualCooldownTimer > 0f || bufferTimer <= 0f) return;

        bufferTimer = 0f;

        if (!bufferedStrong)
        {
            comboStep = comboStep == 1 ? 2 : 1;
            comboTimer = comboWindow;
            Swing(false, false);
        }
        else
        {
            bool finisher = comboStep == 2 && comboTimer > 0f;
            comboStep = 0;
            comboTimer = 0f;
            Swing(true, finisher);
        }
    }

    /// <summary>
    /// Spends from a per-second healing budget, so faster swinging no longer multiplies
    /// how much lifesteal returns.
    /// </summary>
    float TakeLifestealBudget()
    {
        float now = Time.time;
        if (now - lifestealWindowStart >= 1f)
        {
            lifestealWindowStart = now;
            lifestealSpentThisSecond = 0f;
        }

        float allowance = Mathf.Max(0f, lifestealPerSecondCap - lifestealSpentThisSecond);
        float granted = Mathf.Min(lifestealPerHit, allowance);
        lifestealSpentThisSecond += granted;
        return granted;
    }

void Swing(bool strong, bool finisher)
    {
        float cooldownScale = strong ? strongCooldownMultiplier : 1f;
        manualCooldownTimer = manualAttackCooldown * cooldownScale;
        if (RunStats.Instance != null) RunStats.Instance.RegisterClick();
        GameAudio.Play(GameAudio.Sfx.Swing);

        float damage = manualAttackDamage;
        float range = manualAttackRange;
        float angle = attackAngle;
        float feedback = 1f;

        if (finisher)
        {
            damage *= finisherDamageMultiplier;
            range *= finisherRangeMultiplier;
            angle = finisherAngle;
            feedback = 1.75f;
        }
        else if (strong)
        {
            damage *= strongDamageMultiplier;
            range *= strongRangeMultiplier;
            angle = Mathf.Min(300f, angle + strongAngleBonus);
            feedback = 1.3f;
        }

        float delay = PlayAttackMotion();
        StartCoroutine(StrikeAfter(delay, damage, range, angle, feedback));
    }

    /// <summary>Starts the swing visuals and returns when the blade should connect.</summary>
    float PlayAttackMotion()
    {
        if (animator != null)
        {
            animator.SetTrigger("Attack");
            swingHold = swingStateDuration;
            return impactDelay;
        }

        if (swingBone != null)
        {
            if (swingRoutine != null) StopCoroutine(swingRoutine);
            swingRoutine = StartCoroutine(SwingSword());
            return swingDuration * 0.5f;    // the fallback swing connects at its apex
        }

        return 0f;
    }

System.Collections.IEnumerator StrikeAfter(float delay, float damage, float range, float angle, float feedback)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Color sweepColor = feedback >= 1.7f
            ? new Color(0.35f, 0.9f, 1f, 1f)
            : feedback >= 1.2f
                ? new Color(1f, 0.32f, 0.08f, 1f)
                : new Color(1f, 0.88f, 0.45f, 1f);
        float sweepLifetime = 0.22f * Mathf.Lerp(1f, 1.45f, (feedback - 1f) / 0.75f);
        float sweepGrowth = Mathf.Lerp(1.12f, 1.34f, (feedback - 1f) / 0.75f);

        SwordSweep.Spawn(transform.position + Vector3.up * sweepEffectHeight,
                         transform.forward, range, angle, range * sweepInnerRatio,
                         sweepColor, sweepLifetime, sweepGrowth);

        var targets = FindTargetsInFront(range, angle);

        if (targets.Count == 0)
        {
            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(missShakeDuration, missShakeMagnitude * feedback);
            yield break;
        }

        if (RunStats.Instance != null) RunStats.Instance.RegisterHit();
        GameAudio.Play(GameAudio.Sfx.Hit);

        foreach (var target in targets)
        {
            if (target == null) continue;
            HitImpact.Spawn(ContactPoint(target), transform.forward, impactEffectSize * feedback);
            target.TakeDamage(damage);
        }

        if (lifestealPerHit > 0f)
        {
            var health = GetComponent<PlayerHealth>();
            if (health != null) health.Heal(TakeLifestealBudget());
        }

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(impactShakeDuration * feedback,
                                       impactShakeMagnitude * feedback);

        if (useHitStop)
            yield return HitStop(feedback);
    }

System.Collections.IEnumerator HitStop(float strength)
    {
        if (Time.timeScale <= 0f) yield break;

        float scale = strength >= 1.7f ? 0.08f : hitStopScale;
        Time.timeScale = scale;
        yield return new WaitForSecondsRealtime(hitStopDuration * strength);

        if (Mathf.Approximately(Time.timeScale, scale))
            Time.timeScale = 1f;
    }

    Vector3 ContactPoint(Enemy target)
    {
        var col = target.GetComponent<Collider>();
        // aim from the blade's own height, so on a huge enemy the flash lands on the
        // part the sword actually reaches instead of somewhere up its body
        Vector3 from = transform.position + Vector3.up * impactSourceHeight;
        return col != null ? col.ClosestPoint(from) : target.transform.position + Vector3.up * impactSourceHeight;
    }

    System.Collections.IEnumerator SwingSword()
    {
        Vector3 startEuler = swingRestEuler;
        Vector3 swingEuler = swingRestEuler + swingEulerDelta;
        float half = swingDuration * 0.5f;
        float t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            swingBone.localEulerAngles = Vector3.Lerp(startEuler, swingEuler, t / half);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            swingBone.localEulerAngles = Vector3.Lerp(swingEuler, startEuler, t / half);
            yield return null;
        }

        swingBone.localEulerAngles = startEuler;
    }

    System.Collections.Generic.List<Enemy> FindTargetsInFront(float range, float angle)
    {
        var found = new System.Collections.Generic.List<Enemy>();
        Collider[] hits = Physics.OverlapSphere(transform.position, range);
        Enemy nearest = null;
        float nearestDist = float.MaxValue;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        float halfAngle = angle * 0.5f;

        foreach (var hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy == null) continue;

            Vector3 toEnemy = enemy.transform.position - transform.position;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude < 0.0001f) continue;

            // ignore anything the swing is not aimed at
            if (Vector3.Angle(forward, toEnemy.normalized) > halfAngle) continue;

            if (hitEveryTargetInArc)
            {
                if (!found.Contains(enemy)) found.Add(enemy);
                continue;
            }

            float dist = toEnemy.magnitude;
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = enemy;
            }
        }

        if (!hitEveryTargetInArc && nearest != null)
            found.Add(nearest);

        return found;
    }
}
