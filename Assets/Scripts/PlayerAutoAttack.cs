using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAutoAttack : MonoBehaviour
{
    public float manualAttackRange = 3f;
    public float manualAttackCooldown = 0.5f;

    [Header("Basic attack")]
    public float inputBufferSeconds = 0.22f;
    public float manualAttackDamage = 30f;
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
    // Contact time for the base swing; scales with its playback duration.
    public float impactDelay = 0.21f;

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
    public float sweepInnerRatio = 0.82f;

    [Header("Impact feedback")]
    public float impactShakeDuration = 0.07f;
    public float impactShakeMagnitude = 0.035f;
    public float missShakeDuration = 0.05f;
    public float missShakeMagnitude = 0f;
    public float impactEffectSize = 0.8f;

    public bool useHitStop = true;
    public float hitStopDuration = 0.04f;
    public float hitStopScale = 0.2f;

    [Header("Sword grip")]
    // Keep the weapon rigidly attached to the hand throughout locomotion and attacks.
    public Transform swordModel;
    public WeaponHandGrip handGrip;
    public Vector3 gripIdleEuler = new Vector3(59.03f, 7.22f, 180.80f);
    public float gripBlendIn = 0.06f;
    public float gripBlendOut = 0.12f;
    public float swingStateDuration = 0.5f;

    [Header("Attack layer")]
    // Locomotion drives the body. SwordArmPose supplies the right-arm cut in
    // LateUpdate, so the old retargeted slash layer stays at zero weight.
    public bool driveAttackLayerWeight = true;
    public int attackLayerIndex = 1;

    public bool AttackInProgress => manualCooldownTimer > 0f || swingHold > 0f;
    PlayerWeapons weapons;
    bool IsAxe => weapons != null && weapons.Equipped == PlayerWeapons.Kind.Axe;
    private float manualCooldownTimer;
    private Vector3 swingRestEuler;
    private Coroutine swingRoutine;
    private float swingHold;
    private float currentSwingDuration = 0.5f;
    private readonly SwordBodyPose swordBodyPose = new SwordBodyPose();
    private float lifestealWindowStart;

    private float bufferTimer;
    private float lifestealSpentThisSecond;

    void Start()
    {
        weapons = GetComponent<PlayerWeapons>();
        if (weapons == null) weapons = gameObject.AddComponent<PlayerWeapons>();
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

    // Pose the arm and wrist as one chain, keeping the blade clear of the head.
    void LateUpdate()
    {
        ApplySwordPose(1f - swingHold / Mathf.Max(0.001f, currentSwingDuration), swingHold > 0f);
    }

    public void ApplySwordPose(float phase, bool attacking)
    {
        swordBodyPose.Restore();
        if(weapons!=null && weapons.IsGun){GetComponent<PlayerGunCombat>()?.ApplyPose();return;}
        swordBodyPose.Apply(animator, transform, phase, attacking, IsAxe);
        SwordArmPose.Apply(animator, transform, swordModel, gripIdleEuler, phase, attacking, IsAxe);
        if(handGrip!=null) handGrip.Apply(swordModel);
    }

    public void ResetSwordPose() => swordBodyPose.Restore();

    bool ownsHitStop;
    float hitStopPreviousScale;
    float hitStopAppliedScale;

    void OnDisable()
    {
        swordBodyPose.Restore();
        RestoreHitStop();
        StopAllCoroutines();
    }

    public void EndImpactSlowdown() { RestoreHitStop(); }

    void RestoreHitStop()
    {
        if (!ownsHitStop) return;
        // A pause/result screen owns zero time scale and must not be resumed here.
        if (Mathf.Approximately(Time.timeScale, hitStopAppliedScale))
            Time.timeScale = hitStopPreviousScale;
        ownsHitStop = false;
    }

    void UpdateAttackLayer()
    {
        if (swingHold > 0f) swingHold = Mathf.Max(0f, swingHold - Time.deltaTime);
        if (driveAttackLayerWeight && animator != null && attackLayerIndex >= 0 && animator.layerCount > attackLayerIndex)
            animator.SetLayerWeight(attackLayerIndex, 0f);
    }

    void Update()
    {
        swordBodyPose.Restore();
        if (Time.timeScale <= 0f)
        {
            bufferTimer = 0f;
            return;
        }

        UpdateAttackLayer();

        manualCooldownTimer -= Time.deltaTime;
        if(weapons!=null && weapons.IsGun){bufferTimer=0;return;}
        if (weapons != null && weapons.TryApplyPendingEquip()) { bufferTimer=0f; return; }
        var mouse = Mouse.current;
        if (!MobileControls.Enabled && mouse == null) return;

        bufferTimer = Mathf.Max(0f, bufferTimer - Time.deltaTime);

        // Read new clicks after aging the buffer so a slow frame cannot discard them.
        if (MobileControls.Enabled ? MobileControls.Firing : mouse.leftButton.wasPressedThisFrame)
        {
            bufferTimer = inputBufferSeconds;
        }
        if (manualCooldownTimer > 0f || bufferTimer <= 0f) return;
        bufferTimer = 0f;
        Swing();
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

    void Swing()
    {
        float cooldownScale = weapons != null ? weapons.CooldownScale : 1f;
        var effects = GetComponent<PowerUpEffects>();
        manualCooldownTimer = manualAttackCooldown * cooldownScale * (effects != null ? effects.CooldownMultiplier : 1f) * (GetComponent<CharacterGrowth>()?.CooldownMultiplier ?? 1f);
        if (RunStats.Instance != null) RunStats.Instance.RegisterClick();
        GameAudio.Play(GameAudio.Sfx.Swing);

        float damage = manualAttackDamage * (GetComponent<CharacterGrowth>()?.DamageMultiplier ?? 1f) * (effects != null ? effects.DamageMultiplier : 1f);
        damage *= weapons != null ? weapons.DamageScale : 1f;
        damage *= GetComponent<CharacterGrowth>()?.ConsumeDashStrike() ?? 1f;
        float range = manualAttackRange * (weapons != null ? weapons.RangeScale : 1f);
        float angle = Mathf.Min(300f, attackAngle + (weapons != null ? weapons.ArcBonus : 0f));
        float feedback = 1f;

        if (IsAxe) feedback *= 1.55f;
        float delay = PlayAttackMotion();
        StartCoroutine(StrikeAfter(delay, damage, range, angle, feedback));
    }

    /// <summary>Starts the swing visuals and returns when the blade should connect.</summary>
    float PlayAttackMotion()
    {
        if (animator != null)
        {
            float duration = Mathf.Max(0.1f, IsAxe ? Mathf.Min(.78f, manualCooldownTimer) : manualCooldownTimer);
            animator.SetFloat("AttackSpeed", swingStateDuration / duration);
            currentSwingDuration = duration;
            swingHold = duration;
            return (IsAxe ? AxeSwingMotion.ContactPhase : handGrip!=null ? SwordSwingMotion.ContactPhase : Mathf.Clamp01(impactDelay / Mathf.Max(0.01f, swingStateDuration))) * duration;
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

        Color sweepColor = IsAxe ? new Color(1f,.48f,.12f,1f) : feedback >= 1.7f
            ? new Color(0.35f, 0.9f, 1f, 1f)
            : feedback >= 1.2f
                ? new Color(1f, 0.32f, 0.08f, 1f)
                : new Color(1f, 0.88f, 0.45f, 1f);
        float sweepLifetime = 0.12f * Mathf.Lerp(1f, 1.45f, (feedback - 1f) / 0.75f);
        float sweepGrowth = Mathf.Lerp(1.06f, 1.18f, (feedback - 1f) / 0.75f);

        // The axe chops down; a horizontal sword crescent contradicts that motion.
        if (!IsAxe) SwordSweep.Spawn(transform.position + Vector3.up * sweepEffectHeight,
                         transform.forward, range, angle, range * sweepInnerRatio,
                         sweepColor, sweepLifetime, sweepGrowth);

        var targets = FindTargetsInFront(range, angle);

        if (targets.Count == 0)
        {
            if (CameraShake.Instance != null && missShakeMagnitude > 0f)
                CameraShake.Instance.Shake(missShakeDuration, missShakeMagnitude * feedback);
            yield break;
        }

        if (RunStats.Instance != null) RunStats.Instance.RegisterHit();
        GameAudio.Play(IsAxe ? GameAudio.Sfx.AxeHit : GameAudio.Sfx.Hit);

        bool burstTriggered=false;
        foreach (var target in targets)
        {
            if (target == null || target.IsDying) continue;
            HitImpact.Spawn(ContactPoint(target), transform.forward, impactEffectSize * feedback);
            MeleeImpactBurst.Spawn(ContactPoint(target), feedback);
            target.TakeDamage(damage, ContactPoint(target));
            if(target.IsDying && !burstTriggered)
            {
                burstTriggered=true;
                GetComponent<CharacterGrowth>()?.BurstOnMeleeKill(target.transform.position,damage);
            }
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

        // Never extend or stack a stop when several enemies are hit together.
        if (ownsHitStop) yield break;
        hitStopPreviousScale = Time.timeScale;
        hitStopAppliedScale = Mathf.Min(hitStopPreviousScale, strength >= 1.7f ? 0.08f : hitStopScale);
        ownsHitStop = true;
        Time.timeScale = hitStopAppliedScale;
        try
        {
            yield return new WaitForSecondsRealtime(hitStopDuration * strength);
        }
        finally { RestoreHitStop(); }
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

    readonly RaycastHit[] strikeObstacles = new RaycastHit[64];
    bool HasClearStrikePath(Collider target)
    {
        Vector3 origin = transform.position + Vector3.up * impactSourceHeight;
        Vector3 delta = target.ClosestPoint(origin) - origin;
        float distance = delta.magnitude;
        if (distance < .01f) return true;
        int count = Physics.RaycastNonAlloc(origin, delta / distance, strikeObstacles, distance,
            ~(1 << 2), QueryTriggerInteraction.Ignore);
        if (count == strikeObstacles.Length) return false;
        for (int i=0; i<count; i++)
        {
            var blocker = strikeObstacles[i].collider;
            if (blocker.transform.IsChildOf(transform) || blocker.GetComponentInParent<Enemy>() != null) continue;
            return false;
        }
        return true;
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
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null || enemy.IsDying || !HasClearStrikePath(hit)) continue;

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
