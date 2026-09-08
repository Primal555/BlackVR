using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShadowMoonCombatSetup
{
    private const string ModelPath = "Assets/Characters/ShadowMoon/Models/ShadowMoon.fbx";
    private const string IdlePath = "Assets/Samples/Meta Movement/71.0.1/Advanced Samples/Locomotion/ThirdParty/Animations/Idle.fbx";
    private const string Folder = "Assets/Characters/ShadowMoon/Combat";

    [MenuItem("Kamen Rider/Setup Shadow Moon Combat Target")]
    private static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[ShadowMoon] Exit Play Mode before creating the combat target.");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
        {
            Debug.LogError("[ShadowMoon] Open a normal scene before creating the NPC.");
            return;
        }
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            Debug.LogError("[ShadowMoon] Model asset not found: " + ModelPath);
            return;
        }
        var modelAnimator = modelAsset.GetComponentInChildren<Animator>(true);
        if (modelAnimator == null || modelAnimator.avatar == null)
        {
            Debug.LogError("[ShadowMoon] The imported model has no Animator with an Avatar: " + ModelPath, modelAsset);
            return;
        }
        if (!modelAnimator.avatar.isValid || !modelAnimator.avatar.isHuman)
        {
            Debug.LogError("[ShadowMoon] The model Avatar cannot play Humanoid Idle. " +
                $"isValid={modelAnimator.avatar.isValid}, isHuman={modelAnimator.avatar.isHuman}. Check the model's Rig import settings.", modelAsset);
            return;
        }
        var idle = AssetDatabase.LoadAllAssetsAtPath(IdlePath).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (idle == null)
        {
            Debug.LogError("[ShadowMoon] No animation clip found in: " + IdlePath);
            return;
        }
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogError("[ShadowMoon] Hit-spark shader not found: Universal Render Pipeline/Unlit.");
            return;
        }

        var canBindPlayer = TryResolvePlayer(scene, out var rig, out var player);
        var existing = UnityEngine.Object.FindObjectsOfType<ShadowMoonHitReceiver>(true)
            .FirstOrDefault(t => t.gameObject.scene == scene);
        Undo.IncrementCurrentGroup();
        var undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Shadow Moon Combat Target");
        GameObject root = null;
        try
        {
            if (canBindPlayer)
            {
                BindHand(rig.leftHandAnchor, rig, player, OVRInput.Controller.LTouch);
                BindHand(rig.rightHandAnchor, rig, player, OVRInput.Controller.RTouch);
            }
            if (existing != null)
            {
                if (canBindPlayer)
                {
                    Undo.RecordObject(existing, "Bind NPC collision source");
                    existing.BindPlayer(player);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(existing);
                    EditorUtility.SetDirty(existing);
                }
                Selection.activeGameObject = existing.gameObject;
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log(canBindPlayer ? "[ShadowMoon] Existing NPC retained; player hit detection bindings refreshed."
                    : "[ShadowMoon] Existing NPC retained. Player hit detection is not bound; see the binding warning.", existing);
                return;
            }

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Characters/ShadowMoon", "Combat");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/ShadowMoonIdle.controller");
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/ShadowMoonIdle.controller");
                var state = controller.layers[0].stateMachine.AddState("Idle");
                state.motion = idle;
                controller.layers[0].stateMachine.defaultState = state;
                EditorUtility.SetDirty(controller);
            }
            var sparkMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/HitSpark.mat");
            if (sparkMaterial == null)
            {
                sparkMaterial = new Material(shader) { name = "HitSpark" };
                sparkMaterial.SetColor("_BaseColor", new Color(3f, 1.8f, 0.5f, 1));
                AssetDatabase.CreateAsset(sparkMaterial, Folder + "/HitSpark.mat");
            }

            root = new GameObject("ShadowMoon Combat Target");
            SceneManager.MoveGameObjectToScene(root, scene);
            var pivot = new GameObject("Hit Reaction Pivot").transform;
            pivot.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, scene);
            model.transform.SetParent(pivot, false);
            var animator = model.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (head == null || leftFoot == null || rightFoot == null)
                throw new InvalidOperationException("ShadowMoon avatar is missing head or foot bones.");
            var feet = (leftFoot.position + rightFoot.position) * 0.5f;
            var height = head.position.y - feet.y;
            if (height <= 0.01f)
                throw new InvalidOperationException("ShadowMoon's initial bone positions cannot be used to fit its collider.");
            model.transform.localScale *= 1.55f / height;
            feet = (leftFoot.position + rightFoot.position) * 0.5f;
            model.transform.position -= new Vector3(feet.x, feet.y - 0.08f, feet.z);
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                renderer.updateWhenOffscreen = true;

            // The NPC can be placed and tested even in a scene without a VR player.
            var viewpoint = canBindPlayer ? rig.centerEyeAnchor : null;
            var forward = viewpoint != null ? Vector3.ProjectOnPlane(viewpoint.forward, Vector3.up).normalized : Vector3.forward;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            var destination = viewpoint != null ? viewpoint.position + forward * 2f
                : SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            var floorReferenceHeight = canBindPlayer ? player.position.y : destination.y;
            destination.y = floorReferenceHeight;
            var groundHits = Physics.RaycastAll(destination + Vector3.up * 3f, Vector3.down, 10f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var floor = groundHits.Where(h => (player == null || !h.transform.IsChildOf(player)) &&
                    (rig == null || !h.transform.IsChildOf(rig.transform)) && !h.transform.IsChildOf(root.transform) && h.normal.y > 0.7f)
                .OrderBy(h => Mathf.Abs(h.point.y - floorReferenceHeight)).ToArray();
            if (floor.Length > 0) destination.y = floor[0].point.y;
            else Debug.LogWarning("[ShadowMoon] No floor found at the initial position. Place the target on a street collider before Play.");
            root.transform.SetPositionAndRotation(destination + Vector3.up * 0.02f, Quaternion.LookRotation(-forward, Vector3.up));

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.28f;
            capsule.center = Vector3.up * 0.9f;
            var body = root.AddComponent<Rigidbody>();
            body.mass = 90f;
            body.drag = 3f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 1f;
            audio.maxDistance = 12f;
            var receiver = root.AddComponent<ShadowMoonHitReceiver>();
            receiver.Configure(player, pivot, CreateSparks(root.transform, sparkMaterial));
            Undo.RegisterCreatedObjectUndo(root, "Create Shadow Moon target");
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("[ShadowMoon] Independent standing NPC created. Player hit detection bound: " + canBindPlayer +
                ". Check its street position and save the scene. " +
                "In Play Mode, the receiver component's Test Hit context menu works without a headset.", root);
        }
        catch (Exception exception)
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            Undo.RevertAllDownToGroup(undoGroup);
            Debug.LogException(exception);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static bool TryResolvePlayer(Scene targetScene, out OVRCameraRig rig, out Transform player)
    {
        rig = null;
        player = null;
        var inputs = UnityEngine.Object.FindObjectsOfType<MovementSdkOvrThumbstickInput>()
            .Where(input => input.isActiveAndEnabled && input.gameObject.scene == targetScene).ToArray();
        if (inputs.Length != 1)
        {
            Debug.LogWarning($"[ShadowMoon] Found {inputs.Length} candidate player input components in scene '{targetScene.name}'. " +
                "The NPC can still be created and tested independently. To bind punches, enable one player input component and rerun Setup.");
            return false;
        }

        // Use the same explicit reference as locomotion. In this project the camera
        // rig is a separate scene root, linked by OVRCameraRigFollowsLocomotion.
        var inputData = new SerializedObject(inputs[0]);
        var locomotion = inputData.FindProperty("_locomotion")?.objectReferenceValue;
        if (locomotion != null)
        {
            var locomotionData = new SerializedObject(locomotion);
            rig = locomotionData.FindProperty("_cameraRig")?.objectReferenceValue as OVRCameraRig;
        }
        if (rig == null)
        {
            var childRigs = inputs[0].GetComponentsInChildren<OVRCameraRig>()
                .Where(candidate => candidate.isActiveAndEnabled).ToArray();
            if (childRigs.Length == 1) rig = childRigs[0];
        }
        if (rig == null || rig.gameObject.scene != targetScene || !rig.isActiveAndEnabled || rig.trackingSpace == null ||
            rig.leftHandAnchor == null || rig.rightHandAnchor == null || rig.centerEyeAnchor == null)
        {
            Debug.LogWarning("[ShadowMoon] Player locomotion's camera rig is missing, in another scene, inactive, or lacks tracking anchors. " +
                "The independent NPC can still be tested using Test Hit. Check the player's camera reference and rerun Setup.", inputs[0]);
            rig = null;
            return false;
        }
        player = inputs[0].transform;
        Debug.Log($"[ShadowMoon] Player hit detection: {player.name} -> {rig.name}. The NPC uses its own Animator.", inputs[0]);
        return true;
    }

    private static void BindHand(Transform anchor, OVRCameraRig rig, Transform player, OVRInput.Controller hand)
    {
        var detector = anchor.GetComponent<VrHandStrikeDetector>();
        if (detector == null) detector = Undo.AddComponent<VrHandStrikeDetector>(anchor.gameObject);
        Undo.RecordObject(detector, "Bind hand strike detector");
        detector.Configure(rig, player, hand);
        PrefabUtility.RecordPrefabInstancePropertyModifications(detector);
        EditorUtility.SetDirty(detector);
        EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
    }

    private static ParticleSystem CreateSparks(Transform parent, Material material)
    {
        var go = new GameObject("Hit Sparks");
        go.transform.SetParent(parent, false);
        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.018f);
        main.startSpeed = 0f;
        main.gravityModifier = 0.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 96;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        renderer.mesh = sphere.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(sphere);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return particles;
    }
}
