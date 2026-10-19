using UnityEngine;
using UnityEngine.UI;

// Dark theme extras that need no post-processing (off on iOS): embers drifting up around the camera
// and a darkened screen edge drawn as a UI image under the HUD.
public sealed class ThemeAmbience : MonoBehaviour
{
    static ThemeAmbience instance;
    ParticleSystem embers;

    public static void Set(bool on)
    {
        if (!Application.isPlaying) return; // edit-mode captures show the lighting only
        if (instance == null)
        {
            if (!on) return;
            instance = new GameObject("ThemeAmbience").AddComponent<ThemeAmbience>();
            instance.Build();
        }
        instance.gameObject.SetActive(on);
    }

    void Build()
    {
        embers = new GameObject("Embers").AddComponent<ParticleSystem>();
        embers.transform.SetParent(transform, false);
        embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = embers.main;
        main.duration = 5f; main.loop = true; main.maxParticles = 260;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // the emitter follows the camera, sparks stay put
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(.06f, .16f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .5f, .18f), new Color(1f, .78f, .38f));
        var emission = embers.emission; emission.rateOverTime = 40f;
        var shape = embers.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(28f, 7f, 28f);
        var velocity = embers.velocityOverLifetime; velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-.25f, .25f);
        velocity.y = new ParticleSystem.MinMaxCurve(.25f, .7f);
        velocity.z = new ParticleSystem.MinMaxCurve(-.25f, .25f);
        var fade = embers.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .2f), new GradientAlphaKey(.8f, .7f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        embers.GetComponent<ParticleSystemRenderer>().sharedMaterial = Resources.Load<Material>("FX/M_Ember");
        embers.Play();

        var canvas = new GameObject("EdgeShade", typeof(Canvas)).GetComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5; // under every HUD canvas
        var shade = new GameObject("Shade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        shade.transform.SetParent(canvas.transform, false);
        var rect = shade.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        shade.sprite = EdgeSprite(); shade.color = new Color(.05f, .02f, .09f, .8f); shade.raycastTarget = false;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam != null) embers.transform.position = cam.transform.position + Vector3.up * 1.5f;
    }

    static Sprite EdgeSprite()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2((x + .5f) / size - .5f, (y + .5f) / size - .5f).magnitude * 2f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1.25f, d)));
            }
        texture.SetPixels32(pixels); texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
    }
}
