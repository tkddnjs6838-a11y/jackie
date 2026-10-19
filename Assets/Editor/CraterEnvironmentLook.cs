using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CraterEnvironmentLook
{
    const string Dir="Assets/Design/Environment/Map05_Crater/";
    public static void Apply()
    {
        var scene=SceneManager.GetSceneByName("Map05_Crater");
        if(!scene.IsValid() || Application.isPlaying)throw new System.InvalidOperationException("Open crater scene in Edit mode");
        Terrain terrain=null;MapMood mood=null;
        foreach(var root in scene.GetRootGameObjects()){if(terrain==null)terrain=root.GetComponentInChildren<Terrain>();if(mood==null)mood=root.GetComponentInChildren<MapMood>();}
        const int size=512;var height=new float[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float u=x/(float)size,v=y/(float)size;
            float n=Tile(u,v,5)*.55f+Tile(u,v,13)*.3f+Tile(u,v,43)*.15f;
            height[y*size+x]=n;
        }
        for(int layer=0;layer<3;layer++)
        {
            var tex=new Texture2D(size,size,TextureFormat.RGB24,false);var pixels=new Color[size*size];
            for(int i=0;i<pixels.Length;i++)
            {
                float n=height[i],grain=Mathf.SmoothStep(.16f,.36f,n);
                float crack=1-Mathf.SmoothStep(.012f,.045f,Mathf.Abs(n-.48f));grain*=1-crack*.42f;
                float brightness=layer==1?.76f:layer==2?1.20f:1;
                pixels[i]=new Color(grain*1.15f,grain*.64f,grain*.48f)*brightness;
            }
            tex.SetPixels(pixels);tex.Apply();File.WriteAllBytes(Dir+"BasaltGround"+layer+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);
        }
        var normal=new Texture2D(size,size,TextureFormat.RGB24,false);var normals=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float dx=height[y*size+(x+1)%size]-height[y*size+(x+size-1)%size];
            float dy=height[((y+1)%size)*size+x]-height[((y+size-1)%size)*size+x];
            var n=new Vector3(-dx*7,-dy*7,1).normalized;normals[y*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f);
        }
        normal.SetPixels(normals);normal.Apply();File.WriteAllBytes(Dir+"BasaltNormal.png",normal.EncodeToPNG());Object.DestroyImmediate(normal);
        AssetDatabase.Refresh();var ni=(TextureImporter)AssetImporter.GetAtPath(Dir+"BasaltNormal.png");ni.textureType=TextureImporterType.NormalMap;ni.SaveAndReimport();
        for(int i=0;i<3;i++)
        {
            var l=AssetDatabase.LoadAssetAtPath<TerrainLayer>(Dir+"TL_Map05_Ground"+i+".terrainlayer");
            l.diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Dir+"BasaltGround"+i+".png");l.normalMapTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Dir+"BasaltNormal.png");
            l.normalScale=.65f;l.smoothness=.15f;l.tileSize=Vector2.one*(i==2?5:8);EditorUtility.SetDirty(l);
        }
        foreach(var name in new[]{"M_Basalt","M_BasaltTop","M_VolcanoRock","M_BurntWax"})
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Models/"+name+".mat");if(mat==null)continue;
            mat.SetColor("_BaseColor",new Color(.85f,.62f,.50f));mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Dir+"BasaltGround0.png"));
            mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Dir+"BasaltNormal.png"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.5f);mat.SetFloat("_Smoothness",.18f);EditorUtility.SetDirty(mat);
        }
        foreach(var path in new[]{Dir+"M_Map05_Liquid.mat",Dir+"Models/M_LavaGlow.mat"})
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);mat.shader=Shader.Find("Game/Crater Lava");mat.SetColor("_Color",Color.white);mat.SetFloat("_Pool",1);mat.SetFloat("_Stream",1);mat.SetFloat("_Cooling",0);EditorUtility.SetDirty(mat);
        }
        var old=GameObject.Find("CraterCoolingBanks");if(old!=null)Object.DestroyImmediate(old);
        var banks=new GameObject("CraterCoolingBanks");SceneManager.MoveGameObjectToScene(banks,scene);
        var seed=new System.Random(505);var pools=new[]{new Vector2(-12.4f,10.7f),new Vector2(14.2f,-10.7f),new Vector2(11.4f,13.5f),new Vector2(-14.2f,-12.4f)};
        var primitive=GameObject.CreatePrimitive(PrimitiveType.Sphere);var sphere=primitive.GetComponent<MeshFilter>().sharedMesh;var combines=new List<CombineInstance>();
        foreach(var pool in pools)for(int i=0;i<25;i++)
        {
            float a=i*Mathf.PI*2/25,r=3.85f+(float)seed.NextDouble()*.5f;Vector3 p=new Vector3(pool.x+Mathf.Cos(a)*r,0,pool.y+Mathf.Sin(a)*r);
            p.y=terrain.SampleHeight(p)+terrain.transform.position.y-.1f;
            var scale=new Vector3(.65f+(float)seed.NextDouble()*.6f,.28f+(float)seed.NextDouble()*.25f,.55f+(float)seed.NextDouble()*.6f);
            combines.Add(new CombineInstance{mesh=sphere,transform=Matrix4x4.TRS(p,Quaternion.Euler(12,(float)seed.NextDouble()*360,8),scale)});
        }
        Object.DestroyImmediate(primitive);var mesh=new Mesh{name="CooledLavaBanks"};mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(combines.ToArray());
        string meshPath=Dir+"CooledLavaBanks.asset";var stored=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(stored==null){AssetDatabase.CreateAsset(mesh,meshPath);stored=mesh;}else{EditorUtility.CopySerialized(mesh,stored);Object.DestroyImmediate(mesh);}
        banks.AddComponent<MeshFilter>().sharedMesh=stored;banks.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Models/M_Basalt.mat");banks.isStatic=true;
        // Local warm spill around molten pools; no collision or gameplay changes.
        var oldGlow=GameObject.Find("CraterLavaBounce");if(oldGlow!=null)Object.DestroyImmediate(oldGlow);
        var glowRoot=new GameObject("CraterLavaBounce");SceneManager.MoveGameObjectToScene(glowRoot,scene);
        foreach(var pool in pools)
        {
            var go=new GameObject("Lava warm spill");go.transform.SetParent(glowRoot.transform);
            go.transform.position=new Vector3(pool.x,4.4f,pool.y);
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1f,.22f,.025f);
            light.range=11;light.intensity=3;light.shadows=LightShadows.None;
        }
        if(mood!=null)
        {
            var bright=mood.bright;bright.sunColor=new Color(1f,.72f,.48f);bright.ambientSky=new Color(.37f,.22f,.18f);bright.ambientEquator=new Color(.27f,.12f,.07f);bright.fogColor=new Color(.22f,.095f,.055f);mood.bright=bright;
            var dark=mood.dark;dark.sunColor=new Color(1f,.66f,.42f);dark.ambientSky=new Color(.30f,.18f,.17f);dark.ambientEquator=new Color(.23f,.10f,.055f);dark.fogColor=new Color(.18f,.065f,.035f);mood.dark=dark;EditorUtility.SetDirty(mood);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
    static float Tile(float u,float v,float scale)
    {
        float a=Mathf.PerlinNoise(u*scale+17,v*scale+31),b=Mathf.PerlinNoise((u-1)*scale+17,v*scale+31);
        float c=Mathf.PerlinNoise(u*scale+17,(v-1)*scale+31),d=Mathf.PerlinNoise((u-1)*scale+17,(v-1)*scale+31);
        return Mathf.Lerp(Mathf.Lerp(a,b,u),Mathf.Lerp(c,d,u),v);
    }
}
