using UnityEngine;

/// <summary>
/// Tracks the current run: kill combo, survival time, and the personal bests, which are
/// kept in PlayerPrefs so they survive between sessions.
/// </summary>
public class RunStats : MonoBehaviour
{
    public static RunStats Instance { get; private set; }

    [Header("Combo")]
    public float comboWindow = 3f;

    public int Combo { get; private set; }
    public int BestComboThisRun { get; private set; }
    public int Kills { get; private set; }
    public int Clicks { get; private set; }
    public int Hits { get; private set; }
    public float Accuracy { get { return Clicks > 0 ? (float)Hits / Clicks : 0f; } }
    public float ComboTimeLeft { get { return comboTimer; } }
    public float ComboFraction { get { return comboWindow <= 0f ? 0f : Mathf.Clamp01(comboTimer / comboWindow); } }
    public float SurvivalTime { get; private set; }
    public bool Running { get; private set; }

    public int BestKills { get { return PlayerPrefs.GetInt(KeyKills, 0); } }
    public int BestCombo { get { return PlayerPrefs.GetInt(KeyCombo, 0); } }
    public float BestTime { get { return PlayerPrefs.GetFloat(KeyTime, 0f); } }

    const string KeyKills = "best_kills";
    const string KeyCombo = "best_combo";
    const string KeyTime = "best_time";

    private float comboTimer;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        Enemy.OnEnemyDied += HandleEnemyDied;
    }

    void OnDisable()
    {
        Enemy.OnEnemyDied -= HandleEnemyDied;
        if (Instance == this) Instance = null;
    }

    /// <summary>Called when the run actually begins, i.e. once the start panel is gone.</summary>
    public void BeginRun()
    {
        Running = true;
        SurvivalTime = 0f;
        Kills = 0;
        Combo = 0;
        BestComboThisRun = 0;
        Clicks = 0;
        Hits = 0;
        comboTimer = 0f;
    }

    /// <summary>One attack input, whether or not it connected.</summary>
    public void RegisterClick()
    {
        if (Running) Clicks++;
    }

    /// <summary>One attack input that connected with at least one enemy.</summary>
    public void RegisterHit()
    {
        if (Running) Hits++;
    }

    public void EndRun()
    {
        if (!Running) return;
        Running = false;

        if (Kills > BestKills) PlayerPrefs.SetInt(KeyKills, Kills);
        if (BestComboThisRun > BestCombo) PlayerPrefs.SetInt(KeyCombo, BestComboThisRun);
        if (SurvivalTime > BestTime) PlayerPrefs.SetFloat(KeyTime, SurvivalTime);
        PlayerPrefs.Save();
    }

    void HandleEnemyDied(Enemy enemy)
    {
        if (!Running || enemy.isBoss) return;

        Kills++;
        Combo++;
        if (Combo > BestComboThisRun) BestComboThisRun = Combo;
        comboTimer = comboWindow;
    }

    void Update()
    {
        if (!Running) return;

        SurvivalTime += Time.deltaTime;

        if (comboTimer > 0f)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f) Combo = 0;
        }
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }
}
