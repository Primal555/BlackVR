using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public sealed class SwingSoundTrigger : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip swingClip;
    [SerializeField] private AudioClip gripClip;
    [SerializeField, Range(0.0f, 1.0f)] private float volume = 1.0f;

    [Header("Swing")]
    [SerializeField, Min(0.1f)] private float swingSpeedThreshold = 2.6f;
    [SerializeField, Min(0.0f)] private float swingCooldown = 0.35f;

    [Header("Four Trigger Grip")]
    [SerializeField, Range(0.0f, 1.0f)] private float triggerPressedThreshold = 0.85f;
    [SerializeField, Min(0.0f)] private float gripCooldown = 1.0f;

    private AudioSource audioSource;
    private bool wasFourTriggerGripHeld;
    private float nextSwingTime;
    private float nextGripTime;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            enabled = false;
            return;
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.0f;
    }

    private void OnEnable()
    {
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
        if (Time.time < nextSwingTime)
        {
            return;
        }

        var leftSpeed = OVRInput.GetLocalControllerVelocity(OVRInput.Controller.LTouch).magnitude;
        var rightSpeed = OVRInput.GetLocalControllerVelocity(OVRInput.Controller.RTouch).magnitude;
        if (leftSpeed < swingSpeedThreshold && rightSpeed < swingSpeedThreshold)
        {
            return;
        }

        PlayClip(swingClip);
        nextSwingTime = Time.time + swingCooldown;
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
            PlayClip(gripClip);
            nextGripTime = Time.time + gripCooldown;
        }

        wasFourTriggerGripHeld = fourTriggerGripHeld;
    }

    private bool IsPressed(OVRInput.Axis1D axis)
    {
        return OVRInput.Get(axis) >= triggerPressedThreshold;
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip, volume);
    }
}
