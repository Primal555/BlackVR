using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class VoiceTemplateRecorderWindow : EditorWindow
{
    private const string SourceAudioFolder = "Assets/Audio/BlackDenoised";
    private const string TemplateFolder = "Assets/Audio/VoiceCommandTemplates";
    private const string ManifestPath = TemplateFolder + "/voice_command_templates.json";
    private static readonly HashSet<string> ExcludedClipNames = new HashSet<string>
    {
        "black动作音效",
        "black握拳"
    };

    [SerializeField] private int selectedDeviceIndex;
    [SerializeField] private int selectedCommandIndex;
    [SerializeField] private int sampleRate = 16000;
    [SerializeField] private int maxRecordSeconds = 3;
    [SerializeField] private float trimSilenceThreshold = 0.015f;
    [SerializeField] private float trimPaddingSeconds = 0.08f;

    private readonly List<VoiceCommandItem> commandItems = new List<VoiceCommandItem>();
    private AudioClip recordingClip;
    private AudioClip lastRecordedClip;
    private double recordStartTime;
    private bool isRecording;
    private Vector2 scrollPosition;

    [MenuItem("Kamen Rider/Voice Template Recorder")]
    public static void ShowWindow()
    {
        var window = GetWindow<VoiceTemplateRecorderWindow>("Voice Templates");
        window.minSize = new Vector2(520, 420);
        window.RefreshCommandItems();
    }

    private void OnEnable()
    {
        RefreshCommandItems();
        EditorApplication.update += RepaintWhileRecording;
    }

    private void OnDisable()
    {
        EditorApplication.update -= RepaintWhileRecording;
        StopRecordingWithoutSaving();
    }

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.Space(8);
        DrawMicrophoneSettings();

        EditorGUILayout.Space(8);
        DrawCommandList();

        EditorGUILayout.Space(8);
        DrawRecordingControls();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                RefreshCommandItems();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{commandItems.Count} commands", EditorStyles.miniLabel, GUILayout.Width(100));
        }
    }

    private void DrawMicrophoneSettings()
    {
        EditorGUILayout.LabelField("Recorder", EditorStyles.boldLabel);

        var devices = Microphone.devices;
        if (devices == null || devices.Length == 0)
        {
            EditorGUILayout.HelpBox("No microphone device was found by Unity.", MessageType.Warning);
            return;
        }

        selectedDeviceIndex = Mathf.Clamp(selectedDeviceIndex, 0, devices.Length - 1);
        selectedDeviceIndex = EditorGUILayout.Popup("Microphone", selectedDeviceIndex, devices);
        sampleRate = EditorGUILayout.IntPopup("Sample Rate", sampleRate, new[] { "16000", "24000", "44100", "48000" }, new[] { 16000, 24000, 44100, 48000 });
        maxRecordSeconds = EditorGUILayout.IntSlider("Max Seconds", maxRecordSeconds, 1, 8);
        trimSilenceThreshold = EditorGUILayout.Slider("Trim Threshold", trimSilenceThreshold, 0.001f, 0.08f);
        trimPaddingSeconds = EditorGUILayout.Slider("Trim Padding", trimPaddingSeconds, 0.0f, 0.3f);
    }

    private void DrawCommandList()
    {
        EditorGUILayout.LabelField("Commands", EditorStyles.boldLabel);

        if (commandItems.Count == 0)
        {
            EditorGUILayout.HelpBox($"No command clips found in {SourceAudioFolder}.", MessageType.Info);
            return;
        }

        selectedCommandIndex = Mathf.Clamp(selectedCommandIndex, 0, commandItems.Count - 1);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(140));
        for (var i = 0; i < commandItems.Count; i++)
        {
            var item = commandItems[i];
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var selected = GUILayout.Toggle(selectedCommandIndex == i, item.commandName, "Button", GUILayout.Width(170));
                if (selected)
                {
                    selectedCommandIndex = i;
                }

                EditorGUILayout.ObjectField(item.effectClip, typeof(AudioClip), false);

                using (new EditorGUI.DisabledScope(item.effectClip == null))
                {
                    if (GUILayout.Button("Play Effect", GUILayout.Width(90)))
                    {
                        PlayPreviewClip(item.effectClip);
                    }
                }

                var template = AssetDatabase.LoadAssetAtPath<AudioClip>(item.templatePath);
                using (new EditorGUI.DisabledScope(template == null))
                {
                    if (GUILayout.Button("Play Template", GUILayout.Width(100)))
                    {
                        PlayPreviewClip(template);
                    }
                }

                GUILayout.Label(template == null ? "Missing" : "Recorded", GUILayout.Width(70));
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawRecordingControls()
    {
        EditorGUILayout.LabelField("Selected Command", EditorStyles.boldLabel);

        if (commandItems.Count == 0)
        {
            return;
        }

        var selectedItem = commandItems[selectedCommandIndex];
        EditorGUILayout.SelectableLabel(selectedItem.commandName, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        EditorGUILayout.LabelField("Template Path", selectedItem.templatePath);

        if (isRecording)
        {
            var elapsed = EditorApplication.timeSinceStartup - recordStartTime;
            EditorGUILayout.HelpBox($"Recording... {elapsed:0.0}s / {maxRecordSeconds}s", MessageType.Info);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(isRecording || Microphone.devices.Length == 0))
            {
                if (GUILayout.Button("Start Recording", GUILayout.Height(32)))
                {
                    StartRecording();
                }
            }

            using (new EditorGUI.DisabledScope(!isRecording))
            {
                if (GUILayout.Button("Stop && Save", GUILayout.Height(32)))
                {
                    StopRecordingAndSave();
                }

                if (GUILayout.Button("Cancel", GUILayout.Height(32), GUILayout.Width(90)))
                {
                    StopRecordingWithoutSaving();
                }
            }
        }

        using (new EditorGUI.DisabledScope(lastRecordedClip == null))
        {
            if (GUILayout.Button("Play Last Recording"))
            {
                PlayPreviewClip(lastRecordedClip);
            }
        }
    }

    private void RefreshCommandItems()
    {
        commandItems.Clear();

        var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { SourceAudioFolder });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var clipName = Path.GetFileNameWithoutExtension(path);
            if (ExcludedClipNames.Contains(clipName))
            {
                continue;
            }

            commandItems.Add(new VoiceCommandItem
            {
                commandName = clipName,
                effectClip = AssetDatabase.LoadAssetAtPath<AudioClip>(path),
                effectPath = path,
                templatePath = $"{TemplateFolder}/{SanitizeFileName(clipName)}_template.wav"
            });
        }

        commandItems.Sort((a, b) => string.CompareOrdinal(a.commandName, b.commandName));
        selectedCommandIndex = Mathf.Clamp(selectedCommandIndex, 0, Mathf.Max(0, commandItems.Count - 1));
        Repaint();
    }

    private void StartRecording()
    {
        if (isRecording || commandItems.Count == 0 || Microphone.devices.Length == 0)
        {
            return;
        }

        var device = Microphone.devices[Mathf.Clamp(selectedDeviceIndex, 0, Microphone.devices.Length - 1)];
        recordingClip = Microphone.Start(device, false, maxRecordSeconds, sampleRate);
        recordStartTime = EditorApplication.timeSinceStartup;
        isRecording = true;
    }

    private void StopRecordingAndSave()
    {
        if (!isRecording || recordingClip == null)
        {
            return;
        }

        var device = Microphone.devices[Mathf.Clamp(selectedDeviceIndex, 0, Microphone.devices.Length - 1)];
        var elapsed = EditorApplication.timeSinceStartup - recordStartTime;
        var samplePosition = Microphone.GetPosition(device);
        Microphone.End(device);
        isRecording = false;

        if (samplePosition <= 0 && elapsed >= maxRecordSeconds - 0.1f)
        {
            samplePosition = recordingClip.samples;
        }

        if (samplePosition <= 0)
        {
            Debug.LogWarning("Voice template recording was empty.");
            recordingClip = null;
            return;
        }

        var channels = recordingClip.channels;
        var frequency = recordingClip.frequency;
        var samples = new float[samplePosition * channels];
        recordingClip.GetData(samples, 0);
        recordingClip = null;

        var trimmed = TrimSilence(samples, channels, frequency, trimSilenceThreshold, trimPaddingSeconds);
        if (trimmed.Length == 0)
        {
            Debug.LogWarning("Voice template recording was trimmed to silence.");
            return;
        }

        var selectedItem = commandItems[selectedCommandIndex];
        EnsureTemplateFolder();
        var absolutePath = Path.GetFullPath(selectedItem.templatePath);
        File.WriteAllBytes(absolutePath, EncodeWav(trimmed, channels, frequency));
        AssetDatabase.ImportAsset(selectedItem.templatePath);
        AssetDatabase.Refresh();
        SaveManifest();

        lastRecordedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(selectedItem.templatePath);
        Debug.Log($"Saved voice template: {selectedItem.templatePath}");
        RefreshCommandItems();
    }

    private void StopRecordingWithoutSaving()
    {
        if (!isRecording)
        {
            return;
        }

        var devices = Microphone.devices;
        if (devices.Length > 0)
        {
            var device = devices[Mathf.Clamp(selectedDeviceIndex, 0, devices.Length - 1)];
            Microphone.End(device);
        }

        isRecording = false;
        recordingClip = null;
    }

    private void SaveManifest()
    {
        var manifest = new VoiceTemplateManifest();
        foreach (var item in commandItems)
        {
            if (!File.Exists(Path.GetFullPath(item.templatePath)))
            {
                continue;
            }

            manifest.entries.Add(new VoiceTemplateEntry
            {
                commandName = item.commandName,
                effectAudioPath = item.effectPath,
                templateAudioPath = item.templatePath
            });
        }

        var json = JsonUtility.ToJson(manifest, true);
        File.WriteAllText(Path.GetFullPath(ManifestPath), json);
        AssetDatabase.ImportAsset(ManifestPath);
    }

    private static float[] TrimSilence(float[] samples, int channels, int frequency, float threshold, float paddingSeconds)
    {
        var frameCount = samples.Length / channels;
        var startFrame = 0;
        var endFrame = frameCount - 1;

        while (startFrame < frameCount && GetFramePeak(samples, startFrame, channels) < threshold)
        {
            startFrame++;
        }

        while (endFrame > startFrame && GetFramePeak(samples, endFrame, channels) < threshold)
        {
            endFrame--;
        }

        if (startFrame >= endFrame)
        {
            return Array.Empty<float>();
        }

        var paddingFrames = Mathf.RoundToInt(paddingSeconds * frequency);
        startFrame = Mathf.Max(0, startFrame - paddingFrames);
        endFrame = Mathf.Min(frameCount - 1, endFrame + paddingFrames);

        var outputFrameCount = endFrame - startFrame + 1;
        var output = new float[outputFrameCount * channels];
        Array.Copy(samples, startFrame * channels, output, 0, output.Length);
        return output;
    }

    private static float GetFramePeak(float[] samples, int frame, int channels)
    {
        var peak = 0.0f;
        var offset = frame * channels;
        for (var channel = 0; channel < channels; channel++)
        {
            peak = Mathf.Max(peak, Mathf.Abs(samples[offset + channel]));
        }

        return peak;
    }

    private static byte[] EncodeWav(float[] samples, int channels, int frequency)
    {
        var sampleCount = samples.Length;
        var dataSize = sampleCount * 2;
        using (var stream = new MemoryStream(44 + dataSize))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(new byte[] { 0x52, 0x49, 0x46, 0x46 });
            writer.Write(36 + dataSize);
            writer.Write(new byte[] { 0x57, 0x41, 0x56, 0x45 });
            writer.Write(new byte[] { 0x66, 0x6d, 0x74, 0x20 });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(frequency);
            writer.Write(frequency * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(new byte[] { 0x64, 0x61, 0x74, 0x61 });
            writer.Write(dataSize);

            for (var i = 0; i < sampleCount; i++)
            {
                var clamped = Mathf.Clamp(samples[i], -1.0f, 1.0f);
                writer.Write((short)(clamped * short.MaxValue));
            }

            return stream.ToArray();
        }
    }

    private static void EnsureTemplateFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Audio"))
        {
            AssetDatabase.CreateFolder("Assets", "Audio");
        }

        if (!AssetDatabase.IsValidFolder(TemplateFolder))
        {
            AssetDatabase.CreateFolder("Assets/Audio", "VoiceCommandTemplates");
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return fileName;
    }

    private static void PlayPreviewClip(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        var audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        var method = audioUtil?.GetMethod("PlayPreviewClip", new[] { typeof(AudioClip), typeof(int), typeof(bool) });
        if (method != null)
        {
            method.Invoke(null, new object[] { clip, 0, false });
        }
    }

    private void RepaintWhileRecording()
    {
        if (!isRecording)
        {
            return;
        }

        if (EditorApplication.timeSinceStartup - recordStartTime >= maxRecordSeconds)
        {
            StopRecordingAndSave();
            return;
        }

        Repaint();
    }

    [Serializable]
    private sealed class VoiceCommandItem
    {
        public string commandName;
        public AudioClip effectClip;
        public string effectPath;
        public string templatePath;
    }

    [Serializable]
    private sealed class VoiceTemplateManifest
    {
        public List<VoiceTemplateEntry> entries = new List<VoiceTemplateEntry>();
    }

    [Serializable]
    private sealed class VoiceTemplateEntry
    {
        public string commandName;
        public string effectAudioPath;
        public string templateAudioPath;
    }
}
