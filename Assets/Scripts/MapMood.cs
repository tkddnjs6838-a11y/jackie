using UnityEngine;
using UnityEngine.SceneManagement;

// Both looks of one design map, written by MapBuilder. The scene itself bakes the bright one.
// RenderSettings belong to the active scene, so MapSandbox calls ApplyIn right after making the map active.
public sealed class MapMood : MonoBehaviour
{
    [System.Serializable]
    public struct Preset
    {
        public Color sunColor; public float sunIntensity; public Vector3 sunEuler;
        public Color ambientSky, ambientEquator, ambientGround;
        public Color fogColor; public float fogStart, fogEnd;
        public Color skyZenith, skyHorizon, skyGround, cloudColor, cloudShade;
        public Color waterBase, waterDeep, waterSky;
    }

    public Preset bright, dark;
    public Light sun;
    public Renderer water;
    Material sky, waterMaterial; // per-play copies, so the map's material assets are never edited

    public static void ApplyIn(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var mood = root.GetComponentInChildren<MapMood>();
            if (mood != null) { mood.Apply(GameTheme.Current); return; }
        }
    }

    void OnEnable() => GameTheme.Changed += OnThemeChanged;
    void OnDisable() => GameTheme.Changed -= OnThemeChanged;
    void OnThemeChanged(GameTheme.Mode mode) { if (gameObject.scene == SceneManager.GetActiveScene()) Apply(mode); }

    public void Apply(GameTheme.Mode mode)
    {
        // The opening meadow establishes the bright toy-action look.
        if (gameObject.scene.name == "Map01_Meadow") mode = GameTheme.Mode.Bright;
        var p = mode == GameTheme.Mode.Dark ? dark : bright;
        if(gameObject.scene.name=="Map01_Meadow")
        {
            // Directional highlights retain volume; reduce the washed-out green ambient fill.
            p.sunColor=new Color(1f,.94f,.83f);
            p.ambientSky=Color.Lerp(p.ambientSky,new Color(.48f,.60f,.72f),.3f);
            p.ambientEquator=Color.Lerp(p.ambientEquator,new Color(.34f,.39f,.43f),.35f);
            p.fogStart=Mathf.Max(38,p.fogStart);
        }
        if (sun != null)
        {
            sun.color = p.sunColor; sun.intensity = p.sunIntensity;
            sun.transform.rotation = Quaternion.Euler(p.sunEuler);
        }
        RenderSettings.ambientSkyColor = p.ambientSky;
        RenderSettings.ambientEquatorColor = p.ambientEquator;
        RenderSettings.ambientGroundColor = p.ambientGround;
        RenderSettings.fogColor = p.fogColor;
        RenderSettings.fogStartDistance = p.fogStart;
        RenderSettings.fogEndDistance = p.fogEnd;
        if (sky == null && RenderSettings.skybox != null) sky = new Material(RenderSettings.skybox);
        if (sky != null)
        {
            RenderSettings.skybox = sky;
            sky.SetColor("_ZenithColor", p.skyZenith); sky.SetColor("_HorizonColor", p.skyHorizon); sky.SetColor("_GroundColor", p.skyGround);
            sky.SetColor("_CloudColor", p.cloudColor); sky.SetColor("_CloudShade", p.cloudShade);
        }
        // lava keeps its own glowing material; only the water shader takes the mood colors
        if (water != null && water.sharedMaterial != null && water.sharedMaterial.HasProperty("_SkyColor"))
        {
            if (waterMaterial == null) waterMaterial = new Material(water.sharedMaterial);
            water.sharedMaterial = waterMaterial;
            waterMaterial.SetColor("_BaseColor", p.waterBase); waterMaterial.SetColor("_DeepColor", p.waterDeep); waterMaterial.SetColor("_SkyColor", p.waterSky);
        }
        ThemeAmbience.Set(mode == GameTheme.Mode.Dark);
    }

    void OnDestroy()
    {
        foreach (var m in new[] { sky, waterMaterial })
            if (m != null) { if (Application.isPlaying) Destroy(m); else DestroyImmediate(m); }
    }
}
