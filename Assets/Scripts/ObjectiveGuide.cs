using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// After the boss dies, points the player back to the goal zone:
/// a rotating on-screen arrow with the remaining distance, plus a short banner.
/// </summary>
public class ObjectiveGuide : MonoBehaviour
{
    public Transform target;
    public Transform player;

    public GameObject guideRoot;
    public RectTransform arrowRect;
    public Image arrowImage;
    public Text distanceText;

    public GameObject bannerRoot;
    public Text bannerText;

    public string bannerMessage = "BOSS DEFEATED!  Return to the start zone.";
    public float bannerDuration = 5f;
    public string distanceFormat = "Goal  {0} m";
    public float arrivedDistance = 3f;

    private float bannerTimer;
    private Camera cam;

    void Awake()
    {
        if (target == null)
        {
            var zone = GameObject.Find("WinZone");
            if (zone != null) target = zone.transform;
        }
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (arrowImage != null && arrowImage.sprite == null)
            arrowImage.sprite = BuildArrowSprite();

        if (guideRoot != null) guideRoot.SetActive(false);
        if (bannerRoot != null) bannerRoot.SetActive(false);
    }

    void OnEnable()
    {
        Enemy.OnBossDefeated += HandleBossDefeated;
        WinZone.OnPlayerWon += HandlePlayerWon;
    }

    void OnDisable()
    {
        Enemy.OnBossDefeated -= HandleBossDefeated;
        WinZone.OnPlayerWon -= HandlePlayerWon;
    }

    void HandleBossDefeated()
    {
        if (guideRoot != null) guideRoot.SetActive(true);

        if (bannerRoot != null)
        {
            if (bannerText != null) bannerText.text = bannerMessage;
            bannerRoot.SetActive(true);
            bannerTimer = bannerDuration;
        }
    }

    void HandlePlayerWon()
    {
        if (guideRoot != null) guideRoot.SetActive(false);
        if (bannerRoot != null) bannerRoot.SetActive(false);
    }

    void Update()
    {
        if (bannerTimer > 0f)
        {
            bannerTimer -= Time.unscaledDeltaTime;
            if (bannerTimer <= 0f && bannerRoot != null)
                bannerRoot.SetActive(false);
        }

        if (guideRoot == null || !guideRoot.activeSelf) return;
        if (target == null || player == null) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // --- distance (horizontal only, so height differences don't inflate it) ---
        Vector3 flat = target.position - player.position;
        flat.y = 0f;
        float dist = flat.magnitude;

        if (distanceText != null)
            distanceText.text = dist <= arrivedDistance
                ? "Goal  arrived"
                : string.Format(distanceFormat, Mathf.RoundToInt(dist));

        // --- arrow rotation toward the target ---
        // Bearing is taken in world space rather than screen space: WorldToScreenPoint
        // mirrors points behind the camera, and the usual "flip around centre" fix
        // distorts the angle badly (measured 112 deg where 150 deg was correct).
        if (arrowRect != null && flat.sqrMagnitude > 0.0001f)
        {
            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            if (camForward.sqrMagnitude > 0.0001f)
            {
                // positive bearing = target is to the player's right
                float bearing = Vector3.SignedAngle(camForward.normalized, flat.normalized, Vector3.up);
                // sprite points up at 0 deg; Unity's z rotation is counter-clockwise
                arrowRect.localRotation = Quaternion.Euler(0f, 0f, -bearing);
            }
        }
    }

    Sprite BuildArrowSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        var clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < size; y++)
        {
            float t = (float)y / (size - 1);            // 0 at base, 1 at tip
            float halfWidth = (1f - t) * 0.5f * size;
            for (int x = 0; x < size; x++)
            {
                bool inside = Mathf.Abs(x - size * 0.5f) <= halfWidth;
                tex.SetPixel(x, y, inside ? Color.white : clear);
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }
}
