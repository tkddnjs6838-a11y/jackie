using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MapNavigationBake
{
    static MapNavigationBake(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="output/gun-effects/bake-navigation.txt";
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(request))return;
        File.Delete(request);Bake();
    }
    [MenuItem("Tools/Design/Bake Walkable Map Paths")]
    public static void Bake()
    {
        var original=SceneManager.GetActiveScene();
        Directory.CreateDirectory("Assets/Design/Navigation");AssetDatabase.Refresh();
        foreach(var name in MapSandbox.Maps)
        {
            var scene=EditorSceneManager.OpenScene("Assets/Design/Scenes/"+name+".unity",OpenSceneMode.Additive);
            try
            {
                var roots=scene.GetRootGameObjects();var root=roots[0];
                var surface=root.GetComponent<NavMeshSurface>();if(surface==null)surface=root.AddComponent<NavMeshSurface>();
                surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask=~(1<<2);surface.overrideVoxelSize=true;surface.voxelSize=.12f;
                // Navigation-only water floor. No runtime collider: the existing
                // player raft and buoyancy continue to own swimming height.
                var waterFloors=new System.Collections.Generic.List<GameObject>();
                try
                {
                    foreach(var water in root.GetComponentsInChildren<WaterFloat>())
                    {
                        var bounds=water.VolumeBounds;
                        var floor=new GameObject("BakeOnlyWaterFloor",typeof(BoxCollider));floor.transform.SetParent(root.transform,true);
                        floor.transform.position=new Vector3(bounds.center.x,water.NavigationHeight-.05f,bounds.center.z);
                        floor.GetComponent<BoxCollider>().size=new Vector3(bounds.size.x,.1f,bounds.size.z);
                        waterFloors.Add(floor);
                    }
                    Physics.SyncTransforms();surface.BuildNavMesh();
                }
                finally{foreach(var floor in waterFloors)Object.DestroyImmediate(floor);}
                string path="Assets/Design/Navigation/"+name+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
                if(existing==null)AssetDatabase.CreateAsset(surface.navMeshData,path);
                else {var built=surface.navMeshData;surface.RemoveData();EditorUtility.CopySerialized(built,existing);Object.DestroyImmediate(built);surface.navMeshData=existing;surface.AddData();EditorUtility.SetDirty(existing);}
                if(root.GetComponent<MapNavigation>()==null)root.AddComponent<MapNavigation>();
                EditorSceneManager.SaveScene(scene);
                Debug.Log("BAKED WALKABLE MAP: "+name);
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
        SceneManager.SetActiveScene(original);AssetDatabase.SaveAssets();
        File.WriteAllText("output/gun-effects/navigation-bake.txt","COMPLETE: five maps baked\n");
    }
}
