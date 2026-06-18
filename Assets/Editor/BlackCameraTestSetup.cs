using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BlackCameraTestSetup
{
    private const string BlackModelPath = "Assets/Characters/Black/Models/Black.fbx";
    private const string SceneRootBlackBodyName = "Black";
    private const string TestBodyName = "Black_PlayerTest";
    private const string LegacyCameraTestBodyName = "Black_CameraTest";
    private const string MinamiBodyName = "PlayerBody_MinamiKotaro";
    private const string LeftGripPoseName = "Black Left Grip Pose";
    private const string RightGripPoseName = "Black Right Grip Pose";
    private const string LeftRotationOffsetName = "Left Hand Rotation Offset";
    private const string RightRotationOffsetName = "Right Hand Rotation Offset";
    private const string LeftWristTargetName = "Left Wrist Target";
    private const string RightWristTargetName = "Right Wrist Target";
    private const string ArmRigName = "Black Arm IK Rig";
    private const string LeftArmIkName = "Black Left Arm Two Bone IK";
    private const string RightArmIkName = "Black Right Arm Two Bone IK";
    private const string LeftElbowHintName = "Black Left Elbow Hint";
    private const string RightElbowHintName = "Black Right Elbow Hint";

    [MenuItem("Kamen Rider/Setup Black Camera Test Avatar")]
    public static void Setup()
    {
        AssetDatabase.ImportAsset(BlackModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        var xrOrigin = FindSceneObjectByNameContains("XR Origin");
        if (!xrOrigin)
        {
            Debug.LogError("[BlackCameraTest] Could not find XR Origin in the active scene.");
            return;
        }

        var mainCamera = FindChildByName(xrOrigin.transform, "Main Camera");
        if (!mainCamera && Camera.main)
        {
            mainCamera = Camera.main.transform;
        }

        if (!mainCamera)
        {
            Debug.LogError("[BlackCameraTest] Could not find Main Camera.");
            return;
        }

        DisableMinamiKotaroPlayer(xrOrigin);
        RemoveExistingGeneratedBlackTestsInScene();

        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlackModelPath);
        if (!modelPrefab)
        {
            Debug.LogError($"[BlackCameraTest] Model not found: {BlackModelPath}");
            return;
        }

        var body = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
        if (!body)
        {
            Debug.LogError($"[BlackCameraTest] Could not instantiate model: {BlackModelPath}");
            return;
        }

        Undo.RegisterCreatedObjectUndo(body, "Create Black camera test body");
        body.name = TestBodyName;
        Undo.RecordObject(body.transform, "Place Black camera test body");
        body.transform.SetParent(null, worldPositionStays: false);
        body.transform.position = xrOrigin.transform.position;
        body.transform.rotation = Quaternion.identity;
        body.transform.localScale = Vector3.one;
        body.SetActive(true);
        RemoveLegacyAvatarDrivers(body.transform);

        var avatar = xrOrigin.GetComponent<VrPlayerAvatarDriver>();
        if (!avatar)
        {
            avatar = Undo.AddComponent<VrPlayerAvatarDriver>(xrOrigin);
        }

        Undo.RecordObject(avatar, "Configure Black player test avatar");
        avatar.enabled = true;
        avatar.modelRoot = body.transform;
        avatar.animator = body.GetComponentInChildren<Animator>(true);
        avatar.headTarget = mainCamera;
        avatar.leftHandTarget = null;
        avatar.rightHandTarget = null;
        avatar.standingEyeHeight = 1.62f;
        avatar.headForwardOffset = 0.22f;
        avatar.useAvatarHeadAnchor = true;
        avatar.avatarEyeLocalOffset = Vector3.zero;
        avatar.autoScaleAvatarToPlayerHeight = true;
        avatar.minAvatarScale = 0.8f;
        avatar.maxAvatarScale = 1.25f;
        avatar.bodyPositionOffset = Vector3.zero;
        avatar.modelYawOffset = 0f;
        avatar.useBodyYawDeadZone = true;
        avatar.bodyYawFollowAngle = 35f;
        avatar.bodyYawSharpness = 7f;
        avatar.alignFeetToGround = true;
        avatar.groundY = xrOrigin.transform.position.y;
        avatar.footGroundOffset = 0f;
        avatar.useGroundRaycast = true;
        avatar.groundRaycastHeight = 3f;
        avatar.groundRaycastDistance = 8f;
        avatar.handPositionWeight = 1f;
        avatar.handRotationWeight = 0.65f;
        avatar.leftWristToGripOffset = Vector3.zero;
        avatar.rightWristToGripOffset = Vector3.zero;
        avatar.leftHandBodySpaceOffset = Vector3.zero;
        avatar.rightHandBodySpaceOffset = Vector3.zero;
        avatar.leftHandRotationOffsetEuler = Vector3.zero;
        avatar.rightHandRotationOffsetEuler = Vector3.zero;
        avatar.armMaxReachMultiplier = 0.9995f;
        avatar.shoulderAimWeight = 0.45f;
        avatar.handDirectionAlignmentWeight = 0.65f;
        avatar.leftHumanoidGripFingerCurlEuler = new Vector3(-55f, 0f, 0f);
        avatar.rightHumanoidGripFingerCurlEuler = new Vector3(55f, 0f, 0f);
        avatar.leftHumanoidTriggerFingerCurlEuler = new Vector3(-50f, 0f, 0f);
        avatar.rightHumanoidTriggerFingerCurlEuler = new Vector3(50f, 0f, 0f);
        avatar.leftHumanoidThumbFingerCurlEuler = new Vector3(20f, 0f, -35f);
        avatar.rightHumanoidThumbFingerCurlEuler = new Vector3(-20f, 0f, -35f);
        avatar.closeHumanoidTriggerFingersWithGrip = true;
        avatar.hideHeadBone = true;
        avatar.hideControllerVisuals = false;
        avatar.enableHeadTracking = true;
        avatar.enableHandTracking = false;
        avatar.enableControllerFingerCurl = false;
        avatar.useAnimationRiggingArmIk = false;
        avatar.leftArmIkConstraint = null;
        avatar.rightArmIkConstraint = null;
        avatar.leftElbowHint = null;
        avatar.rightElbowHint = null;

        EditorUtility.SetDirty(avatar);
        EditorUtility.SetDirty(body);
        Selection.activeGameObject = body;
        EditorSceneManager.MarkSceneDirty(xrOrigin.scene);
        Debug.Log("[BlackCameraTest] Black model configured without wrist targets, controller grip poses, or arm IK. Configure hands manually.");
    }

    [MenuItem("Kamen Rider/Cleanup Black Wrist Controller IK Config")]
    public static void CleanupBlackWristControllerIkConfig()
    {
        var xrOrigin = FindSceneObjectByNameContains("XR Origin");
        if (!xrOrigin)
        {
            Debug.LogError("[BlackCameraTest] Could not find XR Origin in the active scene.");
            return;
        }

        var avatar = xrOrigin.GetComponent<VrPlayerAvatarDriver>();
        if (avatar)
        {
            Undo.RecordObject(avatar, "Clear Black wrist/controller IK config");
            avatar.leftHandTarget = null;
            avatar.rightHandTarget = null;
            avatar.enableHandTracking = false;
            avatar.useAnimationRiggingArmIk = false;
            avatar.leftArmIkConstraint = null;
            avatar.rightArmIkConstraint = null;
            avatar.leftElbowHint = null;
            avatar.rightElbowHint = null;
            avatar.leftWristToGripOffset = Vector3.zero;
            avatar.rightWristToGripOffset = Vector3.zero;
            avatar.leftHandBodySpaceOffset = Vector3.zero;
            avatar.rightHandBodySpaceOffset = Vector3.zero;
            avatar.leftHandRotationOffsetEuler = Vector3.zero;
            avatar.rightHandRotationOffsetEuler = Vector3.zero;
            avatar.enableControllerFingerCurl = false;
            avatar.hideControllerVisuals = false;
            EditorUtility.SetDirty(avatar);
        }

        DestroyNamedObjects(
            xrOrigin.transform,
            LeftGripPoseName,
            RightGripPoseName,
            LeftRotationOffsetName,
            RightRotationOffsetName,
            LeftWristTargetName,
            RightWristTargetName,
            ArmRigName,
            LeftArmIkName,
            RightArmIkName,
            LeftElbowHintName,
            RightElbowHintName);

        EditorSceneManager.MarkSceneDirty(xrOrigin.scene);
        Debug.Log("[BlackCameraTest] Cleared Black wrist targets, controller grip poses, and arm IK config.");
    }

    [MenuItem("Kamen Rider/Cleanup Black Camera Test Avatar")]
    public static void Cleanup()
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            RemoveExistingBlackTest(root.transform);
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[BlackCameraTest] Black camera test cleanup complete.");
    }

    private static void DisableMinamiKotaroPlayer(GameObject xrOrigin)
    {
        var avatar = xrOrigin.GetComponent<VrPlayerAvatarDriver>();
        if (avatar)
        {
            Undo.RecordObject(avatar, "Disable MinamiKotaro avatar driver");
            avatar.enabled = false;
            EditorUtility.SetDirty(avatar);
        }

        var minamiBody = FindChildByName(xrOrigin.transform, MinamiBodyName);
        if (minamiBody)
        {
            Undo.RecordObject(minamiBody.gameObject, "Disable MinamiKotaro player body");
            minamiBody.gameObject.SetActive(false);
            EditorUtility.SetDirty(minamiBody.gameObject);
        }
    }

    private static void RemoveExistingBlackTest(Transform root)
    {
        if (IsGeneratedBlackTestBodyName(root.name))
        {
            Undo.DestroyObjectImmediate(root.gameObject);
            return;
        }

        var children = root.GetComponentsInChildren<Transform>(true);
        for (var index = children.Length - 1; index >= 0; index--)
        {
            var child = children[index];
            if (child != root && IsGeneratedBlackTestBodyName(child.name))
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
    }

    private static void RemoveExistingGeneratedBlackTestsInScene()
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            RemoveExistingBlackTest(root.transform);
        }
    }

    private static bool IsGeneratedBlackTestBodyName(string objectName)
    {
        return objectName == TestBodyName
            || objectName == LegacyCameraTestBodyName;
    }

    private static void DisableSceneRootBlackBodies()
    {
        SetSceneRootBlackBodiesActive(false);
    }

    private static void EnableSceneRootBlackBodies()
    {
        SetSceneRootBlackBodiesActive(true);
    }

    private static void SetSceneRootBlackBodiesActive(bool active)
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != SceneRootBlackBodyName || root.activeSelf == active)
            {
                continue;
            }

            Undo.RecordObject(root, active ? "Enable preserved Black body" : "Disable preserved Black body");
            root.SetActive(active);
            EditorUtility.SetDirty(root);
        }
    }

    private static void RemoveLegacyAvatarDrivers(Transform root)
    {
        foreach (var legacyDriver in root.GetComponentsInChildren<IKTargetFollowVRRig>(true))
        {
            Undo.DestroyObjectImmediate(legacyDriver);
        }
    }

    private static void DestroyNamedObjects(Transform root, params string[] names)
    {
        var children = root.GetComponentsInChildren<Transform>(true);
        for (var index = children.Length - 1; index >= 0; index--)
        {
            var child = children[index];
            foreach (var name in names)
            {
                if (child.name == name)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                    break;
                }
            }
        }
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
}
