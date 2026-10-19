using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the design-side start screen: generated sprites, P_UI_StartScreen.prefab and a
// standalone preview scene, then renders the preview at several resolutions into output/design.
// It never opens or saves SampleScene; the preview scene is created additively and closed again.
[InitializeOnLoad]
public static class StartScreenBuilder
{
    const string Root = "Assets/Design";
    const string TexDir = Root + "/UI/Textures";
    const string PrefabPath = Root + "/UI/P_UI_StartScreen.prefab";
    const string ScenePath = Root + "/Scenes/DesignStartScreenPreview.unity";
    const string FontPath = "Assets/Fonts/NotoSansKR-Bold.otf";
    const string CaptureDir = "output/design";
    const string Trigger = "Temp/build_start_screen.trigger";
    const int UiLayer = 5;

    static readonly Color Cream = new Color(1f, 0.96f, 0.86f);
    static readonly Color Amber = new Color(1f, 0.72f, 0.25f);
    static readonly Color Ink = new Color(0.07f, 0.05f, 0.1f);
    static readonly Color HeroBlack = new Color(0.055f, 0.055f, 0.055f);

    static Font font;
    static Sprite circle, softCircle, rounded, gradient;
    static readonly StringBuilder log = new StringBuilder();

    static StartScreenBuilder()
    {
        if (!File.Exists(Trigger)) return;
        File.Delete(Trigger);
        EditorApplication.delayCall += Build;
    }

    [MenuItem("Tools/Design/Build Start Screen")]
    public static void Build()
    {
        log.Clear();
        Directory.CreateDirectory(TexDir);
        Directory.CreateDirectory(Root + "/Scenes");
        Directory.CreateDirectory(CaptureDir);

        font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        circle = MakeSprite("T_UI_Circle", 128, 128, Vector4.zero, (x, y) => CircleAlpha(x, y, 128, 1.5f));
        softCircle = MakeSprite("T_UI_SoftCircle", 128, 128, Vector4.zero, (x, y) => SoftAlpha(x, y, 128));
        rounded = MakeSprite("T_UI_Rounded", 64, 64, new Vector4(24, 24, 24, 24), (x, y) => RoundedAlpha(x, y, 64, 22f));
        gradient = MakeGradient("T_UI_StartBg");

        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

        var cam = new GameObject("PreviewCamera").AddComponent<Camera>();
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 1 << UiLayer;
        cam.orthographic = true;

        var canvasGo = new GameObject("PreviewCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = UiLayer;
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 5f;
        // same scaler as UICanvas in SampleScene
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 1f;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var screen = BuildScreen(canvasGo.transform);
        PrefabUtility.SaveAsPrefabAssetAndConnect(screen, PrefabPath, InteractionMode.AutomatedAction);
        EditorSceneManager.SaveScene(scene, ScenePath);

        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(960, 600), new Vector2Int(1024, 768), new Vector2Int(1600, 720) })
            Capture(cam, canvas, size.x, size.y);

        CheckGlyphs("WAKKUBALL SMASH왁꾸볼뿌수기게임시작이동회피기본공격강마무리콤보보유증확인좌우클릭→");

        EditorSceneManager.CloseScene(scene, true);
        if (previous.IsValid()) SceneManager.SetActiveScene(previous);

        log.AppendLine("prefab " + PrefabPath);
        log.AppendLine("scene " + ScenePath);
        File.WriteAllText(CaptureDir + "/build_log.txt", log.ToString());
        Debug.Log("[StartScreenBuilder]\n" + log);
    }

    static GameObject BuildScreen(Transform parent)
    {
        // Root: GameFlowManager.startPanel. Full-screen so it blocks clicks to the game below.
        var root = Rect("StartScreen", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var bg = Img("Background", root, gradient, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.raycastTarget = true;

        // wax balls drifting near the edges, kept above the control guide so 4:3 screens stay clear
        var floatLayer = Rect("WaxBalls", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var floaters = new[]
        {
            WaxBall(floatLayer, new Vector2(0f, 1f), new Vector2(120f, -130f), 150f, new Color(1f, 0.55f, 0.2f)),
            WaxBall(floatLayer, new Vector2(0f, 0f), new Vector2(80f, 255f), 100f, new Color(0.45f, 0.85f, 0.35f)),
            WaxBall(floatLayer, new Vector2(1f, 1f), new Vector2(-110f, -150f), 120f, new Color(0.62f, 0.42f, 0.95f)),
            WaxBall(floatLayer, new Vector2(1f, 0f), new Vector2(-110f, 265f), 120f, new Color(1f, 0.45f, 0.6f)),
            WaxBall(floatLayer, new Vector2(0f, 0.5f), new Vector2(60f, 40f), 64f, new Color(1f, 0.85f, 0.3f)),
            WaxBall(floatLayer, new Vector2(1f, 0.5f), new Vector2(-55f, 70f), 70f, new Color(0.3f, 0.8f, 0.85f)),
        };

        // hero: black round body with a small topknot, same as the in-game character
        var heroGroup = Rect("Hero", root, Half, Half, new Vector2(0f, 205f), new Vector2(200f, 200f));
        var shadow = Img("Shadow", heroGroup, softCircle, new Color(0f, 0f, 0f, 0.55f), Half, Half, new Vector2(0f, -80f), new Vector2(150f, 26f));
        var body = Rect("Body", heroGroup, Half, Half, new Vector2(0f, -80f), new Vector2(150f, 180f));
        body.pivot = new Vector2(0.5f, 0f);
        Img("Rim", body, circle, new Color(0.3f, 0.28f, 0.4f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(150f, 150f));
        Img("Ball", body, circle, HeroBlack, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(142f, 142f));
        Img("Shine", body, softCircle, new Color(1f, 1f, 1f, 0.16f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-30f, 106f), new Vector2(46f, 40f));
        Img("TopknotBand", body, rounded, new Color(0.2f, 0.2f, 0.22f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 147f), new Vector2(20f, 9f));
        var tuft = Img("Topknot", body, circle, HeroBlack, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(3f, 163f), new Vector2(26f, 32f));
        tuft.rectTransform.localEulerAngles = new Vector3(0f, 0f, -14f);

        var title = Txt("Title", root, "WAKKUBALL SMASH", 78, Cream, TextAnchor.MiddleCenter, Half, new Vector2(0f, 48f), new Vector2(1000f, 100f));
        var outline = title.gameObject.AddComponent<Outline>();
        outline.effectColor = Ink;
        outline.effectDistance = new Vector2(4f, -4f);
        Txt("TitleKo", root, "왁꾸볼 뿌수기", 38, Amber, TextAnchor.MiddleCenter, Half, new Vector2(0f, -20f), new Vector2(600f, 54f))
            .gameObject.AddComponent<Outline>().effectColor = Ink;

        // StartButton: GameFlowManager.startButton. No onClick here; the game wires it.
        Img("StartButtonShadow", root, rounded, new Color(0f, 0f, 0f, 0.45f), Half, Half, new Vector2(0f, -116f), new Vector2(320f, 84f)).type = Image.Type.Sliced;
        var buttonImage = Img("StartButton", root, rounded, Amber, Half, Half, new Vector2(0f, -108f), new Vector2(320f, 84f));
        buttonImage.type = Image.Type.Sliced;
        buttonImage.raycastTarget = true;
        var button = buttonImage.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.9f, 0.7f);
        colors.pressedColor = new Color(0.8f, 0.62f, 0.4f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        Txt("Label", buttonImage.rectTransform, "게임 시작", 38, Ink, TextAnchor.MiddleCenter, Half, Vector2.zero, new Vector2(300f, 80f));

        // Named "ControlGuide" on purpose: GameFlowManager.EnsureControlGuide skips its own
        // generated guide when the start panel already has a direct child with this name.
        var guide = Img("ControlGuide", root, rounded, new Color(0.03f, 0.035f, 0.06f, 0.85f), Half, Half, new Vector2(0f, -258f), new Vector2(900f, 150f));
        guide.type = Image.Type.Sliced;
        var rows = new[]
        {
            new[] { ("WASD", "이동", 92f), ("좌클릭", "기본 공격", 92f), ("1–5", "무기 교체", 92f) },
            new[] { ("Space", "회피", 92f), ("R", "재장전", 132f), ("Tab", "보유 증강 확인", 70f) },
        };
        for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < 3; c++)
            {
                var (key, label, chipWidth) = rows[r][c];
                Vector2 cell = new Vector2(-290f + c * 290f, 32f - r * 64f);
                float left = cell.x - 135f;
                var chip = Img("Key_" + label, guide.rectTransform, rounded, new Color(0.93f, 0.9f, 0.84f), Half, Half,
                    new Vector2(left + chipWidth * 0.5f, cell.y), new Vector2(chipWidth, 42f));
                chip.type = Image.Type.Sliced;
                Txt("Key", chip.rectTransform, key, 21, Ink, TextAnchor.MiddleCenter, Half, Vector2.zero, new Vector2(chipWidth, 42f));
                float labelWidth = 270f - chipWidth - 12f;
                Txt("Label_" + label, guide.rectTransform, label, 22, Color.white, TextAnchor.MiddleLeft, Half,
                    new Vector2(left + chipWidth + 12f + labelWidth * 0.5f, cell.y), new Vector2(labelWidth, 42f));
            }

        var motion = root.gameObject.AddComponent<StartScreenMotion>();
        var so = new SerializedObject(motion);
        so.FindProperty("hero").objectReferenceValue = body;
        so.FindProperty("heroShadow").objectReferenceValue = shadow.rectTransform;
        so.FindProperty("startButton").objectReferenceValue = buttonImage.rectTransform;
        var list = so.FindProperty("floaters");
        list.arraySize = floaters.Length;
        for (int i = 0; i < floaters.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = floaters[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        return root.gameObject;
    }

    static RectTransform WaxBall(RectTransform parent, Vector2 anchor, Vector2 pos, float size, Color color)
    {
        color.a = 0.5f;
        var ball = Img("WaxBall", parent, circle, color, anchor, anchor, pos, new Vector2(size, size));
        Img("Shine", ball.rectTransform, softCircle, new Color(1f, 1f, 1f, 0.22f), Half, Half, new Vector2(-size * 0.2f, size * 0.2f), new Vector2(size * 0.35f, size * 0.3f));
        return ball.rectTransform;
    }

    // ---------- capture ----------

    static void Capture(Camera cam, Canvas canvas, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var scaler = canvas.GetComponent<CanvasScaler>();
        cam.targetTexture = rt;
        scaler.enabled = false;
        canvas.scaleFactor = h / 720f; // matchWidthOrHeight = 1
        for (int i = 0; i < 2; i++) { Canvas.ForceUpdateCanvases(); cam.Render(); }

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        string path = $"{CaptureDir}/start_{w}x{h}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        log.AppendLine($"capture {path} canvas {((RectTransform)canvas.transform).rect.size}");

        cam.targetTexture = null;
        scaler.enabled = true;
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    static void CheckGlyphs(string text)
    {
        font.RequestCharactersInTexture(text, 38, FontStyle.Normal);
        var missing = new StringBuilder();
        foreach (char ch in text)
            if (!font.HasCharacter(ch)) missing.Append(ch);
        log.AppendLine("font " + font.name + " missing glyphs: " + (missing.Length == 0 ? "none" : missing.ToString()));
    }

    // ---------- ui helpers ----------

    static readonly Vector2 Half = new Vector2(0.5f, 0.5f);

    static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = UiLayer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Image Img(string name, Transform parent, Sprite sprite, Color color, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        var image = Rect(name, parent, min, max, pos, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Text Txt(string name, Transform parent, string text, int size, Color color, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
    {
        var label = Rect(name, parent, anchor, anchor, pos, box).gameObject.AddComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow; // Noto's line height exceeds the box; Truncate drops the whole line
        label.raycastTarget = false;
        return label;
    }

    // ---------- generated sprites ----------

    static float CircleAlpha(int x, int y, int n, float edge)
    {
        float r = n * 0.5f;
        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
        return Mathf.Clamp01((r - d) / edge);
    }

    static float SoftAlpha(int x, int y, int n)
    {
        float r = n * 0.5f;
        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
        return Mathf.SmoothStep(1f, 0f, d);
    }

    static float RoundedAlpha(int x, int y, int n, float radius)
    {
        float px = Mathf.Max(Mathf.Abs(x + 0.5f - n * 0.5f) - (n * 0.5f - radius), 0f);
        float py = Mathf.Max(Mathf.Abs(y + 0.5f - n * 0.5f) - (n * 0.5f - radius), 0f);
        return Mathf.Clamp01(radius - Mathf.Sqrt(px * px + py * py));
    }

    static Sprite MakeSprite(string name, int w, int h, Vector4 border, System.Func<int, int, float> alpha)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(x, y)));
        return SaveSprite(name, tex, border);
    }

    static Sprite MakeGradient(string name)
    {
        // deep violet top to near-black bottom, with a warm glow band behind the title
        var top = new Color(0.16f, 0.11f, 0.26f);
        var mid = new Color(0.23f, 0.12f, 0.2f);
        var bottom = new Color(0.04f, 0.04f, 0.08f);
        var tex = new Texture2D(4, 256, TextureFormat.RGBA32, false);
        for (int y = 0; y < 256; y++)
        {
            float t = y / 255f;
            var c = t > 0.55f ? Color.Lerp(mid, top, (t - 0.55f) / 0.45f) : Color.Lerp(bottom, mid, t / 0.55f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
        }
        return SaveSprite(name, tex, Vector4.zero);
    }

    static Sprite SaveSprite(string name, Texture2D tex, Vector4 border)
    {
        string path = $"{TexDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
