using System;
using Oculus.Movement.AnimationRigging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;

public static class MetaMovementGroundingIkMigrator
{
    private const string GeneratedRigName = "LocomotionLegGroundingRig";
    private const string LegacyGeneratedRigName = "GroundingIKRig";
    private const string MovementLocomotionScenePath =
        "Assets/Samples/Meta Movement/71.0.1/Advanced Samples/Scenes/MovementLocomotion.unity";

    [MenuItem("Tools/Meta Movement/Locomotion/Add Minimal Leg Grounding IK")]
    public static void AddMinimalLegGroundingIkMenu()
    {
        EnsureEditMode();
        AddMinimalLegGroundingIkToActiveScene();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Meta Movement/Locomotion/Remove Minimal Leg Grounding IK")]
    public static void RemoveMinimalLegGroundingIkMenu()
    {
        EnsureEditMode();
        RemoveMinimalLegGroundingIkFromActiveScene();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Meta Movement/Locomotion/Apply Minimal Leg Grounding IK To MovementLocomotion Scene")]
    public static void ApplyMinimalLegGroundingIkToMovementLocomotionSceneMenu()
    {
        EnsureEditMode();
        EditorSceneManager.OpenScene(MovementLocomotionScenePath, OpenSceneMode.Single);
        AddMinimalLegGroundingIkToActiveScene();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    private static void EnsureEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("Exit Play Mode before changing the locomotion grounding IK rig.");
        }
    }

    private static void AddMinimalLegGroundingIkToActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        var character = FindSceneObjectByName(scene, "Character");
        if (character == null)
        {
            throw new InvalidOperationException("Could not find a scene GameObject named Character.");
        }

        var animator = FindCharacterAnimator(character);
        var bodyRoot = animator.transform;

        var hips = ResolveHumanoidBone(animator, HumanBodyBones.Hips);
        var leftUpperLeg = ResolveHumanoidBone(animator, HumanBodyBones.LeftUpperLeg);
        var leftLowerLeg = ResolveHumanoidBone(animator, HumanBodyBones.LeftLowerLeg);
        var leftFoot = ResolveHumanoidBone(animator, HumanBodyBones.LeftFoot);
        var rightUpperLeg = ResolveHumanoidBone(animator, HumanBodyBones.RightUpperLeg);
        var rightLowerLeg = ResolveHumanoidBone(animator, HumanBodyBones.RightLowerLeg);
        var rightFoot = ResolveHumanoidBone(animator, HumanBodyBones.RightFoot);

        RemoveGeneratedRigLayers(bodyRoot);
        RemoveGeneratedRigObjects(bodyRoot);

        var rigBuilder = bodyRoot.GetComponent<RigBuilder>();
        if (rigBuilder == null)
        {
            rigBuilder = bodyRoot.gameObject.AddComponent<RigBuilder>();
        }

        var rigTransform = CreateChild(bodyRoot, GeneratedRigName, bodyRoot.gameObject.layer);
        var rig = rigTransform.gameObject.AddComponent<Rig>();
        rig.weight = 1.0f;
        MarkDirty(rig);

        var targetsRoot = CreateChild(rigTransform, "Targets", bodyRoot.gameObject.layer);
        var hipsTarget = CreateConstrainedTarget(targetsRoot, "Target_Hips", hips);
        var leftGroundingKneeTarget = CreateConstrainedTarget(targetsRoot, "GroundingKnee_Left", leftLowerLeg);
        var rightGroundingKneeTarget = CreateConstrainedTarget(targetsRoot, "GroundingKnee_Right", rightLowerLeg);
        var leftKneeHint = CreateKneeHintTarget(targetsRoot, "IKHint_LeftKnee", leftLowerLeg, bodyRoot);
        var rightKneeHint = CreateKneeHintTarget(targetsRoot, "IKHint_RightKnee", rightLowerLeg, bodyRoot);
        var leftFootTarget = CreateFreeTarget(targetsRoot, "Target_LeftFoot", leftFoot);
        var rightFootTarget = CreateFreeTarget(targetsRoot, "Target_RightFoot", rightFoot);
        var leftGroundingLegProxy = CreateFreeTarget(targetsRoot, "GroundingLegProxy_Left", leftUpperLeg);
        var rightGroundingLegProxy = CreateFreeTarget(targetsRoot, "GroundingLegProxy_Right", rightUpperLeg);

        var leftGrounding = CreateGroundingConstraint(
            rigTransform,
            "GroundingLeft",
            animator,
            null,
            hips,
            leftGroundingLegProxy,
            leftFoot,
            hipsTarget,
            leftGroundingKneeTarget,
            leftFootTarget,
            Quaternion.AngleAxis(30.0f, Vector3.up));

        var rightGrounding = CreateGroundingConstraint(
            rigTransform,
            "GroundingRight",
            animator,
            leftGrounding,
            hips,
            rightGroundingLegProxy,
            rightFoot,
            hipsTarget,
            rightGroundingKneeTarget,
            rightFootTarget,
            Quaternion.AngleAxis(-30.0f, Vector3.up));

        SetGroundingPair(leftGrounding, rightGrounding);

        var leftIk = CreateTwoBoneIk(
            rigTransform,
            "LeftLegIK",
            leftUpperLeg,
            leftLowerLeg,
            leftFoot,
            leftFootTarget,
            leftKneeHint);

        var rightIk = CreateTwoBoneIk(
            rigTransform,
            "RightLegIK",
            rightUpperLeg,
            rightLowerLeg,
            rightFoot,
            rightFootTarget,
            rightKneeHint);

        ValidateConstraint(leftGrounding);
        ValidateConstraint(rightGrounding);
        ValidateConstraint(leftIk);
        ValidateConstraint(rightIk);

        rigBuilder.layers.Add(new RigLayer(rig, true));
        MarkDirty(rigBuilder);
        MarkDirty(bodyRoot.gameObject);
        MarkDirty(character);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"Added minimal official Grounding/TwoBoneIK leg rig under {GetPath(bodyRoot)}.");
    }

    private static void RemoveMinimalLegGroundingIkFromActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        var character = FindSceneObjectByName(scene, "Character");
        if (character == null)
        {
            throw new InvalidOperationException("Could not find a scene GameObject named Character.");
        }

        var animator = FindCharacterAnimator(character);
        var bodyRoot = animator.transform;

        RemoveGeneratedRigLayers(bodyRoot);
        RemoveGeneratedRigObjects(bodyRoot);

        MarkDirty(bodyRoot.gameObject);
        MarkDirty(character);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"Removed minimal official Grounding/TwoBoneIK leg rig from {GetPath(bodyRoot)}.");
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

        foreach (var animator in animators)
        {
            if (IsHumanoidAnimator(animator))
            {
                return animator;
            }
        }

        return animators[0];
    }

    private static Transform ResolveHumanoidBone(Animator animator, HumanBodyBones bone)
    {
        if (!IsHumanoidAnimator(animator))
        {
            throw new InvalidOperationException(
                $"{GetPath(animator.transform)} must use a valid humanoid Avatar to resolve {bone}.");
        }

        var transform = animator.GetBoneTransform(bone);
        if (transform == null)
        {
            throw new InvalidOperationException($"Could not resolve required humanoid bone {bone}.");
        }

        return transform;
    }

    private static bool IsHumanoidAnimator(Animator animator)
    {
        return animator != null &&
               animator.avatar != null &&
               animator.avatar.isValid &&
               animator.avatar.isHuman;
    }

    private static Transform CreateConstrainedTarget(Transform parent, string name, Transform source)
    {
        var target = CreateFreeTarget(parent, name, source);
        ConfigurePositionConstraint(target.gameObject, source);
        return target;
    }

    private static Transform CreateFreeTarget(Transform parent, string name, Transform source)
    {
        var target = CreateChild(parent, name, parent.gameObject.layer);
        target.position = source.position;
        target.rotation = source.rotation;
        target.localScale = Vector3.one;
        return target;
    }

    private static Transform CreateKneeHintTarget(
        Transform parent,
        string name,
        Transform knee,
        Transform bodyRoot)
    {
        var target = CreateFreeTarget(parent, name, knee);
        target.position = knee.position + bodyRoot.forward * 0.35f;
        return target;
    }

    private static GroundingConstraint CreateGroundingConstraint(
        Transform parent,
        string name,
        Animator animator,
        GroundingConstraint pair,
        Transform hips,
        Transform leg,
        Transform foot,
        Transform hipsTarget,
        Transform kneeTarget,
        Transform footTarget,
        Quaternion footRotationOffset)
    {
        var transform = CreateChild(parent, name, parent.gameObject.layer);
        var grounding = transform.gameObject.AddComponent<GroundingConstraint>();
        grounding.weight = 1.0f;

        var so = new SerializedObject(grounding);
        SetObject(so, "m_Data._skeleton", null);
        SetObject(so, "m_Data._animator", animator);
        SetObject(so, "m_Data._pair", pair);
        SetInt(so, "m_Data._groundLayers.m_Bits", 1);
        SetFloat(so, "m_Data._groundRaycastDist", 10.0f);
        SetFloat(so, "m_Data._groundOffset", 0.05f);
        SetObject(so, "m_Data._hipsTarget", hipsTarget);
        SetObject(so, "m_Data._kneeTarget", kneeTarget);
        SetObject(so, "m_Data._footTarget", footTarget);
        SetObject(so, "m_Data._leg", leg);
        SetObject(so, "m_Data._foot", foot);
        SetQuaternion(so, "m_Data._footRotationOffset", footRotationOffset);
        SetCurve(so, "m_Data._stepCurve", CreateStepCurve());
        SetFloat(so, "m_Data._stepDist", 0.1f);
        SetFloat(so, "m_Data._stepSpeed", 2.0f);
        SetFloat(so, "m_Data._stepHeight", 0.072f);
        SetFloat(so, "m_Data._stepHeightScaleDist", 0.25f);
        SetFloat(so, "m_Data._moveLowerThreshold", 0.3f);
        SetFloat(so, "m_Data._moveHigherThreshold", 0.6f);
        SetObject(so, "m_Data._hips", hips);
        so.ApplyModifiedPropertiesWithoutUndo();

        grounding.data.ComputeOffsets();
        MarkDirty(grounding);
        return grounding;
    }

    private static void SetGroundingPair(GroundingConstraint grounding, GroundingConstraint pair)
    {
        var so = new SerializedObject(grounding);
        SetObject(so, "m_Data._pair", pair);
        so.ApplyModifiedPropertiesWithoutUndo();
        MarkDirty(grounding);
    }

    private static AnimationCurve CreateStepCurve()
    {
        var curve = new AnimationCurve(
            new Keyframe(0.0f, 0.0f, 2.0f, 2.0f),
            new Keyframe(0.5f, 1.0f, 0.0f, 0.0f),
            new Keyframe(1.0f, 0.0f, -2.0f, -2.0f));
        curve.preWrapMode = WrapMode.ClampForever;
        curve.postWrapMode = WrapMode.ClampForever;
        return curve;
    }

    private static TwoBoneIKConstraint CreateTwoBoneIk(
        Transform parent,
        string name,
        Transform root,
        Transform mid,
        Transform tip,
        Transform target,
        Transform hint)
    {
        var transform = CreateChild(parent, name, parent.gameObject.layer);
        var ik = transform.gameObject.AddComponent<TwoBoneIKConstraint>();
        ik.weight = 1.0f;

        ref var data = ref ik.data;
        data.root = root;
        data.mid = mid;
        data.tip = tip;
        data.target = target;
        data.hint = hint;
        data.targetPositionWeight = 1.0f;
        data.targetRotationWeight = 0.0f;
        data.hintWeight = 1.0f;
        data.maintainTargetPositionOffset = false;
        data.maintainTargetRotationOffset = false;
        MarkDirty(ik);

        return ik;
    }

    private static void ConfigurePositionConstraint(GameObject target, Transform source)
    {
        var constraint = target.AddComponent<PositionConstraint>();
        while (constraint.sourceCount > 0)
        {
            constraint.RemoveSource(0);
        }

        constraint.AddSource(new ConstraintSource
        {
            sourceTransform = source,
            weight = 1.0f
        });
        constraint.translationAtRest = target.transform.localPosition;
        constraint.translationOffset = Vector3.zero;
        constraint.translationAxis = Axis.X | Axis.Y | Axis.Z;
        constraint.weight = 1.0f;
        constraint.constraintActive = true;
        constraint.locked = true;
        MarkDirty(constraint);
    }

    private static void ValidateConstraint(IRigConstraint constraint)
    {
        if (!constraint.IsValid())
        {
            throw new InvalidOperationException(
                $"Generated constraint {constraint.component.name} is not valid. Check its transform references.");
        }
    }

    private static void RemoveGeneratedRigLayers(Transform bodyRoot)
    {
        var rigBuilder = bodyRoot.GetComponent<RigBuilder>();
        if (rigBuilder == null)
        {
            return;
        }

        for (var i = rigBuilder.layers.Count - 1; i >= 0; i--)
        {
            var layer = rigBuilder.layers[i];
            if (layer == null || layer.rig == null || IsGeneratedRigName(layer.rig.gameObject.name))
            {
                rigBuilder.layers.RemoveAt(i);
            }
        }

        MarkDirty(rigBuilder);
    }

    private static void RemoveGeneratedRigObjects(Transform bodyRoot)
    {
        for (var i = bodyRoot.childCount - 1; i >= 0; i--)
        {
            var child = bodyRoot.GetChild(i);
            if (IsGeneratedRigName(child.name))
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static bool IsGeneratedRigName(string rigName)
    {
        return rigName == GeneratedRigName || rigName == LegacyGeneratedRigName;
    }

    private static Transform CreateChild(Transform parent, string name, int layer)
    {
        var gameObject = new GameObject(name)
        {
            layer = layer
        };
        var transform = gameObject.transform;
        transform.SetParent(parent, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        return transform;
    }

    private static GameObject FindSceneObjectByName(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }

            var child = FindChildRecursive(root.transform, name);
            if (child != null)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child;
            }

            var found = FindChildRecursive(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static string GetPath(Transform transform)
    {
        var path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = $"{transform.name}/{path}";
        }

        return path;
    }

    private static void MarkDirty(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            return;
        }

        EditorUtility.SetDirty(obj);
        if (PrefabUtility.IsPartOfPrefabInstance(obj))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        }
    }

    private static void SetObject(SerializedObject so, string path, UnityEngine.Object value)
    {
        RequireProperty(so, path).objectReferenceValue = value;
    }

    private static void SetFloat(SerializedObject so, string path, float value)
    {
        RequireProperty(so, path).floatValue = value;
    }

    private static void SetInt(SerializedObject so, string path, int value)
    {
        RequireProperty(so, path).intValue = value;
    }

    private static void SetQuaternion(SerializedObject so, string path, Quaternion value)
    {
        RequireProperty(so, path).quaternionValue = value;
    }

    private static void SetCurve(SerializedObject so, string path, AnimationCurve value)
    {
        RequireProperty(so, path).animationCurveValue = value;
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
