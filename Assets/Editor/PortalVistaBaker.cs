using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public static class PortalVistaBaker
{
    // Invoke only in a temporary play session after loading the intended map.
    public static string CaptureCurrent()
    {
        var map=Object.FindFirstObjectByType<MapSandbox>();
        if(!Application.isPlaying || map==null || map.Busy)throw new System.InvalidOperationException("Load a destination first.");
        var go=new GameObject("VistaBakeCamera");var cam=go.AddComponent<Camera>();cam.enabled=false;
        var forward=new Vector3(-map.Arrival.x,0,-map.Arrival.z).normalized;
        var position=map.Arrival+forward*9;
        if(Physics.Raycast(position+Vector3.up*120,Vector3.down,out var ground,240,~(1<<2),QueryTriggerInteraction.Ignore))position.y=ground.point.y;
        go.transform.position=position+Vector3.up*4;
        cam.fieldOfView=90;cam.aspect=1;
        cam.farClipPlane=240;cam.nearClipPlane=.15f;cam.clearFlags=CameraClearFlags.Skybox;cam.cullingMask=~(1<<2);
        var hidden=new System.Collections.Generic.List<Renderer>();
        foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))if(r.gameObject.scene!=SceneManager.GetActiveScene() && r.enabled){r.enabled=false;hidden.Add(r);}
        var cube=new Cubemap(256,TextureFormat.RGB24,true);cube.name=MapSandbox.Maps[map.CurrentMap]+"Panorama";
        try
        {
            // Explicit readback: RenderToCubemap alone only updates GPU data, which is not serialized into an asset.
            var rt=RenderTexture.GetTemporary(256,256,24,RenderTextureFormat.ARGB32);
            var read=new Texture2D(256,256,TextureFormat.RGB24,false);var previousRT=RenderTexture.active;
            var canvases=Object.FindObjectsByType<UnityEngine.Canvas>(FindObjectsSortMode.None);var visible=new System.Collections.Generic.List<Canvas>();
            foreach(var canvas in canvases)if(canvas.enabled){canvas.enabled=false;visible.Add(canvas);}
            try
            {
                var rotations=new[]{new Vector3(0,90,0),new Vector3(0,-90,0),new Vector3(-90,0,0),new Vector3(90,0,0),Vector3.zero,new Vector3(0,180,0)};
                cam.targetTexture=rt;
                for(int face=0;face<6;face++)
                {
                    go.transform.rotation=Quaternion.Euler(rotations[face]);cam.Render();RenderTexture.active=rt;
                    read.ReadPixels(new Rect(0,0,256,256),0,0);read.Apply();var pixels=read.GetPixels();var flipped=new Color[pixels.Length];for(int y=0;y<256;y++)System.Array.Copy(pixels,y*256,flipped,(255-y)*256,256);cube.SetPixels(flipped,(CubemapFace)face);
                }
                cube.Apply(true,false);
            }
            finally{cam.targetTexture=null;RenderTexture.active=previousRT;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(read);foreach(var canvas in visible)if(canvas!=null)canvas.enabled=true;}

            var path="Assets/Resources/Portals/"+MapSandbox.Maps[map.CurrentMap];
            var previous=AssetDatabase.LoadAssetAtPath<Cubemap>(path+"Cube.asset");
            if(previous==null)AssetDatabase.CreateAsset(cube,path+"Cube.asset");else{EditorUtility.CopySerialized(cube,previous);Object.DestroyImmediate(cube);cube=previous;EditorUtility.SetDirty(cube);}
            var data=AssetDatabase.LoadAssetAtPath<PortalVista>(path+".asset");if(data==null){data=ScriptableObject.CreateInstance<PortalVista>();AssetDatabase.CreateAsset(data,path+".asset");}
            data.panorama=cube;data.forward=forward;EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();return path;
        }
        finally{foreach(var r in hidden)if(r!=null)r.enabled=true;Object.DestroyImmediate(go);}
    }
}
