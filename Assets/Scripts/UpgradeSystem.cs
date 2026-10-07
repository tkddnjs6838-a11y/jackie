using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Every so many kills the run pauses and offers three random upgrades to pick from.
/// This is what turns the game from a fixed countdown into a build that grows.
/// </summary>
public class UpgradeSystem : MonoBehaviour
{
    private class Upgrade
    {
        public string Title;
        public string Description;
        public System.Action Apply;
        public System.Func<bool> Available;
    }

    [Header("Milestones")]
    // Upgrades are milestone rewards, not a steady drip: health orbs cover the
    // moment-to-moment recovery, and a build-changing pick only lands at these kill
    // counts. Which three are offered is still random.
    public int[] upgradeAtKills = new int[] { 50, 100, 200, 300 };

    [Header("UI")]
    public GameObject panel;
    public Text headerText;
    public Button[] choiceButtons = new Button[3];
    public Text[] choiceTitles = new Text[3];
    public Text[] choiceDescriptions = new Text[3];

    private readonly List<Upgrade> pool = new List<Upgrade>();
    private readonly List<Upgrade> offered = new List<Upgrade>();
    private int kills;
    private int nextMilestone;
    private int taken;
    private bool choosing;

    private PlayerAutoAttack attack;
    private PlayerController movement;
    private PlayerHealth health;
    private PlayerDash dash;

    void Awake()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            attack = player.GetComponent<PlayerAutoAttack>();
            movement = player.GetComponent<PlayerController>();
            health = player.GetComponent<PlayerHealth>();
            dash = player.GetComponent<PlayerDash>();
        }

        BuildPool();
        if (panel != null) panel.SetActive(false);

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int index = i;
            if (choiceButtons[i] != null)
                choiceButtons[i].onClick.AddListener(delegate { Choose(index); });
        }
    }

    void OnEnable()
    {
        Enemy.OnEnemyDied += HandleEnemyDied;
    }

    void OnDisable()
    {
        Enemy.OnEnemyDied -= HandleEnemyDied;
    }

    void BuildPool()
    {
        pool.Clear();
        if (attack == null) return;

        pool.Add(new Upgrade {
            Title = "SHARPER BLADE",
            Description = "Attack damage +8",
            Apply = delegate { attack.manualAttackDamage += 8f; },
            Available = delegate { return true; }
        });
        pool.Add(new Upgrade {
            Title = "QUICK HANDS",
            Description = "Attack 15% faster",
            Apply = delegate { attack.manualAttackCooldown = Mathf.Max(0.15f, attack.manualAttackCooldown * 0.85f); },
            Available = delegate { return attack.manualAttackCooldown > 0.16f; }
        });
        pool.Add(new Upgrade {
            Title = "LONG REACH",
            Description = "Attack range +0.4m",
            Apply = delegate { attack.manualAttackRange += 0.4f; },
            Available = delegate { return attack.manualAttackRange < 6f; }
        });
        pool.Add(new Upgrade {
            Title = "WIDE SWING",
            Description = "Attack arc +25 degrees",
            Apply = delegate { attack.attackAngle = Mathf.Min(340f, attack.attackAngle + 25f); },
            Available = delegate { return attack.attackAngle < 340f; }
        });
        pool.Add(new Upgrade {
            Title = "LIGHT FEET",
            Description = "Move speed +0.9",
            Apply = delegate { if (movement != null) movement.moveSpeed += 0.9f; },
            Available = delegate { return movement != null && movement.moveSpeed < 14f; }
        });
        pool.Add(new Upgrade {
            Title = "TOUGH HIDE",
            Description = "Max health +25, healed now",
            Apply = delegate { if (health != null) health.AddMaxHealth(25f); },
            Available = delegate { return health != null; }
        });
        pool.Add(new Upgrade {
            Title = "BLOOD EDGE",
            Description = "Heal 3 per connecting swing",
            // capped: lifesteal is already limited per second, and stacking it endlessly
            // was what made the player unkillable while attacking
            Apply = delegate { attack.lifestealPerHit += 3f; attack.lifestealPerSecondCap += 1.5f; },
            Available = delegate { return attack.lifestealPerHit < 9f; }
        });
        pool.Add(new Upgrade {
            Title = "SECOND WIND",
            Description = "Dash cooldown -0.4s",
            Apply = delegate { if (dash != null) dash.cooldown = Mathf.Max(0.5f, dash.cooldown - 0.4f); },
            Available = delegate { return dash != null && dash.cooldown > 0.55f; }
        });
    }

    void HandleEnemyDied(Enemy enemy)
    {
        if (enemy.isBoss || choosing) return;
        if (RunStats.Instance != null && !RunStats.Instance.Running) return;

        // an elite pays out immediately: that payout is the whole reason to chase one
        if (enemy.GetComponent<EliteMarker>() != null)
        {
            kills++;
            Offer();
            return;
        }

        kills++;
        if (upgradeAtKills == null) return;

        if (nextMilestone < upgradeAtKills.Length && kills >= upgradeAtKills[nextMilestone])
        {
            nextMilestone++;
            Offer();
        }
    }

    void Offer()
    {
        if (panel == null) return;

        var candidates = new List<Upgrade>();
        foreach (var u in pool)
            if (u.Available == null || u.Available()) candidates.Add(u);
        if (candidates.Count == 0) return;

        offered.Clear();
        for (int i = 0; i < choiceButtons.Length && candidates.Count > 0; i++)
        {
            int pick = Random.Range(0, candidates.Count);
            offered.Add(candidates[pick]);
            candidates.RemoveAt(pick);
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool has = i < offered.Count;
            if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(has);
            if (!has) continue;
            if (choiceTitles[i] != null) choiceTitles[i].text = offered[i].Title;
            if (choiceDescriptions[i] != null) choiceDescriptions[i].text = offered[i].Description;
        }

        if (headerText != null) headerText.text = "CHOOSE AN UPGRADE     " + (taken + 1);

        choosing = true;
        panel.SetActive(true);
        GameAudio.Play(GameAudio.Sfx.UpgradeOpen);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Choose(int index)
    {
        if (!choosing || index < 0 || index >= offered.Count) return;

        offered[index].Apply();
        GameAudio.Play(GameAudio.Sfx.UpgradePick);
        taken++;
        choosing = false;
        if (panel != null) panel.SetActive(false);

        // only hand time back if nothing else (death, victory) has taken over the pause
        if (RunStats.Instance == null || RunStats.Instance.Running)
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public int Taken { get { return taken; } }
    public bool Choosing { get { return choosing; } }
}
