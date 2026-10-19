using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float mouseSensitivity = 2f;
    public float gravity = -20f;
    public Transform cameraTransform;

    [Header("Animation")]
    public Animator animator;
    public string speedParameter = "Speed";
    public float speedDampTime = 0.1f;

    private CharacterController controller;
    private float verticalVelocity;
    private float pitch = 18f;
    private bool hasSpeedParameter;
    private PlayerDash dash;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if(cameraTransform!=null && cameraTransform.GetComponent<CombatCamera>()==null)cameraTransform.gameObject.AddComponent<CombatCamera>();
        if(GetComponent<PlayerGroundSafety>()==null)gameObject.AddComponent<PlayerGroundSafety>();
        dash = GetComponent<PlayerDash>();
        if(GetComponent<MobileControls>()==null)gameObject.AddComponent<MobileControls>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            foreach (var p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Float && p.name == speedParameter)
                    hasSpeedParameter = true;
        }
    }

    void Update()
    {
        if (Time.timeScale <= 0f) return;
        HandleLook();
        if (dash == null || !dash.IsDashing) HandleMove();
    }

    void HandleLook()
    {
        var mouse = Mouse.current;
        if (!MobileControls.Enabled && mouse == null) return;

        Vector2 delta = MobileControls.Enabled ? MobileControls.TakeLook() : mouse.delta.ReadValue() * mouseSensitivity * 0.05f;
        transform.Rotate(Vector3.up * delta.x);

        pitch = Mathf.Clamp(pitch - delta.y, -10f, 60f);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleMove()
    {
        var kb = Keyboard.current;
        Vector2 input = MobileControls.Enabled ? MobileControls.Move : Vector2.zero;
        if(!MobileControls.Enabled && kb!=null)
        {
        if (kb.wKey.isPressed) input.y += 1f;
        if (kb.sKey.isPressed) input.y -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        if (kb.aKey.isPressed) input.x -= 1f;
        }
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 move = (transform.right * input.x + transform.forward * input.y) * moveSpeed * (GetComponent<CharacterGrowth>()?.MoveMultiplier ?? 1f);

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        // drive the walk blend from the horizontal speed only, so falling never looks like walking
        if (hasSpeedParameter)
            animator.SetFloat(speedParameter, new Vector2(move.x, move.z).magnitude, speedDampTime, Time.deltaTime);

        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);
    }
}
