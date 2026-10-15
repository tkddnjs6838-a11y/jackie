using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// The current iOS URP post-processing RenderGraph throws on device before presenting
// a frame. Omit that pass in iOS builds; scene lighting, materials and UI are retained.
public sealed class MobileRenderingBuild : IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || report.summary.platform != BuildTarget.iOS) return;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var camera in root.GetComponentsInChildren<UniversalAdditionalCameraData>(true))
        {
            camera.renderPostProcessing = false;
            camera.antialiasing = AntialiasingMode.None;
        }
    }
}
