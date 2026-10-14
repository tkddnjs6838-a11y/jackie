using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Space-bar dodge roll. Carries the player a fixed distance fast, with i-frames for
/// most of it, so being surrounded by the swarm is escapable with timing rather than
/// simply fatal. Exposes CooldownFraction for the HUD.
/// </summary>
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(CharacterController))]
public class PlayerDash : MonoBehaviour
{
    // Distance rather than speed: driving it off speed x deltaTime overshot the intended
    // travel (measured 5.67m for a nominal 3.06m), so the dash now spends an exact budget.
    public float dashDistance = 4.5f;
    public float dashDuration = 0.18f;
    public float cooldown = 2f;
    public float invincibleDuration = 0.28f;

    [Header("Feel")]
    public float shakeDuration = 0.06f;
    public float shakeAmount = 0.03f;

    private CharacterController controller;
    private PlayerHealth health;
    private float cooldownTimer;
    private float dashTimer;
    private float dashRemaining;
    private Vector3 dashDirection;

    public bool IsDashing { get { return dashTimer > 0f; } }
    public bool IsReady { get { return cooldownTimer <= 0f; } }
    public float CooldownFraction { get { return cooldown <= 0f ? 1f : Mathf.Clamp01(1f - cooldownTimer / cooldown); } }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<PlayerHealth>();
    }

    void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (dashTimer > 0f)
        {
            dashTimer -= Time.deltaTime;

            float step = (dashDistance / Mathf.Max(0.01f, dashDuration)) * Time.deltaTime;
            step = Mathf.Min(step, dashRemaining);
            dashRemaining -= step;

            controller.Move(dashDirection * step);
            if (dashRemaining <= 0f) dashTimer = 0f;
            return;
        }

        var kb = Keyboard.current;
        bool pressed=MobileControls.Enabled ? MobileControls.TakeDash() : kb!=null && kb.spaceKey.wasPressedThisFrame;
        if (pressed && cooldownTimer <= 0f)
            StartDash(ReadDirection(kb));
    }

    Vector3 ReadDirection(Keyboard kb)
    {
        Vector2 input = MobileControls.Enabled ? MobileControls.Move : Vector2.zero;
        if(!MobileControls.Enabled && kb!=null)
        {
        if (kb.wKey.isPressed) input.y += 1f;
        if (kb.sKey.isPressed) input.y -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        if (kb.aKey.isPressed) input.x -= 1f;

        }
        // no stick input means dodge the way the player faces
        if (input.sqrMagnitude < 0.01f) return transform.forward;

        input = Vector2.ClampMagnitude(input, 1f);
        Vector3 dir = transform.right * input.x + transform.forward * input.y;
        dir.y = 0f;
        return dir.normalized;
    }

    void StartDash(Vector3 direction)
    {
        dashDirection = direction;
        dashTimer = dashDuration;
        dashRemaining = dashDistance;
        cooldownTimer = cooldown;

        if (health != null) health.GrantInvincibility(invincibleDuration);
        GameAudio.Play(GameAudio.Sfx.Dash);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(shakeDuration, shakeAmount);
    }
}
