using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MinamiKotaroPlayerAvatarSetup
{
    private const string ModelPath = "Assets/Characters/MinamiKotaro/Models/kotarotest.fbx";
    private const string BodyName = "PlayerBody_MinamiKotaro";
    private const float KotaroGroundingOffsetY = -0.212f;

    [MenuItem("Kamen Rider/Cleanup Third Person Cameras")]
    public static void CleanupThirdPersonCameras()
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            RemoveQuestThirdPersonCameraToggle(root);
            RemoveGeneratedThirdPersonCameras(root.transform);
            DisableControllerVisualObjects(root.transform);
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[MinamiKotaroSetup] Third person camera cleanup complete.");
    }

    [MenuItem("Kamen Rider/Cleanup Old MinamiKotaro Player Instances")]
    public static void CleanupOldPlayerInstances()
    {
        var xrOrigin = FindSceneObjectByNameContains("XR Origin");
        if (!xrOrigin)
        {
            Debug.LogError("[MinamiKotaroSetup] Could not find XR Origin in the active scene.");
            return;
        }

        RemoveOldMinamiKotaroInstancesInScene();
        EditorSceneManager.MarkSceneDirty(xrOrigin.scene);
        Debug.Log("[MinamiKotaroSetup] Old MinamiKotaro player instances cleanup complete.");
    }

    [MenuItem("Kamen Rider/Validate MinamiKotaro Player Skinning")]
    public static void ValidatePlayerSkinning()
    {
        var body = FindSceneObjectByNameContains(BodyName);
        if (!body)
        {
            Debug.LogError($"[MinamiKotaroSetup] Could not find {BodyName} in the active scene.");
            return;
        }

        var rigHumanoid = FindChildByName(body.transform, "rig_humanoid");
        if (!rigHumanoid)
        {
            Debug.LogError($"[MinamiKotaroSetup] {BodyName} does not contain rig_humanoid.");
            return;
        }

        var renderers = body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogError($"[MinamiKotaroSetup] {BodyName} has no SkinnedMeshRenderer.");
            return;
        }

        foreach (var renderer in renderers)
        {
            var rootBonePath = renderer.rootBone ? GetRelativePath(body.transform, renderer.rootBone) : "<none>";
            var rigHumanoidBoneCount = 0;
            var oldRigBoneCount = 0;

            foreach (var bone in renderer.bones)
            {
                if (!bone)
                {
                    continue;
                }

                if (IsDescendantOf(bone, rigHumanoid))
                {
                    rigHumanoidBoneCount++;
                }
                else if (FindAncestorByName(bone, "rig", body.transform))
                {
                    oldRigBoneCount++;
                }
            }

            Debug.Log($"[MinamiKotaroSetup] Skinning check: renderer={renderer.name}, rootBone={rootBonePath}, totalBones={renderer.bones.Length}, rigHumanoidBones={rigHumanoidBoneCount}, oldRigBones={oldRigBoneCount}.");
        }
    }

    [MenuItem("Kamen Rider/Setup MinamiKotaro Player Avatar")]
    public static void Setup()
    {
        KamenRiderMinamiKotaroImport.ConfigureKotaroFbxPlayerModel();

        var xrOrigin = FindSceneObjectByNameContains("XR Origin");
        if (!xrOrigin)
        {
            Debug.LogError("[MinamiKotaroSetup] Could not find XR Origin in the active scene.");
            return;
        }

        RemoveOldMinamiKotaroInstancesInScene();

        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (!modelPrefab)
        {
            Debug.LogError($"[MinamiKotaroSetup] Model not found: {ModelPath}");
            return;
        }

        var body = PrefabUtility.InstantiatePrefab(modelPrefab, xrOrigin.transform) as GameObject;
        if (!body)
        {
            Debug.LogError($"[MinamiKotaroSetup] Could not instantiate model: {ModelPath}");
            return;
        }

        Undo.RegisterCreatedObjectUndo(body, "Create MinamiKotaro player body");
        body.name = BodyName;
        Undo.RecordObject(body, "Enable MinamiKotaro player body");
        Undo.RecordObject(body.transform, "Place MinamiKotaro player body");
        body.transform.SetParent(xrOrigin.transform, worldPositionStays: false);
        body.transform.localPosition = new Vector3(0f, KotaroGroundingOffsetY, 0f);
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one;
        body.SetActive(true);
        RemoveEmbeddedCamerasAndLights(body.transform);
        NormalizeKotaroChildNames(body.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(body.transform);

        var avatar = xrOrigin.GetComponent<VrPlayerAvatarDriver>();
        if (!avatar)
        {
            avatar = Undo.AddComponent<VrPlayerAvatarDriver>(xrOrigin);
        }

        Undo.RecordObject(avatar, "Configure MinamiKotaro player avatar");
        avatar.modelRoot = body.transform;
        avatar.animator = FindAnimatorOnChild(body.transform, "rig_humanoid");
        if (!avatar.animator)
        {
            avatar.animator = body.GetComponentInChildren<Animator>(true);
        }

        avatar.headTarget = FindChildByName(xrOrigin.transform, "Main Camera");
        if (!avatar.headTarget && Camera.main)
        {
            avatar.headTarget = Camera.main.transform;
        }

        avatar.leftHandTarget = FindChildByName(xrOrigin.transform, "Left Controller");
        avatar.rightHandTarget = FindChildByName(xrOrigin.transform, "Right Controller");
        avatar.headForwardOffset = 0.22f;
        avatar.useAvatarHeadAnchor = false;
        avatar.avatarEyeLocalOffset = Vector3.zero;
        avatar.autoScaleAvatarToPlayerHeight = false;
        avatar.minAvatarScale = 0.8f;
        avatar.maxAvatarScale = 1.25f;
        avatar.bodyPositionOffset = new Vector3(0f, KotaroGroundingOffsetY, 0f);
        avatar.modelYawOffset = 180f;
        avatar.useBodyYawDeadZone = true;
        avatar.bodyYawFollowAngle = 35f;
        avatar.bodyYawSharpness = 7f;
        avatar.alignFeetToGround = false;
        avatar.groundY = xrOrigin.transform.position.y;
        avatar.footGroundOffset = 0f;
        avatar.useGroundRaycast = true;
        avatar.groundRaycastHeight = 3f;
        avatar.groundRaycastDistance = 8f;
        avatar.handPositionWeight = 0.95f;
        avatar.handRotationWeight = 0.35f;
        avatar.armMaxReachMultiplier = 0.9995f;
        avatar.hideHeadBone = false;
        avatar.hideControllerVisuals = true;
        avatar.enableHeadTracking = true;
        avatar.enableHandTracking = true;
        avatar.enableControllerFingerCurl = true;

        if (!avatar.animator)
        {
            Debug.LogWarning($"[MinamiKotaroSetup] Animator not found under {BodyName}. ARP transform fallback will be used if bones exist.");
        }
        else if (!avatar.animator.isHuman)
        {
            Debug.LogWarning($"[MinamiKotaroSetup] Animator under {BodyName} is not Humanoid. ARP transform fallback will be used.");
        }

        RemoveQuestThirdPersonCameraToggle(xrOrigin);
        RemoveGeneratedThirdPersonCameras(xrOrigin.transform);
        RemoveExtraCamerasInXrOrigin(xrOrigin.transform, avatar.headTarget);
        DisableControllerVisualObjects(xrOrigin.transform);

        EditorUtility.SetDirty(avatar);
        EditorUtility.SetDirty(body);
        Selection.activeGameObject = body;
        EditorSceneManager.MarkSceneDirty(xrOrigin.scene);
        Debug.Log($"[MinamiKotaroSetup] Player avatar setup complete on {xrOrigin.name}.");
    }

    private static GameObject FindSceneObjectByNameContains(string namePart)
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            var match = FindChildByNameContains(root.transform, namePart);
            if (match)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static GameObject FindDirectChild(Transform parent, string childName)
    {
        for (var index = 0; index < parent.childCount; index++)
        {
            var child = parent.GetChild(index);
            if (child.name == childName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindChildByNameContains(Transform root, string namePart)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Contains(namePart))
            {
                return child;
            }
        }

        return null;
    }

    private static Animator FindAnimatorOnChild(Transform root, string childName)
    {
        var child = FindChildByName(root, childName);
        return child ? child.GetComponent<Animator>() : null;
    }

    private static bool IsDescendantOf(Transform child, Transform ancestor)
    {
        for (var current = child; current; current = current.parent)
        {
            if (current == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    private static Transform FindAncestorByName(Transform child, string objectName, Transform stopAt)
    {
        for (var current = child.parent; current && current != stopAt; current = current.parent)
        {
            if (current.name == objectName)
            {
                return current;
            }
        }

        return null;
    }

    private static string GetRelativePath(Transform root, Transform child)
    {
        if (!child)
        {
            return string.Empty;
        }

        var path = child.name;
        for (var current = child.parent; current && current != root; current = current.parent)
        {
            path = current.name + "/" + path;
        }

        return path;
    }

    private static void RemoveQuestThirdPersonCameraToggle(GameObject xrOrigin)
    {
        foreach (var thirdPerson in xrOrigin.GetComponentsInChildren<QuestThirdPersonCameraToggle>(true))
        {
            Undo.DestroyObjectImmediate(thirdPerson);
        }
    }

    private static void RemoveGeneratedThirdPersonCameras(Transform xrOrigin)
    {
        foreach (var child in xrOrigin.GetComponentsInChildren<Transform>(true))
        {
            if (child != xrOrigin && child.name == "Third Person Debug Camera")
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
    }

    private static void RemoveExtraCamerasInXrOrigin(Transform xrOrigin, Transform mainCamera)
    {
        foreach (var camera in xrOrigin.GetComponentsInChildren<Camera>(true))
        {
            if (mainCamera && camera.transform == mainCamera)
            {
                continue;
            }

            Undo.DestroyObjectImmediate(camera.gameObject);
        }
    }

    private static void RemoveOldMinamiKotaroInstances(Transform xrOrigin)
    {
        var candidates = xrOrigin.GetComponentsInChildren<Transform>(true);
        for (var index = candidates.Length - 1; index >= 0; index--)
        {
            var child = candidates[index];
            if (child == xrOrigin)
            {
                continue;
            }

            if (IsOldMinamiKotaroInstanceName(child.name))
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
    }

    private static void RemoveOldMinamiKotaroInstancesInScene()
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (IsOldMinamiKotaroInstanceName(root.name))
            {
                Undo.DestroyObjectImmediate(root);
                continue;
            }

            RemoveOldMinamiKotaroInstances(root.transform);
        }
    }

    private static bool IsOldMinamiKotaroInstanceName(string objectName)
    {
        return objectName == BodyName
            || objectName == "kotaro"
            || objectName.StartsWith("kotaro ")
            || objectName.StartsWith("kotaro(")
            || objectName == "kotaro_game"
            || objectName.StartsWith("kotaro_game ")
            || objectName.StartsWith("kotaro_game(")
            || objectName == "kotaro_unity_direct"
            || objectName.StartsWith("kotaro_unity_direct ")
            || objectName.StartsWith("kotaro_unity_direct(")
            || objectName == "kotarotest"
            || objectName.StartsWith("kotarotest ")
            || objectName.StartsWith("kotarotest(")
            || objectName == "MinamiKotaro"
            || objectName.StartsWith("MinamiKotaro_")
            || objectName.StartsWith("PlayerBody_MinamiKotaro");
    }

    private static void RemoveEmbeddedCamerasAndLights(Transform modelRoot)
    {
        foreach (var camera in modelRoot.GetComponentsInChildren<Camera>(true))
        {
            Undo.DestroyObjectImmediate(camera.gameObject);
        }

        foreach (var light in modelRoot.GetComponentsInChildren<Light>(true))
        {
            Undo.DestroyObjectImmediate(light.gameObject);
        }

        var children = modelRoot.GetComponentsInChildren<Transform>(true);
        for (var index = children.Length - 1; index >= 0; index--)
        {
            var child = children[index];
            if (child == modelRoot)
            {
                continue;
            }

            if (child.name == "Camera" || child.name == "Light")
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
    }

    private static void NormalizeKotaroChildNames(Transform modelRoot)
    {
        foreach (var child in modelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "MinamiKotaro_Rerig_Mesh")
            {
                Undo.RecordObject(child.gameObject, "Rename Kotaro mesh");
                child.name = "Kotaro_ARP_Mesh";
                EditorUtility.SetDirty(child.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            }
        }
    }

    private static void DisableControllerVisualObjects(Transform xrOrigin)
    {
        foreach (var child in xrOrigin.GetComponentsInChildren<Transform>(true))
        {
            if (child == xrOrigin)
            {
                continue;
            }

            if (IsControllerVisualObjectName(child.name) && child.gameObject.activeSelf)
            {
                Undo.RecordObject(child.gameObject, "Disable controller visual object");
                child.gameObject.SetActive(false);
                EditorUtility.SetDirty(child.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            }
        }
    }

    private static bool IsControllerVisualObjectName(string objectName)
    {
        return objectName.Contains("Controller Visual")
            || objectName.Contains("ControllerVisual")
            || objectName.Contains("Controller Model")
            || objectName.Contains("ControllerModel")
            || objectName == "XR Controller Left"
            || objectName == "XR Controller Right"
            || objectName == "XRController_Thumbstick_Buttons";
    }

}
