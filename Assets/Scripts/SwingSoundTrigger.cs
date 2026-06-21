using UnityEngine;

[DisallowMultipleComponent]
public sealed class SwingSoundTrigger : MonoBehaviour
{
    [Header("Tracked Hands")]
    [SerializeField] private Transform leftTrackedTransform;
    [SerializeField] private Transform rightTrackedTransform;

    [Header("Audio")]
    [SerializeField] private AudioClip swingClip;
    [SerializeField] private AudioClip gripClip;
    [SerializeField, Range(0.0f, 1.0f)] private float volume = 1.0f;
    [SerializeField, Range(0.0f, 1.0f)] private float spatialBlend = 0.0f;

    [Header("Swing")]
    [SerializeField, Min(0.1f)] private float swingSpeedThreshold = 2.6f;
    [SerializeField, Min(0.0f)] private float swingCooldown = 0.35f;

    [Header("Four Trigger Grip")]
    [SerializeField, Range(0.0f, 1.0f)] private float triggerPressedThreshold = 0.85f;
    [SerializeField, Min(0.0f)] private float gripCooldown = 1.0f;

    private AudioSource audioSource;
    private Vector3 lastLeftPosition;
    private Vector3 lastRightPosition;
    private bool hasLastPositions;
    private bool wasFourTriggerGripHeld;
    private float nextSwingTime;
    private float nextGripTime;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = spatialBlend;
    }

    private void OnEnable()
    {
        hasLastPositions = false;
        wasFourTriggerGripHeld = false;
        nextSwingTime = 0.0f;
        nextGripTime = 0.0f;
    }

    private void Update()
    {
        CheckSwing();
        CheckFourTriggerGrip();
    }

    private void CheckSwing()
    {
        if (leftTrackedTransform == null || rightTrackedTransform == null)
        {
            return;
        }

        var leftPosition = leftTrackedTransform.position;
        var rightPosition = rightTrackedTransform.position;
        var deltaTime = Time.deltaTime;

        if (hasLastPositions && deltaTime > Mathf.Epsilon && Time.time >= nextSwingTime)
        {
            var leftSpeed = Vector3.Distance(leftPosition, lastLeftPosition) / deltaTime;
            var rightSpeed = Vector3.Distance(rightPosition, lastRightPosition) / deltaTime;
            if (leftSpeed >= swingSpeedThreshold || rightSpeed >= swingSpeedThreshold)
            {
                var soundPosition = leftSpeed >= rightSpeed ? leftPosition : rightPosition;
                PlayClip(swingClip, soundPosition);
                nextSwingTime = Time.time + swingCooldown;
            }
        }

        lastLeftPosition = leftPosition;
        lastRightPosition = rightPosition;
        hasLastPositions = true;
    }

    private void CheckFourTriggerGrip()
    {
        var fourTriggerGripHeld =
            IsPressed(OVRInput.Axis1D.PrimaryIndexTrigger) &&
            IsPressed(OVRInput.Axis1D.PrimaryHandTrigger) &&
            IsPressed(OVRInput.Axis1D.SecondaryIndexTrigger) &&
            IsPressed(OVRInput.Axis1D.SecondaryHandTrigger);

        if (fourTriggerGripHeld && !wasFourTriggerGripHeld && Time.time >= nextGripTime)
        {
            PlayClip(gripClip, GetCenterPosition());
            nextGripTime = Time.time + gripCooldown;
        }

        wasFourTriggerGripHeld = fourTriggerGripHeld;
    }

    private bool IsPressed(OVRInput.Axis1D axis)
    {
        return OVRInput.Get(axis) >= triggerPressedThreshold;
    }

    private Vector3 GetCenterPosition()
    {
        if (leftTrackedTransform != null && rightTrackedTransform != null)
        {
            return (leftTrackedTransform.position + rightTrackedTransform.position) * 0.5f;
        }

        return transform.position;
    }

    private void PlayClip(AudioClip clip, Vector3 position)
    {
        if (clip == null || audioSource == null)
        {
            return;
        }

        audioSource.transform.position = position;
        audioSource.spatialBlend = spatialBlend;
        audioSource.PlayOneShot(clip, volume);
    }
}
