using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Applies one clean visual language to every in-game status bar.</summary>
public static class HudBarPolish
{
    static Sprite roundedSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Apply()
    {
        roundedSprite = CreateRoundedSprite();

        StyleImage("HealthBarRoot", new Color32(10, 16, 24, 225));
        StyleImage("HealthBarInner", new Color32(25, 35, 46, 245));
        StyleImage("HealthBarFill", new Color32(53, 211, 153, 255));

        StyleImage("DashBar", new Color32(10, 16, 24, 210));
        StyleImage("DashFill", new Color32(56, 189, 248, 255));

        StyleImage("ComboTimerBg", new Color32(10, 16, 24, 210));
        StyleImage("ComboTimerFill", new Color32(250, 180, 55, 255));

        StyleImage("BossHealthRoot", new Color32(10, 16, 24, 235));
        StyleImage("Fill", new Color32(239, 68, 91, 255));

        StyleText("HealthBarText", 16);
        StyleText("DashLabel", 14);
        StyleText("ComboText", 15);
        StyleText("BossHealthText", 15);
        StyleText("BossNameText", 20);
    }

    static void StyleImage(string objectName, Color color)
    {
        foreach (Image image in Resources.FindObjectsOfTypeAll<Image>())
        {
            if (image.gameObject.name != objectName || !IsSceneObject(image.gameObject)) continue;
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;

            Outline outline = image.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            Shadow shadow = image.GetComponent<Shadow>();
            if (shadow != null && !(shadow is Outline))
            {
                shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
                shadow.effectDistance = new Vector2(0f, -2f);
            }
        }
    }

    static void StyleText(string objectName, int size)
    {
        foreach (Text text in Resources.FindObjectsOfTypeAll<Text>())
        {
            if (text.gameObject.name != objectName || !IsSceneObject(text.gameObject)) continue;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color32(235, 242, 248, 255);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, size - 4);
            text.resizeTextMaxSize = size;

            Outline outline = text.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
        }
    }

    static bool IsSceneObject(GameObject go)
    {
        Scene scene = go.scene;
        return scene.IsValid() && scene.isLoaded;
    }

    static Sprite CreateRoundedSprite()
    {
        const int size = 64;
        const float radius = 15f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "HUD Rounded Rectangle";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = Mathf.Clamp(x, radius, size - 1f - radius);
            float cy = Mathf.Clamp(y, radius, size - 1f - radius);
            float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
            byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(radius + 0.75f - distance));
            pixels[y * size + x] = new Color32(255, 255, 255, alpha);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
        sprite.name = "HUD Rounded Rectangle";
        return sprite;
    }
}
