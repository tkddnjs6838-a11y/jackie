using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the player's health bar: fill width, colour by remaining health,
/// and a short flash when damage lands.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public RectTransform fillRect;
    public Image fillImage;
    public Text healthText;

    public Color highColor = new Color(0.25f, 0.80f, 0.35f, 1f);
    public Color midColor = new Color(0.95f, 0.78f, 0.20f, 1f);
    public Color lowColor = new Color(0.90f, 0.25f, 0.22f, 1f);
    public Color flashColor = Color.white;

    public float flashDuration = 0.12f;
    public float fillLerpSpeed = 8f;

    private float displayedRatio = 1f;
    private float flashTimer;
    private float lastHealth = -1f;

    void Awake()
    {
        if (playerHealth == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerHealth = p.GetComponent<PlayerHealth>();
        }
    }

    void Update()
    {
        if (playerHealth == null || fillRect == null) return;

        float max = Mathf.Max(1f, playerHealth.maxHealth);
        float current = Mathf.Clamp(playerHealth.currentHealth, 0f, max);
        float ratio = current / max;

        if (lastHealth >= 0f && current < lastHealth)
            flashTimer = flashDuration;
        lastHealth = current;

        // ease the bar toward the real value so chip damage reads as motion
        displayedRatio = Mathf.MoveTowards(displayedRatio, ratio, fillLerpSpeed * Time.unscaledDeltaTime);

        var anchorMax = fillRect.anchorMax;
        anchorMax.x = displayedRatio;
        fillRect.anchorMax = anchorMax;

        if (fillImage != null)
        {
            // green while healthy, yellow around a third left, red once it is critical
            Color target;
            if (ratio >= 0.6f)
                target = highColor;
            else if (ratio >= 0.35f)
                target = Color.Lerp(midColor, highColor, (ratio - 0.35f) / 0.25f);
            else if (ratio >= 0.15f)
                target = Color.Lerp(lowColor, midColor, (ratio - 0.15f) / 0.20f);
            else
                target = lowColor;

            if (flashTimer > 0f)
            {
                flashTimer -= Time.unscaledDeltaTime;
                target = Color.Lerp(target, flashColor, Mathf.Clamp01(flashTimer / flashDuration));
            }

            fillImage.color = target;
        }

        if (healthText != null)
            healthText.text = "HP  " + Mathf.CeilToInt(current) + " / " + Mathf.RoundToInt(max);
    }
}
