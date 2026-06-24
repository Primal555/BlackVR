using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class HenshinModelSwitcher : MonoBehaviour
{
    private enum VisibilityMode
    {
        RenderersOnly,
        GameObjectActive
    }

    [Header("Voice Command")]
    [SerializeField] private VoiceTemplateCommandRecognizer recognizer;
    [SerializeField] private string commandId = "black变身音效";
    [SerializeField] private bool transformOnlyOnce = true;

    [Header("Models")]
    [SerializeField] private GameObject[] beforeModels = Array.Empty<GameObject>();
    [SerializeField] private GameObject transformedModel;
    [SerializeField] private VisibilityMode visibilityMode = VisibilityMode.RenderersOnly;
    [SerializeField] private bool applyInitialVisibilityOnStart;
    [SerializeField] private bool startTransformed;

    [Header("Manual Reset")]
    [SerializeField] private bool enableLeftYReset = true;
    [SerializeField] private AudioClip resetClip;
    [SerializeField, Range(0.0f, 1.0f)] private float resetVolume = 1.0f;

    [Header("Henshin Sequence")]
    [SerializeField] private bool useHenshinSequence = true;
    [SerializeField] private AudioClip henshinClip;
    [SerializeField, Range(0.0f, 1.0f)] private float henshinVolume = 1.0f;
    [SerializeField] private GameObject previewSourceModel;
    [SerializeField, Range(0.1f, 1.0f)] private float previewStartScale = 0.75f;
    [SerializeField, Range(0.5f, 1.5f)] private float previewEndScale = 1.0f;
    [SerializeField, Range(0, 10)] private int previewTrackingWarmupFrames = 3;
    [SerializeField, Range(0.05f, 1.0f)] private float previewFadeDurationRatio = 0.55f;
    [SerializeField] private bool enableWhiteOutlineSweep = true;
    [SerializeField] private Material outlineSweepMaterial;
    [SerializeField, ColorUsage(true, true)] private Color outlineSweepColor = Color.white;
    [SerializeField, Range(0.0f, 8.0f)] private float outlineSweepIntensity = 2.5f;
    [SerializeField, Range(0.0f, 0.08f)] private float outlineSweepThickness = 0.018f;
    [SerializeField, Range(0.05f, 1.0f)] private float outlineSweepWidth = 0.35f;
    [SerializeField, Range(0.1f, 5.0f)] private float outlineSweepSpeed = 1.35f;
    [SerializeField, Min(0.1f)] private float minimumSequenceSeconds = 0.35f;
    [SerializeField] private ParticleSystem henshinParticles;
    [SerializeField] private ParticleSystem steamParticles;

    [Header("Extension Events")]
    [SerializeField] private UnityEvent onTransformed;
    [SerializeField] private UnityEvent onResetToBefore;

    private AudioSource audioSource;
    private bool isTransformed;
    private Coroutine henshinSequenceRoutine;
    private Vector3 sequencePreviewOriginalScale;
    private GameObject activeSequencePreviewModel;
    private readonly Dictionary<Renderer, bool> originalRendererStates = new Dictionary<Renderer, bool>();
    private readonly List<RendererMaterialState> previewRendererMaterialStates = new List<RendererMaterialState>();
    private readonly List<Material> previewMaterials = new List<Material>();
    private readonly List<OutlineMaterialState> outlineMaterials = new List<OutlineMaterialState>();

    private readonly struct RendererMaterialState
    {
        public readonly Renderer Renderer;
        public readonly Material[] SharedMaterials;
        public readonly bool Enabled;

        public RendererMaterialState(Renderer renderer, Material[] sharedMaterials, bool enabled)
        {
            Renderer = renderer;
            SharedMaterials = sharedMaterials;
            Enabled = enabled;
        }
    }

    private readonly struct OutlineMaterialState
    {
        public readonly Renderer SourceRenderer;
        public readonly GameObject OutlineObject;
        public readonly Material Material;

        public OutlineMaterialState(Renderer sourceRenderer, GameObject outlineObject, Material material)
        {
            SourceRenderer = sourceRenderer;
            OutlineObject = outlineObject;
            Material = material;
        }
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (recognizer == null)
        {
            recognizer = GetComponent<VoiceTemplateCommandRecognizer>();
        }

        CacheOriginalRendererStates();
        KeepAnimationSystemsRunningWhileHidden();
    }

    private void OnEnable()
    {
        if (recognizer != null)
        {
            recognizer.CommandRecognized += HandleCommandRecognized;
        }
    }

    private void Start()
    {
        if (applyInitialVisibilityOnStart)
        {
            SetTransformedState(startTransformed, invokeEvents: false);
        }
        else
        {
            isTransformed = transformedModel != null && IsVisible(transformedModel);
        }
    }

    private void Update()
    {
        if (henshinSequenceRoutine != null || !enableLeftYReset)
        {
            return;
        }

        if (IsLeftYPressedThisFrame())
        {
            ToggleFormWithLeftY();
        }
    }

    private void OnDisable()
    {
        if (recognizer != null)
        {
            recognizer.CommandRecognized -= HandleCommandRecognized;
        }

        StopHenshinSequence();
    }

    public void TransformNow()
    {
        if (transformOnlyOnce && isTransformed)
        {
            return;
        }

        StopHenshinSequence();
        SetTransformedState(true, invokeEvents: true);
    }

    public void ResetToBefore()
    {
        StopHenshinSequence();
        SetTransformedState(false, invokeEvents: false);
    }

    public void ResetToBeforeWithEffect()
    {
        if (!isTransformed)
        {
            return;
        }

        StopHenshinSequence();
        PlayResetClip();
        SetTransformedState(false, invokeEvents: true);
    }

    private void HandleCommandRecognized(string recognizedCommandId)
    {
        if (!string.Equals(recognizedCommandId, commandId, StringComparison.Ordinal))
        {
            return;
        }

        if (useHenshinSequence)
        {
            StartHenshinSequence(playHenshinClip: false);
            return;
        }

        TransformNow();
    }

    private void ToggleFormWithLeftY()
    {
        if (isTransformed)
        {
            ResetToBeforeWithEffect();
            return;
        }

        TransformFromManualToggle();
    }

    private void TransformFromManualToggle()
    {
        if (transformOnlyOnce && isTransformed)
        {
            return;
        }

        if (useHenshinSequence)
        {
            StartHenshinSequence(playHenshinClip: true);
            return;
        }

        PlayHenshinClip();
        TransformNow();
    }

    private void StartHenshinSequence(bool playHenshinClip)
    {
        if (transformOnlyOnce && isTransformed)
        {
            return;
        }

        if (henshinSequenceRoutine != null)
        {
            return;
        }

        if (playHenshinClip)
        {
            PlayHenshinClip();
        }

        henshinSequenceRoutine = StartCoroutine(RunHenshinSequence());
    }

    private IEnumerator RunHenshinSequence()
    {
        if (henshinParticles != null)
        {
            henshinParticles.Play(true);
        }

        var sequenceStartTime = Time.time;

        BeginSequencePreview();
        yield return WaitForPreviewTrackingWarmup();
        SetSequencePreviewRenderersVisible(true);

        var remainingDuration = GetRemainingHenshinSequenceDuration(Time.time - sequenceStartTime);
        var fadeDuration = Mathf.Clamp(
            remainingDuration * previewFadeDurationRatio,
            0.01f,
            remainingDuration);
        var outlineDuration = Mathf.Max(0.0f, remainingDuration - fadeDuration);
        var elapsed = 0.0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            var normalizedTime = Mathf.Clamp01(elapsed / fadeDuration);
            var easedTime = Mathf.SmoothStep(0.0f, 1.0f, normalizedTime);

            UpdatePreview(easedTime);
            yield return null;
        }

        UpdatePreview(1.0f);
        yield return RunOutlineSweep(outlineDuration);

        EndSequencePreview(restoreVisibility: false);
        SetTransformedState(true, invokeEvents: true);

        if (steamParticles != null)
        {
            steamParticles.Play(true);
        }

        henshinSequenceRoutine = null;
    }

    private float GetRemainingHenshinSequenceDuration(float elapsedBeforeFade)
    {
        var clipDuration = henshinClip != null ? henshinClip.length : 0.0f;
        var targetDuration = Mathf.Max(minimumSequenceSeconds, clipDuration);
        return Mathf.Max(0.01f, targetDuration - elapsedBeforeFade);
    }

    private void StopHenshinSequence()
    {
        if (henshinSequenceRoutine != null)
        {
            StopCoroutine(henshinSequenceRoutine);
            henshinSequenceRoutine = null;
        }

        EndSequencePreview(restoreVisibility: true);
    }

    private void BeginSequencePreview()
    {
        EndSequencePreview(restoreVisibility: true);

        activeSequencePreviewModel = previewSourceModel != null ? previewSourceModel : transformedModel;
        if (activeSequencePreviewModel == null)
        {
            return;
        }

        sequencePreviewOriginalScale = activeSequencePreviewModel.transform.localScale;
        activeSequencePreviewModel.transform.localScale = sequencePreviewOriginalScale * previewStartScale;
        CachePreviewMaterials(activeSequencePreviewModel);
        SetPreviewAlpha(0.0f);
        SetSequencePreviewRenderersVisible(false);
    }

    private void UpdatePreview(float normalizedTime)
    {
        if (activeSequencePreviewModel == null)
        {
            return;
        }

        activeSequencePreviewModel.transform.localScale =
            sequencePreviewOriginalScale * Mathf.Lerp(previewStartScale, previewEndScale, normalizedTime);
        SetPreviewAlpha(normalizedTime);
    }

    private void EndSequencePreview(bool restoreVisibility)
    {
        if (activeSequencePreviewModel != null)
        {
            activeSequencePreviewModel.transform.localScale = sequencePreviewOriginalScale;
        }

        RestorePreviewMaterials();

        if (restoreVisibility && activeSequencePreviewModel != null)
        {
            SetVisible(activeSequencePreviewModel, isTransformed);
        }

        activeSequencePreviewModel = null;
    }

    private void RestorePreviewMaterials()
    {
        for (var i = 0; i < previewRendererMaterialStates.Count; i++)
        {
            var state = previewRendererMaterialStates[i];
            if (state.Renderer != null)
            {
                state.Renderer.sharedMaterials = state.SharedMaterials;
                state.Renderer.enabled = state.Enabled;
            }
        }

        previewRendererMaterialStates.Clear();

        for (var i = 0; i < previewMaterials.Count; i++)
        {
            if (previewMaterials[i] != null)
            {
                DestroyUnityObject(previewMaterials[i]);
            }
        }

        previewMaterials.Clear();

        for (var i = 0; i < outlineMaterials.Count; i++)
        {
            if (outlineMaterials[i].OutlineObject != null)
            {
                DestroyUnityObject(outlineMaterials[i].OutlineObject);
            }

            if (outlineMaterials[i].Material != null)
            {
                DestroyUnityObject(outlineMaterials[i].Material);
            }
        }

        outlineMaterials.Clear();
    }

    private void CachePreviewMaterials(GameObject target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            var targetRenderer = renderers[rendererIndex];
            var originalMaterials = targetRenderer.sharedMaterials;
            var previewMaterialArray = new Material[originalMaterials.Length];

            previewRendererMaterialStates.Add(new RendererMaterialState(targetRenderer, originalMaterials, targetRenderer.enabled));

            for (var materialIndex = 0; materialIndex < originalMaterials.Length; materialIndex++)
            {
                if (originalMaterials[materialIndex] == null)
                {
                    continue;
                }

                var previewMaterial = new Material(originalMaterials[materialIndex]);
                ConfigurePreviewMaterial(previewMaterial);
                previewMaterialArray[materialIndex] = previewMaterial;
                previewMaterials.Add(previewMaterial);
            }

            targetRenderer.sharedMaterials = previewMaterialArray;
            CreateOutlineRenderer(targetRenderer);
        }
    }

    private void CreateOutlineRenderer(Renderer sourceRenderer)
    {
        if (!enableWhiteOutlineSweep || sourceRenderer == null)
        {
            return;
        }

        var outlineMaterial = CreateOutlineMaterial(sourceRenderer);
        if (outlineMaterial == null)
        {
            return;
        }

        if (sourceRenderer is SkinnedMeshRenderer sourceSkinnedRenderer)
        {
            var outlineObject = CreateOutlineObject(sourceRenderer);
            var outlineRenderer = outlineObject.AddComponent<SkinnedMeshRenderer>();
            outlineRenderer.sharedMesh = sourceSkinnedRenderer.sharedMesh;
            outlineRenderer.rootBone = sourceSkinnedRenderer.rootBone;
            outlineRenderer.bones = sourceSkinnedRenderer.bones;
            outlineRenderer.localBounds = sourceSkinnedRenderer.localBounds;
            outlineRenderer.quality = sourceSkinnedRenderer.quality;
            outlineRenderer.updateWhenOffscreen = true;
            outlineRenderer.skinnedMotionVectors = sourceSkinnedRenderer.skinnedMotionVectors;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.sharedMaterials = CreateRepeatedMaterialArray(outlineMaterial, sourceSkinnedRenderer.sharedMesh);

            outlineMaterials.Add(new OutlineMaterialState(sourceRenderer, outlineObject, outlineMaterial));
            return;
        }

        var sourceMeshFilter = sourceRenderer.GetComponent<MeshFilter>();
        if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null)
        {
            DestroyUnityObject(outlineMaterial);
            return;
        }

        var meshOutlineObject = CreateOutlineObject(sourceRenderer);
        var outlineMeshFilter = meshOutlineObject.AddComponent<MeshFilter>();
        outlineMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

        var meshRenderer = meshOutlineObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.sharedMaterials = CreateRepeatedMaterialArray(outlineMaterial, sourceMeshFilter.sharedMesh);

        outlineMaterials.Add(new OutlineMaterialState(sourceRenderer, meshOutlineObject, outlineMaterial));
    }

    private static GameObject CreateOutlineObject(Renderer sourceRenderer)
    {
        var outlineObject = new GameObject($"{sourceRenderer.name}_HenshinOutline");
        outlineObject.layer = sourceRenderer.gameObject.layer;
        outlineObject.transform.SetParent(sourceRenderer.transform, false);
        outlineObject.transform.localPosition = Vector3.zero;
        outlineObject.transform.localRotation = Quaternion.identity;
        outlineObject.transform.localScale = Vector3.one;
        return outlineObject;
    }

    private static Material[] CreateRepeatedMaterialArray(Material material, Mesh mesh)
    {
        var subMeshCount = Mathf.Max(1, mesh != null ? mesh.subMeshCount : 1);
        var materials = new Material[subMeshCount];
        for (var i = 0; i < materials.Length; i++)
        {
            materials[i] = material;
        }

        return materials;
    }

    private Material CreateOutlineMaterial(Renderer targetRenderer)
    {
        if (!enableWhiteOutlineSweep || targetRenderer == null)
        {
            return null;
        }

        Material material;
        if (outlineSweepMaterial != null)
        {
            material = new Material(outlineSweepMaterial);
        }
        else
        {
            var outlineShader = Shader.Find("KamenRider/HenshinOutline");
            if (outlineShader == null)
            {
                return null;
            }

            material = new Material(outlineShader);
        }

        ConfigureOutlineMaterial(material, targetRenderer, 0.0f, 0.0f);
        return material;
    }

    private IEnumerator WaitForPreviewTrackingWarmup()
    {
        var warmupFrames = Mathf.Max(0, previewTrackingWarmupFrames);
        for (var i = 0; i < warmupFrames; i++)
        {
            UpdatePreview(0.0f);
            yield return null;
        }
    }

    private IEnumerator RunOutlineSweep(float duration)
    {
        if (!enableWhiteOutlineSweep || duration <= 0.0f)
        {
            yield break;
        }

        var elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            UpdatePreview(1.0f);
            UpdatePreviewOutlineSweep(elapsed);
            yield return null;
        }

        ClearPreviewOutlineSweep();
    }

    private void UpdatePreviewOutlineSweep(float elapsed)
    {
        var materialCount = outlineMaterials.Count;
        if (materialCount == 0)
        {
            return;
        }

        for (var i = 0; i < materialCount; i++)
        {
            var materialOffset = materialCount <= 1 ? 0.0f : (float)i / materialCount;
            var sweepProgress = Mathf.Repeat(elapsed * outlineSweepSpeed - materialOffset, 1.0f);
            ConfigureOutlineMaterial(outlineMaterials[i].Material, outlineMaterials[i].SourceRenderer, 1.0f, sweepProgress);
        }
    }

    private void ClearPreviewOutlineSweep()
    {
        for (var i = 0; i < outlineMaterials.Count; i++)
        {
            ConfigureOutlineMaterial(outlineMaterials[i].Material, outlineMaterials[i].SourceRenderer, 0.0f, 0.0f);
        }
    }

    private void SetSequencePreviewRenderersVisible(bool visible)
    {
        for (var i = 0; i < previewRendererMaterialStates.Count; i++)
        {
            var targetRenderer = previewRendererMaterialStates[i].Renderer;
            if (targetRenderer != null)
            {
                targetRenderer.enabled = visible && GetOriginalRendererState(targetRenderer);
            }
        }
    }

    private static void DestroyUnityObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
            return;
        }

        UnityEngine.Object.DestroyImmediate(target);
    }

    private static void ConfigurePreviewMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1.0f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0.0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0.0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private void ConfigureOutlineMaterial(Material material, Renderer targetRenderer, float alpha, float sweepProgress)
    {
        if (material == null || targetRenderer == null)
        {
            return;
        }

        var bounds = targetRenderer.bounds;
        if (material.HasProperty("_OutlineColor"))
        {
            material.SetColor("_OutlineColor", outlineSweepColor);
        }

        if (material.HasProperty("_OutlineWidth"))
        {
            material.SetFloat("_OutlineWidth", outlineSweepThickness);
        }

        if (material.HasProperty("_OutlineAlpha"))
        {
            material.SetFloat("_OutlineAlpha", alpha);
        }

        if (material.HasProperty("_OutlineIntensity"))
        {
            material.SetFloat("_OutlineIntensity", outlineSweepIntensity);
        }

        if (material.HasProperty("_SweepMinY"))
        {
            material.SetFloat("_SweepMinY", bounds.min.y);
        }

        if (material.HasProperty("_SweepMaxY"))
        {
            material.SetFloat("_SweepMaxY", bounds.max.y);
        }

        if (material.HasProperty("_SweepProgress"))
        {
            material.SetFloat("_SweepProgress", sweepProgress);
        }

        if (material.HasProperty("_SweepWidth"))
        {
            material.SetFloat("_SweepWidth", outlineSweepWidth);
        }
    }

    private void SetPreviewAlpha(float alpha)
    {
        for (var i = 0; i < previewMaterials.Count; i++)
        {
            SetMaterialAlpha(previewMaterials[i], alpha);
        }
    }

    private static void SetMaterialAlpha(Material material, float alpha)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            var color = material.GetColor("_BaseColor");
            color.a = alpha;
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            var color = material.GetColor("_Color");
            color.a = alpha;
            material.SetColor("_Color", color);
        }
    }

    private void SetTransformedState(bool transformed, bool invokeEvents)
    {
        isTransformed = transformed;

        for (var i = 0; i < beforeModels.Length; i++)
        {
            SetVisible(beforeModels[i], !transformed);
        }

        SetVisible(transformedModel, transformed);

        if (!invokeEvents)
        {
            return;
        }

        if (transformed)
        {
            onTransformed?.Invoke();
            return;
        }

        onResetToBefore?.Invoke();
    }

    private void PlayResetClip()
    {
        if (resetClip == null || audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(resetClip, resetVolume);
    }

    private void PlayHenshinClip()
    {
        if (henshinClip == null || audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(henshinClip, henshinVolume);
    }

    private static bool IsLeftYPressedThisFrame()
    {
        return OVRInput.GetDown(OVRInput.RawButton.Y, OVRInput.Controller.LTouch) ||
               OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch);
    }

    private void SetVisible(GameObject target, bool visible)
    {
        if (target == null)
        {
            return;
        }

        if (visibilityMode == VisibilityMode.GameObjectActive)
        {
            target.SetActive(visible);
            return;
        }

        var renderers = target.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = visible && GetOriginalRendererState(renderers[i]);
        }
    }

    private bool IsVisible(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        if (visibilityMode == VisibilityMode.GameObjectActive)
        {
            return target.activeSelf;
        }

        var renderers = target.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].enabled)
            {
                return true;
            }
        }

        return false;
    }

    private void CacheOriginalRendererStates()
    {
        CacheOriginalRendererStates(beforeModels);
        CacheOriginalRendererStates(transformedModel);
    }

    private void CacheOriginalRendererStates(GameObject[] targets)
    {
        if (targets == null)
        {
            return;
        }

        for (var i = 0; i < targets.Length; i++)
        {
            CacheOriginalRendererStates(targets[i]);
        }
    }

    private void CacheOriginalRendererStates(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        var renderers = target.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            if (!originalRendererStates.ContainsKey(renderers[i]))
            {
                originalRendererStates.Add(renderers[i], renderers[i].enabled);
            }
        }
    }

    private bool GetOriginalRendererState(Renderer target)
    {
        return !originalRendererStates.TryGetValue(target, out var enabled) || enabled;
    }

    private void KeepAnimationSystemsRunningWhileHidden()
    {
        KeepAnimationSystemsRunningWhileHidden(beforeModels);
        KeepAnimationSystemsRunningWhileHidden(transformedModel);
    }

    private void KeepAnimationSystemsRunningWhileHidden(GameObject[] targets)
    {
        if (targets == null)
        {
            return;
        }

        for (var i = 0; i < targets.Length; i++)
        {
            KeepAnimationSystemsRunningWhileHidden(targets[i]);
        }
    }

    private void KeepAnimationSystemsRunningWhileHidden(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        var animators = target.GetComponentsInChildren<Animator>(true);
        for (var i = 0; i < animators.Length; i++)
        {
            animators[i].cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        var skinnedMeshRenderers = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (var i = 0; i < skinnedMeshRenderers.Length; i++)
        {
            skinnedMeshRenderers[i].updateWhenOffscreen = true;
        }
    }
}
