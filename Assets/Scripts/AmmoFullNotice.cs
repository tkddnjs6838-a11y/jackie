using UnityEngine;

// Screen-space presentation is owned by CombatHudView, so scenery cannot hide the notice.
public sealed class AmmoFullNotice : MonoBehaviour
{
    float remaining;
    public bool Visible => remaining > 0 && RunStats.Instance != null && RunStats.Instance.Running;
    public float Opacity => Mathf.Clamp01(remaining / .35f);
    public void Show()
    {
        if (remaining > 0) return;
        remaining = 2.5f;
    }
    void Update()
    {
        if (RunStats.Instance == null || !RunStats.Instance.Running) remaining = 0;
        remaining = Mathf.Max(0, remaining - Time.deltaTime);
    }
    void OnDisable() { remaining = 0; }
}
