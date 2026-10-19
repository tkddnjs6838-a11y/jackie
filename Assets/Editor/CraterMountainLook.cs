using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
public static class CraterMountainLook
{
 const string Dir="Assets/Design/Environment/Map05_Crater/";
 public static void Apply()
 {
  var scene=SceneManager.GetSceneByName("Map05_Crater");Terrain t=null;
  foreach(var root in scene.GetRootGameObjects()){var candidate=root.GetComponentInChildren<Terrain>();if(candidate!=null)t=candidate;}
  var data=t.terrainData;int n=data.heightmapResolution;var h=data.GetHeights(0,0,n,n);
  string backup="output/crater-boss-approved/lava-rework/TD_Map05.before-mountain.asset";
  if(!System.IO.File.Exists(backup))System.IO.File.Copy(AssetDatabase.GetAssetPath(data),backup);
  for(int z=0;z<n;z++)for(int x=0;x<n;x++)
  {
   float wx=-48+x*96f/(n-1),wz=-48+z*96f/(n-1),r=new Vector2(wx,wz).magnitude;
   float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(20.5f,28,r));
   float d=new Vector2((wx+5)*.96f,(wz-33)*1.05f).magnitude;
   float cone=20*Mathf.Pow(Mathf.Clamp01(1-d/30),1.15f);
   float vent=5.2f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2,5,d)));
   float ridge=(Mathf.PerlinNoise(wx*.09f+40,wz*.09f+70)-.5f)*2.4f;
   float target=3+cone-vent+ridge*Mathf.Clamp01(d/8);
   float baseHeight=3+(Mathf.PerlinNoise(wx*.09f+17,wz*.09f+31)-.5f)*.6f;
   foreach(var pool in new[]{new Vector2(-12.4f,10.7f),new Vector2(14.2f,-10.7f),new Vector2(11.4f,13.5f),new Vector2(-14.2f,-12.4f)})baseHeight-=1.6f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2,5.5f,Vector2.Distance(new Vector2(wx,wz),pool))));
   h[z,x]=Mathf.Lerp(baseHeight,Mathf.Clamp(target,3,23),blend)/24;
  }
  data.SetHeights(0,0,h);EditorUtility.SetDirty(data);
  foreach(var root in scene.GetRootGameObjects())foreach(var tr in root.GetComponentsInChildren<Transform>())
  {
   if(tr.name.Contains("Crater_Backdrop")){tr.gameObject.SetActive(false);continue;}
   if(tr.name.StartsWith("Crater_Basalt")||tr.name.StartsWith("Crater_Vent")||tr.name.StartsWith("Crater_Stalagmite"))
   {var p=tr.position;p.y=data.GetInterpolatedHeight((p.x+48)/96,(p.z+48)/96);tr.position=p;}
  }
  var old=GameObject.Find("CraterMountainLava");if(old!=null)Object.DestroyImmediate(old);
  var go=new GameObject("CraterMountainLava");SceneManager.MoveGameObjectToScene(go,scene);
  var verts=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
  AddRiver(t,new Vector2(-5,32),new Vector2(-12.4f,10.7f),1.45f,0,verts,uv,tris);
  AddRiver(t,new Vector2(-3,32),new Vector2(11.4f,13.5f),1.85f,2,verts,uv,tris);
  AddRiver(t,new Vector2(3,24),new Vector2(24,8),.65f,4,verts,uv,tris);
  var mesh=new Mesh{name="MountainLavaRivers"};mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();
  string path=Dir+"MountainLavaRivers.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{saved.Clear();saved.vertices=mesh.vertices;saved.uv=mesh.uv;saved.triangles=mesh.triangles;saved.RecalculateNormals();saved.RecalculateBounds();saved.UploadMeshData(false);EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);}
  var mat=AssetDatabase.LoadAssetAtPath<Material>(Dir+"M_MountainLava.mat");if(mat==null){mat=new Material(Shader.Find("Game/Crater Lava"));AssetDatabase.CreateAsset(mat,Dir+"M_MountainLava.mat");}
  mat.SetFloat("_Pool",0);mat.SetFloat("_Stream",0);mat.SetFloat("_Cooling",0);EditorUtility.SetDirty(mat);
  go.AddComponent<MeshFilter>().sharedMesh=saved;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
  // Visual streams stay on the rear slopes, outside the central combat floor.
  foreach(var root in scene.GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Component>())
   if(c!=null&&c.GetType().Name=="NavMeshSurface")c.GetType().GetMethod("BuildNavMesh").Invoke(c,null);
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
 }
 static void AddRiver(Terrain terrain,Vector2 a,Vector2 b,float width,float phase,List<Vector3> v,List<Vector2> uv,List<int> tr)
 {
  int start=v.Count;const int rows=100,cols=9;var dir=(b-a).normalized;var right=new Vector2(-dir.y,dir.x);
  for(int i=0;i<rows;i++)for(int j=0;j<cols;j++)
  {
   float f=i/(float)(rows-1),s=j/(float)(cols-1),w=width*(.65f+.35f*f)*(1+.18f*Mathf.Sin(f*23+phase));
   Vector2 p=Vector2.Lerp(a,b,f)+right*(Mathf.Sin(f*9+phase)*Mathf.Sin(f*Mathf.PI)*1.7f+(s*2-1)*w);
   var pos=new Vector3(p.x,0,p.y);pos.y=Mathf.Max(2.42f,terrain.terrainData.GetInterpolatedHeight((pos.x+48)/96,(pos.z+48)/96)+.07f);v.Add(pos);uv.Add(new Vector2(s,f*15));
   if(i<rows-1&&j<cols-1){int k=start+i*cols+j;tr.Add(k);tr.Add(k+cols);tr.Add(k+1);tr.Add(k+1);tr.Add(k+cols);tr.Add(k+cols+1);}
  }
 }
}
