using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class WeaponHudArt
{
    static WeaponHudArt(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="output/gun-effects/weapon-hud-art.txt";
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(request))return;
        File.Delete(request);Build();
    }
    static void Build()
    {
        var attack=Object.FindFirstObjectByType<PlayerAutoAttack>();
        if(attack==null || attack.swordModel==null)throw new System.Exception("Sword model missing");
        var sources=new[]{attack.swordModel.gameObject,Resources.Load<GameObject>("Weapons/Axe"),Resources.Load<GameObject>("Weapons/Guns/Pistol"),Resources.Load<GameObject>("Weapons/Guns/MachineGun"),Resources.Load<GameObject>("Weapons/Guns/LaserGun")};
        var names=new[]{"Sword","Axe","Shotgun","MachineGun","LaserGun"};
        Directory.CreateDirectory("Assets/Resources/UI/WeaponPortraits");
        for(int i=0;i<sources.Length;i++)
        {
            var preview=new PreviewRenderUtility();var root=new GameObject("WeaponPortrait");
            try
            {
                var model=Object.Instantiate(sources[i],root.transform);model.SetActive(true);
                foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(script);
                model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one;
                root.transform.rotation=i<2?Quaternion.Euler(0,0,-35):Quaternion.Euler(0,90,-10);
                var renderers=root.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var r in renderers){r.enabled=true;bounds.Encapsulate(r.bounds);}
                preview.AddSingleGO(root);
                preview.camera.orthographic=true;preview.camera.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x/2f)*1.12f;
                preview.camera.transform.position=bounds.center+Vector3.back*(bounds.size.magnitude+5);preview.camera.transform.rotation=Quaternion.identity;
                preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=bounds.size.magnitude*3+20;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=Color.clear;
                preview.lights[0].intensity=1.1f;preview.lights[0].transform.rotation=Quaternion.Euler(30,20,0);
                preview.lights[1].intensity=.7f;preview.ambientColor=new Color(.4f,.4f,.4f);
                preview.BeginStaticPreview(new Rect(0,0,512,256));preview.Render(true);
                var rendered=preview.EndStaticPreview();
                var texture=new Texture2D(rendered.width,rendered.height,TextureFormat.RGBA32,false);
                var pixels=rendered.GetPixels32();
                // Static preview exports RGB. Its pure black clear colour is not model geometry.
                for(int j=0;j<pixels.Length;j++)if(pixels[j].r==0 && pixels[j].g==0 && pixels[j].b==0)pixels[j].a=0;
                texture.SetPixels32(pixels);texture.Apply();Object.DestroyImmediate(rendered);
                var path="Assets/Resources/UI/WeaponPortraits/"+names[i]+".png";
                File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
            }
            finally{preview.Cleanup();if(root!=null)Object.DestroyImmediate(root);}
        }
        File.WriteAllText("output/gun-effects/weapon-hud-art-result.txt","COMPLETE\n");
    }
}
