using System.Collections.Generic;
using UnityEngine;

// Presentation only: follows the existing buff timer and never changes combat stats.
public sealed class TimedEffectPlayerVisual : MonoBehaviour
{
    readonly List<GameObject> outlines = new List<GameObject>();
    Material glowMaterial;
    PowerUpEffects effects;
    TextMesh label, labelShadow;
    Transform head;
    Camera view;
    int version = -1;
    float elapsed;
    bool showing;
    float augmentRemaining;
    Color augmentColor;
    string augmentName;
    public void ShowAugment(CharacterGrowth.Kind kind)
    {
        augmentRemaining = 3f;
        augmentColor = CharacterGrowth.ColorFor(kind);
        augmentName = CharacterGrowth.TitleFor(kind);
        elapsed = 0;
    }


    void Start()
    {
        effects = GetComponent<PowerUpEffects>();
        var animator = GetComponentInChildren<Animator>();
        head = ToyCharacterRig.Bone(animator, HumanBodyBones.Head);
        var g = new GameObject("ActiveEffectOverhead");
        g.transform.SetParent(transform, false);
        label = g.AddComponent<TextMesh>();
        label.font = Resources.Load<Font>("UI/AugmentKorean");
        label.fontSize = 64;
        label.characterSize = .032f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        if (label.font != null) g.GetComponent<Renderer>().sharedMaterial = label.font.material;
        var shadow = new GameObject("TextShadow");
        shadow.transform.SetParent(g.transform, false);
        shadow.transform.localPosition = new Vector3(.025f, -.025f, .008f);
        labelShadow = shadow.AddComponent<TextMesh>();
        labelShadow.font = label.font;
        labelShadow.fontSize = label.fontSize;
        labelShadow.characterSize = label.characterSize;
        labelShadow.anchor = label.anchor;
        labelShadow.alignment = label.alignment;
        if (label.font != null) shadow.GetComponent<Renderer>().sharedMaterial = label.font.material;
        shadow.GetComponent<Renderer>().sortingOrder = 1;
        g.GetComponent<Renderer>().sortingOrder = 2;
        g.SetActive(false);
    }

    void Begin()
    {
        var shader = Resources.Load<Shader>("FX/PlayerBuffRim");
        if (shader == null) return;
        glowMaterial = new Material(shader);
        // A separate skin pass follows the bones without changing body or eye materials.
        foreach (var source in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (source.sharedMesh == null || !source.enabled) continue;
            var g = new GameObject("BuffRim");
            g.transform.SetParent(source.transform, false);
            var rim = g.AddComponent<SkinnedMeshRenderer>();
            rim.sharedMesh = source.sharedMesh;
            rim.bones = source.bones;
            rim.rootBone = source.rootBone;
            rim.localBounds = source.localBounds;
            rim.updateWhenOffscreen = source.updateWhenOffscreen;
            rim.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rim.receiveShadows = false;
            var materials = new Material[source.sharedMesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = glowMaterial;
            rim.sharedMaterials = materials;
            outlines.Add(g);
        }
        showing = true;
    }

    void LateUpdate()
    {
        if (label == null) return;
        bool timedActive = effects != null && effects.Active;
        if ((!timedActive && augmentRemaining <= 0) || (RunStats.Instance != null && !RunStats.Instance.Running))
        {
            Clear();
            return;
        }
        if (!showing) Begin();
        if (augmentRemaining <= 0 && timedActive && version != effects.ActivationVersion)
        {
            version = effects.ActivationVersion;
            elapsed = 0;
            label.text = PowerUpEffects.DisplayName(effects.ActiveKind);
        }
        elapsed += Time.deltaTime;
        bool augmentActive = augmentRemaining > 0;
        Color tint = augmentActive ? augmentColor : PowerUpEffects.ColorOf(effects.ActiveKind);
        label.text = augmentActive ? augmentName : PowerUpEffects.DisplayName(effects.ActiveKind);
        if (augmentActive) { augmentRemaining = Mathf.Max(0, augmentRemaining - Time.deltaTime); version = -1; }
        // A readable acquisition flash, followed by slower paired glints.
        float beat = Mathf.Repeat(Mathf.Max(0, elapsed - .4f), 1.25f);
        float first = Mathf.SmoothStep(0, 1, Mathf.Clamp01(1 - Mathf.Abs(beat - .2f) / .25f));
        float second = Mathf.SmoothStep(0, 1, Mathf.Clamp01(1 - Mathf.Abs(beat - .68f) / .25f));
        float pulse = elapsed < .4f ? 1f : Mathf.Max(first, second);
        if (glowMaterial != null)
        {
            glowMaterial.SetColor("_GlowColor", tint);
            glowMaterial.SetFloat("_Strength", 4.5f * pulse);
        }
        if (view == null) view = Camera.main;
        float phase = Mathf.Repeat(elapsed, 1.6f) / 1.6f;
        float alpha = 1 - Mathf.SmoothStep(.72f, .98f, phase);
        label.color = new Color(1, 1, 1, alpha);
        labelShadow.text = label.text;
        labelShadow.color = new Color(.015f, .025f, .035f, alpha);
        label.transform.position = (head != null ? head.position : transform.position + Vector3.up * 1.5f)
            + Vector3.up * (.85f + phase * .3f);
        if (view != null) label.transform.rotation = view.transform.rotation;
        label.gameObject.SetActive(true);
    }

    void Clear()
    {
        if (label != null) label.gameObject.SetActive(false);
        if (!showing) return;
        foreach (var outline in outlines) if (outline != null) Destroy(outline);
        outlines.Clear();
        if (glowMaterial != null) Destroy(glowMaterial);
        glowMaterial = null;
        showing = false;
        version = -1;
    }
    void OnDisable() { augmentRemaining = 0; Clear(); }
    void OnDestroy() { Clear(); if (label != null) Destroy(label.gameObject); }
}
