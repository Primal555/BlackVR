using System.Collections.Generic;
using UnityEngine.Animations.Rigging;
using UnityEngine;
using UnityEngine.XR;

[DefaultExecutionOrder(10000)]
public sealed class VrPlayerAvatarDriver : MonoBehaviour
{
    [Header("References")]
    public Transform modelRoot;
    public Animator animator;
    public Transform headTarget;
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    [Header("Body Follow")]
    public float standingEyeHeight = 1.62f;
    public float headForwardOffset = 0.22f;
    public bool useAvatarHeadAnchor;
    public Vector3 avatarEyeLocalOffset;
    public bool autoScaleAvatarToPlayerHeight;
    public float minAvatarScale = 0.8f;
    public float maxAvatarScale = 1.25f;
    public Vector3 bodyPositionOffset;
    public float modelYawOffset = 180f;
    public float positionSharpness = 18f;
    public float rotationSharpness = 18f;
    public bool useBodyYawDeadZone = true;
    public float bodyYawFollowAngle = 35f;
    public float bodyYawSharpness = 7f;
    public bool alignFeetToGround;
    public float groundY;
    public float footGroundOffset;
    public bool useGroundRaycast = true;
    public LayerMask groundLayers = ~0;
    public float groundRaycastHeight = 3f;
    public float groundRaycastDistance = 8f;

    [Header("Hands")]
    public bool enableHandTracking = true;
    public bool useAnimationRiggingArmIk;
    public TwoBoneIKConstraint leftArmIkConstraint;
    public TwoBoneIKConstraint rightArmIkConstraint;
    public Transform leftElbowHint;
    public Transform rightElbowHint;
    [Range(0f, 1f)] public float handPositionWeight = 0.95f;
    [Range(0f, 1f)] public float handRotationWeight = 0.35f;
    public float armMaxReachMultiplier = 0.9995f;
    public Vector3 leftHandPositionOffset;
    public Vector3 rightHandPositionOffset;
    public Vector3 leftWristToGripOffset;
    public Vector3 rightWristToGripOffset;
    public Vector3 leftHandBodySpaceOffset;
    public Vector3 rightHandBodySpaceOffset;
    public Vector3 leftHandRotationOffsetEuler;
    public Vector3 rightHandRotationOffsetEuler;
    public Vector3 leftElbowPoleLocal = new Vector3(-0.25f, -0.35f, 0.15f);
    public Vector3 rightElbowPoleLocal = new Vector3(0.25f, -0.35f, 0.15f);
    [Range(0f, 1f)] public float shoulderAimWeight = 0.35f;
    public bool enableHandDirectionAlignment = true;
    [Range(0f, 1f)] public float handDirectionAlignmentWeight = 1f;

    [Header("Head")]
    public bool enableHeadTracking = true;
    public Vector3 headRotationOffsetEuler = new Vector3(0f, 180f, 0f);

    [Header("Fingers")]
    public bool enableControllerFingerCurl = true;
    public Vector3 leftGripFingerCurlEuler = new Vector3(55f, 0f, 0f);
    public Vector3 rightGripFingerCurlEuler = new Vector3(55f, 0f, 0f);
    public Vector3 leftTriggerFingerCurlEuler = new Vector3(45f, 0f, 0f);
    public Vector3 rightTriggerFingerCurlEuler = new Vector3(45f, 0f, 0f);
    public Vector3 leftHumanoidGripFingerCurlEuler = new Vector3(-55f, 0f, 0f);
    public Vector3 rightHumanoidGripFingerCurlEuler = new Vector3(55f, 0f, 0f);
    public Vector3 leftHumanoidTriggerFingerCurlEuler = new Vector3(-50f, 0f, 0f);
    public Vector3 rightHumanoidTriggerFingerCurlEuler = new Vector3(50f, 0f, 0f);
    public Vector3 leftHumanoidThumbFingerCurlEuler = new Vector3(20f, 0f, -35f);
    public Vector3 rightHumanoidThumbFingerCurlEuler = new Vector3(-20f, 0f, -35f);
    public bool closeHumanoidTriggerFingersWithGrip = true;

    [Header("First Person")]
    public bool hideHeadBone;
    public bool hideControllerVisuals = true;

    readonly Dictionary<Transform, Quaternion> initialLocalRotations = new Dictionary<Transform, Quaternion>();
    Transform headBone;
    Transform leftShoulder;
    Transform leftUpperArm;
    Transform leftLowerArm;
    Transform leftHand;
    Transform rightShoulder;
    Transform rightUpperArm;
    Transform rightLowerArm;
    Transform rightHand;
    readonly List<Transform> leftUpperArmFollowers = new List<Transform>();
    readonly List<Transform> rightUpperArmFollowers = new List<Transform>();
    readonly List<Transform> leftLowerArmFollowers = new List<Transform>();
    readonly List<Transform> rightLowerArmFollowers = new List<Transform>();
    readonly List<Transform> leftGripFingerControls = new List<Transform>();
    readonly List<Transform> rightGripFingerControls = new List<Transform>();
    readonly List<Transform> leftTriggerFingerControls = new List<Transform>();
    readonly List<Transform> rightTriggerFingerControls = new List<Transform>();
    readonly List<Transform> leftThumbFingerControls = new List<Transform>();
    readonly List<Transform> rightThumbFingerControls = new List<Transform>();
    Transform leftMiddleFingerDirectionBone;
    Transform rightMiddleFingerDirectionBone;
    readonly List<InputDevice> leftHandDevices = new List<InputDevice>();
    readonly List<InputDevice> rightHandDevices = new List<InputDevice>();
    Transform rigRoot;
    Transform cachedModelRoot;
    Quaternion bodyYawRotation = Quaternion.identity;
    Vector3 headInitialScale = Vector3.one;
    Vector3 modelHeadLocalPosition;
    Vector3 initialModelLocalScale = Vector3.one;
    float avatarScaleMultiplier = 1f;
    float modelFootLocalY;
    bool cached;
    bool bodyYawInitialized;
    bool footLocalYCached;
    bool headLocalPositionCached;
    bool initialModelScaleCached;
    bool avatarScaleCalibrated;
    bool missingReferenceLogged;
    bool boneFallbackLogged;
    bool rigSummaryLogged;

    void Awake()
    {
        AutoFindReferences();
        CacheBones();
    }

    void OnEnable()
    {
        AutoFindReferences();
        CacheBones();
    }

    void LateUpdate()
    {
        AutoFindReferences();
        CacheBones();

        if (!modelRoot || !headTarget)
        {
            return;
        }

        RemoveGeneratedThirdPersonCameras();
        ApplyControllerVisualVisibility();
        FollowHeadYaw();

        if (!cached)
        {
            return;
        }

        ResetTrackedBoneRotations(!useAnimationRiggingArmIk);

        if (enableHeadTracking)
        {
            ApplyHeadTracking();
        }

        if (useAnimationRiggingArmIk)
        {
            UpdateAnimationRiggingArmIk();
        }
        else if (enableHandTracking)
        {
            SolveArm(leftShoulder, leftUpperArm, leftLowerArm, leftHand, leftHandTarget, leftUpperArmFollowers, leftLowerArmFollowers, leftHandPositionOffset, leftWristToGripOffset, leftHandBodySpaceOffset, leftHandRotationOffsetEuler, leftElbowPoleLocal);
            SolveArm(rightShoulder, rightUpperArm, rightLowerArm, rightHand, rightHandTarget, rightUpperArmFollowers, rightLowerArmFollowers, rightHandPositionOffset, rightWristToGripOffset, rightHandBodySpaceOffset, rightHandRotationOffsetEuler, rightElbowPoleLocal);
            ApplyHandDirectionAlignment();
        }

        if (enableControllerFingerCurl)
        {
            ApplyControllerFingerCurl();
        }

        if (hideHeadBone && headBone)
        {
            headBone.localScale = Vector3.zero;
        }
        else if (headBone)
        {
            headBone.localScale = headInitialScale;
        }
    }

    void ApplyControllerVisualVisibility()
    {
        var visible = !hideControllerVisuals;
        SetControllerVisualsVisible(leftHandTarget, visible);
        SetControllerVisualsVisible(rightHandTarget, visible);
        SetNamedControllerVisualsVisible(transform, visible);
    }

    void AutoFindReferences()
    {
        if (!headTarget)
        {
            var xrCamera = FindChildByName(transform, "Main Camera");
            if (xrCamera)
            {
                headTarget = xrCamera;
            }
            else if (Camera.main)
            {
                headTarget = Camera.main.transform;
            }
        }

        if (!modelRoot)
        {
            var namedBody = FindFirstChildByName(transform, "Black_PlayerTest", "Black_CameraTest", "PlayerBody_MinamiKotaro");
            if (!namedBody)
            {
                namedBody = FindSceneTransformByName("Black_PlayerTest", "Black_CameraTest", "PlayerBody_MinamiKotaro");
            }

            if (namedBody)
            {
                modelRoot = namedBody;
            }
        }

        if (!animator && modelRoot)
        {
            var preferredRig = FindChildByName(modelRoot, "rig_humanoid");
            animator = preferredRig ? preferredRig.GetComponent<Animator>() : modelRoot.GetComponentInChildren<Animator>(true);
        }
    }

    void CacheBones()
    {
        if (cached && cachedModelRoot == modelRoot && leftUpperArm && leftLowerArm && leftHand && rightUpperArm && rightLowerArm && rightHand)
        {
            return;
        }

        var modelChanged = cachedModelRoot != modelRoot;
        ClearBoneCache(modelChanged);
        cachedModelRoot = modelRoot;

        if (animator && animator.isHuman)
        {
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
        else if (modelRoot)
        {
            CacheArpFallbackBones();
        }

        CacheInitialRotation(headBone);
        CacheInitialRotation(leftShoulder);
        CacheInitialRotation(leftUpperArm);
        CacheInitialRotation(leftLowerArm);
        CacheInitialRotation(leftHand);
        CacheInitialRotation(rightShoulder);
        CacheInitialRotation(rightUpperArm);
        CacheInitialRotation(rightLowerArm);
        CacheInitialRotation(rightHand);
        CacheInitialRotations(leftUpperArmFollowers);
        CacheInitialRotations(rightUpperArmFollowers);
        CacheInitialRotations(leftLowerArmFollowers);
        CacheInitialRotations(rightLowerArmFollowers);
        CacheFingerControls();

        if (headBone && modelRoot)
        {
            headInitialScale = headBone.localScale;
            modelHeadLocalPosition = modelRoot.InverseTransformPoint(headBone.position);
            headLocalPositionCached = true;
        }

        CacheInitialModelScale();
        CacheModelFootLocalY();

        cached = leftUpperArm && leftLowerArm && leftHand && rightUpperArm && rightLowerArm && rightHand;

        if (cached && !rigSummaryLogged)
        {
            rigSummaryLogged = true;
            Debug.Log($"[VrPlayerAvatarDriver] Arm IK bones: L={leftUpperArm.name}/{leftLowerArm.name}/{leftHand.name}, R={rightUpperArm.name}/{rightLowerArm.name}/{rightHand.name}. Followers: LUpper={leftUpperArmFollowers.Count}, LLower={leftLowerArmFollowers.Count}, RUpper={rightUpperArmFollowers.Count}, RLower={rightLowerArmFollowers.Count}. Finger controls: LGrip={leftGripFingerControls.Count}, LTrigger={leftTriggerFingerControls.Count}, RGrip={rightGripFingerControls.Count}, RTrigger={rightTriggerFingerControls.Count}.");
        }

        if (!cached && !missingReferenceLogged)
        {
            missingReferenceLogged = true;
            Debug.LogWarning("[VrPlayerAvatarDriver] Arm bones were not found. Body follow will still run, but hand IK is disabled until ARP bone names are available.");
        }
    }

    void ClearBoneCache(bool resetLogs)
    {
        cached = false;
        rigRoot = null;
        headBone = null;
        leftShoulder = null;
        leftUpperArm = null;
        leftLowerArm = null;
        leftHand = null;
        rightShoulder = null;
        rightUpperArm = null;
        rightLowerArm = null;
        rightHand = null;
        leftUpperArmFollowers.Clear();
        rightUpperArmFollowers.Clear();
        leftLowerArmFollowers.Clear();
        rightLowerArmFollowers.Clear();
        leftGripFingerControls.Clear();
        rightGripFingerControls.Clear();
        leftTriggerFingerControls.Clear();
        rightTriggerFingerControls.Clear();
        leftThumbFingerControls.Clear();
        rightThumbFingerControls.Clear();
        leftMiddleFingerDirectionBone = null;
        rightMiddleFingerDirectionBone = null;
        initialLocalRotations.Clear();
        if (resetLogs)
        {
            missingReferenceLogged = false;
            rigSummaryLogged = false;
            bodyYawInitialized = false;
            footLocalYCached = false;
            headLocalPositionCached = false;
            initialModelScaleCached = false;
            avatarScaleCalibrated = false;
            avatarScaleMultiplier = 1f;
        }
    }

    void CacheInitialModelScale()
    {
        if (initialModelScaleCached || !modelRoot)
        {
            return;
        }

        initialModelLocalScale = modelRoot.localScale;
        initialModelScaleCached = true;
    }

    void CacheModelFootLocalY()
    {
        if (footLocalYCached || !modelRoot)
        {
            return;
        }

        var hasBounds = false;
        var minLocalY = 0f;
        foreach (var renderer in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!renderer.sharedMesh || renderer.sharedMesh.name.StartsWith("cs_"))
            {
                continue;
            }

            var bounds = renderer.bounds;
            var corners = new[]
            {
                new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.max.z),
            };

            foreach (var corner in corners)
            {
                var localY = modelRoot.InverseTransformPoint(corner).y;
                if (!hasBounds || localY < minLocalY)
                {
                    hasBounds = true;
                    minLocalY = localY;
                }
            }
        }

        if (hasBounds)
        {
            modelFootLocalY = minLocalY;
            footLocalYCached = true;
            Debug.Log($"[VrPlayerAvatarDriver] Foot local Y calibrated to {modelFootLocalY:F3}.");
        }
    }

    void CacheArpFallbackBones()
    {
        rigRoot = FindChildByName(modelRoot, "rig_humanoid");
        var boneRoot = rigRoot ? rigRoot : modelRoot;
        headBone = FindFirstChildByName(boneRoot, "head.x", "c_head.x", "neck.x");
        leftShoulder = FindFirstChildByName(boneRoot, "shoulder.l", "c_shoulder.l");
        leftUpperArm = FindFirstChildByName(boneRoot, "arm_stretch.l", "arm.l", "arm_fk.l", "c_arm_fk.l", "arm_twist.l");
        leftLowerArm = FindFirstChildByName(boneRoot, "forearm_stretch.l", "forearm_twist.l", "forearm.l", "forearm_fk.l", "c_forearm_fk.l");
        leftHand = FindFirstChildByName(boneRoot, "hand.l", "c_hand_fk.l");
        rightShoulder = FindFirstChildByName(boneRoot, "shoulder.r", "c_shoulder.r");
        rightUpperArm = FindFirstChildByName(boneRoot, "arm_stretch.r", "arm.r", "arm_fk.r", "c_arm_fk.r", "arm_twist.r");
        rightLowerArm = FindFirstChildByName(boneRoot, "forearm_stretch.r", "forearm_twist.r", "forearm.r", "forearm_fk.r", "c_forearm_fk.r");
        rightHand = FindFirstChildByName(boneRoot, "hand.r", "c_hand_fk.r");
        AddExistingBone(leftUpperArmFollowers, "arm_twist.l", "arm_twist.l.001");
        AddExistingBone(rightUpperArmFollowers, "arm_twist.r", "arm_twist.r.001");
        AddExistingBone(leftLowerArmFollowers, "forearm_twist.l", "forearm_twist.l.001");
        AddExistingBone(rightLowerArmFollowers, "forearm_twist.r", "forearm_twist.r.001");

        if (!boneFallbackLogged)
        {
            boneFallbackLogged = true;
            Debug.Log($"[VrPlayerAvatarDriver] Using ARP transform bone fallback under {(rigRoot ? rigRoot.name : modelRoot.name)}.");
        }
    }

    void AddExistingBone(List<Transform> bones, params string[] boneNames)
    {
        foreach (var boneName in boneNames)
        {
            var bone = FindChildByName(rigRoot ? rigRoot : modelRoot, boneName);
            if (bone && !bones.Contains(bone))
            {
                bones.Add(bone);
            }
        }
    }

    void CacheFingerControls()
    {
        if (animator && animator.isHuman)
        {
            CacheHumanoidFingerControls();
            return;
        }

        CacheArpFingerControls();
    }

    void CacheHumanoidFingerControls()
    {
        if (leftGripFingerControls.Count > 0 || rightGripFingerControls.Count > 0)
        {
            return;
        }

        AddHumanoidFingerControls(leftGripFingerControls,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal);
        AddHumanoidFingerControls(rightGripFingerControls,
            HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal);
        AddHumanoidFingerControls(leftTriggerFingerControls,
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal);
        AddHumanoidFingerControls(rightTriggerFingerControls,
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal);
        AddHumanoidFingerControls(leftThumbFingerControls,
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate);
        AddHumanoidFingerControls(rightThumbFingerControls,
            HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate);
        leftMiddleFingerDirectionBone = animator.GetBoneTransform(HumanBodyBones.LeftMiddleDistal)
            ? animator.GetBoneTransform(HumanBodyBones.LeftMiddleDistal)
            : animator.GetBoneTransform(HumanBodyBones.LeftMiddleIntermediate);
        rightMiddleFingerDirectionBone = animator.GetBoneTransform(HumanBodyBones.RightMiddleDistal)
            ? animator.GetBoneTransform(HumanBodyBones.RightMiddleDistal)
            : animator.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate);
    }

    void AddHumanoidFingerControls(List<Transform> controls, params HumanBodyBones[] fingerBones)
    {
        foreach (var fingerBone in fingerBones)
        {
            var control = animator.GetBoneTransform(fingerBone);
            if (control)
            {
                controls.Add(control);
                CacheInitialRotation(control);
            }
        }
    }

    void CacheArpFingerControls()
    {
        if (!modelRoot || leftGripFingerControls.Count > 0 || rightGripFingerControls.Count > 0)
        {
            return;
        }

        AddFingerControls(leftGripFingerControls,
            "c_middle1.l", "c_middle2.l", "c_middle3.l",
            "c_ring1.l", "c_ring2.l", "c_ring3.l",
            "c_pinky1.l", "c_pinky2.l", "c_pinky3.l",
            "middle1.l", "middle2_rot.l", "middle3_rot.l",
            "ring1.l", "ring2_rot.l", "ring3_rot.l",
            "pinky1.l", "pinky2_rot.l", "pinky3_rot.l");
        AddFingerControls(rightGripFingerControls,
            "c_middle1.r", "c_middle2.r", "c_middle3.r",
            "c_ring1.r", "c_ring2.r", "c_ring3.r",
            "c_pinky1.r", "c_pinky2.r", "c_pinky3.r",
            "middle1.r", "middle2_rot.r", "middle3_rot.r",
            "ring1.r", "ring2_rot.r", "ring3_rot.r",
            "pinky1.r", "pinky2_rot.r", "pinky3_rot.r");
        AddFingerControls(leftTriggerFingerControls,
            "c_index1.l", "c_index2.l", "c_index3.l",
            "c_thumb1.l", "c_thumb2.l", "c_thumb3.l",
            "index1.l", "index2_rot.l", "index3_rot.l",
            "thumb1.l", "thumb2_rot.l", "thumb3_rot.l");
        AddFingerControls(rightTriggerFingerControls,
            "c_index1.r", "c_index2.r", "c_index3.r",
            "c_thumb1.r", "c_thumb2.r", "c_thumb3.r",
            "index1.r", "index2_rot.r", "index3_rot.r",
            "thumb1.r", "thumb2_rot.r", "thumb3_rot.r");
    }

    void AddFingerControls(List<Transform> controls, params string[] controlNames)
    {
        foreach (var controlName in controlNames)
        {
            var control = FindChildByName(rigRoot ? rigRoot : modelRoot, controlName);
            if (control)
            {
                controls.Add(control);
                CacheInitialRotation(control);
            }
        }
    }

    void CacheInitialRotation(Transform bone)
    {
        if (bone && !initialLocalRotations.ContainsKey(bone))
        {
            initialLocalRotations.Add(bone, bone.localRotation);
        }
    }

    void CacheInitialRotations(List<Transform> bones)
    {
        foreach (var bone in bones)
        {
            CacheInitialRotation(bone);
        }
    }

    void ResetTrackedBoneRotations(bool includeArmBones)
    {
        foreach (var item in initialLocalRotations)
        {
            if (item.Key && (includeArmBones || !IsArmBone(item.Key)))
            {
                item.Key.localRotation = item.Value;
            }
        }
    }

    bool IsArmBone(Transform bone)
    {
        return bone == leftShoulder
            || bone == leftUpperArm
            || bone == leftLowerArm
            || bone == leftHand
            || bone == rightShoulder
            || bone == rightUpperArm
            || bone == rightLowerArm
            || bone == rightHand
            || leftUpperArmFollowers.Contains(bone)
            || leftLowerArmFollowers.Contains(bone)
            || rightUpperArmFollowers.Contains(bone)
            || rightLowerArmFollowers.Contains(bone);
    }

    void FollowHeadYaw()
    {
        var forward = Vector3.ProjectOnPlane(headTarget.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(modelRoot.forward, Vector3.up);
        }

        var headYawRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        if (!bodyYawInitialized)
        {
            bodyYawInitialized = true;
            bodyYawRotation = headYawRotation;
        }

        var yawDelta = Vector3.SignedAngle(bodyYawRotation * Vector3.forward, headYawRotation * Vector3.forward, Vector3.up);
        if (!useBodyYawDeadZone || Mathf.Abs(yawDelta) > bodyYawFollowAngle)
        {
            bodyYawRotation = Quaternion.Slerp(bodyYawRotation, headYawRotation, SmoothingWeight(bodyYawSharpness));
        }

        var targetRotation = bodyYawRotation * Quaternion.Euler(0f, modelYawOffset, 0f);
        var positionWeight = SmoothingWeight(positionSharpness);
        var targetLocalScale = ResolveTargetLocalScale();
        var nextLocalScale = Vector3.Lerp(modelRoot.localScale, targetLocalScale, positionWeight);
        var targetPosition = ResolveBodyTargetPosition(targetRotation, nextLocalScale);

        var rotationWeight = SmoothingWeight(rotationSharpness);
        modelRoot.localScale = nextLocalScale;
        modelRoot.position = Vector3.Lerp(modelRoot.position, targetPosition, positionWeight);
        modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, targetRotation, rotationWeight);
    }

    Vector3 ResolveBodyTargetPosition(Quaternion targetRotation, Vector3 targetLocalScale)
    {
        Vector3 targetPosition;
        if (useAvatarHeadAnchor && headLocalPositionCached)
        {
            var localEyePosition = modelHeadLocalPosition + avatarEyeLocalOffset;
            var scaledEyeOffset = Vector3.Scale(localEyePosition, targetLocalScale);
            targetPosition = headTarget.position - targetRotation * scaledEyeOffset + bodyPositionOffset;
            targetPosition -= bodyYawRotation * new Vector3(0f, 0f, headForwardOffset);
        }
        else
        {
            targetPosition = headTarget.position - bodyYawRotation * new Vector3(0f, standingEyeHeight, headForwardOffset) + bodyPositionOffset;
        }

        if (alignFeetToGround && footLocalYCached)
        {
            var targetGroundY = ResolveGroundY(targetPosition);
            targetPosition.y = targetGroundY + footGroundOffset - modelFootLocalY * targetLocalScale.y;
        }

        return targetPosition;
    }

    Vector3 ResolveTargetLocalScale()
    {
        CacheInitialModelScale();
        if (!initialModelScaleCached)
        {
            return modelRoot.localScale;
        }

        if (!autoScaleAvatarToPlayerHeight || !alignFeetToGround || !headLocalPositionCached || !footLocalYCached)
        {
            return initialModelLocalScale;
        }

        if (!avatarScaleCalibrated)
        {
            var avatarEyeHeight = modelHeadLocalPosition.y + avatarEyeLocalOffset.y - modelFootLocalY;
            var groundProbePosition = headTarget ? headTarget.position : modelRoot.position;
            var desiredEyeHeight = headTarget.position.y - ResolveGroundY(groundProbePosition) - footGroundOffset;
            if (avatarEyeHeight > 0.1f && desiredEyeHeight > 0.1f && initialModelLocalScale.y > 0.0001f)
            {
                var minScale = Mathf.Min(minAvatarScale, maxAvatarScale);
                var maxScale = Mathf.Max(minAvatarScale, maxAvatarScale);
                avatarScaleMultiplier = Mathf.Clamp(desiredEyeHeight / (avatarEyeHeight * initialModelLocalScale.y), minScale, maxScale);
                avatarScaleCalibrated = true;
                Debug.Log($"[VrPlayerAvatarDriver] Avatar height scale calibrated to {avatarScaleMultiplier:F3} from eye height {desiredEyeHeight:F3}.");
            }
        }

        return initialModelLocalScale * avatarScaleMultiplier;
    }

    float ResolveGroundY(Vector3 targetPosition)
    {
        if (!useGroundRaycast)
        {
            return groundY;
        }

        var rayOrigin = new Vector3(targetPosition.x, targetPosition.y + groundRaycastHeight, targetPosition.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, groundRaycastDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return groundY;
    }

    void ApplyHeadTracking()
    {
        if (!headBone || !headTarget)
        {
            return;
        }

        headBone.rotation = headTarget.rotation * Quaternion.Euler(headRotationOffsetEuler);
    }

    void UpdateAnimationRiggingArmIk()
    {
        UpdateElbowHint(leftElbowHint, leftElbowPoleLocal);
        UpdateElbowHint(rightElbowHint, rightElbowPoleLocal);
        UpdateTwoBoneIkWeights(leftArmIkConstraint);
        UpdateTwoBoneIkWeights(rightArmIkConstraint);
    }

    void UpdateElbowHint(Transform hint, Vector3 localPosition)
    {
        if (!hint || !modelRoot)
        {
            return;
        }

        hint.position = modelRoot.TransformPoint(localPosition);
    }

    void UpdateTwoBoneIkWeights(TwoBoneIKConstraint constraint)
    {
        if (!constraint)
        {
            return;
        }

        constraint.weight = enableHandTracking ? 1f : 0f;
        var data = constraint.data;
        data.targetPositionWeight = Mathf.Clamp01(handPositionWeight);
        data.targetRotationWeight = Mathf.Clamp01(handRotationWeight);
        data.hintWeight = 1f;
        constraint.data = data;
    }

    void SolveArm(Transform shoulder, Transform upperArm, Transform lowerArm, Transform hand, Transform target, List<Transform> upperFollowers, List<Transform> lowerFollowers, Vector3 positionOffset, Vector3 wristToGripOffset, Vector3 bodySpaceOffset, Vector3 rotationOffsetEuler, Vector3 elbowPoleLocal)
    {
        if (!upperArm || !lowerArm || !hand || !target || !modelRoot)
        {
            return;
        }

        var targetRotation = target.rotation * Quaternion.Euler(rotationOffsetEuler);
        var desiredTargetPosition = GetDesiredHandTargetPosition(target, positionOffset, wristToGripOffset, bodySpaceOffset, rotationOffsetEuler);
        var targetPosition = Vector3.Lerp(hand.position, desiredTargetPosition, handPositionWeight);

        AimShoulderTowardTarget(shoulder, upperArm, targetPosition);
        ApplyTwoBoneIk(upperArm, lowerArm, hand, targetPosition, modelRoot.TransformPoint(elbowPoleLocal), upperFollowers, lowerFollowers, armMaxReachMultiplier);
        hand.rotation = Quaternion.Slerp(hand.rotation, targetRotation, handRotationWeight);
    }

    void AimShoulderTowardTarget(Transform shoulder, Transform upperArm, Vector3 targetPosition)
    {
        if (!shoulder || !upperArm || shoulderAimWeight <= 0f)
        {
            return;
        }

        var currentDirection = upperArm.position - shoulder.position;
        var desiredDirection = targetPosition - shoulder.position;
        if (currentDirection.sqrMagnitude < 0.0001f || desiredDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var delta = Quaternion.FromToRotation(currentDirection.normalized, desiredDirection.normalized);
        var targetRotation = delta * shoulder.rotation;
        shoulder.rotation = Quaternion.Slerp(shoulder.rotation, targetRotation, Mathf.Clamp01(shoulderAimWeight));
    }

    void ApplyHandDirectionAlignment()
    {
        AlignHandDirection(leftHand, leftMiddleFingerDirectionBone, leftHandTarget, leftHandRotationOffsetEuler);
        AlignHandDirection(rightHand, rightMiddleFingerDirectionBone, rightHandTarget, rightHandRotationOffsetEuler);
    }

    void AlignHandDirection(Transform hand, Transform middleFingerDirectionBone, Transform target, Vector3 rotationOffsetEuler)
    {
        if (!enableHandDirectionAlignment || !hand || !middleFingerDirectionBone || !target)
        {
            return;
        }

        var currentDirection = middleFingerDirectionBone.position - hand.position;
        var targetDirection = (target.rotation * Quaternion.Euler(rotationOffsetEuler)) * Vector3.forward;
        if (currentDirection.sqrMagnitude < 0.0001f || targetDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var delta = Quaternion.FromToRotation(currentDirection.normalized, targetDirection.normalized);
        var alignedRotation = delta * hand.rotation;
        hand.rotation = Quaternion.Slerp(hand.rotation, alignedRotation, Mathf.Clamp01(handDirectionAlignmentWeight));
    }

    Vector3 GetDesiredHandTargetPosition(Transform target, Vector3 positionOffset, Vector3 wristToGripOffset, Vector3 bodySpaceOffset, Vector3 rotationOffsetEuler)
    {
        var targetRotation = target.rotation * Quaternion.Euler(rotationOffsetEuler);
        return target.position - targetRotation * wristToGripOffset + targetRotation * positionOffset + modelRoot.TransformVector(bodySpaceOffset);
    }

    void ApplyControllerFingerCurl()
    {
        var leftGrip = ReadControllerAxis(InputDeviceCharacteristics.Left, CommonUsages.grip);
        var rightGrip = ReadControllerAxis(InputDeviceCharacteristics.Right, CommonUsages.grip);
        var leftTrigger = ReadControllerAxis(InputDeviceCharacteristics.Left, CommonUsages.trigger);
        var rightTrigger = ReadControllerAxis(InputDeviceCharacteristics.Right, CommonUsages.trigger);

        if (animator && animator.isHuman)
        {
            var leftTriggerCurl = closeHumanoidTriggerFingersWithGrip ? Mathf.Max(leftGrip, leftTrigger) : leftTrigger;
            var rightTriggerCurl = closeHumanoidTriggerFingersWithGrip ? Mathf.Max(rightGrip, rightTrigger) : rightTrigger;
            ApplyFingerCurl(leftGripFingerControls, leftGrip, leftHumanoidGripFingerCurlEuler);
            ApplyFingerCurl(rightGripFingerControls, rightGrip, rightHumanoidGripFingerCurlEuler);
            ApplyFingerCurl(leftTriggerFingerControls, leftTriggerCurl, leftHumanoidTriggerFingerCurlEuler);
            ApplyFingerCurl(rightTriggerFingerControls, rightTriggerCurl, rightHumanoidTriggerFingerCurlEuler);
            ApplyFingerCurl(leftThumbFingerControls, leftTriggerCurl, leftHumanoidThumbFingerCurlEuler);
            ApplyFingerCurl(rightThumbFingerControls, rightTriggerCurl, rightHumanoidThumbFingerCurlEuler);
            return;
        }

        ApplyFingerCurl(leftGripFingerControls, leftGrip, leftGripFingerCurlEuler);
        ApplyFingerCurl(rightGripFingerControls, rightGrip, rightGripFingerCurlEuler);
        ApplyFingerCurl(leftTriggerFingerControls, leftTrigger, leftTriggerFingerCurlEuler);
        ApplyFingerCurl(rightTriggerFingerControls, rightTrigger, rightTriggerFingerCurlEuler);
    }

    float ReadControllerAxis(InputDeviceCharacteristics hand, InputFeatureUsage<float> usage)
    {
        var devices = hand == InputDeviceCharacteristics.Left ? leftHandDevices : rightHandDevices;
        devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(hand | InputDeviceCharacteristics.Controller, devices);
        foreach (var device in devices)
        {
            if (device.TryGetFeatureValue(usage, out var value))
            {
                return value;
            }
        }

        return 0f;
    }

    void ApplyFingerCurl(List<Transform> controls, float amount, Vector3 curlEuler)
    {
        foreach (var control in controls)
        {
            if (control && initialLocalRotations.TryGetValue(control, out var initialRotation))
            {
                control.localRotation = initialRotation * Quaternion.Euler(curlEuler * amount);
            }
        }
    }

    static void ApplyTwoBoneIk(Transform upperArm, Transform lowerArm, Transform hand, Vector3 targetPosition, Vector3 polePosition, List<Transform> upperFollowers, List<Transform> lowerFollowers, float maxReachMultiplier)
    {
        var rootPosition = upperArm.position;
        var middlePosition = lowerArm.position;
        var endPosition = hand.position;
        var upperLength = Vector3.Distance(rootPosition, middlePosition);
        var lowerLength = Vector3.Distance(middlePosition, endPosition);
        var targetVector = targetPosition - rootPosition;
        var maxReach = (upperLength + lowerLength) * Mathf.Clamp(maxReachMultiplier, 0.95f, 0.9999f);
        var targetDistance = Mathf.Clamp(targetVector.magnitude, 0.001f, maxReach);
        var targetDirection = targetVector.normalized;

        var poleDirection = Vector3.ProjectOnPlane(polePosition - rootPosition, targetDirection);
        if (poleDirection.sqrMagnitude < 0.0001f)
        {
            poleDirection = Vector3.ProjectOnPlane(middlePosition - rootPosition, targetDirection);
        }

        if (poleDirection.sqrMagnitude < 0.0001f)
        {
            poleDirection = Vector3.up;
        }

        poleDirection.Normalize();
        var rootToMiddleDistance = (targetDistance * targetDistance + upperLength * upperLength - lowerLength * lowerLength) / (2f * targetDistance);
        var bendDistance = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - rootToMiddleDistance * rootToMiddleDistance));
        var solvedMiddlePosition = rootPosition + targetDirection * rootToMiddleDistance + poleDirection * bendDistance;

        var upperDelta = RotateBoneTowards(upperArm, middlePosition - rootPosition, solvedMiddlePosition - rootPosition);
        ApplyRotationDelta(upperFollowers, upperDelta);
        var lowerDelta = RotateBoneTowards(lowerArm, endPosition - lowerArm.position, targetPosition - lowerArm.position);
        ApplyRotationDelta(lowerFollowers, lowerDelta);
    }

    static Quaternion RotateBoneTowards(Transform bone, Vector3 currentDirection, Vector3 desiredDirection)
    {
        if (currentDirection.sqrMagnitude < 0.0001f || desiredDirection.sqrMagnitude < 0.0001f)
        {
            return Quaternion.identity;
        }

        var delta = Quaternion.FromToRotation(currentDirection, desiredDirection);
        bone.rotation = delta * bone.rotation;
        return delta;
    }

    static void ApplyRotationDelta(List<Transform> bones, Quaternion delta)
    {
        if (delta == Quaternion.identity)
        {
            return;
        }

        foreach (var bone in bones)
        {
            if (bone)
            {
                bone.rotation = delta * bone.rotation;
            }
        }
    }

    static float SmoothingWeight(float sharpness)
    {
        if (sharpness <= 0f)
        {
            return 1f;
        }

        return 1f - Mathf.Exp(-sharpness * Time.deltaTime);
    }

    static Transform FindChildByName(Transform root, string childName)
    {
        if (!root)
        {
            return null;
        }

        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    static Transform FindFirstChildByName(Transform root, params string[] childNames)
    {
        if (!root)
        {
            return null;
        }

        foreach (var childName in childNames)
        {
            var child = FindChildByName(root, childName);
            if (child)
            {
                return child;
            }
        }

        return null;
    }

    static Transform FindSceneTransformByName(params string[] objectNames)
    {
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return null;
        }

        foreach (var rootObject in activeScene.GetRootGameObjects())
        {
            var match = FindFirstChildByName(rootObject.transform, objectNames);
            if (match)
            {
                return match;
            }
        }

        return null;
    }

    static void SetControllerVisualsVisible(Transform controllerRoot, bool visible)
    {
        if (!controllerRoot)
        {
            return;
        }

        foreach (var meshRenderer in controllerRoot.GetComponentsInChildren<MeshRenderer>(true))
        {
            meshRenderer.enabled = visible;
        }

        foreach (var skinnedRenderer in controllerRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            skinnedRenderer.enabled = visible;
        }
    }

    static void SetNamedControllerVisualsVisible(Transform root, bool visible)
    {
        if (!root)
        {
            return;
        }

        foreach (var meshRenderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (IsControllerVisual(meshRenderer.transform, root))
            {
                meshRenderer.enabled = visible;
            }
        }

        foreach (var skinnedRenderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (IsControllerVisual(skinnedRenderer.transform, root))
            {
                skinnedRenderer.enabled = visible;
            }
        }
    }

    static bool IsControllerVisual(Transform rendererTransform, Transform root)
    {
        for (var current = rendererTransform; current; current = current.parent)
        {
            if (IsControllerVisualName(current.name))
            {
                return true;
            }

            if (current == root)
            {
                break;
            }
        }

        return false;
    }

    static bool IsControllerVisualName(string objectName)
    {
        return objectName.Contains("Controller Visual")
            || objectName.Contains("ControllerVisual")
            || objectName.Contains("Controller Model")
            || objectName.Contains("ControllerModel")
            || objectName.Contains("XR Controller")
            || objectName.Contains("XRController");
    }

    void RemoveGeneratedThirdPersonCameras()
    {
        foreach (var camera in GetComponentsInChildren<Camera>(true))
        {
            if (camera.name == "Third Person Debug Camera")
            {
                Destroy(camera.gameObject);
            }
        }
    }
}
