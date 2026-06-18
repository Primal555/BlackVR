using System;
using System.Collections.Generic;
using System.Linq;
using Oculus.Movement.AnimationRigging;
using Oculus.Movement.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;

public static class BlackOfficialUpperBodyRetargetingSetup
{
    private const string ScenePath = "Assets/Scenes/JapaneseStreetVR.unity";
    private const string PlayerControllerName = "PlayerController";
    private const string LocomotionSkeletonProcessorName = "BonesFollowLocomotionWithStaticCharacter";

    [MenuItem("Kamen Rider/Setup Active Black Official MovementLocomotion Retargeting")]
    public static void SetupActiveBlackMovementLocomotionRetargetingMenu()
    {
        EnsureEditMode();
        SetupActiveBlackMovementLocomotionRetargeting();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    [MenuItem("Kamen Rider/Setup Japanese Street Black Official MovementLocomotion Retargeting")]
    public static void SetupJapaneseStreetBlackMovementLocomotionRetargetingMenu()
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

        SetupActiveBlackMovementLocomotionRetargeting();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    [MenuItem("Kamen Rider/Setup Active Black Official Upper Body Retargeting")]
    public static void SetupActiveBlackUpperBodyRetargetingMenu()
    {
        EnsureEditMode();
        SetupActiveBlackMovementLocomotionRetargeting();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    [MenuItem("Kamen Rider/Setup Japanese Street Black Official Upper Body Retargeting")]
    public static void SetupJapaneseStreetBlackUpperBodyRetargetingMenu()
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

        SetupActiveBlackMovementLocomotionRetargeting();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    private static void SetupActiveBlackMovementLocomotionRetargeting()
    {
        var scene = SceneManager.GetActiveScene();
        var driver = FindObjectsInScene<VrPlayerAvatarDriver>(scene)
            .FirstOrDefault(candidate => candidate != null && candidate.enabled && candidate.modelRoot != null);
        var modelRoot = driver != null ? driver.modelRoot : FindActiveBlackRoot(scene);
        if (modelRoot == null)
        {
            throw new InvalidOperationException("Could not find the active Black model root in the active scene.");
        }

        var animator = ResolveHumanoidAnimator(driver, modelRoot);
        if (animator == null)
        {
            throw new InvalidOperationException(
                $"Could not find a humanoid Animator under {modelRoot.name}. Official Movement retargeting requires a Humanoid avatar.");
        }

        var setupObject = animator.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(setupObject, "Setup Black Official Upper Body Retargeting");

        AnimationUtilities.UpdateToAnimatorPose(animator, false);
        var restPoseObject = AddComponentsHelper.GetRestPoseObject(AddComponentsHelper.CheckIfTPose(animator));
        if (restPoseObject == null)
        {
            throw new InvalidOperationException("Could not find Meta Movement body tracking humanoid reference pose asset.");
        }

        ConfigureRuntimeSettingsForOfficialUpperBody();

        HelperMenusBody.SetupCharacterForAnimationRiggingRetargetingConstraints(
            setupObject,
            restPoseObject,
            addConstraints: true,
            isFullBody: false);

        var retargetingLayer = ConfigureOfficialUpperBodyLocomotionComponents(setupObject);
        DisableFullBodyDeformation(setupObject);
        ConfigureOfficialLocomotionSkeletonProcessor(scene, setupObject, retargetingLayer, driver);
        ConfigureLocalDriverForOfficialMovementLocomotion(driver, modelRoot);
        DisableLegacyUpperBodyRigHelpers(modelRoot);

        EditorUtility.SetDirty(setupObject);
        EditorUtility.SetDirty(modelRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = setupObject;

        Debug.Log(
            $"[BlackOfficialUpperBodyRetargeting] Installed official upper-body OVRBody/RetargetingLayer " +
            $"with the locomotion skeleton processor on {setupObject.name}. Lower-body animation remains animator-driven.");
    }

    private static void ConfigureRuntimeSettingsForOfficialUpperBody()
    {
        var runtimeSettings = OVRRuntimeSettings.GetRuntimeSettings();
        if (runtimeSettings == null)
        {
            return;
        }

        Undo.RecordObject(runtimeSettings, "Configure Movement SDK upper body runtime settings");
        runtimeSettings.BodyTrackingJointSet = OVRPlugin.BodyJointSet.UpperBody;
        runtimeSettings.BodyTrackingFidelity = OVRPlugin.BodyTrackingFidelity2.Low;
        EditorUtility.SetDirty(runtimeSettings);
    }

    private static RetargetingLayer ConfigureOfficialUpperBodyLocomotionComponents(GameObject setupObject)
    {
        var body = setupObject.GetComponent<OVRBody>();
        if (body != null)
        {
            Undo.RecordObject(body, "Configure OVRBody upper body joint set");
            body.ProvidedSkeletonType = OVRPlugin.BodyJointSet.UpperBody;
            EditorUtility.SetDirty(body);
        }

        var retargetingLayer = setupObject.GetComponent<RetargetingLayer>();
        if (retargetingLayer != null)
        {
            Undo.RecordObject(retargetingLayer, "Configure RetargetingLayer official upper body locomotion defaults");
            retargetingLayer.ApplyAnimationConstraintsToCorrectedPositions = true;
            retargetingLayer.EnableTrackingByProxy = false;
            var retargetingSo = new SerializedObject(retargetingLayer);
            RequireProperty(retargetingSo, "_skeletonType").intValue = (int)OVRSkeleton.SkeletonType.Body;
            retargetingSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(retargetingLayer);
        }

        var rigBuilder = setupObject.GetComponent<RigBuilder>();
        if (rigBuilder != null)
        {
            Undo.RecordObject(rigBuilder, "Enable official locomotion RigBuilder");
            rigBuilder.enabled = true;
            EditorUtility.SetDirty(rigBuilder);
        }

        return retargetingLayer;
    }

    private static void DisableFullBodyDeformation(GameObject setupObject)
    {
        foreach (var constraint in setupObject.GetComponentsInChildren<FullBodyDeformationConstraint>(true))
        {
            Undo.RecordObject(constraint, "Disable full body deformation for upper body retargeting");
            constraint.enabled = false;
            EditorUtility.SetDirty(constraint);
        }
    }

    private static void ConfigureOfficialLocomotionSkeletonProcessor(
        Scene scene,
        GameObject setupObject,
        RetargetingLayer retargetingLayer,
        VrPlayerAvatarDriver driver)
    {
        if (retargetingLayer == null)
        {
            return;
        }

        var playerRoot = ResolvePlayerController(scene, driver);
        var processorRoot = FindChildByName(playerRoot, LocomotionSkeletonProcessorName);
        if (processorRoot == null)
        {
            var processorObject = new GameObject(LocomotionSkeletonProcessorName);
            Undo.RegisterCreatedObjectUndo(processorObject, "Create official locomotion skeleton processor");
            Undo.SetTransformParent(processorObject.transform, playerRoot, "Parent official locomotion skeleton processor");
            processorRoot = processorObject.transform;
        }

        Undo.RecordObject(processorRoot, "Configure official locomotion skeleton processor transform");
        processorRoot.localPosition = Vector3.zero;
        processorRoot.localRotation = Quaternion.identity;
        processorRoot.localScale = Vector3.one;

        var translateProcessor = processorRoot.GetComponent<SkeletonTranslateProcessor>();
        if (translateProcessor == null)
        {
            translateProcessor = Undo.AddComponent<SkeletonTranslateProcessor>(processorRoot.gameObject);
        }

        Undo.RecordObject(translateProcessor, "Configure skeleton translate processor");
        translateProcessor.enabled = true;
        var translateSo = new SerializedObject(translateProcessor);
        RequireProperty(translateSo, "_transformOffsetForSkeleton").objectReferenceValue = playerRoot;
        translateSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(translateProcessor);

        var aggregator = setupObject.GetComponent<SkeletonProcessAggregator>();
        if (aggregator == null)
        {
            aggregator = Undo.AddComponent<SkeletonProcessAggregator>(setupObject);
        }

        Undo.RecordObject(aggregator, "Configure official locomotion skeleton process aggregator");
        aggregator.enabled = true;
        aggregator.SkeletonProcessors = new List<SkeletonProcessAggregator.Item>();
        aggregator.AddProcessor(translateProcessor);
        var aggregatorSo = new SerializedObject(aggregator);
        RequireProperty(aggregatorSo, "_autoAddTo").objectReferenceValue = retargetingLayer;
        aggregatorSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(aggregator);
    }

    private static void ConfigureLocalDriverForOfficialMovementLocomotion(VrPlayerAvatarDriver driver, Transform modelRoot)
    {
        if (driver == null)
        {
            return;
        }

        Undo.RecordObject(driver, "Disable direct upper body avatar driver");
        driver.enabled = true;
        driver.modelRoot = modelRoot;
        if (driver.animator == null)
        {
            driver.animator = modelRoot.GetComponentInChildren<Animator>(true);
        }

        driver.enableHeadTracking = false;
        driver.enableHandTracking = false;
        driver.enableControllerFingerCurl = false;
        driver.useAnimationRiggingArmIk = false;
        driver.hideControllerVisuals = true;
        driver.alignFeetToGround = true;
        EditorUtility.SetDirty(driver);
    }

    private static Transform ResolvePlayerController(Scene scene, VrPlayerAvatarDriver driver)
    {
        var playerController = FindSceneTransformByName(scene, PlayerControllerName);
        if (playerController != null)
        {
            return playerController;
        }

        if (driver != null)
        {
            return driver.transform;
        }

        throw new InvalidOperationException("Could not find PlayerController for official locomotion skeleton processor.");
    }

    private static void DisableLegacyUpperBodyRigHelpers(Transform modelRoot)
    {
        foreach (var helper in modelRoot.GetComponentsInChildren<IKTargetFollowVRRig>(true))
        {
            Undo.RecordObject(helper, "Disable legacy VR rig target follower");
            helper.enabled = false;
            EditorUtility.SetDirty(helper);
        }
    }

    private static Animator ResolveHumanoidAnimator(VrPlayerAvatarDriver driver, Transform modelRoot)
    {
        if (driver != null && IsUsableHumanoidAnimator(driver.animator))
        {
            return driver.animator;
        }

        var rootAnimator = modelRoot.GetComponent<Animator>();
        if (IsUsableHumanoidAnimator(rootAnimator))
        {
            return rootAnimator;
        }

        return modelRoot.GetComponentsInChildren<Animator>(true)
            .FirstOrDefault(IsUsableHumanoidAnimator);
    }

    private static bool IsUsableHumanoidAnimator(Animator animator)
    {
        return animator != null && animator.avatar != null && animator.avatar.isHuman;
    }

    private static Transform FindActiveBlackRoot(Scene scene)
    {
        var preferredNames = new[]
        {
            "Black_PlayerTest",
            "Black_CameraTest",
            "Black"
        };

        foreach (var name in preferredNames)
        {
            var transform = FindSceneTransformByName(scene, name);
            if (transform != null && transform.gameObject.activeInHierarchy)
            {
                return transform;
            }
        }

        return FindObjectsInScene<Animator>(scene)
            .Where(IsUsableHumanoidAnimator)
            .Select(animator => animator.transform)
            .FirstOrDefault(transform => transform.name.IndexOf("Black", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static Transform FindSceneTransformByName(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
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

    private static SerializedProperty RequireProperty(SerializedObject so, string propertyPath)
    {
        var property = so.FindProperty(propertyPath);
        if (property == null)
        {
            throw new InvalidOperationException($"Could not find serialized property {propertyPath} on {so.targetObject}.");
        }

        return property;
    }

    private static T[] FindObjectsInScene<T>(Scene scene) where T : UnityEngine.Object
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
    }

    private static void EnsureEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("Exit Play Mode before configuring Black official upper body retargeting.");
        }
    }
}
