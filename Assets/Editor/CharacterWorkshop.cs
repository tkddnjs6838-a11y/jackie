using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using UnityEngine;

public static class CharacterWorkshop
{
    [MenuItem("Tools/Character/Apply Character Polish %#&b")]
    public static void ApplyPolish()
    {
        if (EditorApplication.isPlaying) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if(player.GetComponent<PlayerAutoAttack>().handGrip!=null) { HandRigWorkshop.FinishSurface(); return; }
        var animator = player.GetComponentInChildren<Animator>();
        var skin = player.GetComponentInChildren<SkinnedMeshRenderer>();
        var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/PlayerCharacter.fbx");
        var original = sourceModel.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
        const string meshPath = "Assets/Models/PlayerCharacter_Smooth.asset";
        var mesh = Object.Instantiate(original);
        mesh.name = "PlayerCharacter_Smooth";
        var positions = mesh.vertices;
        var groups = new Dictionary<Vector3Int, int>();
        var vertexGroups = new int[positions.Length];
        var points = new List<Vector3>();
        for (int i = 0; i < positions.Length; i++)
        {
            var p = positions[i];
            var key = new Vector3Int(Mathf.RoundToInt(p.x*100000), Mathf.RoundToInt(p.y*100000), Mathf.RoundToInt(p.z*100000));
            if (!groups.TryGetValue(key, out int group)) { group = points.Count; groups.Add(key,group); points.Add(p); }
            vertexGroups[i] = group;
        }
        var neighbors = points.Select(_ => new HashSet<int>()).ToArray();
        var triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
            for (int edge = 0; edge < 3; edge++)
            {
                int a=vertexGroups[triangles[i+edge]], b=vertexGroups[triangles[i+(edge+1)%3]];
                if (a!=b) { neighbors[a].Add(b); neighbors[b].Add(a); }
            }
        // Two gentle passes retain the silhouette, UVs, bone weights and bind poses.
        for (int pass=0; pass<2; pass++)
        {
            var next = points.ToArray();
            for (int i=0; i<points.Count; i++)
            {
                if (neighbors[i].Count==0) continue;
                Vector3 mean=Vector3.zero;
                foreach (int n in neighbors[i]) mean+=points[n];
                next[i]=Vector3.Lerp(points[i],mean/neighbors[i].Count,0.18f);
            }
            points=next.ToList();
        }
        for (int i=0;i<positions.Length;i++) positions[i]=points[vertexGroups[i]];
        mesh.vertices=positions;
        mesh.RecalculateNormals();
        var normals=mesh.normals;
        var sums=new Vector3[points.Count];
        for(int i=0;i<normals.Length;i++) sums[vertexGroups[i]]+=normals[i];
        for(int i=0;i<normals.Length;i++) normals[i]=sums[vertexGroups[i]].normalized;
        mesh.normals=normals; mesh.RecalculateTangents(); mesh.RecalculateBounds();
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(existing==null) AssetDatabase.CreateAsset(mesh,meshPath);
        else { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); mesh=existing; }
        Undo.RecordObject(skin,"Polish character mesh");
        skin.sharedMesh=mesh;

        const string materialPath="Assets/Materials/PlayerCleanSurface.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null) { material=new Material(Shader.Find("Game/CharacterSurface")); AssetDatabase.CreateAsset(material,materialPath); }
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Player_Albedo.png"));
        material.SetColor("_BodyColor",new Color(0.43f,0.45f,0.48f));
        material.SetFloat("_Cleanup",0.8f);
        skin.sharedMaterial=material;
        PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
        EditorUtility.SetDirty(material);

        var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/Animations/UpperBodyMask.mask");
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head,false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm,false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers,false);
        EditorUtility.SetDirty(mask);
        var controller=(AnimatorController)animator.runtimeAnimatorController;
        if(!controller.parameters.Any(p=>p.name=="AttackSpeed")) controller.AddParameter("AttackSpeed",AnimatorControllerParameterType.Float);
        var state=controller.layers[1].stateMachine.states.First(s=>s.state.name=="Attack").state;
        var slash=AssetDatabase.LoadAllAssetsAtPath("Assets/Animations/SwordSlashAnim.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
        state.motion=slash;
        state.speed=slash.length/0.5f;
        state.speedParameter="AttackSpeed"; state.speedParameterActive=true;
        foreach(var transition in state.transitions) { transition.exitTime=0.96f; transition.duration=0.06f; }
        EditorUtility.SetDirty(state); EditorUtility.SetDirty(controller);

        var attack=player.GetComponent<PlayerAutoAttack>();
        Undo.RecordObject(attack,"Stabilize sword grip");
        attack.swordModel=player.GetComponentsInChildren<Transform>().First(t=>t.name=="SwordModel");
        attack.gripIdleEuler=new Vector3(59.03f,7.22f,180.8f);
        attack.swingStateDuration=0.5f;
        attack.impactDelay=0.30f;
        attack.gripBlendIn=0.05f; attack.gripBlendOut=0.1f;
        Undo.RecordObject(attack.swordModel,"Align sword grip");
        attack.swordModel.localRotation=Quaternion.Euler(attack.gripIdleEuler);
        PrefabUtility.RecordPrefabInstancePropertyModifications(attack.swordModel);
        EditorUtility.SetDirty(attack);
        EditorSceneManager.MarkSceneDirty(player.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(player.scene);
        Debug.Log("CHARACTER POLISH APPLIED vertices="+mesh.vertexCount+" clip="+slash.name+" length="+slash.length);
        Audit();
    }

    [MenuItem("Tools/Character/Render Character Audit %#&c")]
    public static void Audit()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Character audit requires Edit Mode."); return; }
        var source = GameObject.FindGameObjectWithTag("Player");
        if (source == null) return;
        var preview = new PreviewRenderUtility();
        var report = new StringBuilder();
        var directory = Path.GetFullPath("output/character-review");
        Directory.CreateDirectory(directory);
        try
        {
            var clone = Object.Instantiate(source);
            clone.name = "CharacterReview";
            clone.transform.position = Vector3.zero;
            clone.transform.rotation = Quaternion.identity;
            preview.AddSingleGO(clone);
            foreach (var component in clone.GetComponentsInChildren<MonoBehaviour>()) component.enabled = false;
            foreach (var camera in clone.GetComponentsInChildren<Camera>()) camera.enabled = false;
            var animator = clone.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            animator.Rebind(); animator.SetFloat("AttackSpeed",1f); animator.Update(0f);
            var sword = clone.GetComponentsInChildren<Transform>().First(t => t.name == "SwordModel");
            var attack = clone.GetComponent<PlayerAutoAttack>();
            foreach (var r in clone.GetComponentsInChildren<Renderer>())
            {
                var skin = r as SkinnedMeshRenderer;
                report.AppendLine(r.name + " bounds=" + r.bounds + " mesh=" + (skin != null ? AssetDatabase.GetAssetPath(skin.sharedMesh) + " vertices=" + skin.sharedMesh.vertexCount : "static"));
            }
            var sourceSkin=clone.GetComponentInChildren<SkinnedMeshRenderer>();
            for(int bone=0;bone<sourceSkin.bones.Length;bone++)
                report.AppendLine("bone "+bone+" "+sourceSkin.bones[bone].name+" bind="+sourceSkin.sharedMesh.bindposes[bone].inverse.MultiplyPoint3x4(Vector3.zero));
            report.AppendLine("avatar=" + AssetDatabase.GetAssetPath(animator.avatar));
            report.AppendLine("sword local=" + sword.localPosition + " euler=" + sword.localEulerAngles + " scale=" + sword.localScale + " parent=" + sword.parent.name);
            report.AppendLine("hand=" + animator.GetBoneTransform(HumanBodyBones.RightHand).position);
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = 1.35f;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 50f;
            preview.camera.backgroundColor = new Color(0.12f, 0.15f, 0.19f);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.lights[0].intensity = 1.4f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35, -35, 0);
            preview.lights[1].intensity = 0.8f;
            preview.lights[1].transform.rotation = Quaternion.Euler(20, 145, 0);
            preview.ambientColor = new Color(0.3f, 0.3f, 0.3f);
            var skinned = clone.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked = new Mesh();
            var bakedObject = new GameObject("BakedPose");
            bakedObject.transform.SetParent(skinned.transform, false);
            var skinScale=skinned.transform.lossyScale;
            bakedObject.transform.localScale=new Vector3(1f/skinScale.x,1f/skinScale.y,1f/skinScale.z);
            bakedObject.AddComponent<MeshFilter>().sharedMesh = baked;
            bakedObject.AddComponent<MeshRenderer>().sharedMaterials = skinned.sharedMaterials;
            skinned.enabled = false;
            for (int frame = 0; frame < 44; frame++)
            {
                bool walking=frame>=22;
                int pose=frame%22;
                float normalized=Mathf.Max(0,pose-1)/20f;
                attack.ResetSwordPose();
                animator.Rebind(); animator.SetFloat("AttackSpeed",1f);
                animator.SetFloat("Speed",walking ? 6f : 0f);
                animator.Play(walking ? "Base Layer.Walk" : "Base Layer.Idle",0,walking ? normalized : 0f);
                animator.SetLayerWeight(1, 0f);

                animator.Update(0f);
                attack.ApplySwordPose(normalized,pose>0);
                skinned.enabled = true;
                skinned.BakeMesh(baked);
                skinned.enabled = false;
                bakedObject.GetComponent<MeshFilter>().sharedMesh = baked;
                report.AppendLine("baked="+baked.bounds+" scale="+skinned.transform.lossyScale+" position="+skinned.transform.position+" scene="+bakedObject.scene.name);
                report.AppendLine("frame="+frame+" hand="+animator.GetBoneTransform(HumanBodyBones.RightHand).position+" sword="+sword.position+" axis="+sword.up);
                for (int view=0;view<3;view++)
                {
                    preview.camera.orthographicSize=view==2 ? 0.22f : 1.35f;
                    Vector3 target=view==2 ? sword.parent.position : new Vector3(0,-0.1f,0);
                    preview.camera.transform.position=view==0 ? new Vector3(3,1.5f,5) : view==1 ? new Vector3(5,0.5f,0) : target+new Vector3(2,1.5f,4);
                    preview.camera.transform.LookAt(target);
                    preview.BeginStaticPreview(new Rect(0,0,512,512));
                    preview.Render(true);
                    var image=preview.EndStaticPreview();
                    File.WriteAllBytes(Path.Combine(directory,(walking ? "walk-" : "")+(view==0 ? "pose-" : view==1 ? "side-" : "hand-")+pose+".png"),image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
            }
            Object.DestroyImmediate(baked);
            File.WriteAllText(Path.Combine(directory, "audit.txt"), report.ToString());
            Debug.Log("CHARACTER AUDIT saved to " + directory);
        }
        finally { preview.Cleanup(); }
    }
}
