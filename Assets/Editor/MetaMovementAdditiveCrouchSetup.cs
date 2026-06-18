using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;

public static class MetaMovementAdditiveCrouchSetup
{
    private const string ControllerPath =
        "Assets/Samples/Meta Movement/71.0.1/Advanced Samples/Locomotion/Animations/LocomotionController.controller";
    private const string OutputFolder = "Assets/MetaMovementCustom/LocomotionCrouch";
    private const string CrouchClipPath = OutputFolder + "/AdditiveCrouchPose.anim";
    private const string CrouchMaskPath = OutputFolder + "/CrouchLowerBody.mask";
    private const string CrouchParameter = "Crouch";
    private const string CrouchLayerName = "Additive Crouch";
    private const string GeneratedGroundingRigName = "LocomotionLegGroundingRig";
    private const string LegacyGroundingRigName = "GroundingIKRig";

    [MenuItem("Tools/Meta Movement/Locomotion/Add Additive Crouch Pose")]
    public static void AddAdditiveCrouchPoseMenu()
    {
        EnsureEditMode();
        AddAdditiveCrouchPose();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Meta Movement/Locomotion/Remove Additive Crouch Pose")]
    public static void RemoveAdditiveCrouchPoseMenu()
    {
        EnsureEditMode();
        RemoveAdditiveCrouchPose();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    private static void AddAdditiveCrouchPose()
    {
        EnsureOutputFolder();

        var clip = CreateOrUpdateCrouchClip();
        var mask = CreateOrUpdateCrouchMask();
        var controller = LoadController();
        AddOrUpdateControllerLayer(controller, clip, mask);

        var scene = SceneManager.GetActiveScene();
        var character = FindSceneObjectByName(scene, "Character");
        if (character == null)
        {
            throw new InvalidOperationException("Could not find a scene GameObject named Character.");
        }

        RemoveGeneratedGroundingRig(character.transform);

        var animator = FindCharacterAnimator(character);
        var driver = character.GetComponent<MetaMovementCrouchBlendDriver>();
        if (driver == null)
        {
            driver = Undo.AddComponent<MetaMovementCrouchBlendDriver>(character);
        }

        ConfigureDriver(driver, animator, FindHeadTransform(scene), FindHeightReference(scene, character));

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(character);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[MetaMovementCrouch] Added additive crouch layer and HMD-height driver.");
    }

    private static void RemoveAdditiveCrouchPose()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
        {
            controller.layers = controller.layers
                .Where(layer => layer.name != CrouchLayerName)
                .ToArray();

            var crouchParameter = controller.parameters.FirstOrDefault(parameter => parameter.name == CrouchParameter);
            if (crouchParameter != null)
            {
                controller.RemoveParameter(crouchParameter);
            }

            EditorUtility.SetDirty(controller);
        }

        var scene = SceneManager.GetActiveScene();
        var character = FindSceneObjectByName(scene, "Character");
        if (character != null)
        {
            var driver = character.GetComponent<MetaMovementCrouchBlendDriver>();
            if (driver != null)
            {
                Undo.DestroyObjectImmediate(driver);
            }

            EditorUtility.SetDirty(character);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[MetaMovementCrouch] Removed additive crouch layer and driver.");
    }

    private static void AddOrUpdateControllerLayer(
        AnimatorController controller,
        AnimationClip clip,
        AvatarMask mask)
    {
        if (controller.parameters.All(parameter => parameter.name != CrouchParameter))
        {
            controller.AddParameter(CrouchParameter, AnimatorControllerParameterType.Float);
        }

        var layerIndex = Array.FindIndex(controller.layers, layer => layer.name == CrouchLayerName);
        if (layerIndex < 0)
        {
            controller.AddLayer(CrouchLayerName);
            layerIndex = Array.FindIndex(controller.layers, layer => layer.name == CrouchLayerName);
        }

        var layers = controller.layers;
        var layer = layers[layerIndex];
        layer.defaultWeight = 0.0f;
        layer.blendingMode = AnimatorLayerBlendingMode.Additive;
        layer.avatarMask = mask;
        layer.iKPass = false;
        layer.syncedLayerIndex = -1;

        var stateMachine = layer.stateMachine;
        foreach (var childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }

        var state = stateMachine.AddState("CrouchPose", new Vector3(250.0f, 80.0f, 0.0f));
        state.motion = clip;
        state.writeDefaultValues = false;
        state.speed = 1.0f;
        stateMachine.defaultState = state;

        layers[layerIndex] = layer;
        controller.layers = layers;
    }

    private static AnimationClip CreateOrUpdateCrouchClip()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CrouchClipPath);
        if (clip == null)
        {
            clip = new AnimationClip
            {
                name = "AdditiveCrouchPose",
                frameRate = 30.0f,
                legacy = false,
                wrapMode = WrapMode.ClampForever
            };
            AssetDatabase.CreateAsset(clip, CrouchClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = 30.0f;
        clip.wrapMode = WrapMode.ClampForever;

        SetHumanoidMuscleCurve(clip, "Spine Front-Back", -0.06f);
        SetHumanoidMuscleCurve(clip, "Chest Front-Back", -0.04f);
        SetHumanoidMuscleCurve(clip, "Left Upper Leg Front-Back", 0.34f);
        SetHumanoidMuscleCurve(clip, "Right Upper Leg Front-Back", 0.34f);
        SetHumanoidMuscleCurve(clip, "Left Lower Leg Stretch", 0.48f);
        SetHumanoidMuscleCurve(clip, "Right Lower Leg Stretch", 0.48f);
        SetHumanoidMuscleCurve(clip, "Left Foot Up-Down", -0.18f);
        SetHumanoidMuscleCurve(clip, "Right Foot Up-Down", -0.18f);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void SetHumanoidMuscleCurve(AnimationClip clip, string muscleName, float value)
    {
        var curve = new AnimationCurve(
            new Keyframe(0.0f, value, 0.0f, 0.0f),
            new Keyframe(1.0f / 30.0f, value, 0.0f, 0.0f));
        curve.preWrapMode = WrapMode.ClampForever;
        curve.postWrapMode = WrapMode.ClampForever;

        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscleName),
            curve);
    }

    private static AvatarMask CreateOrUpdateCrouchMask()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(CrouchMaskPath);
        if (mask == null)
        {
            mask = new AvatarMask
            {
                name = "CrouchLowerBody"
            };
            AssetDatabase.CreateAsset(mask, CrouchMaskPath);
        }

        for (var i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        }

        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, true);

        EditorUtility.SetDirty(mask);
        return mask;
    }

    private static void ConfigureDriver(
        MetaMovementCrouchBlendDriver driver,
        Animator animator,
        Transform headTransform,
        Transform heightReference)
    {
        var so = new SerializedObject(driver);
        var animators = RequireProperty(so, "_animators");
        animators.arraySize = 1;
        animators.GetArrayElementAtIndex(0).objectReferenceValue = animator;
        RequireProperty(so, "_headTransform").objectReferenceValue = headTransform;
        RequireProperty(so, "_heightReference").objectReferenceValue = heightReference;
        RequireProperty(so, "_crouchAmountParameter").stringValue = CrouchParameter;
        RequireProperty(so, "_isCrouchingParameter").stringValue = "IsCrouching";
        RequireProperty(so, "_moveSpeedParameter").stringValue = "MoveSpeed";
        RequireProperty(so, "_horizontalParameter").stringValue = "Horizontal";
        RequireProperty(so, "_verticalParameter").stringValue = "Vertical";
        RequireProperty(so, "_crouchLayer").stringValue = CrouchLayerName;
        RequireProperty(so, "_calibrateStandingHeightOnStart").boolValue = true;
        RequireProperty(so, "_enterCrouchEyeHeightRatio").floatValue = 0.86f;
        RequireProperty(so, "_exitCrouchEyeHeightRatio").floatValue = 0.9f;
        RequireProperty(so, "_fullCrouchEyeHeightRatio").floatValue = 0.55f;
        RequireProperty(so, "_smoothSpeed").floatValue = 10.0f;
        RequireProperty(so, "_layerFadeStart").floatValue = 0.15f;
        RequireProperty(so, "_maxLayerWeight").floatValue = 0.75f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(driver);
    }

    private static void RemoveGeneratedGroundingRig(Transform character)
    {
        foreach (var animator in character.GetComponentsInChildren<Animator>(true))
        {
            var bodyRoot = animator.transform;
            var rigBuilder = bodyRoot.GetComponent<RigBuilder>();
            if (rigBuilder != null)
            {
                for (var i = rigBuilder.layers.Count - 1; i >= 0; i--)
                {
                    var layer = rigBuilder.layers[i];
                    if (layer == null || layer.rig == null ||
                        IsGeneratedGroundingRigName(layer.rig.gameObject.name))
                    {
                        rigBuilder.layers.RemoveAt(i);
                    }
                }

                EditorUtility.SetDirty(rigBuilder);
            }

            for (var i = bodyRoot.childCount - 1; i >= 0; i--)
            {
                var child = bodyRoot.GetChild(i);
                if (IsGeneratedGroundingRigName(child.name))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }
    }

    private static bool IsGeneratedGroundingRigName(string objectName)
    {
        return objectName == GeneratedGroundingRigName || objectName == LegacyGroundingRigName;
    }

    private static AnimatorController LoadController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            throw new InvalidOperationException($"Could not load Locomotion AnimatorController: {ControllerPath}");
        }

        return controller;
    }

    private static Animator FindCharacterAnimator(GameObject character)
    {
        var animators = character.GetComponentsInChildren<Animator>(true);
        if (animators.Length == 0)
        {
            throw new InvalidOperationException("Character does not contain an Animator.");
        }

        foreach (var animator in animators)
        {
            if (animator.gameObject.name == "ArmatureSkinningUpdateRetargetLocomotion")
            {
                return animator;
            }
        }

        return animators[0];
    }

    private static Transform FindHeadTransform(Scene scene)
    {
        return FindSceneTransformByName(scene, "CenterEyeAnchor") ??
               FindSceneTransformByName(scene, "Main Camera") ??
               Camera.main?.transform;
    }

    private static Transform FindHeightReference(Scene scene, GameObject character)
    {
        return FindSceneObjectByName(scene, "PlayerController")?.transform ?? character.transform;
    }

    private static void EnsureOutputFolder()
    {
        EnsureFolder("Assets", "MetaMovementCustom");
        EnsureFolder("Assets/MetaMovementCustom", "LocomotionCrouch");
    }

    private static void EnsureFolder(string parent, string child)
    {
        var path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static void EnsureEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("Exit Play Mode before changing the additive crouch setup.");
        }
    }

    private static GameObject FindSceneObjectByName(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }

            var child = FindChildByName(root.transform, name);
            if (child != null)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static Transform FindSceneTransformByName(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var child = FindChildByName(root.transform, name);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
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
}
