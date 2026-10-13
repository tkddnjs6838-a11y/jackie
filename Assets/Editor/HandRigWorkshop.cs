using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class HandRigWorkshop
{
    const string Path="Assets/Models/PlayerCharacter_HandRig.fbx";
    [MenuItem("Tools/Character/Apply Rebuilt Hand Rig %#&h")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying) return;
        var player=GameObject.FindGameObjectWithTag("Player");
        var oldAnimator=player.GetComponentInChildren<Animator>();
        if(oldAnimator.GetComponent<WeaponHandGrip>()!=null) { Debug.Log("Hand rig already applied."); return; }
        var oldModel=oldAnimator.gameObject;
        var oldSkin=oldModel.GetComponentInChildren<SkinnedMeshRenderer>();
        var oldBounds=oldSkin.bounds;
        var attack=player.GetComponent<PlayerAutoAttack>();
        var sword=attack.swordModel;
        var swordScale=sword.lossyScale;
        var oldController=oldAnimator.runtimeAnimatorController;
        var importer=(ModelImporter)AssetImporter.GetAtPath(Path);
        importer.animationType=ModelImporterAnimationType.Generic;
        importer.importAnimation=true; importer.isReadable=true;
        importer.globalScale=1f; importer.materialImportMode=ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var sample=Object.Instantiate(prefab);
        var clip=AssetDatabase.LoadAllAssetsAtPath(Path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
        clip.SampleAnimation(sample,0f);
        var rotations=sample.GetComponentsInChildren<Transform>().Where(t=>IsFinger(t.name)).ToDictionary(t=>t.name,t=>t.localRotation);
        Object.DestroyImmediate(sample);
        var description=importer.humanDescription;
        var source=(ModelImporter)AssetImporter.GetAtPath("Assets/Models/PlayerCharacter.fbx");
        var human=source.humanDescription.human.ToList();
        foreach(string side in new[]{"Left","Right"})
            foreach(string finger in new[]{"Thumb","Index","Middle","Ring"})
                for(int i=1;i<=3;i++)
                    human.Add(new HumanBone { boneName=side+finger+i, humanName=side+" "+finger+" "+new[]{"Proximal","Intermediate","Distal"}[i-1], limit=new HumanLimit{useDefaultValues=true} });
        description.human=human.ToArray();
        importer.animationType=ModelImporterAnimationType.Human;
        importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
        importer.humanDescription=description;
        importer.SaveAndReimport();
        prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,oldModel.transform.parent);
        Undo.RegisterCreatedObjectUndo(model,"Rebuild character grip rig");
        model.name="PlayerModel_HandRig";
        model.transform.localPosition=oldModel.transform.localPosition;
        model.transform.localRotation=oldModel.transform.localRotation;
        var animator=model.GetComponent<Animator>();
        if(animator==null || animator.avatar==null || !animator.avatar.isValid || !animator.avatar.isHuman)
        { Object.DestroyImmediate(model); throw new InvalidOperationException("Rebuilt avatar did not validate; original kept."); }
        var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();
        model.transform.localScale*=oldBounds.size.y/skin.bounds.size.y;
        model.transform.position+=Vector3.up*(oldBounds.min.y-skin.bounds.min.y);
        skin.sharedMaterial=oldSkin.sharedMaterial;
        animator.runtimeAnimatorController=oldController;
        animator.applyRootMotion=false;
        var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
        var guide=model.GetComponentsInChildren<Transform>().First(t=>t.name=="WeaponGrip_R");
        Vector3 blade=model.transform.forward;
        Quaternion grip=Quaternion.Inverse(hand.rotation)*Quaternion.LookRotation(model.transform.up,blade);
        Vector3 handleCenter=guide.position;
        var gripComponent=model.AddComponent<WeaponHandGrip>();
        gripComponent.fingers=model.GetComponentsInChildren<Transform>().Where(t=>IsFinger(t.name)&&t.name.StartsWith("Right")).Select(t=>new WeaponHandGrip.FingerPose{bone=t,rotation=rotations[t.name]}).ToArray();
        gripComponent.swordLocalPosition=hand.InverseTransformPoint(handleCenter-blade*(0.075f*Mathf.Abs(player.transform.lossyScale.y)/0.72f));
        Undo.SetTransformParent(sword,hand,"Attach sword to rebuilt grip");
        sword.localScale=new Vector3(swordScale.x/hand.lossyScale.x,swordScale.y/hand.lossyScale.y,swordScale.z/hand.lossyScale.z);
        sword.localRotation=grip;
        sword.localPosition=gripComponent.swordLocalPosition;
        // Retarget scene references, including movement's Animator and the fallback swing bone.
        var transforms=model.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        foreach(var behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if(behaviour==null) continue;
            var serialized=new SerializedObject(behaviour); var property=serialized.GetIterator();
            while(property.NextVisible(true))
            {
                if(property.propertyType!=SerializedPropertyType.ObjectReference) continue;
                if(property.objectReferenceValue==oldAnimator) property.objectReferenceValue=animator;
                else if(property.objectReferenceValue==oldSkin) property.objectReferenceValue=skin;
                else if(property.objectReferenceValue is Transform t && t.IsChildOf(oldModel.transform) && transforms.TryGetValue(t.name,out var replacement)) property.objectReferenceValue=replacement;
            }
            serialized.ApplyModifiedProperties();
        }
        attack.animator=animator;attack.gripIdleEuler=grip.eulerAngles;
        attack.handGrip=gripComponent;
        Undo.RecordObject(oldModel,"Preserve previous character visual");
        oldModel.SetActive(false);oldModel.name="PlayerModel_PreHandRig";
        PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(sword);
        EditorUtility.SetDirty(attack);
        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveScene(player.scene);AssetDatabase.SaveAssets();
        Debug.Log("HAND RIG APPLIED bones="+skin.bones.Length+" grip joints="+gripComponent.fingers.Length+" scale="+model.transform.localScale+" grip="+gripComponent.swordLocalPosition+" euler="+attack.gripIdleEuler);
        CharacterWorkshop.Audit();
    }
    [MenuItem("Tools/Character/Finish Rig Surface %#&j")]
    public static void FinishSurface()
    {
        if(EditorApplication.isPlaying) return;
        var importer=(ModelImporter)AssetImporter.GetAtPath(Path);
        importer.importNormals=ModelImporterNormals.Import;
        importer.normalSmoothingAngle=180f;
        importer.SaveAndReimport();
        var skin=GameObject.FindGameObjectWithTag("Player").GetComponentInChildren<SkinnedMeshRenderer>();
        const string path="Assets/Materials/PlayerRebuiltSurface.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) { material=new Material(skin.sharedMaterial); AssetDatabase.CreateAsset(material,path); }
        material.SetFloat("_Cleanup",0.98f);
        material.SetColor("_BodyColor",new Color(0.06f,0.06f,0.06f));
        skin.sharedMaterial=material;
        var sourceMesh=AssetDatabase.LoadAssetAtPath<GameObject>(Path).GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
        var mesh=Object.Instantiate(sourceMesh);mesh.name="PlayerHandRig_Smooth";
        mesh.RecalculateBounds();
        const string meshPath="Assets/Models/PlayerHandRig_Smooth.asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(existing==null) AssetDatabase.CreateAsset(mesh,meshPath);
        else { EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing; }
        skin.sharedMesh=mesh;
        EditorUtility.SetDirty(material); PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
        EditorSceneManager.MarkSceneDirty(skin.gameObject.scene);
        EditorSceneManager.SaveScene(skin.gameObject.scene);AssetDatabase.SaveAssets();
        CharacterWorkshop.Audit();
    }
    [MenuItem("Tools/Character/Apply Light Sword Timing %#&k")]
    public static void ApplyLightSwordTiming()
    {
        if(EditorApplication.isPlaying) return;
        var attack=GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerAutoAttack>();
        Undo.RecordObject(attack,"Match light sword contact timing");
        attack.impactDelay=attack.swingStateDuration*SwordSwingMotion.ContactPhase;
        EditorUtility.SetDirty(attack);
        EditorSceneManager.MarkSceneDirty(attack.gameObject.scene);
        EditorSceneManager.SaveScene(attack.gameObject.scene);
        CharacterWorkshop.Audit();
    }
    [MenuItem("Tools/Character/Apply Sword Impact Feedback %#&m")]
    public static void ApplySwordImpactFeedback()
    {
        if (EditorApplication.isPlaying) return;
        var attack = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerAutoAttack>();
        Undo.RecordObject(attack, "Refine sword impact feedback");
        attack.impactShakeDuration = 0.07f;
        attack.impactShakeMagnitude = 0.035f;
        attack.missShakeMagnitude = 0f;
        attack.impactEffectSize = 0.8f;
        attack.sweepInnerRatio = 0.82f;
        EditorUtility.SetDirty(attack);
        EditorSceneManager.MarkSceneDirty(attack.gameObject.scene);
        EditorSceneManager.SaveScene(attack.gameObject.scene);
        Debug.Log("SWORD IMPACT APPLIED: compact flash, thin trail, contact-only shake.");
    }
    static bool IsFinger(string name) => new[]{"Thumb","Index","Middle","Ring"}.Any(f=>name.StartsWith("Right"+f)||name.StartsWith("Left"+f));
}
