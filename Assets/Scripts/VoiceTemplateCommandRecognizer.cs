using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public sealed class VoiceTemplateCommandRecognizer : MonoBehaviour
{
    [Serializable]
    public sealed class VoiceCommand
    {
        public string commandId;
        public AudioClip templateClip;
        public AudioClip defaultEffectClip;
        [Range(0.0f, 1.0f)] public float minSimilarityOverride;
        public UnityEvent onRecognized;

        [NonSerialized] public VoiceTemplateSignature templateSignature;
    }

    public event Action<string> CommandRecognized;

    [Header("Microphone")]
    [SerializeField] private string microphoneDevice;
    [SerializeField] private bool startListeningOnEnable = true;
    [SerializeField, Min(0.0f)] private float startDelaySeconds = 2.0f;
    [SerializeField] private int sampleRate = 16000;
    [SerializeField, Range(2, 30)] private int microphoneBufferSeconds = 10;

    [Header("Voice Activity")]
    [SerializeField, Range(0.001f, 0.2f)] private float voiceThreshold = 0.015f;
    [SerializeField, Range(0.05f, 1.0f)] private float silenceToEndSeconds = 0.35f;
    [SerializeField, Range(0.1f, 3.0f)] private float minUtteranceSeconds = 0.25f;
    [SerializeField, Range(0.5f, 5.0f)] private float maxUtteranceSeconds = 2.5f;

    [Header("Recognition")]
    [SerializeField, Range(0.1f, 5.0f)] private float recognitionCooldownSeconds = 1.0f;

    [Header("Matching")]
    [SerializeField, Range(0.0f, 1.0f)] private float defaultMinSimilarity = 0.5f;
    [SerializeField] private bool logRecognition = true;
    [SerializeField] private VoiceCommand[] commands = Array.Empty<VoiceCommand>();

    private const int FeatureFrames = 32;
    private const int FeatureBands = 8;
    private const int VadFrameSize = 320;
    private static readonly float[] BandCenters = { 180f, 320f, 520f, 820f, 1250f, 1850f, 2800f, 4200f };

    private AudioSource audioSource;
    private AudioClip microphoneClip;
    private readonly List<float> pendingFrameSamples = new List<float>(VadFrameSize * 2);
    private readonly List<float> currentUtteranceSamples = new List<float>(16000 * 3);
    private string activeMicrophoneDevice;
    private int lastMicrophonePosition;
    private float currentSilenceSeconds;
    private float nextRecognitionAllowedTime;
    private bool isListening;
    private bool isCapturingUtterance;
    private Coroutine startRoutine;

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
        BuildTemplateSignatures();
    }

    private void OnEnable()
    {
        if (startListeningOnEnable)
        {
            startRoutine = StartCoroutine(StartListeningAfterDelay());
        }
    }

    private void OnDisable()
    {
        if (startRoutine != null)
        {
            StopCoroutine(startRoutine);
            startRoutine = null;
        }

        StopListening();
    }

    private void Update()
    {
        if (!isListening || microphoneClip == null)
        {
            return;
        }

        ReadMicrophoneSamples();
    }

    public void StartListening()
    {
        if (isListening)
        {
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            return;
        }
#endif

        var device = GetMicrophoneDevice();
        if (!string.IsNullOrEmpty(device) && Array.IndexOf(Microphone.devices, device) < 0)
        {
            Debug.LogWarning($"Microphone device not found: {device}. Falling back to default device.", this);
            device = null;
        }

        activeMicrophoneDevice = device;
        microphoneClip = Microphone.Start(activeMicrophoneDevice, true, microphoneBufferSeconds, sampleRate);
        if (microphoneClip == null)
        {
            activeMicrophoneDevice = null;
            Debug.LogWarning("Failed to start microphone for voice template recognition.", this);
            return;
        }

        lastMicrophonePosition = 0;
        currentSilenceSeconds = 0.0f;
        pendingFrameSamples.Clear();
        currentUtteranceSamples.Clear();
        isCapturingUtterance = false;
        isListening = true;
    }

    public void StopListening()
    {
        if (isListening || Microphone.IsRecording(activeMicrophoneDevice))
        {
            Microphone.End(activeMicrophoneDevice);
        }

        isListening = false;
        isCapturingUtterance = false;
        activeMicrophoneDevice = null;
        microphoneClip = null;
        pendingFrameSamples.Clear();
        currentUtteranceSamples.Clear();
    }

    private IEnumerator StartListeningAfterDelay()
    {
        if (startDelaySeconds > 0.0f)
        {
            yield return new WaitForSeconds(startDelaySeconds);
        }

        startRoutine = null;
        StartListening();
    }

    private void BuildTemplateSignatures()
    {
        if (commands == null)
        {
            return;
        }

        foreach (var command in commands)
        {
            if (command == null || command.templateClip == null)
            {
                continue;
            }

            command.templateSignature = VoiceTemplateSignature.FromClip(command.templateClip, FeatureFrames, FeatureBands, BandCenters);
        }
    }

    private void ReadMicrophoneSamples()
    {
        var position = Microphone.GetPosition(activeMicrophoneDevice);
        if (position < 0 || position == lastMicrophonePosition)
        {
            return;
        }

        if (position > lastMicrophonePosition)
        {
            ReadMicrophoneRange(lastMicrophonePosition, position - lastMicrophonePosition);
        }
        else
        {
            ReadMicrophoneRange(lastMicrophonePosition, microphoneClip.samples - lastMicrophonePosition);
            if (position > 0)
            {
                ReadMicrophoneRange(0, position);
            }
        }

        lastMicrophonePosition = position;
    }

    private void ReadMicrophoneRange(int offsetSamples, int sampleCount)
    {
        if (sampleCount <= 0)
        {
            return;
        }

        var channels = microphoneClip.channels;
        var interleaved = new float[sampleCount * channels];
        microphoneClip.GetData(interleaved, offsetSamples);

        for (var sample = 0; sample < sampleCount; sample++)
        {
            var mono = 0.0f;
            var baseIndex = sample * channels;
            for (var channel = 0; channel < channels; channel++)
            {
                mono += interleaved[baseIndex + channel];
            }

            pendingFrameSamples.Add(mono / channels);
        }

        ProcessPendingVadFrames();
    }

    private void ProcessPendingVadFrames()
    {
        while (pendingFrameSamples.Count >= VadFrameSize)
        {
            var frame = new float[VadFrameSize];
            pendingFrameSamples.CopyTo(0, frame, 0, VadFrameSize);
            pendingFrameSamples.RemoveRange(0, VadFrameSize);

            var rms = CalculateRms(frame);
            var voiced = rms >= voiceThreshold;
            var frameDuration = (float)VadFrameSize / sampleRate;

            if (voiced)
            {
                if (!isCapturingUtterance)
                {
                    isCapturingUtterance = true;
                    currentUtteranceSamples.Clear();
                }

                currentSilenceSeconds = 0.0f;
                currentUtteranceSamples.AddRange(frame);
            }
            else if (isCapturingUtterance)
            {
                currentSilenceSeconds += frameDuration;
                currentUtteranceSamples.AddRange(frame);
            }

            if (isCapturingUtterance && ShouldFinishUtterance())
            {
                FinishUtterance();
            }
        }
    }

    private bool ShouldFinishUtterance()
    {
        var duration = (float)currentUtteranceSamples.Count / sampleRate;
        return currentSilenceSeconds >= silenceToEndSeconds || duration >= maxUtteranceSeconds;
    }

    private void FinishUtterance()
    {
        var samples = currentUtteranceSamples.ToArray();
        currentUtteranceSamples.Clear();
        currentSilenceSeconds = 0.0f;
        isCapturingUtterance = false;

        var trimmed = VoiceTemplateSignature.TrimSilence(samples, voiceThreshold * 0.5f);
        var duration = (float)trimmed.Length / sampleRate;
        if (duration < minUtteranceSeconds || duration > maxUtteranceSeconds)
        {
            return;
        }

        Recognize(trimmed);
    }

    private void Recognize(float[] samples)
    {
        if (commands == null || commands.Length == 0)
        {
            return;
        }

        if (Time.unscaledTime < nextRecognitionAllowedTime)
        {
            return;
        }

        var inputSignature = VoiceTemplateSignature.FromSamples(samples, sampleRate, FeatureFrames, FeatureBands, BandCenters);
        VoiceCommand bestCommand = null;
        var bestScore = 0.0f;

        foreach (var command in commands)
        {
            if (command == null || command.templateSignature == null)
            {
                continue;
            }

            var score = inputSignature.CompareTo(command.templateSignature);
            if (score > bestScore)
            {
                bestScore = score;
                bestCommand = command;
            }
        }

        if (bestCommand == null)
        {
            return;
        }

        var minSimilarity = bestCommand.minSimilarityOverride > 0.0f ? bestCommand.minSimilarityOverride : defaultMinSimilarity;
        if (bestScore < minSimilarity)
        {
            if (logRecognition)
            {
                Debug.Log($"Voice command rejected. Best={bestCommand.commandId}, score={bestScore:0.000}, required={minSimilarity:0.000}", this);
            }

            return;
        }

        DispatchCommand(bestCommand, bestScore);
    }

    private void DispatchCommand(VoiceCommand command, float score)
    {
        nextRecognitionAllowedTime = Time.unscaledTime + recognitionCooldownSeconds;

        if (logRecognition)
        {
            Debug.Log($"Voice command recognized: {command.commandId}, score={score:0.000}", this);
        }

        if (command.defaultEffectClip != null)
        {
            audioSource.PlayOneShot(command.defaultEffectClip);
        }

        command.onRecognized?.Invoke();
        CommandRecognized?.Invoke(command.commandId);
    }

    private string GetMicrophoneDevice()
    {
        return string.IsNullOrWhiteSpace(microphoneDevice) ? null : microphoneDevice;
    }

    private static float CalculateRms(float[] samples)
    {
        var sum = 0.0f;
        for (var i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }

        return Mathf.Sqrt(sum / Mathf.Max(1, samples.Length));
    }

    public sealed class VoiceTemplateSignature
    {
        private readonly float[] features;

        private VoiceTemplateSignature(float[] features)
        {
            this.features = features;
        }

        public static VoiceTemplateSignature FromClip(AudioClip clip, int frameCount, int bandCount, float[] bandCenters)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            var mono = ToMono(samples, clip.channels);
            return FromSamples(mono, clip.frequency, frameCount, bandCount, bandCenters);
        }

        public static VoiceTemplateSignature FromSamples(float[] samples, int frequency, int frameCount, int bandCount, float[] bandCenters)
        {
            var trimmed = TrimSilence(samples, 0.01f);
            if (trimmed.Length == 0)
            {
                trimmed = samples;
            }

            var features = new float[frameCount * (bandCount + 2)];
            var frameLength = Mathf.Max(64, trimmed.Length / frameCount);

            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                var start = Mathf.RoundToInt((float)frameIndex / frameCount * trimmed.Length);
                var end = frameIndex == frameCount - 1
                    ? trimmed.Length
                    : Mathf.RoundToInt((float)(frameIndex + 1) / frameCount * trimmed.Length);
                end = Mathf.Max(end, start + 1);

                var featureOffset = frameIndex * (bandCount + 2);
                features[featureOffset] = CalculateRangeRms(trimmed, start, end);
                features[featureOffset + 1] = CalculateZeroCrossingRate(trimmed, start, end);

                for (var band = 0; band < bandCount; band++)
                {
                    var center = Mathf.Min(bandCenters[band], frequency * 0.45f);
                    features[featureOffset + 2 + band] = CalculateGoertzelMagnitude(trimmed, start, end, frequency, center, frameLength);
                }
            }

            Normalize(features);
            return new VoiceTemplateSignature(features);
        }

        public float CompareTo(VoiceTemplateSignature other)
        {
            if (other == null || other.features == null || features == null || other.features.Length != features.Length)
            {
                return 0.0f;
            }

            var dot = 0.0f;
            var a = 0.0f;
            var b = 0.0f;
            for (var i = 0; i < features.Length; i++)
            {
                dot += features[i] * other.features[i];
                a += features[i] * features[i];
                b += other.features[i] * other.features[i];
            }

            if (a <= Mathf.Epsilon || b <= Mathf.Epsilon)
            {
                return 0.0f;
            }

            return Mathf.Clamp01((dot / Mathf.Sqrt(a * b) + 1.0f) * 0.5f);
        }

        public static float[] TrimSilence(float[] samples, float threshold)
        {
            var start = 0;
            var end = samples.Length - 1;

            while (start < samples.Length && Mathf.Abs(samples[start]) < threshold)
            {
                start++;
            }

            while (end > start && Mathf.Abs(samples[end]) < threshold)
            {
                end--;
            }

            if (start >= end)
            {
                return Array.Empty<float>();
            }

            var length = end - start + 1;
            var output = new float[length];
            Array.Copy(samples, start, output, 0, length);
            return output;
        }

        private static float[] ToMono(float[] samples, int channels)
        {
            if (channels <= 1)
            {
                return samples;
            }

            var mono = new float[samples.Length / channels];
            for (var sample = 0; sample < mono.Length; sample++)
            {
                var value = 0.0f;
                var offset = sample * channels;
                for (var channel = 0; channel < channels; channel++)
                {
                    value += samples[offset + channel];
                }

                mono[sample] = value / channels;
            }

            return mono;
        }

        private static float CalculateRangeRms(float[] samples, int start, int end)
        {
            var sum = 0.0f;
            for (var i = start; i < end && i < samples.Length; i++)
            {
                sum += samples[i] * samples[i];
            }

            return Mathf.Sqrt(sum / Mathf.Max(1, end - start));
        }

        private static float CalculateZeroCrossingRate(float[] samples, int start, int end)
        {
            var crossings = 0;
            var previous = samples[Mathf.Clamp(start, 0, samples.Length - 1)];
            for (var i = start + 1; i < end && i < samples.Length; i++)
            {
                var current = samples[i];
                if ((previous < 0.0f && current >= 0.0f) || (previous >= 0.0f && current < 0.0f))
                {
                    crossings++;
                }

                previous = current;
            }

            return crossings / (float)Mathf.Max(1, end - start);
        }

        private static float CalculateGoertzelMagnitude(float[] samples, int start, int end, int frequency, float targetFrequency, int frameLength)
        {
            var normalizedFrequency = targetFrequency / frequency;
            var coefficient = 2.0f * Mathf.Cos(2.0f * Mathf.PI * normalizedFrequency);
            var q0 = 0.0f;
            var q1 = 0.0f;
            var q2 = 0.0f;
            var length = Mathf.Min(frameLength, end - start);

            for (var i = 0; i < length; i++)
            {
                var index = Mathf.Clamp(start + i, 0, samples.Length - 1);
                q0 = coefficient * q1 - q2 + samples[index];
                q2 = q1;
                q1 = q0;
            }

            var magnitudeSquared = q1 * q1 + q2 * q2 - coefficient * q1 * q2;
            return Mathf.Log10(1.0f + Mathf.Sqrt(Mathf.Max(0.0f, magnitudeSquared)));
        }

        private static void Normalize(float[] values)
        {
            var mean = 0.0f;
            for (var i = 0; i < values.Length; i++)
            {
                mean += values[i];
            }

            mean /= Mathf.Max(1, values.Length);

            var variance = 0.0f;
            for (var i = 0; i < values.Length; i++)
            {
                var delta = values[i] - mean;
                variance += delta * delta;
            }

            var standardDeviation = Mathf.Sqrt(variance / Mathf.Max(1, values.Length));
            if (standardDeviation <= Mathf.Epsilon)
            {
                return;
            }

            for (var i = 0; i < values.Length; i++)
            {
                values[i] = (values[i] - mean) / standardDeviation;
            }
        }
    }
}
