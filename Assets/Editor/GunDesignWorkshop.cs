using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Design assets only. Does not alter the player's equipment or the open scene.
[InitializeOnLoad]
public static class GunDesignWorkshop
{
    const string Folder="Assets/Resources/Weapons/Guns";
    static GunDesignWorkshop(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="output/gun-design/unity-request.txt";
        if(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request))return;
        var action=File.ReadAllText(request).Trim();File.Delete(request);
        try{Prepare(action=="laser",action=="shotgun");}catch(Exception e){File.WriteAllText("output/gun-design/unity-validation.txt",e.ToString());Debug.LogException(e);}
    }
    [MenuItem("Tools/Weapons/Prepare Gun Design Assets")]
    public static void Prepare() => Prepare(false);
    static void Prepare(bool laserOnly,bool shotgunOnly=false)
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var shader=Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null || !shader.isSupported)throw new Exception("URP Lit shader unavailable");
        string[] names={"GunGraphite","GunSilver","GunAmber","GunOlive","GunCeramic","GunEnergy","GunWood"};
        Color[] colors={new Color(.045f,.06f,.075f),new Color(.53f,.62f,.69f),new Color(.95f,.28f,.035f),new Color(.32f,.43f,.19f),new Color(.76f,.85f,.88f),new Color(.03f,.8f,1),new Color(.34f,.12f,.035f)};
        var materials=new Material[names.Length];
        for(int i=0;i<names.Length;i++)
        {
            string path=Folder+"/"+names[i]+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.shader=shader;material.SetColor("_BaseColor",colors[i]);material.SetFloat("_Metallic",i==1?.55f:.25f);material.SetFloat("_Smoothness",.4f);
            if(i==5){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",colors[i]*1.3f);}
            EditorUtility.SetDirty(material);materials[i]=material;
        }
        var report=new StringBuilder();
        foreach(string name in shotgunOnly ? new[]{"Pistol"} : laserOnly ? new[]{"LaserGun"} : new[]{"Pistol","MachineGun","LaserGun"})
        {
            string path="Assets/Models/Guns/"+name+".fbx";
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            if(importer==null)throw new Exception("Missing model: "+path);
            importer.importAnimation=false;importer.isReadable=false;importer.addCollider=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var root=new GameObject(name);
            try
            {
                var instance=UnityEngine.Object.Instantiate(model,root.transform);instance.name="Model";
                var transforms=instance.GetComponentsInChildren<Transform>();
                var muzzle=transforms.First(t=>t.name==name+"_Muzzle");var grip=transforms.First(t=>t.name==name+"_GripPoint");
                foreach(var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                {
                    var slots=renderer.sharedMaterials;
                    for(int j=0;j<slots.Length;j++)
                    {
                        int index=Array.FindIndex(names,n=>slots[j]!=null && slots[j].name.StartsWith(n,StringComparison.Ordinal));
                        if(index<0)throw new Exception("Unknown material in "+name+": "+slots[j]);
                        slots[j]=materials[index];
                    }
                    renderer.sharedMaterials=slots;
                }
                // Explicit common attachment convention: +Z forward, +Y up, grip at origin.
                var aim=transforms.First(t=>t.name==name+"_AimAxis");
                var up=transforms.First(t=>t.name==name+"_UpAxis");
                instance.transform.rotation=Quaternion.Inverse(Quaternion.LookRotation(aim.position-grip.position,up.position-grip.position))*instance.transform.rotation;
                instance.transform.localPosition-=root.transform.InverseTransformPoint(grip.position);
                var muzzlePosition=root.transform.InverseTransformPoint(muzzle.position);
                if(muzzlePosition.z<=.1f || Mathf.Abs(muzzlePosition.x)>.01f)throw new Exception("Unexpected muzzle axis: "+name+" "+muzzlePosition);
                foreach(string markerName in new[]{"Muzzle","GripPoint"})
                {
                    var marker=transforms.First(t=>t.name==name+"_"+markerName);
                    marker.SetParent(root.transform,true);marker.name=markerName;marker.localRotation=Quaternion.identity;
                }
                int totalTriangles=0;
                foreach(var filter in instance.GetComponentsInChildren<MeshFilter>())for(int s=0;s<filter.sharedMesh.subMeshCount;s++)totalTriangles+=(int)filter.sharedMesh.GetIndexCount(s)/3;
                int slotsCount=instance.GetComponentsInChildren<MeshRenderer>().Sum(r=>r.sharedMaterials.Length);
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");
                report.AppendLine(name+": PASS; triangles="+totalTriangles+"; material slots="+slotsCount+"; muzzle="+muzzlePosition.ToString("F3")+"; grip at origin; URP materials assigned");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        AssetDatabase.SaveAssets();File.WriteAllText("output/gun-design/unity-validation.txt",report.ToString());Debug.Log("GUN DESIGN ASSETS READY\n"+report);
    }
}
