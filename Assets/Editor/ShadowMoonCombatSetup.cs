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
        var rigs = UnityEngine.Object.FindObjectsOfType<OVRCameraRig>()
            .Where(r => r.gameObject.scene == scene && r.GetComponentInParent<MovementSdkOvrThumbstickInput>() != null).ToArray();
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var modelAnimator = modelAsset != null ? modelAsset.GetComponentInChildren<Animator>() : null;
        var idle = AssetDatabase.LoadAllAssetsAtPath(IdlePath).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (rigs.Length != 1 || modelAnimator == null || modelAnimator.avatar == null ||
            !modelAnimator.avatar.isValid || !modelAnimator.avatar.isHuman || idle == null || shader == null)
        {
            Debug.LogError("[ShadowMoon] Setup requires exactly one active player OVRCameraRig in the active scene, " +
                "a valid ShadowMoon Humanoid avatar, the sample Idle clip, and the URP Unlit shader. No scene objects were changed.");
            return;
        }
        var rig = rigs[0];
        if (rig.leftHandAnchor == null || rig.rightHandAnchor == null || rig.centerEyeAnchor == null)
        {
            Debug.LogError("[ShadowMoon] The player rig is missing hand or eye anchors.");
            return;
        }

        var player = rig.GetComponentInParent<MovementSdkOvrThumbstickInput>().transform;
        var existing = UnityEngine.Object.FindObjectsOfType<ShadowMoonHitReceiver>(true)
            .FirstOrDefault(t => t.gameObject.scene == scene);
        Undo.IncrementCurrentGroup();
        var undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Shadow Moon Combat Target");
        GameObject root = null;
        try
        {
            BindHand(rig.leftHandAnchor, rig, player, OVRInput.Controller.LTouch);
            BindHand(rig.rightHandAnchor, rig, player, OVRInput.Controller.RTouch);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[ShadowMoon] Existing target retained; hand bindings refreshed.", existing);
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
            var animator = model.GetComponentInChildren<Animator>();
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

            var forward = Vector3.ProjectOnPlane(rig.centerEyeAnchor.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = player.forward;
            var destination = rig.centerEyeAnchor.position + forward * 2f;
            destination.y = player.position.y;
            var groundHits = Physics.RaycastAll(destination + Vector3.up * 3f, Vector3.down, 10f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var floor = groundHits.Where(h => !h.transform.IsChildOf(player) && !h.transform.IsChildOf(root.transform) && h.normal.y > 0.7f)
                .OrderBy(h => Mathf.Abs(h.point.y - player.position.y)).ToArray();
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
            Debug.Log("[ShadowMoon] Target created with hand hit detection. Check its street position and save the scene. " +
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

    private static void BindHand(Transform anchor, OVRCameraRig rig, Transform player, OVRInput.Controller hand)
    {
        var detector = anchor.GetComponent<VrHandStrikeDetector>();
        if (detector == null) detector = Undo.AddComponent<VrHandStrikeDetector>(anchor.gameObject);
        Undo.RecordObject(detector, "Bind hand strike detector");
        detector.Configure(rig, player, hand);
        PrefabUtility.RecordPrefabInstancePropertyModifications(detector);
        EditorUtility.SetDirty(detector);
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
