using UnityEngine;

// Swap the mesh on the existing renderer, retaining animation, materials and damage effects.
[DefaultExecutionOrder(40)]
public class EnemyMeshLod : MonoBehaviour
{
    [System.Serializable]
    public class Binding
    {
        public Renderer renderer;
        public MeshFilter filter;
        public Mesh[] levels;
        public void Apply(int level)
        {
            if (levels == null || level >= levels.Length || levels[level] == null) return;
            if (renderer is SkinnedMeshRenderer skin)
            {
                // Clear native skin buffers before rebinding a different vertex layout.
                bool visible = skin.enabled;
                skin.enabled = false;
                skin.sharedMesh = null;
                skin.sharedMesh = levels[level];
                skin.enabled = visible;
            }
            else if (filter != null) filter.sharedMesh = levels[level];
        }
    }
    public Binding[] bindings;
    public int CurrentLevel { get; private set; } = -1;
    Camera view;
    float nextCheck;
    void OnEnable() { nextCheck = 0f; }
    void LateUpdate()
    {
        if (Time.deltaTime <= 0f || Time.time < nextCheck) return;
        if (view == null || !view.isActiveAndEnabled) view = Camera.main;
        if (view == null) return;
        UpdateForCamera(view);
        nextCheck = Time.time + 0.1f;
    }
    public void UpdateForCamera(Camera camera)
    {
        if (bindings == null || bindings.Length == 0 || bindings[0].renderer == null || camera == null) return;
        var bounds = bindings[0].renderer.bounds;
        float depth = Vector3.Dot(bounds.center - camera.transform.position, camera.transform.forward);
        float height = camera.orthographic ? bounds.size.y / Mathf.Max(2f * camera.orthographicSize, .001f)
            : bounds.size.y / Mathf.Max(2f * depth * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f), .001f);
        if (depth < 0f) height = 0f;
        height *= QualitySettings.lodBias;
        // Hysteresis prevents flickering around a threshold.
        int level = CurrentLevel;
        if (level < 0) level = height >= .28f ? 0 : height >= .09f ? 1 : 2;
        else if (height > (level == 0 ? .25f : .31f)) level = 0;
        else if (height < (level == 2 ? .105f : .075f)) level = 2;
        else level = 1;
        SetLevel(level);
    }
    public void SetLevel(int level)
    {
        level = Mathf.Clamp(level, 0, 2);
        if (level == CurrentLevel || bindings == null) return;
        foreach (var binding in bindings) binding.Apply(level);
        CurrentLevel = level;
    }
}
