using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class EnemyPacingSetup
{
    static EnemyPacingSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="output/gun-effects/pacing-setup.txt";
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(request))return;
        File.Delete(request);
        foreach(var spawner in Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            Undo.RecordObject(spawner,"Apply opening enemy pacing");
            spawner.initialInterval=1.4f;spawner.minInterval=.5f;spawner.intervalDecayPerMinute=.2f;
            spawner.killsToSpawnBoss=40;spawner.bossEarliestSeconds=60;
            spawner.firstChargerAtSeconds=25;spawner.eliteFirstAt=45;spawner.eliteInterval=45;
            spawner.enemiesPerSpawn=1;spawner.maxAliveEnemies=35;
            if(spawner.variants!=null)foreach(var v in spawner.variants)
            {
                if(v==null || v.prefab==null)continue;
                if(v.prefab.GetComponent<EnemyCharger>()!=null)v.unlockAfterSeconds=25;
                else if(v.prefab.GetComponent<EnemySplitter>()!=null)v.unlockAfterSeconds=50;
                else if(v.prefab.GetComponent<EnemyWarden>()!=null)v.unlockAfterSeconds=80;
            }
            EditorUtility.SetDirty(spawner);EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        }
        File.WriteAllText("output/gun-effects/pacing-setup-result.txt","Applied 1.4s opening interval, 1 enemy, charger 25s, elite 45s, boss 60s AND 40 kills.");
    }
}
