using System.Collections;
using UnityEngine;

/// <summary>
/// Short, loud buffs the player picks up off the floor.
///
/// The only thing worth grabbing until now was a health orb, which is a number going up and
/// nothing else. These change how the game plays for a few seconds — long enough to charge
/// into a crowd you would normally run from, short enough that you have to spend it.
///
/// Only one runs at a time on purpose: picking a second one should feel like a choice to
/// overwrite, not like stacking bonuses until nothing can hurt you.
/// </summary>
public class PowerUpEffects : MonoBehaviour
{
    public enum Kind
    {
        Rage,       // double damage
        Frenzy,     // double attack speed
        Shield,     // untouchable
        Slow        // the swarm crawls
    }

    public static PowerUpEffects Instance;

    /// <summary>Movement scale every monster multiplies into its own speed.</summary>
    public static float EnemySpeedScale = 1f;

    [Header("Durations")]
    public float rageSeconds = 9f;
    public float frenzySeconds = 8f;
    public float shieldSeconds = 5f;
    public float slowSeconds = 6f;

    [Header("Strength")]
    public float rageDamageMultiplier = 2f;
    public float frenzyCooldownMultiplier = 0.5f;
    public float slowEnemyScale = 0.45f;

    private PlayerAutoAttack attack;
    private PlayerHealth health;
    private Coroutine running;

    // read by the HUD, and by the play-mode checks
    public Kind ActiveKind { get; private set; }
    public float Remaining { get; private set; }
    public bool Active { get { return Remaining > 0f; } }

    void Awake()
    {
        Instance = this;
        EnemySpeedScale = 1f;       // static: it would survive a scene reload otherwise
        attack = GetComponent<PlayerAutoAttack>();
        health = GetComponent<PlayerHealth>();
    }

    void OnDestroy()
    {
        EnemySpeedScale = 1f;
        if (Instance == this) Instance = null;
    }

    public static Color ColorOf(Kind kind)
    {
        switch (kind)
        {
            case Kind.Rage: return new Color(1f, 0.35f, 0.3f, 1f);
            case Kind.Frenzy: return new Color(1f, 0.85f, 0.3f, 1f);
            case Kind.Shield: return new Color(0.45f, 0.75f, 1f, 1f);
            default: return new Color(0.7f, 0.5f, 1f, 1f);
        }
    }

    public static string NameOf(Kind kind)
    {
        switch (kind)
        {
            case Kind.Rage: return "RAGE";
            case Kind.Frenzy: return "FRENZY";
            case Kind.Shield: return "SHIELD";
            default: return "SLOW";
        }
    }

    public void Activate(Kind kind)
    {
        // a new pickup replaces whatever is running, and the old effect must be undone
        // first or its restore would overwrite the new one's values
        if (running != null) StopCoroutine(running);
        Restore();
        running = StartCoroutine(Run(kind));
    }

    IEnumerator Run(Kind kind)
    {
        ActiveKind = kind;
        float duration = DurationOf(kind);
        Remaining = duration;

        switch (kind)
        {
            case Kind.Rage:
                if (attack != null) attack.manualAttackDamage *= rageDamageMultiplier;
                break;
            case Kind.Frenzy:
                if (attack != null) attack.manualAttackCooldown *= frenzyCooldownMultiplier;
                break;
            case Kind.Slow:
                EnemySpeedScale = slowEnemyScale;
                break;
            case Kind.Shield:
                break;      // topped up below, since invincibility is a decaying timer
        }

        while (Remaining > 0f)
        {
            Remaining -= Time.deltaTime;
            // the shield has to be re-granted: PlayerHealth counts its i-frames down and
            // any hit would otherwise shorten it
            if (kind == Kind.Shield && health != null) health.GrantInvincibility(0.4f);
            yield return null;
        }

        Remaining = 0f;
        Restore();
        running = null;
    }

    float DurationOf(Kind kind)
    {
        switch (kind)
        {
            case Kind.Rage: return rageSeconds;
            case Kind.Frenzy: return frenzySeconds;
            case Kind.Shield: return shieldSeconds;
            default: return slowSeconds;
        }
    }

    /// <summary>
    /// Undoes whatever is currently applied. Written as exact inverse multiplications so it
    /// stays correct even when an upgrade changed the underlying stat mid-effect.
    /// </summary>
    void Restore()
    {
        if (Remaining <= 0f && EnemySpeedScale == 1f && running == null) return;

        if (attack != null)
        {
            if (ActiveKind == Kind.Rage && Remaining > 0f)
                attack.manualAttackDamage /= rageDamageMultiplier;
            if (ActiveKind == Kind.Frenzy && Remaining > 0f)
                attack.manualAttackCooldown /= frenzyCooldownMultiplier;
        }
        EnemySpeedScale = 1f;
        Remaining = 0f;
    }
}
