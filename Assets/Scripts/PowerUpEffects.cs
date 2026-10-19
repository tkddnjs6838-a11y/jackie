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
    public float rageSeconds = 5f;
    public float frenzySeconds = 5f;
    public float shieldSeconds = 3f;
    public float slowSeconds = 5f;

    [Header("Strength")]
    public float rageDamageMultiplier = 2f;
    public float frenzyCooldownMultiplier = 0.5f;
    public float slowEnemyScale = 0.45f;


    // read by the HUD, and by the play-mode checks
    public Kind ActiveKind { get; private set; }
    public float Remaining { get; private set; }
    public bool Active { get { return Remaining > 0f; } }

    void Awake()
    {
        Instance = this;
        if(GetComponent<TimedEffectHud>()==null)gameObject.AddComponent<TimedEffectHud>();
        if(GetComponent<TimedEffectPlayerVisual>()==null)gameObject.AddComponent<TimedEffectPlayerVisual>();
        EnemySpeedScale = 1f;       // static: it would survive a scene reload otherwise
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            EnemySpeedScale = 1f;
            Instance = null;
        }
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

    public static string DisplayName(Kind kind)
    {
        switch(kind){case Kind.Rage:return "공격력 강화";case Kind.Frenzy:return "공격속도 강화";case Kind.Shield:return "보호막";default:return "적 둔화";}
    }
    public string Description(Kind kind)
    {
        switch(kind)
        {
            case Kind.Rage:return "공격력 ×"+rageDamageMultiplier.ToString("0.#");
            case Kind.Frenzy:return "공격 간격 "+(frenzyCooldownMultiplier*100).ToString("0")+"%";
            case Kind.Shield:return "받는 피해 무효";
            default:return "적 이동속도 "+(Mathf.Clamp01(slowEnemyScale)*100).ToString("0")+"%";
        }
    }
    public int ActivationVersion {get;private set;}

    // Temporary multipliers never modify the permanent upgrade stats.
    public float DamageMultiplier => Active && ActiveKind == Kind.Rage ? rageDamageMultiplier : 1f;
    public float CooldownMultiplier => Active && ActiveKind == Kind.Frenzy ? frenzyCooldownMultiplier : 1f;
    public bool ShieldActive => Active && ActiveKind == Kind.Shield;

    public void Activate(Kind kind)
    {
        if (!isActiveAndEnabled || kind<Kind.Rage || kind>Kind.Slow) return;
        Restore();
        ActiveKind = kind;
        ActivationVersion++;
        Remaining = Mathf.Max(0f, DurationOf(kind));
        if (kind == Kind.Slow && Active) EnemySpeedScale = Mathf.Clamp01(slowEnemyScale);
    }

    void Update()
    {
        if (!Active) return;
        if(RunStats.Instance!=null && !RunStats.Instance.Running){Restore();return;}
        Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
        if (!Active) Restore();
    }

    void OnDisable() { Restore(); }

    public float DurationOf(Kind kind)
    {
        switch (kind)
        {
            case Kind.Rage: return rageSeconds;
            case Kind.Frenzy: return frenzySeconds;
            case Kind.Shield: return shieldSeconds;
            default: return slowSeconds;
        }
    }

    void Restore()
    {
        if (Instance == this) EnemySpeedScale = 1f;
        Remaining = 0f;
    }
}
