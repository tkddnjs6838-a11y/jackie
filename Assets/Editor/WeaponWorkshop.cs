using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

[InitializeOnLoad]
public static class WeaponWorkshop
{
    static WeaponWorkshop() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string path="output/weapons/request.txt";
        if(EditorApplication.isCompiling || !File.Exists(path))return;
        var action=File.ReadAllText(path).Trim();File.Delete(path);
        if(action=="prepare")Prepare();
        if(action=="test" && EditorApplication.isPlaying)WeaponChecks.Run();
    }
    [MenuItem("Tools/Weapons/Prepare Axe Assets")]
    public static void Prepare()
    {
        Directory.CreateDirectory("Assets/Resources/Weapons");AssetDatabase.Refresh();
        var steel=Material("AxeSteel",new Color(.48f,.56f,.62f),.65f);
        var wood=Material("AxeHandle",new Color(.16f,.075f,.035f),.05f);
        var brass=Material("AxeBrass",new Color(.78f,.42f,.1f),.45f);
        var root=new GameObject("Axe");
        Part(root,"Handle",PrimitiveType.Cylinder,new Vector3(0,.36f,0),new Vector3(.047f,.36f,.047f),wood);
        Part(root,"Grip",PrimitiveType.Cylinder,new Vector3(0,.12f,0),new Vector3(.061f,.12f,.061f),wood);
        foreach(float y in new[]{.03f,.23f,.64f,.76f})Part(root,"Band",PrimitiveType.Cylinder,new Vector3(0,y,0),new Vector3(.066f,.019f,.066f),brass);
        var points=new[]{new Vector2(-.055f,.63f),new Vector2(.08f,.60f),new Vector2(.30f,.53f),new Vector2(.35f,.58f),new Vector2(.38f,.72f),new Vector2(.33f,.87f),new Vector2(.08f,.81f),new Vector2(-.055f,.78f)};
        var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int face=0;face<2;face++)foreach(var p in points)vertices.Add(new Vector3(p.x,p.y,face==0?-.045f:.045f));
        for(int i=1;i<points.Length-1;i++){triangles.AddRange(new[]{0,i+1,i,8,8+i,8+i+1});}
        for(int i=0;i<8;i++){int j=(i+1)%8;triangles.AddRange(new[]{i,j,j+8,i,j+8,i+8});}
        var mesh=new Mesh{name="AxeHead"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        const string meshPath="Assets/Resources/Weapons/AxeHead.asset";
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(saved==null){AssetDatabase.CreateAsset(mesh,meshPath);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);}
        var head=new GameObject("Head",typeof(MeshFilter),typeof(MeshRenderer));head.transform.SetParent(root.transform,false);head.GetComponent<MeshFilter>().sharedMesh=saved;head.GetComponent<MeshRenderer>().sharedMaterial=steel;
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Weapons/Axe.prefab");Object.DestroyImmediate(root);
        var crate=new GameObject("AxeCrate",typeof(WeaponPickup));
        Part(crate,"Crate",PrimitiveType.Cube,Vector3.zero,new Vector3(.48f,.36f,.48f),wood);
        foreach(float x in new[]{-.18f,.18f})Part(crate,"MetalBand",PrimitiveType.Cube,new Vector3(x,0,0),new Vector3(.045f,.38f,.50f),brass);
        var icon=(GameObject)PrefabUtility.InstantiatePrefab(prefab);icon.transform.SetParent(crate.transform,false);icon.transform.localPosition=new Vector3(-.09f,.25f,0);icon.transform.localScale=Vector3.one*.7f;
        PrefabUtility.SaveAsPrefabAsset(crate,"Assets/Resources/Weapons/AxeCrate.prefab");Object.DestroyImmediate(crate);
        AssetDatabase.SaveAssets();Debug.Log("AXE ASSETS READY");
    }
    static Material Material(string name,Color color,float metallic)
    {
        string path="Assets/Resources/Weapons/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",.38f);EditorUtility.SetDirty(mat);return mat;
    }
    static void Part(GameObject parent,string name,PrimitiveType shape,Vector3 at,Vector3 scale,Material mat)
    {
        var go=GameObject.CreatePrimitive(shape);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent.transform,false);go.transform.localPosition=at;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;
    }
}
