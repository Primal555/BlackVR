using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public sealed class VoiceKeywordSoundTrigger : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip henshinClip;
    [SerializeField, Range(0.0f, 1.0f)] private float volume = 1.0f;

    private AudioSource audioSource;

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

    public void TriggerHenshin()
    {
        if (henshinClip == null || audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(henshinClip, volume);
    }
}
