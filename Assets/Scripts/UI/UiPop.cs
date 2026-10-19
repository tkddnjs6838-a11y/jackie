using UnityEngine;
using UnityEngine.EventSystems;

// Springy scale for overlay cards: pops in (after `delay`) whenever it is shown and grows slightly
// under the pointer. Runs on unscaled time because choice screens pause the game.
public sealed class UiPop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float delay;
    public bool hoverScale = true;
    float shownAt, scale, velocity, target;

    void OnEnable()
    {
        shownAt = Time.unscaledTime + delay;
        scale = .55f; velocity = 0f; target = 1f;
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        if (Time.unscaledTime < shownAt) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
        // underdamped spring: a short overshoot reads as a "pop" instead of a slide
        velocity += (target - scale) * 260f * dt;
        velocity *= Mathf.Exp(-18f * dt);
        scale += velocity * dt;
        transform.localScale = Vector3.one * scale;
    }

    public void OnPointerEnter(PointerEventData eventData) => target = hoverScale ? 1.05f : 1f;
    public void OnPointerExit(PointerEventData eventData) => target = 1f;
}
