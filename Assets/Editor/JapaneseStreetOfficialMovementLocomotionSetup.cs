using System;
using System.Collections.Generic;
using System.Linq;
using Oculus.Movement.AnimationRigging;
using Oculus.Movement.Locomotion;
using Oculus.Movement.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public static class JapaneseStreetOfficialMovementLocomotionSetup
{
    private const string ScenePath = "Assets/Scenes/JapaneseStreetVR.unity";
    private const string PlayerControllerName = "PlayerController";
    private const string OfficialCharacterName = "ArmatureSkinningUpdateRetargetLocomotion";
    private const string OfficialCharacterPrefabPath =
        "Assets/Samples/Meta Movement/71.0.1/Advanced Samples/Locomotion/Prefabs/ArmatureSkinningUpdateRetargetLocomotion.prefab";
    private const string FallbackOfficialCharacterPrefabPath =
        "Packages/com.meta.movement/Samples~/AdvancedSamples/Locomotion/Prefabs/ArmatureSkinningUpdateRetargetLocomotion.prefab";
    private const string CameraFollowerName = "OVRCameraRigFollowsLocomotion";
    private const string CharacterFollowerName = "CharacterObjectFollowsLocomotion";
    private const string SkeletonProcessorName = "BonesFollowLocomotionWithStaticCharacter";

    [MenuItem("Kamen Rider/Setup Japanese Street Official MovementLocomotion Sample Body")]
    public static void SetupJapaneseStreetOfficialMovementLocomotion()
    {
        EnsureEditMode();
        Debug.Log("[JapaneseStreetOfficialMovementLocomotion] Starting setup.");
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[JapaneseStreetOfficialMovementLocomotion] Setup cancelled before opening JapaneseStreetVR.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
        }

        try
        {
            SetupActiveScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[JapaneseStreetOfficialMovementLocomotion] Setup finished and scene saved.");
        }
        catch (Exception exception)
        {
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.LogError($"[JapaneseStreetOfficialMovementLocomotion] Setup failed after saving partial progress:\n{exception}");
            throw;
        }
    }

    private static void SetupActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        var cameraRig = FindObjectInScene<OVRCameraRig>(scene);
        if (cameraRig == null)
        {
            throw new InvalidOperationException("Could not find OVRCameraRig in JapaneseStreetVR.");
        }

        ConfigureRuntimeSettingsForOfficialLocomotion();
        DisableBlackSceneBodies(scene);

        var playerController = GetOrCreatePlayerController(scene, cameraRig);
        var officialCharacter = GetOrCreateOfficialCharacter(scene, playerController.transform);
        ValidateOfficialCharacter(officialCharacter);
        EditorUtility.SetDirty(officialCharacter);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[JapaneseStreetOfficialMovementLocomotion] Official character exists in scene: {GetHierarchyPath(officialCharacter.transform)}.");

        ConfigureOfficialCharacter(officialCharacter, playerController.transform);

        ConfigurePlayerController(playerController, cameraRig, officialCharacter);
        DisableCompetingAvatarDrivers(scene, playerController);
        DisableCompetingXriLocomotion(scene);

        EditorUtility.SetDirty(playerController);
        EditorUtility.SetDirty(officialCharacter);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = playerController;
        Debug.Log(
            "[JapaneseStreetOfficialMovementLocomotion] Installed the official MovementLocomotion sample body " +
            "and control chain in JapaneseStreetVR. Black bodies were disabled, not deleted.");
    }

    private static void ConfigureRuntimeSettingsForOfficialLocomotion()
    {
        var runtimeSettings = OVRRuntimeSettings.GetRuntimeSettings();
        if (runtimeSettings == null)
        {
            return;
        }

        Undo.RecordObject(runtimeSettings, "Configure official MovementLocomotion runtime settings");
        runtimeSettings.BodyTrackingJointSet = OVRPlugin.BodyJointSet.FullBody;
        runtimeSettings.BodyTrackingFidelity = OVRPlugin.BodyTrackingFidelity2.Low;
        EditorUtility.SetDirty(runtimeSettings);
    }

    private static GameObject GetOrCreatePlayerController(Scene scene, OVRCameraRig cameraRig)
    {
        var playerController = FindSceneObjectByName(scene, PlayerControllerName);
        if (playerController == null)
        {
            playerController = new GameObject(PlayerControllerName);
            Undo.RegisterCreatedObjectUndo(playerController, "Create official MovementLocomotion PlayerController");
        }

        Undo.RecordObject(playerController.transform, "Configure official MovementLocomotion PlayerController transform");
        var cameraWorldPosition = cameraRig.transform.position;
        var cameraWorldRotation = cameraRig.transform.rotation;
        if (playerController.transform.parent != null)
        {
            playerController.transform.SetParent(null, true);
        }

        playerController.transform.SetPositionAndRotation(cameraWorldPosition, FlattenYaw(cameraWorldRotation));
        playerController.transform.localScale = Vector3.one;
        EditorUtility.SetDirty(playerController.transform);
        return playerController;
    }

    private static GameObject GetOrCreateOfficialCharacter(Scene scene, Transform playerController)
    {
        var existing = FindSceneObjectByName(scene, OfficialCharacterName);
        if (existing != null)
        {
            Undo.RecordObject(existing.transform, "Reuse official MovementLocomotion character");
            existing.SetActive(true);
            existing.transform.SetParent(null, true);
            existing.transform.SetPositionAndRotation(playerController.position, playerController.rotation);
            existing.transform.localScale = Vector3.one;
            return existing;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OfficialCharacterPrefabPath);
        if (prefab == null)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FallbackOfficialCharacterPrefabPath);
        }

        if (prefab == null)
        {
            throw new InvalidOperationException(
                $"Could not load official MovementLocomotion character prefab at {OfficialCharacterPrefabPath}.");
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        if (instance == null)
        {
            instance = UnityEngine.Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(instance, scene);
        }

        Undo.RegisterCreatedObjectUndo(instance, "Instantiate official MovementLocomotion character");
        instance.name = OfficialCharacterName;
        instance.hideFlags = HideFlags.None;
        instance.SetActive(true);
        instance.transform.SetPositionAndRotation(playerController.position, playerController.rotation);
        instance.transform.localScale = Vector3.one;
        EditorUtility.SetDirty(instance);
        return instance;
    }

    private static void ValidateOfficialCharacter(GameObject officialCharacter)
    {
        if (officialCharacter == null)
        {
            throw new InvalidOperationException("Official MovementLocomotion character was not created.");
        }

        if (!officialCharacter.scene.IsValid() || officialCharacter.scene.path != ScenePath)
        {
            throw new InvalidOperationException(
                $"Official MovementLocomotion character is not in {ScenePath}. Current scene: {officialCharacter.scene.path}");
        }

        var renderers = officialCharacter.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length == 0)
        {
            throw new InvalidOperationException(
                $"Official MovementLocomotion character {officialCharacter.name} has no SkinnedMeshRenderer children.");
        }
    }

    private static void ConfigureOfficialCharacter(GameObject character, Transform playerController)
    {
        var ovrBody = character.GetComponent<OVRBody>();
        if (ovrBody != null)
        {
            Undo.RecordObject(ovrBody, "Configure official OVRBody full body");
            ovrBody.ProvidedSkeletonType = OVRPlugin.BodyJointSet.FullBody;
            EditorUtility.SetDirty(ovrBody);
        }

        var retargetingLayer = character.GetComponent<RetargetingLayer>();
        if (retargetingLayer != null)
        {
            Undo.RecordObject(retargetingLayer, "Configure official RetargetingLayer locomotion");
            retargetingLayer.ApplyAnimationConstraintsToCorrectedPositions = true;
            retargetingLayer.EnableTrackingByProxy = false;
            var so = new SerializedObject(retargetingLayer);
            RequireProperty(so, "_skeletonType").intValue = (int)OVRSkeleton.SkeletonType.FullBody;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(retargetingLayer);
        }

        // Movement retargeting owns the rig lifecycle at runtime through RetargetingLayer.
        // Do not force-enable RigBuilder in edit mode; doing so can stall Quest/Link startup.

        foreach (var animator in character.GetComponentsInChildren<Animator>(true))
        {
            Undo.RecordObject(animator, "Enable official locomotion Animator");
            animator.enabled = true;
            EditorUtility.SetDirty(animator);
        }
    }

    private static void ConfigurePlayerController(
        GameObject playerController,
        OVRCameraRig cameraRig,
        GameObject officialCharacter)
    {
        ConfigurePhysics(playerController, out var rigidbody, out var bodyCollider, out var footCollider);
        var locomotion = ConfigureMovementSdkLocomotion(playerController, rigidbody, footCollider, cameraRig);

        if (cameraRig.transform.parent != null)
        {
            Undo.RecordObject(cameraRig.transform, "Unparent OVRCameraRig for official MovementLocomotion follow chain");
            cameraRig.transform.SetParent(null, true);
        }

        var cameraFollower = ConfigureFollower(
            playerController.transform,
            CameraFollowerName,
            new[] { cameraRig.transform });
        var characterFollower = ConfigureFollower(
            playerController.transform,
            CharacterFollowerName,
            new[] { officialCharacter.transform });

        ConfigureSkeletonProcessor(playerController.transform, officialCharacter, characterFollower);
        ConfigureAnimatorHooks(playerController, officialCharacter, locomotion);
        ConfigureUnityInputBinding(playerController, locomotion);
        ConfigureOvrThumbstickBridge(playerController, locomotion, officialCharacter);
        ConfigureSphereColliderFollower(playerController, bodyCollider, footCollider, officialCharacter);

        cameraFollower.DoFollowMe();
        characterFollower.DoFollowMe();
    }

    private static void ConfigurePhysics(
        GameObject playerController,
        out Rigidbody rigidbody,
        out CapsuleCollider bodyCollider,
        out SphereCollider footCollider)
    {
        rigidbody = playerController.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = Undo.AddComponent<Rigidbody>(playerController);
        }

        Undo.RecordObject(rigidbody, "Configure official MovementLocomotion Rigidbody");
        rigidbody.useGravity = true;
        rigidbody.isKinematic = false;
        rigidbody.freezeRotation = true;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        bodyCollider = playerController.GetComponent<CapsuleCollider>();
        if (bodyCollider == null)
        {
            bodyCollider = Undo.AddComponent<CapsuleCollider>(playerController);
        }

        Undo.RecordObject(bodyCollider, "Configure official MovementLocomotion body capsule");
        bodyCollider.isTrigger = true;
        bodyCollider.radius = 0.25f;
        bodyCollider.height = 1.75f;
        bodyCollider.center = new Vector3(0.0f, 0.875f, 0.0f);
        bodyCollider.direction = 1;

        footCollider = playerController.GetComponent<SphereCollider>();
        if (footCollider == null)
        {
            footCollider = Undo.AddComponent<SphereCollider>(playerController);
        }

        Undo.RecordObject(footCollider, "Configure official MovementLocomotion foot sphere");
        footCollider.isTrigger = false;
        footCollider.radius = 0.125f;
        footCollider.center = new Vector3(0.0f, 0.125f, 0.0f);

        EditorUtility.SetDirty(rigidbody);
        EditorUtility.SetDirty(bodyCollider);
        EditorUtility.SetDirty(footCollider);
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
        RequireProperty(so, "_scaleInputByActualVelocity").boolValue = true;
        RequireProperty(so, "_rotationAngle").floatValue = 45.0f;
        RequireProperty(so, "_rotationPerSecond").floatValue = 180.0f;
        RequireProperty(so, "_speed").floatValue = 3.0f;
        RequireProperty(so, "_cameraRig").objectReferenceValue = cameraRig;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(locomotion);
        return locomotion;
    }

    private static TransformsFollowMe ConfigureFollower(Transform playerController, string followerName, Transform[] targets)
    {
        var followerTransform = FindDirectChild(playerController, followerName);
        if (followerTransform == null)
        {
            var followerObject = new GameObject(followerName);
            Undo.RegisterCreatedObjectUndo(followerObject, $"Create {followerName}");
            Undo.SetTransformParent(followerObject.transform, playerController, $"Parent {followerName}");
            followerTransform = followerObject.transform;
        }

        Undo.RecordObject(followerTransform, $"Configure {followerName} transform");
        followerTransform.localPosition = Vector3.zero;
        followerTransform.localRotation = Quaternion.identity;
        followerTransform.localScale = Vector3.one;

        var follower = followerTransform.GetComponent<TransformsFollowMe>();
        if (follower == null)
        {
            follower = Undo.AddComponent<TransformsFollowMe>(followerTransform.gameObject);
        }

        var so = new SerializedObject(follower);
        SetObjectArray(RequireProperty(so, "_transformsFollowingMe"), targets);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(follower);
        return follower;
    }

    private static void ConfigureSkeletonProcessor(
        Transform playerController,
        GameObject officialCharacter,
        TransformsFollowMe characterFollower)
    {
        var processorTransform = FindDirectChild(playerController, SkeletonProcessorName);
        if (processorTransform == null)
        {
            var processorObject = new GameObject(SkeletonProcessorName);
            Undo.RegisterCreatedObjectUndo(processorObject, $"Create {SkeletonProcessorName}");
            Undo.SetTransformParent(processorObject.transform, playerController, $"Parent {SkeletonProcessorName}");
            processorTransform = processorObject.transform;
        }

        Undo.RecordObject(processorTransform, $"Configure {SkeletonProcessorName} transform");
        processorTransform.localPosition = Vector3.zero;
        processorTransform.localRotation = Quaternion.identity;
        processorTransform.localScale = Vector3.one;

        var translateProcessor = processorTransform.GetComponent<SkeletonTranslateProcessor>();
        if (translateProcessor == null)
        {
            translateProcessor = Undo.AddComponent<SkeletonTranslateProcessor>(processorTransform.gameObject);
        }

        Undo.RecordObject(translateProcessor, "Configure official locomotion SkeletonTranslateProcessor");
        translateProcessor.enabled = true;
        var processorSo = new SerializedObject(translateProcessor);
        RequireProperty(processorSo, "_transformOffsetForSkeleton").objectReferenceValue = playerController;
        var moverProperty = processorSo.FindProperty("_notifyOnStateChange.AlternativeCharacterMover");
        if (moverProperty != null)
        {
            moverProperty.objectReferenceValue = characterFollower;
        }

        processorSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(translateProcessor);

        var retargetingLayer = officialCharacter.GetComponent<RetargetingLayer>();
        var aggregator = officialCharacter.GetComponent<SkeletonProcessAggregator>();
        if (aggregator == null)
        {
            aggregator = Undo.AddComponent<SkeletonProcessAggregator>(officialCharacter);
        }

        Undo.RecordObject(aggregator, "Configure official locomotion SkeletonProcessAggregator");
        aggregator.enabled = true;
        aggregator.SkeletonProcessors = new List<SkeletonProcessAggregator.Item>();
        aggregator.AddProcessor(translateProcessor);
        var aggregatorSo = new SerializedObject(aggregator);
        if (retargetingLayer != null)
        {
            RequireProperty(aggregatorSo, "_autoAddTo").objectReferenceValue = retargetingLayer;
        }

        aggregatorSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(aggregator);
    }

    private static void ConfigureAnimatorHooks(
        GameObject playerController,
        GameObject officialCharacter,
        MovementSDKLocomotion locomotion)
    {
        var animatorHooks = playerController.GetComponent<AnimatorHooks>();
        if (animatorHooks == null)
        {
            animatorHooks = Undo.AddComponent<AnimatorHooks>(playerController);
        }

        var animators = officialCharacter.GetComponentsInChildren<Animator>(true)
            .Where(animator => animator != null)
            .ToArray();
        var hooksSo = new SerializedObject(animatorHooks);
        RequireProperty(hooksSo, "_autoAssignAnimatorsFromChildren").boolValue = false;
        SetObjectArray(RequireProperty(hooksSo, "_animators"), animators);
        RequireProperty(hooksSo, "_maxInputAcceleration").floatValue = 0.1f;
        hooksSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(animatorHooks);

        var locomotionSo = new SerializedObject(locomotion);
        ConfigurePersistentEventCall(
            RequireProperty(locomotionSo, "_movementEvents.OnUserInputChangeJoystickDir"),
            animatorHooks,
            "set_InputHorizontalVertical",
            PersistentListenerMode.EventDefined);
        locomotionSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(locomotion);
    }

    private static void ConfigureUnityInputBinding(GameObject playerController, MovementSDKLocomotion locomotion)
    {
        var inputBinding = FindDirectChild(playerController.transform, "Controls")?.GetComponent<UnityInputBinding>();
        if (inputBinding == null)
        {
            var controls = FindDirectChild(playerController.transform, "Controls");
            if (controls == null)
            {
                var controlsObject = new GameObject("Controls");
                Undo.RegisterCreatedObjectUndo(controlsObject, "Create official MovementLocomotion Controls");
                Undo.SetTransformParent(controlsObject.transform, playerController.transform, "Parent Controls");
                controls = controlsObject.transform;
            }

            inputBinding = Undo.AddComponent<UnityInputBinding>(controls.gameObject);
        }

        Undo.RecordObject(inputBinding.transform, "Configure official MovementLocomotion Controls transform");
        inputBinding.transform.localPosition = new Vector3(0.0f, 1.595f, 0.205f);
        inputBinding.transform.localRotation = Quaternion.identity;
        inputBinding.transform.localScale = Vector3.one;

        var inputSo = new SerializedObject(inputBinding);
        ConfigurePersistentEventCall(
            RequireProperty(inputSo, "_axisHorizontalVertical"),
            locomotion,
            "set_UserInput",
            PersistentListenerMode.EventDefined);
        RequireProperty(inputSo, "_keyBindings").arraySize = 0;
        RequireProperty(inputSo, "_onUpdate").boolValue = false;
        RequireProperty(inputSo, "_onFixedUpdate").boolValue = true;
        inputSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(inputBinding);
    }

    private static void ConfigureOvrThumbstickBridge(
        GameObject playerController,
        MovementSDKLocomotion locomotion,
        GameObject officialCharacter)
    {
        var bridge = playerController.GetComponent<MovementSdkOvrThumbstickInput>();
        if (bridge == null)
        {
            bridge = Undo.AddComponent<MovementSdkOvrThumbstickInput>(playerController);
        }

        var animators = officialCharacter.GetComponentsInChildren<Animator>(true)
            .Where(animator => animator != null)
            .ToArray();
        var bridgeSo = new SerializedObject(bridge);
        RequireProperty(bridgeSo, "_locomotion").objectReferenceValue = locomotion;
        SetEnumProperty(RequireProperty(bridgeSo, "_moveAxis"), nameof(OVRInput.Axis2D.PrimaryThumbstick));
        SetEnumProperty(RequireProperty(bridgeSo, "_fallbackMoveAxis"), nameof(OVRInput.Axis2D.SecondaryThumbstick));
        RequireProperty(bridgeSo, "_deadZone").floatValue = 0.12f;
        RequireProperty(bridgeSo, "_invertX").boolValue = false;
        RequireProperty(bridgeSo, "_invertY").boolValue = false;
        RequireProperty(bridgeSo, "_writeAnimatorParameters").boolValue = false;
        SetObjectArray(RequireProperty(bridgeSo, "_animators"), animators);
        bridgeSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bridge);
    }

    private static void ConfigureSphereColliderFollower(
        GameObject playerController,
        CapsuleCollider bodyCollider,
        SphereCollider footCollider,
        GameObject officialCharacter)
    {
        var colliderFollower = playerController.GetComponent<SphereColliderStaysBelowHips>();
        if (colliderFollower == null)
        {
            colliderFollower = Undo.AddComponent<SphereColliderStaysBelowHips>(playerController);
        }

        var animator = officialCharacter.GetComponentInChildren<Animator>(true);
        var hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        var leftToes = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftToes) : null;
        var rightToes = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightToes) : null;
        var leftFoot = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
        var rightFoot = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightFoot) : null;
        var toes = new[] { leftToes ?? leftFoot, rightToes ?? rightFoot }
            .Where(transform => transform != null)
            .ToArray();

        var so = new SerializedObject(colliderFollower);
        RequireProperty(so, "_collider").objectReferenceValue = footCollider;
        RequireProperty(so, "_expectedBodyCapsule").objectReferenceValue = bodyCollider;
        RequireProperty(so, "_characterRoot").objectReferenceValue = officialCharacter.transform;
        RequireProperty(so, "_floorLayerMask").intValue = 1;
        RequireProperty(so, "_trackingHips").objectReferenceValue = hips;
        SetObjectArray(RequireProperty(so, "_trackingToes"), toes);
        RequireProperty(so, "_colliderFollowsToes").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(colliderFollower);
    }

    private static void DisableBlackSceneBodies(Scene scene)
    {
        var disabled = new HashSet<GameObject>();
        foreach (var transform in FindObjectsInScene<Transform>(scene))
        {
            if (transform.name.IndexOf("Black", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var candidate = transform;
            while (candidate.parent != null &&
                   candidate.parent.name.IndexOf("Black", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                candidate = candidate.parent;
            }

            if (!disabled.Add(candidate.gameObject))
            {
                continue;
            }

            Undo.RecordObject(candidate.gameObject, "Disable Black body while testing official MovementLocomotion");
            candidate.gameObject.SetActive(false);
            EditorUtility.SetDirty(candidate.gameObject);
        }
    }

    private static void DisableCompetingAvatarDrivers(Scene scene, GameObject playerController)
    {
        foreach (var driver in FindObjectsInScene<VrPlayerAvatarDriver>(scene))
        {
            if (driver == null || !driver.enabled)
            {
                continue;
            }

            Undo.RecordObject(driver, "Disable custom avatar driver for official MovementLocomotion test");
            driver.enabled = false;
            EditorUtility.SetDirty(driver);
        }
    }

    private static void DisableCompetingXriLocomotion(Scene scene)
    {
        foreach (var behaviour in FindObjectsInScene<MonoBehaviour>(scene))
        {
            if (behaviour == null || !behaviour.enabled)
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

    private static void ConfigurePersistentEventCall(
        SerializedProperty eventProperty,
        UnityEngine.Object target,
        string methodName,
        PersistentListenerMode mode)
    {
        var persistentCalls = RequireRelative(eventProperty, "m_PersistentCalls");
        var calls = RequireRelative(persistentCalls, "m_Calls");
        calls.arraySize = 1;
        var call = calls.GetArrayElementAtIndex(0);
        RequireRelative(call, "m_Target").objectReferenceValue = target;
        RequireRelative(call, "m_TargetAssemblyTypeName").stringValue = target.GetType().AssemblyQualifiedName;
        RequireRelative(call, "m_MethodName").stringValue = methodName;
        RequireRelative(call, "m_Mode").intValue = (int)mode;
        var arguments = RequireRelative(call, "m_Arguments");
        RequireRelative(arguments, "m_ObjectArgument").objectReferenceValue = null;
        RequireRelative(arguments, "m_ObjectArgumentAssemblyTypeName").stringValue = typeof(UnityEngine.Object).AssemblyQualifiedName;
        RequireRelative(arguments, "m_IntArgument").intValue = 0;
        RequireRelative(arguments, "m_FloatArgument").floatValue = 0.0f;
        RequireRelative(arguments, "m_StringArgument").stringValue = string.Empty;
        RequireRelative(arguments, "m_BoolArgument").boolValue = false;
        RequireRelative(call, "m_CallState").intValue = (int)UnityEventCallState.RuntimeOnly;
    }

    private static void SetObjectArray<T>(SerializedProperty arrayProperty, IReadOnlyList<T> values)
        where T : UnityEngine.Object
    {
        arrayProperty.arraySize = values.Count;
        for (var i = 0; i < values.Count; i++)
        {
            arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
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

    private static SerializedProperty RequireRelative(SerializedProperty property, string path)
    {
        var relative = property.FindPropertyRelative(path);
        if (relative == null)
        {
            throw new InvalidOperationException($"Could not find serialized property {path} under {property.propertyPath}.");
        }

        return relative;
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

    private static Quaternion FlattenYaw(Quaternion rotation)
    {
        return Quaternion.Euler(0.0f, rotation.eulerAngles.y, 0.0f);
    }

    private static Transform FindDirectChild(Transform parent, string objectName)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        var names = new Stack<string>();
        while (transform != null)
        {
            names.Push(transform.name);
            transform = transform.parent;
        }

        return string.Join("/", names);
    }

    private static GameObject FindSceneObjectByName(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root;
            }

            var child = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == objectName);
            if (child != null)
            {
                return child.gameObject;
            }
        }

        return null;
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

    private static void EnsureEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("Exit Play Mode before configuring official MovementLocomotion.");
        }
    }
}
