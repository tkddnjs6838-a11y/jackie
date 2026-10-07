using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The in-run readouts that go with the dash and the combo system: a dash cooldown bar,
/// the kill combo with its own countdown, and the survival clock.
/// </summary>
public class RunHud : MonoBehaviour
{
    [Header("Dash")]
    public PlayerDash dash;
    public RectTransform dashFill;
    public Image dashFillImage;
    public Text dashLabel;
    public Color dashReadyColor = new Color(0.35f, 0.75f, 1f, 1f);
    public Color dashChargingColor = new Color(0.35f, 0.4f, 0.5f, 1f);

    [Header("Combo")]
    public GameObject comboRoot;
    public Text comboText;
    public RectTransform comboTimerFill;
    public int comboShowsFrom = 2;
    public float comboPunch = 0.25f;

    [Header("Clock")]
    public Text timeText;

    [Header("Power-up")]
    // a timed buff the player cannot see the clock on is a buff they will waste
    public Text powerUpText;

    private int lastCombo;
    private float punch;

    void Awake()
    {
        if (dash == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) dash = player.GetComponent<PlayerDash>();
        }
        if (comboRoot != null) comboRoot.SetActive(false);
    }

    void Update()
    {
        UpdatePowerUp();
        UpdateDash();
        UpdateCombo();
        UpdateClock();
    }

    void UpdatePowerUp()
    {
        if (powerUpText == null) return;

        var fx = PowerUpEffects.Instance;
        if (fx == null || !fx.Active)
        {
            if (powerUpText.enabled) powerUpText.enabled = false;
            return;
        }

        powerUpText.enabled = true;
        powerUpText.color = PowerUpEffects.ColorOf(fx.ActiveKind);
        powerUpText.text = PowerUpEffects.NameOf(fx.ActiveKind) + "  " + fx.Remaining.ToString("F1") + "s";
    }

    void UpdateDash()
    {
        if (dash == null) return;

        float f = dash.CooldownFraction;
        if (dashFill != null)
        {
            var anchorMax = dashFill.anchorMax;
            anchorMax.x = f;
            dashFill.anchorMax = anchorMax;
        }
        if (dashFillImage != null)
            dashFillImage.color = dash.IsReady ? dashReadyColor : dashChargingColor;
        if (dashLabel != null)
            dashLabel.text = dash.IsReady ? "DASH  [Space]" : "DASH";
    }

    void UpdateCombo()
    {
        var stats = RunStats.Instance;
        if (stats == null || comboRoot == null) return;

        bool show = stats.Combo >= comboShowsFrom;
        if (comboRoot.activeSelf != show) comboRoot.SetActive(show);
        if (!show) { lastCombo = 0; return; }

        if (stats.Combo != lastCombo)
        {
            lastCombo = stats.Combo;
            punch = comboPunch;             // pop the text on every new kill
        }

        if (punch > 0f) punch -= Time.unscaledDeltaTime;
        float scale = 1f + Mathf.Max(0f, punch) * 1.6f;

        if (comboText != null)
        {
            comboText.text = "x" + stats.Combo + "  COMBO";
            comboText.rectTransform.localScale = Vector3.one * scale;
        }
        if (comboTimerFill != null)
        {
            var anchorMax = comboTimerFill.anchorMax;
            anchorMax.x = stats.ComboFraction;
            comboTimerFill.anchorMax = anchorMax;
        }
    }

    void UpdateClock()
    {
        var stats = RunStats.Instance;
        if (stats == null || timeText == null) return;
        timeText.text = RunStats.FormatTime(stats.SurvivalTime);
    }
}
