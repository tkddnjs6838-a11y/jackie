using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

[InitializeOnLoad]
public static class StartScreenWorkshop
{
    public const string PrefabPath="Assets/Design/UI/StartScreen.prefab";
    public const string PreviewPath="Assets/Design/Scenes/StartScreenPreview.unity";
    static StartScreenWorkshop(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="output/gun-effects/start-screen-setup.txt";
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(request))return;
        string action=File.ReadAllText(request).Trim();File.Delete(request);
        if(action=="preview"){ValidatePreview();return;}
        if(action=="style"){try{StartScreenStyling.Apply();ValidatePreview();Debug.Log("START SCREEN STYLE COMPLETE");}catch(Exception ex){Debug.LogException(ex);}return;}
        try{Extract();File.WriteAllText("output/gun-effects/start-screen-setup-result.txt","PASS: StartScreen prefab, preview scene and gameplay references saved.");}
        catch(Exception ex){Debug.LogException(ex);File.WriteAllText("output/gun-effects/start-screen-setup-result.txt","FAIL: "+ex);}
    }
    static void Extract()
    {
        var flow=UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
        if(flow==null || flow.startPanel==null || flow.startButton==null)throw new Exception("Open the gameplay scene in Edit Mode first.");
        if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)!=null)throw new Exception("StartScreen already exists; refusing to overwrite design work.");
        Directory.CreateDirectory("Assets/Design/UI");Directory.CreateDirectory("Assets/Design/Scenes");
        Directory.CreateDirectory("output/start-screen-backup");
        File.Copy(flow.gameObject.scene.path,"output/start-screen-backup/SampleScene.before-extraction.unity",true);
        AssetDatabase.Refresh();
        var root=flow.startPanel;
        var view=root.AddComponent<StartScreenView>();view.startButton=flow.startButton;
        var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansKR-Bold.otf");
        var guide=new GameObject("ControlGuide",typeof(RectTransform),typeof(Image));guide.transform.SetParent(root.transform,false);
        var rect=guide.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(0,-205);rect.sizeDelta=new Vector2(660,140);
        guide.GetComponent<Image>().color=new Color(.025f,.035f,.055f,.86f);guide.GetComponent<Image>().raycastTarget=false;
        TextAt(guide.transform,"Controls","이동 WASD  ·  공격 좌클릭  ·  회피 Space\n강공격 우클릭  ·  마무리 콤보 좌 → 좌 → 우\n총 발사 좌클릭  ·  재장전 R  ·  보유 증강 Tab",font,22,Vector2.zero,new Vector2(630,120));
        view.testModeNotice=TextAt(root.transform,"TestModeNotice","총 테스트  3 샷건 · 4 기관총 · 5 레이저",font,20,new Vector2(0,-295),new Vector2(660,42)).gameObject;
        view.testModeNotice.SetActive(false);
        // No persistent scene callbacks are stored in the design asset.
        if(view.startButton.onClick.GetPersistentEventCount()!=0)throw new Exception("Start button contains persistent callbacks; inspect before extraction.");
        PrefabUtility.SaveAsPrefabAssetAndConnect(root,PrefabPath,InteractionMode.AutomatedAction);
        flow.startPanel=root;flow.startButton=view.startButton;EditorUtility.SetDirty(flow);
        var gameplay=flow.gameObject.scene;
        if(!EditorSceneManager.SaveScene(gameplay))throw new Exception("Could not save gameplay scene.");
        var originalCanvas=root.GetComponentInParent<Canvas>();var originalScaler=originalCanvas.GetComponent<CanvasScaler>();
        var preview=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(preview);
            var canvasObject=new GameObject("PreviewCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            EditorUtility.CopySerialized(originalScaler,canvasObject.GetComponent<CanvasScaler>());
            var screen=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),preview);
            screen.transform.SetParent(canvasObject.transform,false);
            var screenRect=screen.GetComponent<RectTransform>();screenRect.anchoredPosition=Vector2.zero;screenRect.sizeDelta=Vector2.zero;screenRect.localScale=Vector3.one;
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var camera=new GameObject("PreviewCamera",typeof(Camera)).GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.09f,.12f);camera.cullingMask=0;
            if(!EditorSceneManager.SaveScene(preview,PreviewPath))throw new Exception("Could not save preview scene.");
        }
        finally{SceneManager.SetActiveScene(gameplay);EditorSceneManager.CloseScene(preview,true);}

    }
    static void ValidatePreview()
    {
        var original=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.OpenScene(PreviewPath,OpenSceneMode.Additive);
        try
        {
            var views=new System.Collections.Generic.List<StartScreenView>();
            Canvas canvas=null;Camera camera=null;int gameplayComponents=0;
            foreach(var root in scene.GetRootGameObjects())
            {
                views.AddRange(root.GetComponentsInChildren<StartScreenView>(true));
                gameplayComponents+=root.GetComponentsInChildren<GameFlowManager>(true).Length;
                if(root.GetComponent<Canvas>()!=null)canvas=root.GetComponent<Canvas>();
                if(root.GetComponent<Camera>()!=null)camera=root.GetComponent<Camera>();
            }
            if(views.Count!=1 || views[0].startButton==null || gameplayComponents!=0 || canvas==null || camera==null)
                throw new Exception("Preview must contain one wired view and no gameplay manager.");
            if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(views[0].gameObject)!=PrefabPath)
                throw new Exception("Preview is not linked to the source prefab.");
            foreach(var child in canvas.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;
            camera.cullingMask=1<<5;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080)})
            {
                var rt=new RenderTexture(size.x,size.y,24);var old=RenderTexture.active;var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                try{camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);texture.Apply();File.WriteAllBytes("output/gun-effects/start-preview-"+size.x+".png",texture.EncodeToPNG());}
                finally{RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);}
            }
            File.WriteAllText("output/gun-effects/start-preview-checks.txt","PASS: preview contains a linked StartScreen prefab and valid button reference.\nPASS: no gameplay manager in preview.\nRendered 1280x720 and 1920x1080 previews.\n");
        }
        finally{SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(scene,true);}
    }

    static Text TextAt(Transform parent,string name,string content,Font font,int size,Vector2 at,Vector2 bounds)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=at;rect.sizeDelta=bounds;
        var text=go.GetComponent<Text>();text.font=font;text.fontSize=size;text.alignment=TextAnchor.MiddleCenter;text.text=content;text.color=Color.white;text.raycastTarget=false;return text;
    }
}
