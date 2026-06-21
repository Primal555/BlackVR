using UnityEngine;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public sealed class VoiceKeywordSoundTrigger : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip henshinClip;
    [SerializeField, Range(0.0f, 1.0f)] private float volume = 1.0f;

    [Header("Keywords")]
    [SerializeField] private string[] keywords = { "henshin", "hen shin" };
    [SerializeField, Min(0.0f)] private float cooldown = 2.0f;

    private AudioSource audioSource;
    private float nextTriggerTime;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private KeywordRecognizer keywordRecognizer;
#endif

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
        nextTriggerTime = 0.0f;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        StartRecognizer();
#endif
    }

    private void OnDisable()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        StopRecognizer();
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private void StartRecognizer()
    {
        if (keywordRecognizer != null)
        {
            return;
        }

        var validKeywords = GetValidKeywords();
        if (validKeywords.Length == 0)
        {
            enabled = false;
            return;
        }

        try
        {
            keywordRecognizer = new KeywordRecognizer(validKeywords);
            keywordRecognizer.OnPhraseRecognized += OnPhraseRecognized;
            keywordRecognizer.Start();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Voice keyword recognition failed to start: {exception.Message}", this);
            StopRecognizer();
            enabled = false;
        }
    }

    private void StopRecognizer()
    {
        if (keywordRecognizer == null)
        {
            return;
        }

        keywordRecognizer.OnPhraseRecognized -= OnPhraseRecognized;
        if (keywordRecognizer.IsRunning)
        {
            keywordRecognizer.Stop();
        }

        keywordRecognizer.Dispose();
        keywordRecognizer = null;
    }

    private string[] GetValidKeywords()
    {
        if (keywords == null || keywords.Length == 0)
        {
            return new[] { "henshin" };
        }

        var validKeywords = new System.Collections.Generic.List<string>(keywords.Length);
        for (var i = 0; i < keywords.Length; i++)
        {
            var keyword = keywords[i];
            if (!string.IsNullOrWhiteSpace(keyword) && !validKeywords.Contains(keyword))
            {
                validKeywords.Add(keyword);
            }
        }

        return validKeywords.ToArray();
    }

    private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        if (Time.time < nextTriggerTime)
        {
            return;
        }

        PlayHenshinClip();
        nextTriggerTime = Time.time + cooldown;
    }
#endif

    private void PlayHenshinClip()
    {
        if (henshinClip == null || audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(henshinClip, volume);
    }
}
