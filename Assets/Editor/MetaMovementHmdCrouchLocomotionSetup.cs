using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MetaMovementHmdCrouchLocomotionSetup
{
    private const string ControllerPath =
        "Assets/Samples/Meta Movement/71.0.1/Advanced Samples/Locomotion/Animations/LocomotionController.controller";
    private const string OutputFolder = "Assets/MetaMovementCustom/LocomotionCrouch";
    private const string CrouchMaskPath = OutputFolder + "/CrouchLowerBodyOverride.mask";
    private const string CrouchIdleClipPath = OutputFolder + "/GeneratedCrouchIdle.anim";
    private const string CrouchForwardClipPath = OutputFolder + "/GeneratedCrouchWalkForward.anim";
    private const string CrouchBackClipPath = OutputFolder + "/GeneratedCrouchWalkBack.anim";
    private const string CrouchLeftClipPath = OutputFolder + "/GeneratedCrouchWalkLeft.anim";
    private const string CrouchRightClipPath = OutputFolder + "/GeneratedCrouchWalkRight.anim";
    private const string CrouchAmountParameter = "CrouchAmount";
    private const string IsCrouchingParameter = "IsCrouching";
    private const string MoveSpeedParameter = "MoveSpeed";
    private const string HorizontalParameter = "Horizontal";
    private const string VerticalParameter = "Vertical";
    private const string CrouchLayerName = "Crouch Locomotion";
    private const string LegacyAdditiveLayerName = "Additive Crouch";
    private const string LegacyCrouchParameter = "Crouch";
    private const string GeneratedBlendTreeName = "Generated Crouch Locomotion Tree";

    [MenuItem("Tools/Meta Movement/Locomotion/Add HMD Crouch Locomotion")]
    public static void AddHmdCrouchLocomotionMenu()
    {
        EnsureEditMode();
        AddHmdCrouchLocomotion();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Meta Movement/Locomotion/Remove HMD Crouch Locomotion")]
    public static void RemoveHmdCrouchLocomotionMenu()
    {
        EnsureEditMode();
        RemoveHmdCrouchLocomotion();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    private static void AddHmdCrouchLocomotion()
    {
        EnsureOutputFolder();

        var controller = LoadController();
        RemoveLegacyAdditiveCrouch(controller);
        EnsureControllerParameters(controller);
        RemoveGeneratedBlendTrees(controller);

        var mask = CreateOrUpdateCrouchMask();
        var crouchIdle = CreateOrUpdateCrouchClip(CrouchIdleClipPath, CrouchDirection.Idle);
        var crouchForward = CreateOrUpdateCrouchClip(CrouchForwardClipPath, CrouchDirection.Forward);
        var crouchBack = CreateOrUpdateCrouchClip(CrouchBackClipPath, CrouchDirection.Back);
        var crouchLeft = CreateOrUpdateCrouchClip(CrouchLeftClipPath, CrouchDirection.Left);
        var crouchRight = CreateOrUpdateCrouchClip(CrouchRightClipPath, CrouchDirection.Right);
        AddOrUpdateCrouchLayer(
            controller,
            mask,
            crouchIdle,
            crouchForward,
            crouchBack,
            crouchLeft,
            crouchRight);

        ConfigureSceneDriver();

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log(
            "[MetaMovementCrouch] Added HMD crouch detection and a lower-body override crouch locomotion layer. " +
            "The controller reuses Horizontal/Vertical for movement direction and writes MoveSpeed only as a derived helper parameter.");
    }

    private static void RemoveHmdCrouchLocomotion()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
        {
            controller.layers = controller.layers
                .Where(layer => layer.name != CrouchLayerName)
                .ToArray();
            RemoveParameterIfPresent(controller, CrouchAmountParameter);
            RemoveParameterIfPresent(controller, IsCrouchingParameter);
            RemoveParameterIfPresent(controller, MoveSpeedParameter);
            RemoveGeneratedBlendTrees(controller);
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
                EditorUtility.SetDirty(character);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[MetaMovementCrouch] Removed HMD crouch locomotion setup.");
    }

    private static void EnsureControllerParameters(AnimatorController controller)
    {
        EnsureParameter(controller, CrouchAmountParameter, AnimatorControllerParameterType.Float);
        EnsureParameter(controller, IsCrouchingParameter, AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, MoveSpeedParameter, AnimatorControllerParameterType.Float);
    }

    private static void EnsureParameter(
        AnimatorController controller,
        string name,
        AnimatorControllerParameterType type)
    {
        var parameter = controller.parameters.FirstOrDefault(existing => existing.name == name);
        if (parameter == null)
        {
            controller.AddParameter(name, type);
            return;
        }

        if (parameter.type != type)
        {
            controller.RemoveParameter(parameter);
            controller.AddParameter(name, type);
        }
    }

    private static void RemoveParameterIfPresent(AnimatorController controller, string name)
    {
        var parameter = controller.parameters.FirstOrDefault(existing => existing.name == name);
        if (parameter != null)
        {
            controller.RemoveParameter(parameter);
        }
    }

    private static void RemoveLegacyAdditiveCrouch(AnimatorController controller)
    {
        controller.layers = controller.layers
            .Where(layer => layer.name != LegacyAdditiveLayerName)
            .ToArray();
        RemoveParameterIfPresent(controller, LegacyCrouchParameter);
    }

    private static void AddOrUpdateCrouchLayer(
        AnimatorController controller,
        AvatarMask mask,
        AnimationClip crouchIdle,
        AnimationClip crouchForward,
        AnimationClip crouchBack,
        AnimationClip crouchLeft,
        AnimationClip crouchRight)
    {
        var layerIndex = Array.FindIndex(controller.layers, layer => layer.name == CrouchLayerName);
        if (layerIndex < 0)
        {
            controller.AddLayer(CrouchLayerName);
            layerIndex = Array.FindIndex(controller.layers, layer => layer.name == CrouchLayerName);
        }

        var layers = controller.layers;
        var layer = layers[layerIndex];
        layer.defaultWeight = 0.0f;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        layer.avatarMask = mask;
        layer.iKPass = false;
        layer.syncedLayerIndex = -1;

        var stateMachine = layer.stateMachine;
        foreach (var childState in stateMachine.states.ToArray())
        {
            stateMachine.RemoveState(childState.state);
        }

        var tree = CreateCrouchBlendTree(
            controller,
            crouchIdle,
            crouchForward,
            crouchBack,
            crouchLeft,
            crouchRight);
        var state = stateMachine.AddState("Crouch Locomotion", new Vector3(260.0f, 80.0f, 0.0f));
        state.motion = tree;
        state.writeDefaultValues = false;
        state.iKOnFeet = false;
        stateMachine.defaultState = state;

        layers[layerIndex] = layer;
        controller.layers = layers;
    }

    private static BlendTree CreateCrouchBlendTree(
        AnimatorController controller,
        AnimationClip crouchIdle,
        AnimationClip crouchForward,
        AnimationClip crouchBack,
        AnimationClip crouchLeft,
        AnimationClip crouchRight)
    {
        var tree = new BlendTree
        {
            name = GeneratedBlendTreeName,
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = HorizontalParameter,
            blendParameterY = VerticalParameter,
            useAutomaticThresholds = false,
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(tree, controller);

        tree.AddChild(crouchIdle, Vector2.zero);
        tree.AddChild(crouchRight, Vector2.right);
        tree.AddChild(crouchLeft, Vector2.left);
        tree.AddChild(crouchForward, Vector2.up);
        tree.AddChild(crouchBack, Vector2.down);
        EditorUtility.SetDirty(tree);
        return tree;
    }

    private static AvatarMask CreateOrUpdateCrouchMask()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(CrouchMaskPath);
        if (mask == null)
        {
            mask = new AvatarMask
            {
                name = "CrouchLowerBodyOverride"
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

    private static AnimationClip CreateOrUpdateCrouchClip(string path, CrouchDirection direction)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path),
                frameRate = 30.0f,
                legacy = false,
                wrapMode = WrapMode.Loop
            };
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.ClearCurves();
        clip.frameRate = 30.0f;
        clip.wrapMode = WrapMode.Loop;

        ApplyCrouchPoseCurves(clip, direction);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void ApplyCrouchPoseCurves(AnimationClip clip, CrouchDirection direction)
    {
        var stride = direction == CrouchDirection.Idle ? 0.0f : 0.16f;
        var lateral = direction == CrouchDirection.Left ? -0.1f :
            direction == CrouchDirection.Right ? 0.1f : 0.0f;
        var forwardSign = direction == CrouchDirection.Back ? -1.0f : 1.0f;

        SetHumanoidCurve(clip, "Spine Front-Back", t => -0.03f);
        SetHumanoidCurve(clip, "Chest Front-Back", t => -0.02f);
        SetHumanoidCurve(
            clip,
            "Left Upper Leg Front-Back",
            t => 0.42f + Mathf.Sin(t * Mathf.PI * 2.0f) * stride * forwardSign);
        SetHumanoidCurve(
            clip,
            "Right Upper Leg Front-Back",
            t => 0.42f - Mathf.Sin(t * Mathf.PI * 2.0f) * stride * forwardSign);
        SetHumanoidCurve(clip, "Left Upper Leg In-Out", t => lateral);
        SetHumanoidCurve(clip, "Right Upper Leg In-Out", t => lateral);
        SetHumanoidCurve(
            clip,
            "Left Lower Leg Stretch",
            t => 0.5f + Mathf.Max(0.0f, -Mathf.Sin(t * Mathf.PI * 2.0f)) * stride);
        SetHumanoidCurve(
            clip,
            "Right Lower Leg Stretch",
            t => 0.5f + Mathf.Max(0.0f, Mathf.Sin(t * Mathf.PI * 2.0f)) * stride);
        SetHumanoidCurve(
            clip,
            "Left Foot Up-Down",
            t => -0.1f + Mathf.Max(0.0f, Mathf.Sin(t * Mathf.PI * 2.0f)) * 0.08f);
        SetHumanoidCurve(
            clip,
            "Right Foot Up-Down",
            t => -0.1f + Mathf.Max(0.0f, -Mathf.Sin(t * Mathf.PI * 2.0f)) * 0.08f);
    }

    private static void SetHumanoidCurve(AnimationClip clip, string muscleName, Func<float, float> valueAtTime)
    {
        const int sampleCount = 9;
        var keys = new Keyframe[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var normalizedTime = i / (float)(sampleCount - 1);
            keys[i] = new Keyframe(normalizedTime, valueAtTime(normalizedTime), 0.0f, 0.0f);
        }

        var curve = new AnimationCurve(keys)
        {
            preWrapMode = WrapMode.Loop,
            postWrapMode = WrapMode.Loop
        };
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscleName),
            curve);
    }

    private static void ConfigureSceneDriver()
    {
        var scene = SceneManager.GetActiveScene();
        var character = FindSceneObjectByName(scene, "Character");
        if (character == null)
        {
            throw new InvalidOperationException("Could not find a scene GameObject named Character.");
        }

        var animator = FindCharacterAnimator(character);
        var driver = character.GetComponent<MetaMovementCrouchBlendDriver>();
        if (driver == null)
        {
            driver = Undo.AddComponent<MetaMovementCrouchBlendDriver>(character);
        }

        ConfigureDriver(
            driver,
            animator,
            FindHeadTransform(scene),
            FindHeightReference(scene, character));

        EditorUtility.SetDirty(character);
        EditorSceneManager.MarkSceneDirty(scene);
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
        RequireProperty(so, "_calibrateStandingHeightOnStart").boolValue = true;
        RequireProperty(so, "_enterCrouchEyeHeightRatio").floatValue = 0.86f;
        RequireProperty(so, "_exitCrouchEyeHeightRatio").floatValue = 0.9f;
        RequireProperty(so, "_fullCrouchEyeHeightRatio").floatValue = 0.55f;
        RequireProperty(so, "_smoothSpeed").floatValue = 10.0f;
        RequireProperty(so, "_layerFadeStart").floatValue = 0.15f;
        RequireProperty(so, "_maxLayerWeight").floatValue = 1.0f;
        RequireProperty(so, "_crouchAmountParameter").stringValue = CrouchAmountParameter;
        RequireProperty(so, "_isCrouchingParameter").stringValue = IsCrouchingParameter;
        RequireProperty(so, "_moveSpeedParameter").stringValue = MoveSpeedParameter;
        RequireProperty(so, "_horizontalParameter").stringValue = HorizontalParameter;
        RequireProperty(so, "_verticalParameter").stringValue = VerticalParameter;
        RequireProperty(so, "_crouchLayer").stringValue = CrouchLayerName;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(driver);
    }

    private static void RemoveGeneratedBlendTrees(AnimatorController controller)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
        {
            if (asset is BlendTree tree && tree.name == GeneratedBlendTreeName)
            {
                UnityEngine.Object.DestroyImmediate(tree, true);
            }
        }
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
            throw new InvalidOperationException("Exit Play Mode before changing the HMD crouch locomotion setup.");
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

    private enum CrouchDirection
    {
        Idle,
        Forward,
        Back,
        Left,
        Right
    }
}
