using System.IO;
using UnityEditor;
using UnityEngine;

public static class WaxVisualWorkshop
{
    [MenuItem("Tools/Character/Review Dent In Play %#&e")]
    public static void ReviewDentInPlay()
    {
        if(!EditorApplication.isPlaying) return;
        var flow=Object.FindFirstObjectByType<GameFlowManager>();
        flow.StartCoroutine(CaptureDentInPlay(flow));
    }
    static System.Collections.IEnumerator CaptureDentInPlay(GameFlowManager flow)
    {
        var camera=Camera.main;
        var previousPosition=camera.transform.position;var previousRotation=camera.transform.rotation;
        float previousTime=Time.timeScale;
        var spawner=Object.FindFirstObjectByType<EnemySpawner>();
        var prefab=spawner.enemyPrefab;
        var obj=Object.Instantiate(prefab,Vector3.up*20f,Quaternion.identity);
        var enemy=obj.GetComponent<Enemy>();enemy.enabled=false;
        foreach(var behaviour in obj.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled=false;
        var animator=obj.GetComponentInChildren<Animator>();
        if(animator!=null) { animator.Rebind();animator.Update(0f);animator.enabled=false; }
        var skin=obj.GetComponentInChildren<SkinnedMeshRenderer>();
        var bounds=skin.bounds;
        var tint=new MaterialPropertyBlock();tint.SetColor("_BaseColor",new Color(0.25f,0.72f,0.32f));skin.SetPropertyBlock(tint);
        var directory=Path.GetFullPath("output/wax-dent-review");Directory.CreateDirectory(directory);
        var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var canvasStates=new bool[canvases.Length];
        for(int i=0;i<canvases.Length;i++) { canvasStates[i]=canvases[i].enabled;canvases[i].enabled=false; }
        Time.timeScale=0f;
        camera.transform.position=bounds.center+new Vector3(0.85f,0.4f,1.45f)*bounds.size.magnitude;
        camera.transform.LookAt(bounds.center);
        var dent=obj.GetComponent<WaxDent>();if(dent==null) dent=obj.AddComponent<WaxDent>();dent.enabled=false;
        var cracks=obj.GetComponent<WaxCrackVisual>();
        dent.Punch(bounds.center+new Vector3(0.45f,0.18f,1f)*bounds.size.magnitude,0.8f,false);
        cracks.SetDamage(0.8f);
        var baseline=(Vector3[])dent.SurfaceRenderer.GetComponent<MeshFilter>().sharedMesh.vertices.Clone();
        try
        {
            foreach(float elapsed in new[]{0f,0.045f,0.15f,0.365f})
            {
                dent.Sample(elapsed);cracks.SyncSurface();
                if(elapsed==0.045f)
                {
                    var vertices=dent.SurfaceRenderer.GetComponent<MeshFilter>().sharedMesh.vertices;
                    float max=0f;int moved=0;
                    for(int i=0;i<vertices.Length;i++) { float d=Vector3.Distance(vertices[i],baseline[i]);max=Mathf.Max(max,d);if(d>0.001f)moved++; }
                    if(max<0.02f || moved>=vertices.Length*0.8f) throw new System.Exception("Dent is absent or affects the whole body");
                    Debug.Log("DENT LOCALITY PASS max="+max+" moved="+moved+"/"+vertices.Length);
                }
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"play-dent-"+Mathf.RoundToInt(elapsed*1000)+".png"));
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }
        finally
        {
            camera.transform.position=previousPosition;camera.transform.rotation=previousRotation;
            for(int i=0;i<canvases.Length;i++) if(canvases[i]!=null) canvases[i].enabled=canvasStates[i];
            Time.timeScale=previousTime;Object.Destroy(obj);
        }
        Debug.Log("DENT PLAY REVIEW PASS");
    }
    [MenuItem("Tools/Character/Prepare Local Wax Dent %#&d")]
    public static void PrepareDent()
    {
        if(EditorApplication.isPlaying) return;
        RenderDent();
    }
    static void RenderDent()
    {
        var directory=Path.GetFullPath("output/wax-dent-review"); Directory.CreateDirectory(directory);
        foreach(float elapsed in new[]{0f,0.045f,0.15f,0.365f})
        {
            var preview=new PreviewRenderUtility();
            try
            {
                var obj=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab"));
                obj.transform.position=Vector3.zero; obj.transform.rotation=Quaternion.identity;
                preview.AddSingleGO(obj);
                foreach(var behaviour in obj.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled=false;
                var skin=obj.GetComponentInChildren<SkinnedMeshRenderer>();
                if(skin==null) throw new System.Exception("Dent review needs skinned enemy");
                var bounds=skin.bounds;
                var tint=new MaterialPropertyBlock(); tint.SetColor("baseColorFactor",new Color(0.25f,0.72f,0.32f));
                tint.SetColor("_BaseColor",new Color(0.25f,0.72f,0.32f)); skin.SetPropertyBlock(tint);
                var dent=obj.AddComponent<WaxDent>();
                if(!dent.Punch(bounds.center+new Vector3(0.45f,0.18f,1f)*bounds.size.magnitude,0.8f,false))
                    throw new System.Exception("Dent setup failed");
                dent.Sample(elapsed);
                var cracks=obj.AddComponent<WaxCrackVisual>(); cracks.SetDamage(0.8f);
                cracks.SyncSurface();
                preview.camera.orthographic=true;preview.camera.orthographicSize=bounds.size.magnitude*0.48f;
                preview.camera.nearClipPlane=0.01f;preview.camera.farClipPlane=100;
                preview.camera.transform.position=bounds.center+new Vector3(3,1.4f,5)*bounds.size.magnitude;
                preview.camera.transform.LookAt(bounds.center);
                preview.camera.clearFlags=CameraClearFlags.SolidColor; preview.camera.backgroundColor=new Color(0.18f,0.20f,0.24f);
                preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-35,0);
                preview.lights[1].intensity=0.8f;preview.ambientColor=Color.gray;
                preview.BeginStaticPreview(new Rect(0,0,640,640));
                preview.Render(true);
                var image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(directory,"dent-"+Mathf.RoundToInt(elapsed*1000)+".png"),image.EncodeToPNG());Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
        }
        Debug.Log("LOCAL DENT REVIEW saved: "+directory);
    }
    [MenuItem("Tools/Character/Prepare Wax Shell Visuals %#&w")]
    public static void Prepare()
    {
        var shader=Shader.Find("Game/WaxCracks");
        if(shader==null) throw new System.Exception("Wax crack shader missing");
        const string path="Assets/Resources/WaxCracks.mat";
        if(AssetDatabase.LoadAssetAtPath<Material>(path)==null)
            AssetDatabase.CreateAsset(new Material(shader),path);
        AssetDatabase.SaveAssets();
        if(EditorApplication.isPlaying) return;
        var dir=Path.GetFullPath("output/wax-shell-review");
        Directory.CreateDirectory(dir);
        for(int stage=0;stage<5;stage++)
        {
            var preview=new PreviewRenderUtility();
            try
            {
                var obj=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab"));
                obj.transform.position=Vector3.zero; obj.transform.rotation=Quaternion.identity;
                preview.AddSingleGO(obj);
                foreach(var b in obj.GetComponentsInChildren<MonoBehaviour>()) b.enabled=false;
                var crack=obj.GetComponent<WaxCrackVisual>();
                if(crack==null) crack=obj.AddComponent<WaxCrackVisual>();
                var tint = new MaterialPropertyBlock();
                tint.SetColor("baseColorFactor", new Color(0.25f,0.72f,0.32f));
                tint.SetColor("_BaseColor", new Color(0.25f,0.72f,0.32f));
                foreach (var r in obj.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(tint);
                crack.SetDamage(new[]{0f,0.25f,0.55f,0.85f,1f}[stage]);
                var bounds=new Bounds(); bool first=true;
                foreach(var r in obj.GetComponentsInChildren<Renderer>())
                { if(first) { bounds=r.bounds;first=false; } else bounds.Encapsulate(r.bounds); }
                if (stage == 4)
                {
                    var burst = obj.GetComponent<WaxShardBurst>();
                    if (burst == null) burst = obj.AddComponent<WaxShardBurst>();
                    burst.Burst(new Color(0.25f,0.72f,0.32f),true);
                    foreach(var piece in Object.FindObjectsByType<WaxShardPiece>(FindObjectsSortMode.None))
                    {
                        piece.enabled=false;
                        piece.transform.position=bounds.center+(piece.transform.position-bounds.center)*1.65f;
                        preview.AddSingleGO(piece.gameObject);
                    }
                    foreach(var r in obj.GetComponentsInChildren<Renderer>()) r.enabled=false;
                }
                preview.camera.orthographic=true;
                preview.camera.orthographicSize=bounds.size.magnitude*0.48f;
                preview.camera.nearClipPlane=0.01f; preview.camera.farClipPlane=100;
                preview.camera.transform.position=bounds.center+new Vector3(3,1.4f,5)*bounds.size.magnitude;
                preview.camera.transform.LookAt(bounds.center);
                preview.camera.clearFlags=CameraClearFlags.SolidColor;
                preview.camera.backgroundColor=new Color(0.18f,0.20f,0.24f);
                preview.lights[0].intensity=1.3f; preview.lights[0].transform.rotation=Quaternion.Euler(35,-35,0);
                preview.lights[1].intensity=0.8f;
                preview.ambientColor=Color.gray;
                preview.BeginStaticPreview(new Rect(0,0,640,640));preview.Render(true);
                var image=preview.EndStaticPreview();
                File.WriteAllBytes(Path.Combine(dir,"damage-"+stage+".png"),image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
        }
        Debug.Log("WAX SHELL REVIEW saved: "+dir);
    }
}
