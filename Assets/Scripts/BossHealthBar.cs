using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-of-screen health bar for the boss. Shows itself when a boss spawns and hides
/// again once it dies. The pale trailing bar drains a moment behind the red one so a
/// big hit reads as a chunk taken off.
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    public GameObject root;
    public RectTransform fillRect;
    public RectTransform trailRect;
    public Image fillImage;
    public Text nameText;
    public Text healthText;

    public string bossName = "BOSS";
    public Color fullColor = new Color(0.85f, 0.22f, 0.20f, 1f);
    public Color lowColor = new Color(1f, 0.62f, 0.15f, 1f);
    public float fillLerpSpeed = 9f;
    public float trailLerpSpeed = 1.6f;
    public float trailDelay = 0.35f;

    private Enemy boss;
    private float displayed = 1f;
    private float trailed = 1f;
    private float trailHold;

    void Awake()
    {
        if (root != null) root.SetActive(false);
    }

    void OnEnable()
    {
        Enemy.OnBossSpawned += HandleBossSpawned;
        Enemy.OnEnemyDied += HandleEnemyDied;
    }

    void OnDisable()
    {
        Enemy.OnBossSpawned -= HandleBossSpawned;
        Enemy.OnEnemyDied -= HandleEnemyDied;
    }

    void HandleBossSpawned(Enemy spawned)
    {
        boss = spawned;
        displayed = 1f;
        trailed = 1f;
        trailHold = 0f;

        if (nameText != null) nameText.text = bossName;
        if (root != null) root.SetActive(true);
        UpdateLabel(spawned.maxHealth, spawned.maxHealth);
    }

    void HandleEnemyDied(Enemy died)
    {
        if (died != boss) return;
        boss = null;
        if (root != null) root.SetActive(false);
    }

    void Update()
    {
        if (root == null || !root.activeSelf) return;

        // the boss object is destroyed after its death animation
        if (boss == null)
        {
            root.SetActive(false);
            return;
        }

        float target = boss.HealthFraction;

        float previous = displayed;
        displayed = Mathf.MoveTowards(displayed, target, fillLerpSpeed * Time.unscaledDeltaTime);
        if (displayed < previous) trailHold = trailDelay;

        if (trailHold > 0f) trailHold -= Time.unscaledDeltaTime;
        else trailed = Mathf.MoveTowards(trailed, displayed, trailLerpSpeed * Time.unscaledDeltaTime);
        if (trailed < displayed) trailed = displayed;

        SetWidth(fillRect, displayed);
        SetWidth(trailRect, trailed);

        if (fillImage != null)
            fillImage.color = Color.Lerp(lowColor, fullColor, Mathf.Clamp01(target * 1.4f));

        UpdateLabel(boss.CurrentHealth, boss.maxHealth);
    }

    void UpdateLabel(float current, float max)
    {
        if (healthText == null) return;
        healthText.text = Mathf.Max(0, Mathf.CeilToInt(current)) + " / " + Mathf.RoundToInt(max);
    }

    static void SetWidth(RectTransform rect, float fraction)
    {
        if (rect == null) return;
        var anchorMax = rect.anchorMax;
        anchorMax.x = Mathf.Clamp01(fraction);
        rect.anchorMax = anchorMax;
    }
}
