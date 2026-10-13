using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public static event System.Action OnPlayerDied;

    public float maxHealth = 100f;
    public float currentHealth;
    public float invincibilityDuration = 0.5f;

    private float invincibilityTimer;
    private bool isDead;
    private PowerUpEffects effects;
    public bool IsDead => isDead;

    public bool IsInvincible { get { return invincibilityTimer > 0f || (effects != null && effects.ShieldActive); } }

    /// <summary>Grants i-frames from outside, e.g. for the duration of a dash.</summary>
    public void GrantInvincibility(float duration)
    {
        if (duration > invincibilityTimer) invincibilityTimer = duration;
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    /// <summary>Raises the ceiling and grants the same amount, so the pick feels immediate.</summary>
    public void AddMaxHealth(float amount)
    {
        if (amount <= 0f) return;
        maxHealth += amount;
        Heal(amount);
    }

    void Awake()
    {
        currentHealth = maxHealth;
        effects = GetComponent<PowerUpEffects>();
    }

    void Update()
    {
        if (invincibilityTimer > 0f)
            invincibilityTimer -= Time.deltaTime;
    }

    public void TakeDamage(float amount)
    {
        if (isDead || IsInvincible || amount <= 0f) return;

        invincibilityTimer = invincibilityDuration;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        GameAudio.Play(GameAudio.Sfx.PlayerHurt);
        Debug.Log("Player took " + amount + " damage. HP=" + currentHealth);

        DamageNumber.Create(transform.position + Vector3.up * 1.8f, amount.ToString("F0"), Color.red);
        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.15f, 0.06f);

        if (currentHealth <= 0f)
            Die();
    }

    void Die()
    {
        isDead = true;
        Debug.Log("===== GAME OVER — Player died. Survived for " + Time.timeSinceLevelLoad.ToString("F1") + "s. =====");
        OnPlayerDied?.Invoke();
        Time.timeScale = 0f;
    }
}
