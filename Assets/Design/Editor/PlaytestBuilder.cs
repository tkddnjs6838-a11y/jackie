using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Throwaway play-test scenes: a copy of SampleScene with its terrain swapped for one design map.
// SampleScene itself is only copied, never opened for editing. The real integration (colliders on
// props, spawn points, stage flow) stays with the game side; this is just so the maps can be played.
public static class PlaytestBuilder
{
    const string GameScene = "Assets/Scenes/SampleScene.unity";
    const string OutDir = "Assets/Design/Scenes/Playtest";
    static readonly string[] Maps = { "Map01_Meadow", "Map02_Canyon", "Map03_WaxLake", "Map04_CrystalPlateau", "Map05_Crater" };

    [MenuItem("Tools/Design/Build Playtest Scenes")]
    public static void Build()
    {
        if (!MapBuilder.LeaveDesignScenes()) return;
        Directory.CreateDirectory(OutDir);
        AssetDatabase.Refresh();
        foreach (var map in Maps) BuildOne(map);
        Debug.Log("[PlaytestBuilder] built " + Maps.Length + " play-test scenes in " + OutDir);
        OpenMeadow();
    }

    [MenuItem("Tools/Design/Open Playtest Meadow")]
    public static void OpenMeadow()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene($"{OutDir}/PT_{Maps[0]}.unity", OpenSceneMode.Single);
    }

    static void BuildOne(string map)
    {
        string path = $"{OutDir}/PT_{map}.unity";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CopyAsset(GameScene, path);

        var mapScene = EditorSceneManager.OpenScene($"Assets/Design/Scenes/{map}.unity", OpenSceneMode.Additive);
        var test = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        // RenderSettings live on the active scene: read them from the map, write them into the copy
        SceneManager.SetActiveScene(mapScene);
        var sky = RenderSettings.skybox;
        bool fog = RenderSettings.fog;
        var fogMode = RenderSettings.fogMode;
        var fogColor = RenderSettings.fogColor;
        float fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;
        var ambientMode = RenderSettings.ambientMode;
        Color ambientSky = RenderSettings.ambientSkyColor, ambientEquator = RenderSettings.ambientEquatorColor, ambientGround = RenderSettings.ambientGroundColor;
        SceneManager.SetActiveScene(test);
        RenderSettings.skybox = sky;
        RenderSettings.fog = fog;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;

        var mapRoot = mapScene.GetRootGameObjects()[0];
        Light mapSun = mapRoot.transform.Find("Sun").GetComponent<Light>();
        Vector3 start = mapRoot.transform.Find("Markers/Marker_Start").position;
        Vector3 boss = mapRoot.transform.Find("Markers/Marker_Boss").position;

        foreach (var go in test.GetRootGameObjects())
        {
            switch (go.name)
            {
                case "MapTerrain":
                case "River":
                    Object.DestroyImmediate(go);
                    break;
                case "MapProps":
                    go.SetActive(false); // its runtime props belong to the old meadow look
                    break;
                case "Directional Light":
                    go.GetComponent<Light>().color = mapSun.color;
                    break;
                case "Player":
                    var face = boss - start;
                    face.y = 0f;
                    go.transform.SetPositionAndRotation(start + Vector3.up * 1.0f, Quaternion.LookRotation(face));
                    break;
            }
        }
        mapSun.gameObject.SetActive(false); // the game scene already has its own sun
        SceneManager.MoveGameObjectToScene(mapRoot, test);

        EditorSceneManager.SaveScene(test);
        EditorSceneManager.CloseScene(mapScene, true);
        EditorSceneManager.CloseScene(test, true);
    }
}
