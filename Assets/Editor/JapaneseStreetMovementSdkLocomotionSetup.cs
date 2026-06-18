using System;
using System.Collections.Generic;
using System.Linq;
using Oculus.Movement.Locomotion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class JapaneseStreetMovementSdkLocomotionSetup
{
    private const string ScenePath = "Assets/Scenes/JapaneseStreetVR.unity";
    private const string PlayerControllerName = "PlayerController";

    [MenuItem("Kamen Rider/Setup Japanese Street MovementSDK Locomotion")]
    public static void SetupJapaneseStreetScene()
    {
        EnsureEditMode();
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
        }

        SetupActiveScene();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    [MenuItem("Kamen Rider/Setup Active Scene MovementSDK Locomotion")]
    public static void SetupActiveSceneMenu()
    {
        EnsureEditMode();
        SetupActiveScene();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    private static void SetupActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        var cameraRig = FindObjectInScene<OVRCameraRig>(scene);
        if (cameraRig == null)
        {
            throw new InvalidOperationException("Could not find an OVRCameraRig in the active scene.");
        }

        var playerController = FindSceneObjectByName(scene, PlayerControllerName);
        if (playerController == null)
        {
            playerController = new GameObject(PlayerControllerName);
            Undo.RegisterCreatedObjectUndo(playerController, "Create PlayerController");
            playerController.transform.SetPositionAndRotation(
                cameraRig.transform.position,
                FlattenYaw(cameraRig.transform.rotation));
        }

        Undo.RecordObject(playerController.transform, "Configure PlayerController transform");
        if (playerController.transform.parent != null)
        {
            playerController.transform.SetParent(null, true);
        }

        ConfigurePhysics(playerController, out var rigidbody, out var footCollider);
        var locomotion = ConfigureMovementSdkLocomotion(playerController, rigidbody, footCollider, cameraRig);
        ParentCameraRigToPlayerController(cameraRig, playerController.transform);
        var driver = MoveAvatarDriverToPlayerController(scene, playerController, cameraRig);
        var bridge = ConfigureInputBridge(playerController, locomotion, driver);
        DisableCompetingXriLocomotion(scene, bridge);

        EditorUtility.SetDirty(playerController);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = playerController;
        Debug.Log(
            "[JapaneseStreetMovementSDK] Configured PlayerController with Rigidbody, Collider, " +
            "MovementSDKLocomotion, OVRCameraRig parenting, and OVR thumbstick input.");
    }

    private static void ConfigurePhysics(
        GameObject playerController,
        out Rigidbody rigidbody,
        out Collider footCollider)
    {
        rigidbody = playerController.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = Undo.AddComponent<Rigidbody>(playerController);
        }

        Undo.RecordObject(rigidbody, "Configure PlayerController Rigidbody");
        rigidbody.useGravity = true;
        rigidbody.isKinematic = false;
        rigidbody.freezeRotation = true;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var bodyCollider = playerController.GetComponent<CapsuleCollider>();
        if (bodyCollider == null)
        {
            bodyCollider = Undo.AddComponent<CapsuleCollider>(playerController);
        }

        Undo.RecordObject(bodyCollider, "Configure PlayerController body collider");
        bodyCollider.isTrigger = true;
        bodyCollider.radius = 0.25f;
        bodyCollider.height = 1.75f;
        bodyCollider.center = new Vector3(0.0f, 0.875f, 0.0f);
        bodyCollider.direction = 1;

        var sphereCollider = playerController.GetComponent<SphereCollider>();
        if (sphereCollider == null)
        {
            sphereCollider = Undo.AddComponent<SphereCollider>(playerController);
        }

        Undo.RecordObject(sphereCollider, "Configure PlayerController foot collider");
        sphereCollider.isTrigger = false;
        sphereCollider.radius = 0.125f;
        sphereCollider.center = new Vector3(0.0f, 0.125f, 0.0f);
        footCollider = sphereCollider;

        EditorUtility.SetDirty(rigidbody);
        EditorUtility.SetDirty(bodyCollider);
        EditorUtility.SetDirty(sphereCollider);
    }

    private static MovementSDKLocomotion ConfigureMovementSdkLocomotion(
        GameObject playerController,
        Rigidbody rigidbody,
        Collider footCollider,
        OVRCameraRig cameraRig)
    {
        var locomotion = playerController.GetComponent<MovementSDKLocomotion>();
        if (locomotion == null)
        {
            locomotion = Undo.AddComponent<MovementSDKLocomotion>(playerController);
        }

        var so = new SerializedObject(locomotion);
        RequireProperty(so, "_rigidbody").objectReferenceValue = rigidbody;
        RequireProperty(so, "_collider").objectReferenceValue = footCollider;
        RequireProperty(so, "_enableMovement").boolValue = true;
        RequireProperty(so, "_enableRotation").boolValue = true;
        RequireProperty(so, "_scaleInputByActualVelocity").boolValue = false;
        RequireProperty(so, "_rotationAngle").floatValue = 45.0f;
        RequireProperty(so, "_rotationPerSecond").floatValue = 180.0f;
        RequireProperty(so, "_speed").floatValue = 3.0f;
        RequireProperty(so, "_cameraRig").objectReferenceValue = cameraRig;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(locomotion);
        return locomotion;
    }

    private static void ParentCameraRigToPlayerController(OVRCameraRig cameraRig, Transform playerController)
    {
        Undo.RecordObject(cameraRig.transform, "Parent OVRCameraRig to PlayerController");
        var worldPosition = cameraRig.transform.position;
        var worldRotation = cameraRig.transform.rotation;

        if (playerController.position == Vector3.zero)
        {
            playerController.SetPositionAndRotation(worldPosition, FlattenYaw(worldRotation));
        }

        cameraRig.transform.SetParent(playerController, true);
        cameraRig.transform.localPosition = Vector3.zero;
        cameraRig.transform.localRotation = Quaternion.identity;
        cameraRig.transform.localScale = Vector3.one;
        EditorUtility.SetDirty(cameraRig.transform);
    }

    private static VrPlayerAvatarDriver MoveAvatarDriverToPlayerController(
        Scene scene,
        GameObject playerController,
        OVRCameraRig cameraRig)
    {
        var existingDrivers = FindObjectsInScene<VrPlayerAvatarDriver>(scene);
        var targetDriver = playerController.GetComponent<VrPlayerAvatarDriver>();
        var sourceDriver = existingDrivers.FirstOrDefault(driver => driver != null && driver.gameObject != playerController);

        if (targetDriver == null)
        {
            targetDriver = Undo.AddComponent<VrPlayerAvatarDriver>(playerController);
            if (sourceDriver != null)
            {
                EditorUtility.CopySerialized(sourceDriver, targetDriver);
            }
        }

        if (sourceDriver != null)
        {
            Undo.DestroyObjectImmediate(sourceDriver);
        }

        Undo.RecordObject(targetDriver, "Configure PlayerController avatar driver");
        targetDriver.enabled = true;
        targetDriver.headTarget = cameraRig.centerEyeAnchor != null ? cameraRig.centerEyeAnchor : Camera.main?.transform;
        targetDriver.leftHandTarget = cameraRig.leftHandAnchor;
        targetDriver.rightHandTarget = cameraRig.rightHandAnchor;
        var officialUpperBodyRetargeting = HasOfficialBodyRetargeting(targetDriver);
        targetDriver.enableHeadTracking = !officialUpperBodyRetargeting;
        targetDriver.enableHandTracking = !officialUpperBodyRetargeting;
        targetDriver.enableControllerFingerCurl = !officialUpperBodyRetargeting;
        targetDriver.useAnimationRiggingArmIk = false;
        targetDriver.hideControllerVisuals = true;
        targetDriver.alignFeetToGround = true;

        if (targetDriver.modelRoot == null)
        {
            targetDriver.modelRoot = FindPreferredBody(scene);
        }

        if (targetDriver.animator == null && targetDriver.modelRoot != null)
        {
            targetDriver.animator = targetDriver.modelRoot.GetComponentInChildren<Animator>(true);
        }

        EditorUtility.SetDirty(targetDriver);
        return targetDriver;
    }

    private static bool HasOfficialBodyRetargeting(VrPlayerAvatarDriver avatarDriver)
    {
        if (avatarDriver == null || avatarDriver.modelRoot == null)
        {
            return false;
        }

        return avatarDriver.modelRoot.GetComponentsInChildren<MonoBehaviour>(true)
            .Any(component => component != null && component.GetType().Name == "RetargetingLayer");
    }

    private static MovementSdkOvrThumbstickInput ConfigureInputBridge(
        GameObject playerController,
        MovementSDKLocomotion locomotion,
        VrPlayerAvatarDriver avatarDriver)
    {
        var bridge = playerController.GetComponent<MovementSdkOvrThumbstickInput>();
        if (bridge == null)
        {
            bridge = Undo.AddComponent<MovementSdkOvrThumbstickInput>(playerController);
        }

        var animators = ResolveAnimators(avatarDriver);
        var so = new SerializedObject(bridge);
        RequireProperty(so, "_locomotion").objectReferenceValue = locomotion;
        SetEnumProperty(RequireProperty(so, "_moveAxis"), nameof(OVRInput.Axis2D.PrimaryThumbstick));
        SetEnumProperty(RequireProperty(so, "_fallbackMoveAxis"), nameof(OVRInput.Axis2D.SecondaryThumbstick));
        RequireProperty(so, "_deadZone").floatValue = 0.12f;
        RequireProperty(so, "_invertX").boolValue = false;
        RequireProperty(so, "_invertY").boolValue = false;
        RequireProperty(so, "_writeAnimatorParameters").boolValue = true;
        RequireProperty(so, "_horizontalParameter").stringValue = "Horizontal";
        RequireProperty(so, "_verticalParameter").stringValue = "Vertical";
        RequireProperty(so, "_moveSpeedParameter").stringValue = "MoveSpeed";
        RequireProperty(so, "_isMovingParameter").stringValue = "IsMoving";

        var animatorsProperty = RequireProperty(so, "_animators");
        animatorsProperty.arraySize = animators.Count;
        for (var i = 0; i < animators.Count; i++)
        {
            animatorsProperty.GetArrayElementAtIndex(i).objectReferenceValue = animators[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bridge);
        return bridge;
    }

    private static List<Animator> ResolveAnimators(VrPlayerAvatarDriver avatarDriver)
    {
        var results = new List<Animator>();
        if (avatarDriver != null)
        {
            if (avatarDriver.animator != null)
            {
                results.Add(avatarDriver.animator);
            }

            if (avatarDriver.modelRoot != null)
            {
                foreach (var animator in avatarDriver.modelRoot.GetComponentsInChildren<Animator>(true))
                {
                    if (animator != null && !results.Contains(animator))
                    {
                        results.Add(animator);
                    }
                }
            }
        }

        return results;
    }

    private static void DisableCompetingXriLocomotion(Scene scene, MovementSdkOvrThumbstickInput bridge)
    {
        foreach (var behaviour in FindObjectsInScene<MonoBehaviour>(scene))
        {
            if (behaviour == null || behaviour == bridge || !behaviour.enabled)
            {
                continue;
            }

            var typeName = behaviour.GetType().Name;
            if (typeName.Contains("ContinuousMoveProvider") ||
                typeName.Contains("ContinuousTurnProvider") ||
                typeName.Contains("SnapTurnProvider"))
            {
                Undo.RecordObject(behaviour, "Disable competing XRI locomotion provider");
                behaviour.enabled = false;
                EditorUtility.SetDirty(behaviour);
            }
        }
    }

    private static Transform FindPreferredBody(Scene scene)
    {
        var preferredNames = new[]
        {
            "Black_PlayerTest",
            "Black_CameraTest",
            "Black",
            "PlayerBody_MinamiKotaro"
        };

        foreach (var name in preferredNames)
        {
            var body = FindSceneTransformByName(scene, name);
            if (body != null && body.gameObject.activeInHierarchy)
            {
                return body;
            }
        }

        foreach (var name in preferredNames)
        {
            var body = FindSceneTransformByName(scene, name);
            if (body != null)
            {
                return body;
            }
        }

        return null;
    }

    private static Quaternion FlattenYaw(Quaternion rotation)
    {
        return Quaternion.Euler(0.0f, rotation.eulerAngles.y, 0.0f);
    }

    private static T FindObjectInScene<T>(Scene scene) where T : UnityEngine.Object
    {
        return FindObjectsInScene<T>(scene).FirstOrDefault();
    }

    private static List<T> FindObjectsInScene<T>(Scene scene) where T : UnityEngine.Object
    {
        var results = new List<T>();
        foreach (var root in scene.GetRootGameObjects())
        {
            results.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return results;
    }

    private static GameObject FindSceneObjectByName(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root;
            }

            var child = FindChildByName(root.transform, objectName);
            if (child != null)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static Transform FindSceneTransformByName(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root.transform;
            }

            var child = FindChildByName(root.transform, objectName);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string objectName)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    private static SerializedProperty RequireProperty(SerializedObject so, string path)
    {
        var property = so.FindProperty(path);
        if (property == null)
        {
            throw new InvalidOperationException($"Could not find serialized property {path} on {so.targetObject}.");
        }

        return property;
    }

    private static void SetEnumProperty(SerializedProperty property, string enumName)
    {
        for (var i = 0; i < property.enumNames.Length; i++)
        {
            if (property.enumNames[i] == enumName)
            {
                property.enumValueIndex = i;
                return;
            }
        }

        throw new InvalidOperationException($"Could not find enum value {enumName} for {property.propertyPath}.");
    }

    private static void EnsureEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("Exit Play Mode before configuring MovementSDK locomotion.");
        }
    }
}
