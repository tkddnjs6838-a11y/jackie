using UnityEngine;

// Idle motion for the start screen only. The run is paused (timeScale 0) while this
// screen is up, so everything here runs on unscaled time.
public class StartScreenMotion : MonoBehaviour
{
    [SerializeField] RectTransform hero;
    [SerializeField] RectTransform heroShadow;
    [SerializeField] RectTransform startButton;
    [SerializeField] RectTransform[] floaters;

    [SerializeField] float heroBob = 10f;
    [SerializeField] float buttonPulse = 0.035f;
    [SerializeField] float floatAmount = 14f;

    Vector2 heroBase;
    Vector2[] floaterBase;

    void Awake()
    {
        if (hero != null) heroBase = hero.anchoredPosition;
        floaterBase = new Vector2[floaters.Length];
        for (int i = 0; i < floaters.Length; i++) floaterBase[i] = floaters[i].anchoredPosition;
    }

    void Update()
    {
        float t = Time.unscaledTime;

        if (hero != null)
        {
            // a wax ball bounce: quick up, squash on landing
            float bounce = Mathf.Abs(Mathf.Sin(t * 2.4f));
            hero.anchoredPosition = heroBase + new Vector2(0f, bounce * heroBob);
            float squash = 1f - (1f - bounce) * 0.06f;
            hero.localScale = new Vector3(2f - squash, squash, 1f);
            if (heroShadow != null) heroShadow.localScale = Vector3.one * (1f - bounce * 0.18f);
        }

        if (startButton != null)
        {
            float s = 1f + Mathf.Sin(t * 3.2f) * buttonPulse;
            startButton.localScale = new Vector3(s, s, 1f);
        }

        for (int i = 0; i < floaters.Length; i++)
        {
            float phase = t * (0.5f + i * 0.13f) + i * 1.7f;
            floaters[i].anchoredPosition = floaterBase[i] + new Vector2(Mathf.Cos(phase * 0.7f), Mathf.Sin(phase)) * floatAmount;
        }
    }
}
