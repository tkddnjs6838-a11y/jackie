using System.Collections.Generic;
using UnityEngine;

/// <summary>Shows arrows at the screen edge for living enemies outside the camera view.</summary>
public class OffscreenEnemyIndicator : MonoBehaviour
{
    public int maxArrows = 8;
    public float refreshInterval = 0.2f;
    public float screenMargin = 42f;
    public float arrowSize = 30f;
    public Color arrowColor = new Color(1f, 0.25f, 0.08f, 0.95f);

    private readonly List<Enemy> enemies = new List<Enemy>();
    private Camera viewCamera;
    private Texture2D arrowTexture;
    private float refreshTimer;

    void Awake()
    {
        viewCamera = Camera.main;
        arrowTexture = BuildArrowTexture(32);
    }

    void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = refreshInterval;

        enemies.Clear();
        var found = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        System.Array.Sort(found, (a, b) =>
            ((a.transform.position - transform.position).sqrMagnitude)
            .CompareTo((b.transform.position - transform.position).sqrMagnitude));

        foreach (var enemy in found)
        {
            if (enemy != null && !enemy.IsDying)
                enemies.Add(enemy);
        }
    }

    void OnGUI()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera == null || arrowTexture == null) return;

        int drawn = 0;
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDying) continue;

            Vector3 viewport = viewCamera.WorldToViewportPoint(enemy.transform.position + Vector3.up);
            bool behind = viewport.z <= 0f;
            bool outside = behind || viewport.x < 0.04f || viewport.x > 0.96f
                                  || viewport.y < 0.04f || viewport.y > 0.96f;
            if (!outside) continue;

            Vector2 screenPoint = new Vector2(viewport.x * Screen.width,
                                              (1f - viewport.y) * Screen.height);
            if (behind)
                screenPoint = center - (screenPoint - center);

            Vector2 direction = screenPoint - center;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.up;
            direction.Normalize();

            float maxX = Screen.width * 0.5f - screenMargin;
            float maxY = Screen.height * 0.5f - screenMargin;
            float scale = Mathf.Min(maxX / Mathf.Max(0.001f, Mathf.Abs(direction.x)),
                                    maxY / Mathf.Max(0.001f, Mathf.Abs(direction.y)));
            Vector2 position = center + direction * scale;

            Rect rect = new Rect(position.x - arrowSize * 0.5f,
                                 position.y - arrowSize * 0.5f,
                                 arrowSize, arrowSize);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUIUtility.RotateAroundPivot(angle, position);
            GUI.color = arrowColor;
            GUI.DrawTexture(rect, arrowTexture);
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;

            drawn++;
            if (drawn >= maxArrows) break;
        }
    }

    static Texture2D BuildArrowTexture(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "EnemyDirectionArrow";
        texture.filterMode = FilterMode.Bilinear;
        var clear = new Color32(0, 0, 0, 0);
        var white = new Color32(255, 255, 255, 255);
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            float normalizedY = y / (float)(size - 1);
            float halfWidth = normalizedY < 0.55f
                ? Mathf.Lerp(1f, size * 0.45f, normalizedY / 0.55f)
                : size * 0.13f;
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = Mathf.Abs(x - size * 0.5f) <= halfWidth ? white : clear;
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    void OnDestroy()
    {
        if (arrowTexture != null) Destroy(arrowTexture);
    }
}
