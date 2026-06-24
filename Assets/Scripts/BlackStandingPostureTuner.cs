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

    private Animator animator;
    private BoneOffsetState hips;
    private BoneOffsetState spine;
    private BoneOffsetState leftUpperLeg;
    private BoneOffsetState rightUpperLeg;

    private struct BoneOffsetState
    {
        public Transform Bone;
        public Quaternion LastAppliedOffset;
        public Quaternion LastFinalRotation;
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

        ApplyOffset(ref hips, hipsEulerOffset);
        ApplyOffset(ref spine, spineEulerOffset);
        ApplyOffset(ref leftUpperLeg, leftUpperLegEulerOffset);
        ApplyOffset(ref rightUpperLeg, rightUpperLegEulerOffset);
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
    }

    private void ApplyOffset(ref BoneOffsetState state, Vector3 eulerOffset)
    {
        if (state.Bone == null || eulerOffset == Vector3.zero)
        {
            RestoreLastAppliedOffset(ref state);
            return;
        }

        var baseRotation = GetBaseRotation(ref state);
        var offset = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(eulerOffset), weight);
        var finalRotation = baseRotation * offset;

        state.Bone.localRotation = finalRotation;
        state.LastAppliedOffset = offset;
        state.LastFinalRotation = finalRotation;
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

    private void RestoreLastAppliedOffsets()
    {
        RestoreLastAppliedOffset(ref hips);
        RestoreLastAppliedOffset(ref spine);
        RestoreLastAppliedOffset(ref leftUpperLeg);
        RestoreLastAppliedOffset(ref rightUpperLeg);
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

        state.HasLastAppliedOffset = false;
        state.LastAppliedOffset = Quaternion.identity;
        state.LastFinalRotation = Quaternion.identity;
    }
}
