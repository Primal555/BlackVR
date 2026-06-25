using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class BlackStandingPostureTuner : MonoBehaviour
{
    [SerializeField] private bool enableTuning = true;
    [SerializeField, Range(0.0f, 1.0f)] private float weight = 1.0f;

    [Header("Local Rotation Offsets")]
    [SerializeField] private Vector3 hipsEulerOffset;
    [SerializeField] private Vector3 spineEulerOffset;
    [SerializeField] private Vector3 leftUpperLegEulerOffset;
    [SerializeField] private Vector3 rightUpperLegEulerOffset;

    [Header("Local Position Offsets")]
    [SerializeField] private Vector3 hipsPositionOffset;
    [SerializeField] private Vector3 spinePositionOffset;
    [SerializeField] private Vector3 leftUpperLegPositionOffset;
    [SerializeField] private Vector3 rightUpperLegPositionOffset;
    [SerializeField] private Vector3 leftShoulderPositionOffset;
    [SerializeField] private Vector3 rightShoulderPositionOffset;
    [SerializeField] private Vector3 leftUpperArmPositionOffset;
    [SerializeField] private Vector3 rightUpperArmPositionOffset;

    private Animator animator;
    private BoneOffsetState hips;
    private BoneOffsetState spine;
    private BoneOffsetState leftUpperLeg;
    private BoneOffsetState rightUpperLeg;
    private BoneOffsetState leftShoulder;
    private BoneOffsetState rightShoulder;
    private BoneOffsetState leftUpperArm;
    private BoneOffsetState rightUpperArm;

    private struct BoneOffsetState
    {
        public Transform Bone;
        public Quaternion LastAppliedOffset;
        public Quaternion LastFinalRotation;
        public Vector3 LastAppliedPositionOffset;
        public Vector3 LastFinalPosition;
        public bool HasLastAppliedOffset;
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        CacheBones();
    }

    private void OnEnable()
    {
        CacheBones();
    }

    private void LateUpdate()
    {
        if (!enableTuning || weight <= 0.0f)
        {
            RestoreLastAppliedOffsets();
            return;
        }

        ApplyOffset(ref hips, hipsEulerOffset, hipsPositionOffset);
        ApplyOffset(ref spine, spineEulerOffset, spinePositionOffset);
        ApplyOffset(ref leftUpperLeg, leftUpperLegEulerOffset, leftUpperLegPositionOffset);
        ApplyOffset(ref rightUpperLeg, rightUpperLegEulerOffset, rightUpperLegPositionOffset);
        ApplyOffset(ref leftShoulder, Vector3.zero, leftShoulderPositionOffset);
        ApplyOffset(ref rightShoulder, Vector3.zero, rightShoulderPositionOffset);
        ApplyOffset(ref leftUpperArm, Vector3.zero, leftUpperArmPositionOffset);
        ApplyOffset(ref rightUpperArm, Vector3.zero, rightUpperArmPositionOffset);
    }

    private void OnDisable()
    {
        RestoreLastAppliedOffsets();
    }

    private void CacheBones()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null || !animator.isHuman)
        {
            return;
        }

        hips.Bone = animator.GetBoneTransform(HumanBodyBones.Hips);
        spine.Bone = animator.GetBoneTransform(HumanBodyBones.Spine);
        leftUpperLeg.Bone = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        rightUpperLeg.Bone = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        leftShoulder.Bone = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
        rightShoulder.Bone = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
        leftUpperArm.Bone = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightUpperArm.Bone = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
    }

    private void ApplyOffset(
        ref BoneOffsetState state,
        Vector3 eulerOffset,
        Vector3 positionOffset)
    {
        if (state.Bone == null || (eulerOffset == Vector3.zero && positionOffset == Vector3.zero))
        {
            RestoreLastAppliedOffset(ref state);
            return;
        }

        var baseRotation = GetBaseRotation(ref state);
        var basePosition = GetBasePosition(ref state);
        var rotationOffset = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(eulerOffset), weight);
        var weightedPositionOffset = Vector3.Lerp(Vector3.zero, positionOffset, weight);
        var finalRotation = baseRotation * rotationOffset;
        var finalPosition = basePosition + weightedPositionOffset;

        state.Bone.localRotation = finalRotation;
        state.Bone.localPosition = finalPosition;
        state.LastAppliedOffset = rotationOffset;
        state.LastFinalRotation = finalRotation;
        state.LastAppliedPositionOffset = weightedPositionOffset;
        state.LastFinalPosition = finalPosition;
        state.HasLastAppliedOffset = true;
    }

    private static Quaternion GetBaseRotation(ref BoneOffsetState state)
    {
        var currentRotation = state.Bone.localRotation;
        if (!state.HasLastAppliedOffset)
        {
            return currentRotation;
        }

        var previousOffsetStillPresent =
            Quaternion.Angle(currentRotation, state.LastFinalRotation) < 0.05f;
        return previousOffsetStillPresent
            ? currentRotation * Quaternion.Inverse(state.LastAppliedOffset)
            : currentRotation;
    }

    private static Vector3 GetBasePosition(ref BoneOffsetState state)
    {
        var currentPosition = state.Bone.localPosition;
        if (!state.HasLastAppliedOffset)
        {
            return currentPosition;
        }

        var previousOffsetStillPresent =
            Vector3.Distance(currentPosition, state.LastFinalPosition) < 0.0001f;
        return previousOffsetStillPresent
            ? currentPosition - state.LastAppliedPositionOffset
            : currentPosition;
    }

    private void RestoreLastAppliedOffsets()
    {
        RestoreLastAppliedOffset(ref hips);
        RestoreLastAppliedOffset(ref spine);
        RestoreLastAppliedOffset(ref leftUpperLeg);
        RestoreLastAppliedOffset(ref rightUpperLeg);
        RestoreLastAppliedOffset(ref leftShoulder);
        RestoreLastAppliedOffset(ref rightShoulder);
        RestoreLastAppliedOffset(ref leftUpperArm);
        RestoreLastAppliedOffset(ref rightUpperArm);
    }

    private static void RestoreLastAppliedOffset(ref BoneOffsetState state)
    {
        if (state.Bone == null || !state.HasLastAppliedOffset)
        {
            return;
        }

        if (Quaternion.Angle(state.Bone.localRotation, state.LastFinalRotation) < 0.05f)
        {
            state.Bone.localRotation *= Quaternion.Inverse(state.LastAppliedOffset);
        }

        if (Vector3.Distance(state.Bone.localPosition, state.LastFinalPosition) < 0.0001f)
        {
            state.Bone.localPosition -= state.LastAppliedPositionOffset;
        }

        state.HasLastAppliedOffset = false;
        state.LastAppliedOffset = Quaternion.identity;
        state.LastFinalRotation = Quaternion.identity;
        state.LastAppliedPositionOffset = Vector3.zero;
        state.LastFinalPosition = Vector3.zero;
    }
}
