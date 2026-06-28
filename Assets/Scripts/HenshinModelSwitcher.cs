using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class HenshinModelSwitcher : MonoBehaviour
{
    private const string BlackBeltMaterialPrefix = "Belt";
    private const string BlackBeltCenterMaterialName = "KingStone";

    private enum VisibilityMode
    {
        RenderersOnly,
        GameObjectActive
    }

    [Header("Voice Command")]
    [SerializeField] private VoiceTemplateCommandRecognizer recognizer;
    [SerializeField] private string commandId = "black变身音效";
    [SerializeField] private bool transformOnlyOnce = true;
    [SerializeField] private bool gateVoiceCommandsByForm = true;

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

    [Header("Keyboard Debug")]
    [SerializeField] private bool enableKeyboardDebugToggle = true;
    [SerializeField] private KeyCode keyboardDebugToggleKey = KeyCode.H;

    [Header("Henshin Sequence")]
    [SerializeField] private bool useHenshinSequence = true;
    [SerializeField] private AudioClip henshinClip;
    [SerializeField, Range(0.0f, 1.0f)] private float henshinVolume = 1.0f;
    [SerializeField] private GameObject previewSourceModel;
    [SerializeField, Range(0.1f, 1.0f)] private float previewStartScale = 0.75f;
    [SerializeField, Range(0.5f, 1.5f)] private float previewEndScale = 1.0f;
    [SerializeField, Range(0, 10)] private int previewTrackingWarmupFrames = 3;
    [SerializeField, Range(0.05f, 1.0f)] private float previewFadeDurationRatio = 0.55f;
    [SerializeField] private bool enableBeltReveal = true;
    [SerializeField] private Material beltRevealMaterial;
    [SerializeField] private string beltMaterialKeywords = "本体,metalg,metal,アーマー";
    [SerializeField] private string beltCenterMaterialKeywords = "ファン";
    [FormerlySerializedAs("previewLocalPositionOffset")]
    [Tooltip("Model-local offset applied only to belt reveal geometry and its light center.")]
    [SerializeField] private Vector3 beltRevealLocalPositionOffset;
    [SerializeField] private bool beltRevealRenderOnTop = true;
    [SerializeField] private bool beltRevealDoubleSided = true;
    [SerializeField, Range(0.01f, 0.5f)] private float beltCenterDurationRatio = 0.12f;
    [SerializeField, Range(0.05f, 0.75f)] private float beltExpandDurationRatio = 0.28f;
    [SerializeField, Range(0.01f, 0.5f)] private float beltRevealEdgeWidth = 0.08f;
    [SerializeField, ColorUsage(true, true)] private Color beltRevealGlowColor = new Color(1.0f, 0.05f, 0.0f, 1.0f);
    [SerializeField, Range(0.0f, 8.0f)] private float beltRevealGlowIntensity = 2.5f;
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

    [Header("Auto Steam Burst")]
    [SerializeField] private bool enableAutoSteamBurst = true;
    [SerializeField, Range(0.2f, 5.0f)] private float autoSteamDuration = 2.0f;
    [SerializeField, Range(2.0f, 80.0f)] private float autoSteamEmissionRate = 24.0f;
    [SerializeField, Range(0, 80)] private int autoSteamInitialBurstCount = 18;
    [SerializeField, Range(0.1f, 3.0f)] private float autoSteamMinLifetime = 0.7f;
    [SerializeField, Range(0.1f, 4.0f)] private float autoSteamMaxLifetime = 1.6f;
    [SerializeField, Range(0.02f, 1.0f)] private float autoSteamMinSize = 0.22f;
    [SerializeField, Range(0.02f, 1.5f)] private float autoSteamMaxSize = 0.48f;
    [SerializeField, Range(0.0f, 2.0f)] private float autoSteamRiseSpeed = 0.45f;
    [SerializeField, Range(0.0f, 1.0f)] private float autoSteamSurfaceOutwardSpeed = 0.12f;
    [SerializeField] private Vector3 autoSteamBoundsPadding = new Vector3(0.25f, 0.08f, 0.25f);
    [SerializeField, ColorUsage(false, true)] private Color autoSteamColor = new Color(0.9f, 0.95f, 1.0f, 0.22f);

    [Header("Henshin Light Effects")]
    [SerializeField] private bool enableHenshinLightEffects = true;
    [SerializeField, Range(0.0f, 0.6f)] private float postSwitchLightDurationRatio = 0.24f;
    [SerializeField, Range(8, 72)] private int beltLightRayCount = 32;
    [SerializeField, Range(0.2f, 5.0f)] private float beltLightRayLength = 2.1f;
    [SerializeField, Range(0.02f, 0.8f)] private float beltLightRaySegmentLength = 0.34f;
    [SerializeField, Range(0.0f, 0.5f)] private float beltLightRayInnerRadius = 0.06f;
    [SerializeField, Range(0.04f, 0.8f)] private float beltLightRayLifetime = 0.22f;
    [SerializeField, Range(0.002f, 0.12f)] private float beltLightRayWidth = 0.026f;
    [SerializeField] private Vector3 beltLightLocalOffset = new Vector3(0.0f, 0.14f, 0.02f);
    [SerializeField, ColorUsage(true, true)] private Color beltLightRed = new Color(1.0f, 0.02f, 0.0f, 1.0f);
    [SerializeField, ColorUsage(true, true)] private Color beltLightWhite = new Color(1.0f, 0.95f, 0.85f, 1.0f);
    [SerializeField, Range(0.0f, 12.0f)] private float beltLightIntensity = 5.0f;
    [SerializeField, Range(0.05f, 0.8f)] private float bodyFlashDuration = 0.34f;
    [SerializeField, Range(1, 4)] private int bodyFlashCount = 2;
    [SerializeField, Range(0.0f, 8.0f)] private float bodyFlashIntensity = 3.5f;
    [SerializeField] private bool useBodyCollapseShader;
    [SerializeField, Range(0.02f, 0.45f)] private float bodyFlashCollapseBand = 0.16f;
    [SerializeField] private string eyeMaterialKeywords = "MaskEyes,MaskRed,MaskLamp,eye";
    [SerializeField, ColorUsage(true, true)] private Color eyeFlashColor = new Color(1.0f, 0.0f, 0.0f, 1.0f);
    [SerializeField, Range(0.0f, 10.0f)] private float eyeFlashIntensity = 5.0f;
    [SerializeField, Range(0.0f, 0.35f)] private float eyeFlashDelayAfterBody = 0.08f;
    [SerializeField, Range(0.12f, 0.8f)] private float eyeFlashDuration = 0.42f;
    [SerializeField, Range(1, 4)] private int eyeFlashCount = 2;
    [SerializeField, Range(0.1f, 0.8f)] private float eyeFlashOnRatio = 0.32f;
    [SerializeField] private bool useFixedEyeBlinkTiming = true;
    [SerializeField, Range(0.01f, 0.12f)] private float eyeFlashOnSeconds = 0.035f;
    [SerializeField, Range(0.02f, 0.2f)] private float eyeFlashGapSeconds = 0.075f;
    [SerializeField] private bool suppressEyeEmissionBetweenFlashes = true;
    [SerializeField, Range(0.0f, 1.0f)] private float eyeOffColorMultiplier = 0.45f;
    [SerializeField] private string chestMarkMaterialKeywords = "SuitMark,胸マーク,マーク";
    [SerializeField, Range(0.05f, 1.2f)] private float chestMarkSweepDuration = 0.55f;
    [SerializeField, Range(0.01f, 0.75f)] private float chestMarkSweepWidth = 0.22f;
    [SerializeField] private bool chestMarkSweepRightToLeft = true;
    [SerializeField, Range(0.0f, 1.0f)] private float chestMarkMaskThreshold = 0.55f;
    [SerializeField, Range(0.01f, 0.4f)] private float chestMarkMaskSoftness = 0.12f;
    [SerializeField, ColorUsage(true, true)] private Color chestMarkSweepColor = Color.white;
    [SerializeField, Range(0.0f, 10.0f)] private float chestMarkSweepIntensity = 4.0f;

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
    private readonly List<PreviewMaterialState> previewMaterialStates = new List<PreviewMaterialState>();
    private readonly List<Material> previewMaterials = new List<Material>();
    private readonly List<OutlineMaterialState> outlineMaterials = new List<OutlineMaterialState>();
    private readonly List<BeltLightRayState> beltLightRays = new List<BeltLightRayState>();
    private readonly List<FinalEffectRendererState> finalEffectRendererStates = new List<FinalEffectRendererState>();
    private readonly List<FinalEffectMaterialState> finalEffectMaterialStates = new List<FinalEffectMaterialState>();
    private GameObject beltLightRoot;
    private GameObject beltLightAnchorModel;
    private Material beltLightRedMaterial;
    private Material beltLightWhiteMaterial;
    private Vector3 finalEffectBeltCenter;
    private float finalEffectMaxDistance = 1.0f;

    private enum PreviewMaterialRole
    {
        Body,
        Belt,
        BeltCenter
    }

    private enum FinalEffectMaterialRole
    {
        Body,
        Eye,
        ChestMark
    }

    private sealed class BeltLightRayState
    {
        public readonly LineRenderer Renderer;
        public readonly bool WhiteRay;
        public float Angle;
        public float SegmentLength;
        public float Lifetime;
        public float Age;
        public float AlphaScale;
        public float DensitySeed;

        public BeltLightRayState(LineRenderer renderer, bool whiteRay)
        {
            Renderer = renderer;
            WhiteRay = whiteRay;
            Angle = 0.0f;
            SegmentLength = 0.0f;
            Lifetime = 0.1f;
            Age = 0.0f;
            AlphaScale = 1.0f;
            DensitySeed = 0.0f;
        }
    }

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

    private readonly struct PreviewMaterialState
    {
        public readonly Material Material;
        public readonly PreviewMaterialRole Role;

        public PreviewMaterialState(Material material, PreviewMaterialRole role)
        {
            Material = material;
            Role = role;
        }
    }

    private readonly struct FinalEffectRendererState
    {
        public readonly Renderer Renderer;
        public readonly Material[] SharedMaterials;

        public FinalEffectRendererState(Renderer renderer, Material[] sharedMaterials)
        {
            Renderer = renderer;
            SharedMaterials = sharedMaterials;
        }
    }

    private readonly struct FinalEffectMaterialState
    {
        public readonly Material Material;
        public readonly FinalEffectMaterialRole Role;
        public readonly Color BaseColor;
        public readonly Color Color;
        public readonly Color EmissionColor;

        public FinalEffectMaterialState(
            Material material,
            FinalEffectMaterialRole role,
            Color baseColor,
            Color color,
            Color emissionColor)
        {
            Material = material;
            Role = role;
            BaseColor = baseColor;
            Color = color;
            EmissionColor = emissionColor;
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
            UpdateVoiceCommandGate(isTransformed);
        }
    }

    private void Update()
    {
        if (henshinSequenceRoutine != null)
        {
            return;
        }

        if (enableKeyboardDebugToggle && IsKeyboardKeyPressedThisFrame(keyboardDebugToggleKey))
        {
            ToggleFormFromManualInput();
            return;
        }

        if (enableLeftYReset && IsLeftYPressedThisFrame())
        {
            ToggleFormFromManualInput();
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

    private void ToggleFormFromManualInput()
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
        BeginBeltLightEffects(activeSequencePreviewModel);

        var remainingDuration = GetRemainingHenshinSequenceDuration(Time.time - sequenceStartTime);
        var postSwitchDuration = enableHenshinLightEffects
            ? Mathf.Clamp(remainingDuration * postSwitchLightDurationRatio, 0.0f, remainingDuration * 0.75f)
            : 0.0f;
        var previewDuration = Mathf.Max(0.01f, remainingDuration - postSwitchDuration);
        if (enableBeltReveal)
        {
            yield return RunBeltRevealSequence(previewDuration);
        }
        else
        {
            var fadeDuration = Mathf.Clamp(
                previewDuration * previewFadeDurationRatio,
                0.01f,
                previewDuration);
            var outlineDuration = Mathf.Max(0.0f, previewDuration - fadeDuration);

            yield return RunPreviewBodyFade(fadeDuration);
            yield return RunOutlineSweep(outlineDuration);
        }

        EndSequencePreview(restoreVisibility: false);
        SetTransformedState(true, invokeEvents: true);
        ReanchorBeltLightEffects(transformedModel);

        if (postSwitchDuration > 0.0f)
        {
            yield return RunPostSwitchLightEffects(postSwitchDuration);
        }

        EndHenshinLightEffects();

        PlaySteamEffects();

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
        EndHenshinLightEffects();
        RestoreFinalMaterialEffects();
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
        activeSequencePreviewModel.transform.localScale = enableBeltReveal
            ? sequencePreviewOriginalScale
            : sequencePreviewOriginalScale * previewStartScale;
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

        if (!enableBeltReveal)
        {
            activeSequencePreviewModel.transform.localScale =
                sequencePreviewOriginalScale * Mathf.Lerp(previewStartScale, previewEndScale, normalizedTime);
        }

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
        previewMaterialStates.Clear();

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

                var role = GetPreviewMaterialRole(originalMaterials[materialIndex]);
                var previewMaterial = CreatePreviewMaterial(originalMaterials[materialIndex], targetRenderer, role, target);
                previewMaterialArray[materialIndex] = previewMaterial;
                previewMaterials.Add(previewMaterial);
                previewMaterialStates.Add(new PreviewMaterialState(previewMaterial, role));
            }

            targetRenderer.sharedMaterials = previewMaterialArray;
            CreateOutlineRenderer(targetRenderer);
        }
    }

    private Material CreatePreviewMaterial(
        Material sourceMaterial,
        Renderer targetRenderer,
        PreviewMaterialRole role,
        GameObject previewRoot)
    {
        if (enableBeltReveal && role != PreviewMaterialRole.Body)
        {
            return CreateBeltRevealMaterial(sourceMaterial, targetRenderer, previewRoot);
        }

        var previewMaterial = new Material(sourceMaterial);
        ConfigurePreviewMaterial(previewMaterial);
        return previewMaterial;
    }

    private Material CreateBeltRevealMaterial(Material sourceMaterial, Renderer targetRenderer, GameObject previewRoot)
    {
        Material material;
        if (beltRevealMaterial != null)
        {
            material = new Material(beltRevealMaterial);
        }
        else
        {
            var shader = Shader.Find("KamenRider/HenshinBeltReveal");
            material = shader != null ? new Material(shader) : new Material(sourceMaterial);
        }

        CopyBaseMaterialProperties(sourceMaterial, material);
        ConfigureBeltRevealMaterial(material, targetRenderer, previewRoot, alpha: 0.0f, revealProgress: 0.0f);
        return material;
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

    private IEnumerator RunBeltRevealSequence(float remainingDuration)
    {
        SetBeltRevealState(centerAlpha: 1.0f, beltReveal: 0.0f);
        SetBodyPreviewAlpha(0.0f);

        var centerDuration = remainingDuration * beltCenterDurationRatio;
        var expandDuration = remainingDuration * beltExpandDurationRatio;
        var bodyFadeDuration = Mathf.Clamp(
            remainingDuration * previewFadeDurationRatio,
            0.01f,
            remainingDuration);

        yield return RunTimedPhase(centerDuration, t =>
        {
            SetBeltRevealState(centerAlpha: Mathf.SmoothStep(0.0f, 1.0f, t), beltReveal: 0.0f);
            SetBodyPreviewAlpha(0.0f);
            UpdateBeltLightEffects(Mathf.Lerp(0.22f, 0.55f, t), 0.0f);
        });

        yield return RunTimedPhase(expandDuration, t =>
        {
            SetBeltRevealState(centerAlpha: 1.0f, beltReveal: Mathf.SmoothStep(0.0f, 1.0f, t));
            SetBodyPreviewAlpha(0.0f);
            UpdateBeltLightEffects(Mathf.Lerp(0.55f, 0.85f, t), Mathf.Lerp(0.08f, 0.45f, t));
        });

        yield return RunTimedPhase(bodyFadeDuration, t =>
        {
            SetBeltRevealState(centerAlpha: 1.0f, beltReveal: 1.0f);
            SetBodyPreviewAlpha(Mathf.SmoothStep(0.0f, 1.0f, t));
            UpdateBeltLightEffects(Mathf.Lerp(0.85f, 1.0f, t), Mathf.Lerp(0.45f, 1.0f, t));
        });

        SetBeltRevealState(centerAlpha: 1.0f, beltReveal: 1.0f);
        SetBodyPreviewAlpha(1.0f);
        UpdateBeltLightEffects(1.0f, 1.0f);
    }

    private IEnumerator RunPreviewBodyFade(float duration)
    {
        yield return RunTimedPhase(duration, t =>
        {
            var easedTime = Mathf.SmoothStep(0.0f, 1.0f, t);
            UpdatePreview(easedTime);
        });

        UpdatePreview(1.0f);
    }

    private IEnumerator RunPostSwitchLightEffects(float duration)
    {
        if (!enableHenshinLightEffects || duration <= 0.0f)
        {
            yield break;
        }

        BeginFinalMaterialEffects();

        var elapsed = 0.0f;
        var bodyPhaseDuration = Mathf.Min(Mathf.Max(0.01f, bodyFlashDuration), duration);
        var availableEyeTime = Mathf.Max(0.0f, duration - bodyPhaseDuration);
        var eyeDelay = Mathf.Min(eyeFlashDelayAfterBody, availableEyeTime * 0.35f);
        var eyePhaseDuration = Mathf.Max(0.01f, Mathf.Min(eyeFlashDuration, availableEyeTime - eyeDelay));
        var availableChestTime = Mathf.Max(0.01f, duration - bodyPhaseDuration - eyeDelay);
        var chestPhaseDuration = Mathf.Max(0.01f, Mathf.Min(chestMarkSweepDuration, availableChestTime));

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var normalizedTime = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            var bodyFlashTime = Mathf.Clamp01(elapsed / bodyPhaseDuration);
            var eyeElapsed = elapsed - bodyPhaseDuration - eyeDelay;
            var eyeFlashTime = Mathf.Clamp01(eyeElapsed / eyePhaseDuration);
            var chestSweepTime = Mathf.Clamp01(eyeElapsed / chestPhaseDuration);
            var whiteMix = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, normalizedTime);
            var beltFadeTime = Mathf.InverseLerp(0.65f, 1.0f, normalizedTime);
            var beltIntensity = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, beltFadeTime);
            var bodyFlash = bodyFlashTime < 1.0f ? EvaluatePulse(bodyFlashTime, bodyFlashCount) : 0.0f;
            var eyeFlash = eyeElapsed >= 0.0f
                ? EvaluateEyeFlash(eyeElapsed, eyeFlashTime)
                : 0.0f;
            var chestIntensity = eyeElapsed >= 0.0f && chestSweepTime < 1.0f
                ? 1.0f
                : 0.0f;

            UpdateBeltLightEffects(beltIntensity, whiteMix);
            UpdateFinalMaterialEffects(bodyFlash, bodyFlashTime, eyeFlash, chestSweepTime, chestIntensity);
            yield return null;
        }

        UpdateBeltLightEffects(0.0f, 0.0f);
        UpdateFinalMaterialEffects(0.0f, 1.0f, 0.0f, 1.0f, 0.0f);
        RestoreFinalMaterialEffects();
    }

    private static IEnumerator RunTimedPhase(float duration, Action<float> update)
    {
        duration = Mathf.Max(0.01f, duration);
        var elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            update?.Invoke(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        update?.Invoke(1.0f);
    }

    private static float EvaluatePulse(float normalizedTime, int pulseCount)
    {
        var pulse = Mathf.Abs(Mathf.Sin(Mathf.Clamp01(normalizedTime) * Mathf.PI * Mathf.Max(1, pulseCount)));
        return Mathf.Pow(pulse, 0.55f);
    }

    private static float EvaluateBlinkPulse(float normalizedTime, int pulseCount, float onRatio)
    {
        var count = Mathf.Max(1, pulseCount);
        var scaledTime = Mathf.Clamp01(normalizedTime) * count;
        if (scaledTime >= count)
        {
            return 0.0f;
        }

        var blinkPhase = scaledTime - Mathf.Floor(scaledTime);
        var activeRatio = Mathf.Clamp(onRatio, 0.05f, 0.95f);
        if (blinkPhase > activeRatio)
        {
            return 0.0f;
        }

        var localTime = blinkPhase / activeRatio;
        const float fadeRatio = 0.18f;
        if (localTime < fadeRatio)
        {
            return Mathf.SmoothStep(0.0f, 1.0f, localTime / fadeRatio);
        }

        if (localTime > 1.0f - fadeRatio)
        {
            return Mathf.SmoothStep(0.0f, 1.0f, (1.0f - localTime) / fadeRatio);
        }

        return 1.0f;
    }

    private float EvaluateEyeFlash(float elapsedSeconds, float normalizedTime)
    {
        if (!useFixedEyeBlinkTiming)
        {
            return normalizedTime < 1.0f
                ? EvaluateBlinkPulse(normalizedTime, eyeFlashCount, eyeFlashOnRatio)
                : 0.0f;
        }

        var count = Mathf.Max(1, eyeFlashCount);
        var onSeconds = Mathf.Max(0.001f, eyeFlashOnSeconds);
        var cycleSeconds = onSeconds + Mathf.Max(0.0f, eyeFlashGapSeconds);
        var totalSeconds = (count * onSeconds) + ((count - 1) * Mathf.Max(0.0f, eyeFlashGapSeconds));
        if (elapsedSeconds < 0.0f || elapsedSeconds >= totalSeconds)
        {
            return 0.0f;
        }

        var blinkIndex = Mathf.FloorToInt(elapsedSeconds / cycleSeconds);
        if (blinkIndex >= count)
        {
            return 0.0f;
        }

        var localTime = elapsedSeconds - (blinkIndex * cycleSeconds);
        if (localTime > onSeconds)
        {
            return 0.0f;
        }

        var normalizedBlink = localTime / onSeconds;
        const float fadeRatio = 0.12f;
        if (normalizedBlink < fadeRatio)
        {
            return Mathf.SmoothStep(0.0f, 1.0f, normalizedBlink / fadeRatio);
        }

        if (normalizedBlink > 1.0f - fadeRatio)
        {
            return Mathf.SmoothStep(0.0f, 1.0f, (1.0f - normalizedBlink) / fadeRatio);
        }

        return 1.0f;
    }

    private void BeginBeltLightEffects(GameObject anchorModel)
    {
        EndHenshinLightEffects();

        if (!enableHenshinLightEffects || anchorModel == null || beltLightRayCount <= 0)
        {
            return;
        }

        var beltCenter = GetBeltEffectCenter(anchorModel);
        beltLightAnchorModel = anchorModel;
        beltLightRoot = new GameObject("HenshinBeltLightRays");
        beltLightRoot.transform.SetPositionAndRotation(beltCenter, GetCameraFacingRotation());
        beltLightRoot.transform.SetParent(anchorModel.transform, true);

        beltLightRedMaterial = CreateLightRayMaterial(beltLightRed, beltLightIntensity);
        beltLightWhiteMaterial = CreateLightRayMaterial(beltLightWhite, beltLightIntensity);

        for (var i = 0; i < beltLightRayCount; i++)
        {
            var rayObject = new GameObject($"Ray_{i:00}");
            rayObject.transform.SetParent(beltLightRoot.transform, false);

            var lineRenderer = rayObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.positionCount = 2;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.numCapVertices = 2;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;

            var whiteRay = i % 4 == 1 || i % 4 == 3;
            lineRenderer.sharedMaterial = whiteRay ? beltLightWhiteMaterial : beltLightRedMaterial;

            lineRenderer.SetPosition(0, Vector3.zero);
            lineRenderer.SetPosition(1, Vector3.zero);

            var rayState = new BeltLightRayState(lineRenderer, whiteRay);
            ResetBeltLightRay(rayState, true);
            beltLightRays.Add(rayState);
        }

        UpdateBeltLightEffects(0.0f, 0.0f);
    }

    private void ReanchorBeltLightEffects(GameObject anchorModel)
    {
        if (beltLightRoot == null || anchorModel == null)
        {
            return;
        }

        beltLightRoot.transform.SetParent(anchorModel.transform, true);
        beltLightAnchorModel = anchorModel;
        beltLightRoot.transform.position = GetBeltEffectCenter(anchorModel);
    }

    private void UpdateBeltLightEffects(float intensity, float whiteMix)
    {
        if (beltLightRoot == null)
        {
            return;
        }

        if (beltLightAnchorModel != null)
        {
            beltLightRoot.transform.position = GetBeltEffectCenter(beltLightAnchorModel);
        }

        beltLightRoot.transform.rotation = GetCameraFacingRotation();

        var clampedIntensity = Mathf.Clamp01(intensity);
        var clampedWhiteMix = Mathf.Clamp01(whiteMix);
        var outerRadius = Mathf.Max(beltLightRayInnerRadius + 0.01f, beltLightRayLength);
        var density = Mathf.SmoothStep(0.0f, 1.0f, clampedIntensity);
        for (var i = 0; i < beltLightRays.Count; i++)
        {
            var ray = beltLightRays[i];
            if (ray.Renderer == null)
            {
                continue;
            }

            ray.Age += Time.deltaTime;

            if (clampedIntensity <= 0.001f)
            {
                ray.Renderer.enabled = false;
                continue;
            }

            if (ray.Age >= ray.Lifetime)
            {
                ResetBeltLightRay(ray, false);
            }

            if (ray.DensitySeed > density)
            {
                ray.Renderer.enabled = false;
                continue;
            }

            var color = ray.WhiteRay ? beltLightWhite : beltLightRed;
            var colorMix = ray.WhiteRay ? clampedWhiteMix : Mathf.Lerp(1.0f, 0.35f, clampedWhiteMix);
            var lifeProgress = Mathf.Clamp01(ray.Age / Mathf.Max(0.01f, ray.Lifetime));
            var fadeIn = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(0.0f, 0.12f, lifeProgress));
            var fadeOut = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(0.58f, 1.0f, lifeProgress));
            var alpha = clampedIntensity * colorMix * ray.AlphaScale * fadeIn * fadeOut;
            color.a *= alpha;

            var direction = new Vector3(Mathf.Cos(ray.Angle), Mathf.Sin(ray.Angle), 0.0f);
            var distance = Mathf.Lerp(beltLightRayInnerRadius, outerRadius, Mathf.SmoothStep(0.0f, 1.0f, lifeProgress));
            var segmentLength = ray.SegmentLength * Mathf.Lerp(0.75f, 1.25f, clampedIntensity);
            var start = direction * distance;
            var end = direction * Mathf.Min(outerRadius, distance + segmentLength);
            var width = beltLightRayWidth * Mathf.Lerp(0.35f, 1.15f, alpha);
            ray.Renderer.enabled = alpha > 0.01f;
            ray.Renderer.startWidth = width;
            ray.Renderer.endWidth = width * 0.18f;
            ray.Renderer.startColor = color;
            ray.Renderer.endColor = new Color(color.r, color.g, color.b, 0.0f);
            ray.Renderer.SetPosition(0, start);
            ray.Renderer.SetPosition(1, end);
        }
    }

    private void ResetBeltLightRay(BeltLightRayState ray, bool randomizeAge)
    {
        if (ray == null)
        {
            return;
        }

        ray.Angle = UnityEngine.Random.Range(0.0f, Mathf.PI * 2.0f);
        ray.SegmentLength = beltLightRaySegmentLength * UnityEngine.Random.Range(0.45f, 1.25f);
        ray.Lifetime = Mathf.Max(0.01f, beltLightRayLifetime * UnityEngine.Random.Range(0.65f, 1.35f));
        ray.Age = randomizeAge ? UnityEngine.Random.Range(0.0f, ray.Lifetime) : 0.0f;
        ray.AlphaScale = UnityEngine.Random.Range(0.55f, 1.0f);
        ray.DensitySeed = UnityEngine.Random.value;
    }

    private void EndHenshinLightEffects()
    {
        if (beltLightRoot != null)
        {
            DestroyUnityObject(beltLightRoot);
            beltLightRoot = null;
        }

        beltLightAnchorModel = null;

        if (beltLightRedMaterial != null)
        {
            DestroyUnityObject(beltLightRedMaterial);
            beltLightRedMaterial = null;
        }

        if (beltLightWhiteMaterial != null)
        {
            DestroyUnityObject(beltLightWhiteMaterial);
            beltLightWhiteMaterial = null;
        }

        beltLightRays.Clear();
    }

    private void PlaySteamEffects()
    {
        if (steamParticles != null)
        {
            steamParticles.Play(true);
        }

        if (enableAutoSteamBurst)
        {
            PlayAutoSteamBurst();
        }
    }

    private void PlayAutoSteamBurst()
    {
        if (transformedModel == null)
        {
            return;
        }

        var steamObject = new GameObject("AutoHenshinSteamAirflow");
        steamObject.transform.SetParent(transformedModel.transform, true);
        steamObject.transform.position = transformedModel.transform.position;
        steamObject.transform.rotation = Quaternion.identity;

        var steamMaterial = CreateSteamParticleMaterial();
        var skinnedRenderers = transformedModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var visibleSkinnedRenderers = new List<SkinnedMeshRenderer>();
        for (var i = 0; i < skinnedRenderers.Length; i++)
        {
            var skinnedRenderer = skinnedRenderers[i];
            if (skinnedRenderer != null && skinnedRenderer.enabled && skinnedRenderer.sharedMesh != null)
            {
                visibleSkinnedRenderers.Add(skinnedRenderer);
            }
        }

        if (visibleSkinnedRenderers.Count > 0)
        {
            var ratePerRenderer = autoSteamEmissionRate / visibleSkinnedRenderers.Count;
            var burstPerRenderer = Mathf.CeilToInt((float)autoSteamInitialBurstCount / visibleSkinnedRenderers.Count);
            for (var i = 0; i < visibleSkinnedRenderers.Count; i++)
            {
                var emitter = new GameObject($"SteamAirflow_{visibleSkinnedRenderers[i].name}");
                emitter.transform.SetParent(steamObject.transform, false);
                var airflowParticleSystem = emitter.AddComponent<ParticleSystem>();
                var airflowParticleRenderer = emitter.GetComponent<ParticleSystemRenderer>();
                ConfigureSteamAirflowParticleSystem(
                    airflowParticleSystem,
                    airflowParticleRenderer,
                    steamMaterial,
                    visibleSkinnedRenderers[i],
                    null,
                    ratePerRenderer,
                    burstPerRenderer);
            }

            StartCoroutine(DestroyAutoSteamBurst(
                steamObject,
                steamMaterial,
                autoSteamDuration + Mathf.Max(autoSteamMinLifetime, autoSteamMaxLifetime) + 0.5f));
            return;
        }

        if (!TryGetHumanoidBodyBounds(transformedModel, out var bounds))
        {
            var renderers = transformedModel.GetComponentsInChildren<Renderer>(true);
            if (!TryGetRendererBounds(renderers, out bounds))
            {
                bounds = new Bounds(transformedModel.transform.position + Vector3.up * 0.9f, new Vector3(0.8f, 1.8f, 0.55f));
            }
        }

        steamObject.transform.position = bounds.center;

        var particleSystem = steamObject.AddComponent<ParticleSystem>();
        var particleRenderer = steamObject.GetComponent<ParticleSystemRenderer>();
        ConfigureSteamAirflowParticleSystem(
            particleSystem,
            particleRenderer,
            steamMaterial,
            null,
            bounds,
            autoSteamEmissionRate,
            autoSteamInitialBurstCount);

        StartCoroutine(DestroyAutoSteamBurst(
            steamObject,
            steamMaterial,
            autoSteamDuration + Mathf.Max(autoSteamMinLifetime, autoSteamMaxLifetime) + 0.5f));
    }

    private void ConfigureSteamAirflowParticleSystem(
        ParticleSystem particleSystem,
        ParticleSystemRenderer particleRenderer,
        Material steamMaterial,
        SkinnedMeshRenderer sourceRenderer,
        Bounds? fallbackBounds,
        float emissionRate,
        int initialBurstCount)
    {
        if (steamMaterial != null)
        {
            particleRenderer.sharedMaterial = steamMaterial;
        }

        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.normalDirection = 0.0f;
        particleRenderer.sortingFudge = 0.25f;

        var main = particleSystem.main;
        main.duration = Mathf.Max(0.01f, autoSteamDuration);
        main.loop = false;
        main.prewarm = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            Mathf.Min(autoSteamMinLifetime, autoSteamMaxLifetime),
            Mathf.Max(autoSteamMinLifetime, autoSteamMaxLifetime));
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.18f);
        main.startSize = new ParticleSystem.MinMaxCurve(
            Mathf.Min(autoSteamMinSize, autoSteamMaxSize),
            Mathf.Max(autoSteamMinSize, autoSteamMaxSize));
        main.startRotation = new ParticleSystem.MinMaxCurve(0.0f, Mathf.PI * 2.0f);
        main.startColor = new ParticleSystem.MinMaxGradient(autoSteamColor);
        main.maxParticles = Mathf.CeilToInt((autoSteamDuration + autoSteamMaxLifetime) * emissionRate) + initialBurstCount;

        var emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;
        var burstCount = (short)Mathf.Clamp(initialBurstCount, 0, short.MaxValue);
        emission.SetBursts(burstCount > 0
            ? new[] { new ParticleSystem.Burst(0.0f, burstCount) }
            : Array.Empty<ParticleSystem.Burst>());

        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.randomDirectionAmount = 0.28f;
        shape.sphericalDirectionAmount = autoSteamSurfaceOutwardSpeed;
        if (sourceRenderer != null)
        {
            shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
            shape.skinnedMeshRenderer = sourceRenderer;
        }
        else if (fallbackBounds.HasValue)
        {
            var bounds = fallbackBounds.Value;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(
                Mathf.Max(0.05f, bounds.size.x + autoSteamBoundsPadding.x),
                Mathf.Max(0.05f, bounds.size.y + autoSteamBoundsPadding.y),
                Mathf.Max(0.05f, bounds.size.z + autoSteamBoundsPadding.z));
        }

        var velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.035f, 0.035f);
        velocity.y = new ParticleSystem.MinMaxCurve(autoSteamRiseSpeed * 0.65f, autoSteamRiseSpeed * 1.25f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.035f, 0.035f);

        var noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.42f;
        noise.scrollSpeed = 0.16f;

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0.0f),
                new GradientColorKey(new Color(0.86f, 0.92f, 1.0f), 1.0f)
            },
            new[]
            {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(autoSteamColor.a, 0.12f),
                new GradientAlphaKey(autoSteamColor.a * 0.55f, 0.55f),
                new GradientAlphaKey(0.0f, 1.0f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1.0f,
            new AnimationCurve(
                new Keyframe(0.0f, 0.55f),
                new Keyframe(0.35f, 1.0f),
                new Keyframe(1.0f, 1.55f)));

        var rotationOverLifetime = particleSystem.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

        particleSystem.Play(true);
    }

    private IEnumerator DestroyAutoSteamBurst(GameObject steamObject, Material steamMaterial, float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, delay));
        DestroyUnityObject(steamObject);
        DestroyUnityObject(steamMaterial);
    }

    private static Material CreateSteamParticleMaterial()
    {
        var shader =
            Shader.Find("KamenRider/HenshinSoftSteam") ??
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Particles/Standard Unlit") ??
            Shader.Find("Sprites/Default");
        if (shader == null)
        {
            return null;
        }

        var material = new Material(shader);
        SetMaterialColor(material, "_BaseColor", Color.white);
        SetMaterialColor(material, "_Color", Color.white);
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
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private static bool TryGetHumanoidBodyBounds(GameObject model, out Bounds bounds)
    {
        bounds = default;
        var animator = model != null ? model.GetComponentInChildren<Animator>(true) : null;
        if (animator == null || !animator.isHuman)
        {
            return false;
        }

        var bones = new[]
        {
            HumanBodyBones.Head,
            HumanBodyBones.Chest,
            HumanBodyBones.Hips,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot
        };

        var hasBounds = false;
        for (var i = 0; i < bones.Length; i++)
        {
            var boneTransform = animator.GetBoneTransform(bones[i]);
            if (boneTransform == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = new Bounds(boneTransform.position, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(boneTransform.position);
            }
        }

        if (hasBounds)
        {
            bounds.Expand(new Vector3(0.35f, 0.25f, 0.35f));
        }

        return hasBounds;
    }

    private static Material CreateLightRayMaterial(Color tintColor, float intensity)
    {
        var shader = Shader.Find("KamenRider/HenshinLightRay");
        var fallbackShader = Shader.Find("Sprites/Default");
        var material = new Material(shader != null ? shader : fallbackShader);

        if (material.HasProperty("_TintColor"))
        {
            material.SetColor("_TintColor", tintColor);
        }

        if (material.HasProperty("_Intensity"))
        {
            material.SetFloat("_Intensity", intensity);
        }

        return material;
    }

    private Vector3 GetBeltEffectCenter(GameObject model)
    {
        if (TryGetHumanoidBonePosition(model, HumanBodyBones.Hips, out var hipsPosition))
        {
            return ApplyBeltLightOffset(model, hipsPosition);
        }

        if (TryGetRoleBounds(model, PreviewMaterialRole.BeltCenter, out var centerBounds))
        {
            return ApplyBeltLightOffset(model, centerBounds.center);
        }

        if (TryGetRoleBounds(model, PreviewMaterialRole.Belt, out var beltBounds))
        {
            return ApplyBeltLightOffset(model, beltBounds.center);
        }

        var renderers = model != null ? model.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
        if (TryGetRendererBounds(renderers, out var modelBounds))
        {
            return ApplyBeltLightOffset(model, modelBounds.center + Vector3.down * modelBounds.extents.y * 0.25f);
        }

        return ApplyBeltLightOffset(model, model != null ? model.transform.position : transform.position);
    }

    private Vector3 ApplyBeltLightOffset(GameObject model, Vector3 worldPosition)
    {
        var localOffset = beltLightLocalOffset + beltRevealLocalPositionOffset;
        return model != null
            ? worldPosition + model.transform.TransformVector(localOffset)
            : worldPosition + localOffset;
    }

    private static bool TryGetHumanoidBonePosition(GameObject model, HumanBodyBones bone, out Vector3 position)
    {
        position = default;
        var animator = model != null ? model.GetComponentInChildren<Animator>(true) : null;
        if (animator == null || !animator.isHuman)
        {
            return false;
        }

        var boneTransform = animator.GetBoneTransform(bone);
        if (boneTransform == null)
        {
            return false;
        }

        position = boneTransform.position;
        return true;
    }

    private bool TryGetRoleBounds(GameObject model, PreviewMaterialRole role, out Bounds bounds)
    {
        bounds = default;
        var hasBounds = false;
        if (model == null)
        {
            return false;
        }

        var renderers = model.GetComponentsInChildren<Renderer>(true);
        for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            var targetRenderer = renderers[rendererIndex];
            var materials = targetRenderer.sharedMaterials;
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                if (GetPreviewMaterialRole(materials[materialIndex]) != role)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = targetRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(targetRenderer.bounds);
                }

                break;
            }
        }

        return hasBounds;
    }

    private static bool TryGetRendererBounds(Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        var hasBounds = false;
        if (renderers == null)
        {
            return false;
        }

        for (var i = 0; i < renderers.Length; i++)
        {
            var targetRenderer = renderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = targetRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(targetRenderer.bounds);
            }
        }

        return hasBounds;
    }

    private static float CalculateMaxDistanceFromPoint(Renderer[] renderers, Vector3 point)
    {
        if (!TryGetRendererBounds(renderers, out var bounds))
        {
            return 1.0f;
        }

        var maxDistance = 0.01f;
        var min = bounds.min;
        var max = bounds.max;
        for (var x = 0; x <= 1; x++)
        {
            for (var y = 0; y <= 1; y++)
            {
                for (var z = 0; z <= 1; z++)
                {
                    var corner = new Vector3(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z);
                    maxDistance = Mathf.Max(maxDistance, Vector3.Distance(point, corner));
                }
            }
        }

        return maxDistance;
    }

    private static Quaternion GetCameraFacingRotation()
    {
        var cameraComponent = Camera.main;
        return cameraComponent != null
            ? cameraComponent.transform.rotation
            : Quaternion.identity;
    }

    private void BeginFinalMaterialEffects()
    {
        RestoreFinalMaterialEffects();

        if (transformedModel == null)
        {
            return;
        }

        finalEffectBeltCenter = GetBeltEffectCenter(transformedModel);
        var renderers = transformedModel.GetComponentsInChildren<Renderer>(true);
        finalEffectMaxDistance = CalculateMaxDistanceFromPoint(renderers, finalEffectBeltCenter);

        for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            var targetRenderer = renderers[rendererIndex];
            var originalMaterials = targetRenderer.sharedMaterials;
            if (originalMaterials == null || originalMaterials.Length == 0)
            {
                continue;
            }

            var effectMaterials = new Material[originalMaterials.Length];
            var hasEffectMaterial = false;
            for (var materialIndex = 0; materialIndex < originalMaterials.Length; materialIndex++)
            {
                var sourceMaterial = originalMaterials[materialIndex];
                if (sourceMaterial == null)
                {
                    continue;
                }

                var role = GetFinalEffectMaterialRole(sourceMaterial);
                var effectMaterial = CreateFinalEffectMaterial(sourceMaterial, targetRenderer, role);
                effectMaterials[materialIndex] = effectMaterial;
                finalEffectMaterialStates.Add(new FinalEffectMaterialState(
                    effectMaterial,
                    role,
                    GetMaterialColor(effectMaterial, "_BaseColor", Color.white),
                    GetMaterialColor(effectMaterial, "_Color", Color.white),
                    GetMaterialColor(effectMaterial, "_EmissionColor", Color.black)));
                hasEffectMaterial = true;
            }

            if (!hasEffectMaterial)
            {
                continue;
            }

            finalEffectRendererStates.Add(new FinalEffectRendererState(targetRenderer, originalMaterials));
            targetRenderer.sharedMaterials = effectMaterials;
        }
    }

    private Material CreateFinalEffectMaterial(
        Material sourceMaterial,
        Renderer targetRenderer,
        FinalEffectMaterialRole role)
    {
        if (role == FinalEffectMaterialRole.ChestMark)
        {
            var chestShader = Shader.Find("KamenRider/HenshinChestSweep");
            if (chestShader != null)
            {
                var chestMaterial = new Material(chestShader);
                CopyBaseMaterialProperties(sourceMaterial, chestMaterial);
                ConfigureChestSweepMaterial(chestMaterial, targetRenderer, 1.0f, 0.0f, 0.0f);
                return chestMaterial;
            }
        }

        var bodyFlashShader = useBodyCollapseShader
            ? Shader.Find("KamenRider/HenshinBodyFlash")
            : null;
        if (bodyFlashShader != null)
        {
            var bodyMaterial = new Material(bodyFlashShader);
            CopyBaseMaterialProperties(sourceMaterial, bodyMaterial);
            ConfigureBodyFlashMaterial(bodyMaterial, 0.0f, 0.0f, 0.0f);
            return bodyMaterial;
        }

        var effectMaterial = new Material(sourceMaterial);
        effectMaterial.EnableKeyword("_EMISSION");
        return effectMaterial;
    }

    private void UpdateFinalMaterialEffects(
        float bodyFlash,
        float bodyCollapseProgress,
        float eyeFlash,
        float chestSweepProgress,
        float chestSweepIntensity)
    {
        var clampedBodyFlash = Mathf.Clamp01(bodyFlash);
        var clampedBodyCollapse = Mathf.Clamp01(bodyCollapseProgress);
        var clampedEyeFlash = Mathf.Clamp01(eyeFlash);
        var clampedChestSweep = Mathf.Clamp01(chestSweepProgress);
        var clampedChestIntensity = Mathf.Clamp01(chestSweepIntensity);

        if (transformedModel != null)
        {
            finalEffectBeltCenter = GetBeltEffectCenter(transformedModel);
        }

        for (var i = 0; i < finalEffectMaterialStates.Count; i++)
        {
            var state = finalEffectMaterialStates[i];
            if (state.Material == null)
            {
                continue;
            }

            if (state.Material.HasProperty("_CollapseProgress"))
            {
                ConfigureBodyFlashMaterial(
                    state.Material,
                    clampedBodyFlash * bodyFlashIntensity,
                    clampedBodyCollapse,
                    state.Role == FinalEffectMaterialRole.Eye
                        ? clampedEyeFlash * eyeFlashIntensity
                        : 0.0f);
                continue;
            }

            var baseFlashColor = Color.Lerp(state.BaseColor, Color.white, clampedBodyFlash * 0.72f);
            var colorFlashColor = Color.Lerp(state.Color, Color.white, clampedBodyFlash * 0.72f);
            var emission = state.EmissionColor + Color.white * (clampedBodyFlash * bodyFlashIntensity);

            if (state.Role == FinalEffectMaterialRole.Eye)
            {
                var dimmedBaseColor = state.BaseColor * eyeOffColorMultiplier;
                var dimmedColor = state.Color * eyeOffColorMultiplier;
                baseFlashColor = Color.Lerp(dimmedBaseColor, eyeFlashColor, clampedEyeFlash);
                colorFlashColor = Color.Lerp(dimmedColor, eyeFlashColor, clampedEyeFlash);
                baseFlashColor = Color.Lerp(baseFlashColor, Color.white, clampedBodyFlash * 0.72f);
                colorFlashColor = Color.Lerp(colorFlashColor, Color.white, clampedBodyFlash * 0.72f);
                emission = suppressEyeEmissionBetweenFlashes
                    ? Color.white * (clampedBodyFlash * bodyFlashIntensity)
                    : emission;
                emission += eyeFlashColor * (clampedEyeFlash * eyeFlashIntensity);
            }

            SetMaterialColor(state.Material, "_BaseColor", baseFlashColor);
            SetMaterialColor(state.Material, "_Color", colorFlashColor);
            SetMaterialEmission(state.Material, emission);

            if (state.Role == FinalEffectMaterialRole.ChestMark)
            {
                ConfigureChestSweepMaterial(
                    state.Material,
                    null,
                    clampedChestSweep,
                    chestMarkSweepIntensity * clampedChestIntensity,
                    clampedBodyFlash * bodyFlashIntensity);
            }
        }
    }

    private void RestoreFinalMaterialEffects()
    {
        for (var i = 0; i < finalEffectRendererStates.Count; i++)
        {
            var state = finalEffectRendererStates[i];
            if (state.Renderer != null)
            {
                state.Renderer.sharedMaterials = state.SharedMaterials;
            }
        }

        finalEffectRendererStates.Clear();

        for (var i = 0; i < finalEffectMaterialStates.Count; i++)
        {
            if (finalEffectMaterialStates[i].Material != null)
            {
                DestroyUnityObject(finalEffectMaterialStates[i].Material);
            }
        }

        finalEffectMaterialStates.Clear();
    }

    private FinalEffectMaterialRole GetFinalEffectMaterialRole(Material material)
    {
        if (material == null)
        {
            return FinalEffectMaterialRole.Body;
        }

        var lookupText = GetMaterialLookupText(material);
        if (MatchesAnyKeyword(lookupText, eyeMaterialKeywords))
        {
            return FinalEffectMaterialRole.Eye;
        }

        if (MatchesAnyKeyword(lookupText, chestMarkMaterialKeywords))
        {
            return FinalEffectMaterialRole.ChestMark;
        }

        return FinalEffectMaterialRole.Body;
    }

    private void ConfigureBodyFlashMaterial(
        Material material,
        float flashIntensity,
        float collapseProgress,
        float eyeIntensity)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BeltCenterWS"))
        {
            material.SetVector("_BeltCenterWS", finalEffectBeltCenter);
        }

        if (material.HasProperty("_MaxDistance"))
        {
            material.SetFloat("_MaxDistance", Mathf.Max(0.01f, finalEffectMaxDistance));
        }

        if (material.HasProperty("_CollapseProgress"))
        {
            material.SetFloat("_CollapseProgress", collapseProgress);
        }

        if (material.HasProperty("_CollapseBand"))
        {
            material.SetFloat("_CollapseBand", bodyFlashCollapseBand);
        }

        if (material.HasProperty("_FlashColor"))
        {
            material.SetColor("_FlashColor", Color.white);
        }

        if (material.HasProperty("_FlashIntensity"))
        {
            material.SetFloat("_FlashIntensity", flashIntensity);
        }

        if (material.HasProperty("_EyeFlashColor"))
        {
            material.SetColor("_EyeFlashColor", eyeFlashColor);
        }

        if (material.HasProperty("_EyeFlashIntensity"))
        {
            material.SetFloat("_EyeFlashIntensity", eyeIntensity);
        }
    }

    private void ConfigureChestSweepMaterial(
        Material material,
        Renderer targetRenderer,
        float sweepProgress,
        float sweepIntensity,
        float flashIntensity)
    {
        if (material == null)
        {
            return;
        }

        if (targetRenderer != null)
        {
            var bounds = GetRendererLocalBounds(targetRenderer);
            if (material.HasProperty("_CenterX"))
            {
                material.SetFloat("_CenterX", bounds.center.x);
            }

            if (material.HasProperty("_HalfWidth"))
            {
                material.SetFloat("_HalfWidth", Mathf.Max(0.0001f, bounds.extents.x));
            }
        }

        if (material.HasProperty("_SweepProgress"))
        {
            material.SetFloat("_SweepProgress", sweepProgress);
        }

        if (material.HasProperty("_SweepWidth"))
        {
            material.SetFloat("_SweepWidth", chestMarkSweepWidth);
        }

        if (material.HasProperty("_SweepDirection"))
        {
            material.SetFloat("_SweepDirection", chestMarkSweepRightToLeft ? -1.0f : 1.0f);
        }

        if (material.HasProperty("_MarkThreshold"))
        {
            material.SetFloat("_MarkThreshold", chestMarkMaskThreshold);
        }

        if (material.HasProperty("_MarkSoftness"))
        {
            material.SetFloat("_MarkSoftness", chestMarkMaskSoftness);
        }

        if (material.HasProperty("_SweepColor"))
        {
            material.SetColor("_SweepColor", chestMarkSweepColor);
        }

        if (material.HasProperty("_SweepIntensity"))
        {
            material.SetFloat("_SweepIntensity", sweepIntensity);
        }

        if (material.HasProperty("_FlashColor"))
        {
            material.SetColor("_FlashColor", Color.white);
        }

        if (material.HasProperty("_FlashIntensity"))
        {
            material.SetFloat("_FlashIntensity", flashIntensity);
        }
    }

    private static Color GetMaterialColor(Material material, string propertyName, Color fallback)
    {
        return material != null && material.HasProperty(propertyName)
            ? material.GetColor(propertyName)
            : fallback;
    }

    private static void SetMaterialColor(Material material, string propertyName, Color color)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static void SetMaterialEmission(Material material, Color emission)
    {
        if (material == null || !material.HasProperty("_EmissionColor"))
        {
            return;
        }

        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission);
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

    private static void CopyBaseMaterialProperties(Material sourceMaterial, Material targetMaterial)
    {
        if (sourceMaterial == null || targetMaterial == null)
        {
            return;
        }

        if (targetMaterial.HasProperty("_BaseMap"))
        {
            if (sourceMaterial.HasProperty("_BaseMap"))
            {
                targetMaterial.SetTexture("_BaseMap", sourceMaterial.GetTexture("_BaseMap"));
                targetMaterial.SetTextureScale("_BaseMap", sourceMaterial.GetTextureScale("_BaseMap"));
                targetMaterial.SetTextureOffset("_BaseMap", sourceMaterial.GetTextureOffset("_BaseMap"));
            }
            else if (sourceMaterial.HasProperty("_MainTex"))
            {
                targetMaterial.SetTexture("_BaseMap", sourceMaterial.GetTexture("_MainTex"));
                targetMaterial.SetTextureScale("_BaseMap", sourceMaterial.GetTextureScale("_MainTex"));
                targetMaterial.SetTextureOffset("_BaseMap", sourceMaterial.GetTextureOffset("_MainTex"));
            }
        }

        if (targetMaterial.HasProperty("_BaseColor"))
        {
            if (sourceMaterial.HasProperty("_BaseColor"))
            {
                targetMaterial.SetColor("_BaseColor", sourceMaterial.GetColor("_BaseColor"));
            }
            else if (sourceMaterial.HasProperty("_Color"))
            {
                targetMaterial.SetColor("_BaseColor", sourceMaterial.GetColor("_Color"));
            }
        }
    }

    private void ConfigureBeltRevealMaterial(
        Material material,
        Renderer targetRenderer,
        GameObject previewRoot,
        float alpha,
        float revealProgress)
    {
        if (material == null || targetRenderer == null)
        {
            return;
        }

        var bounds = GetRendererLocalBounds(targetRenderer);
        var centerX = bounds.center.x;
        var halfWidth = Mathf.Max(0.0001f, bounds.extents.x);

        if (material.HasProperty("_Alpha"))
        {
            material.SetFloat("_Alpha", alpha);
        }

        if (material.HasProperty("_RevealProgress"))
        {
            material.SetFloat("_RevealProgress", revealProgress);
        }

        if (material.HasProperty("_CenterX"))
        {
            material.SetFloat("_CenterX", centerX);
        }

        if (material.HasProperty("_HalfWidth"))
        {
            material.SetFloat("_HalfWidth", halfWidth);
        }

        if (material.HasProperty("_EdgeWidth"))
        {
            material.SetFloat("_EdgeWidth", beltRevealEdgeWidth);
        }

        if (material.HasProperty("_GlowColor"))
        {
            material.SetColor("_GlowColor", beltRevealGlowColor);
        }

        if (material.HasProperty("_GlowIntensity"))
        {
            material.SetFloat("_GlowIntensity", beltRevealGlowIntensity);
        }

        if (material.HasProperty("_ZTestMode"))
        {
            material.SetFloat(
                "_ZTestMode",
                beltRevealRenderOnTop ? (float)CompareFunction.Always : (float)CompareFunction.LessEqual);
        }

        if (material.HasProperty("_CullMode"))
        {
            material.SetFloat("_CullMode", beltRevealDoubleSided ? (float)CullMode.Off : (float)CullMode.Back);
        }

        material.renderQueue = beltRevealRenderOnTop
            ? (int)RenderQueue.Transparent + 100
            : (int)RenderQueue.Transparent;

        if (material.HasProperty("_VertexOffsetOS"))
        {
            material.SetVector("_VertexOffsetOS", GetRendererObjectSpaceBeltRevealOffset(targetRenderer, previewRoot));
        }
    }

    private Vector3 GetRendererObjectSpaceBeltRevealOffset(Renderer targetRenderer, GameObject previewRoot)
    {
        if (targetRenderer == null)
        {
            return beltRevealLocalPositionOffset;
        }

        var rootTransform = previewRoot != null ? previewRoot.transform : targetRenderer.transform;
        var worldOffset = rootTransform.TransformVector(beltRevealLocalPositionOffset);
        return targetRenderer.transform.InverseTransformVector(worldOffset);
    }

    private static Bounds GetRendererLocalBounds(Renderer targetRenderer)
    {
        if (targetRenderer is SkinnedMeshRenderer skinnedMeshRenderer)
        {
            return skinnedMeshRenderer.localBounds;
        }

        var meshFilter = targetRenderer.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            return meshFilter.sharedMesh.bounds;
        }

        return new Bounds(Vector3.zero, Vector3.one);
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
        for (var i = 0; i < previewMaterialStates.Count; i++)
        {
            var state = previewMaterialStates[i];
            if (enableBeltReveal && state.Role != PreviewMaterialRole.Body)
            {
                ConfigureBeltPreviewAlpha(state.Material, state.Role, alpha);
                continue;
            }

            SetMaterialAlpha(state.Material, alpha);
        }
    }

    private void SetBodyPreviewAlpha(float alpha)
    {
        for (var i = 0; i < previewMaterialStates.Count; i++)
        {
            var state = previewMaterialStates[i];
            if (state.Role == PreviewMaterialRole.Body)
            {
                SetMaterialAlpha(state.Material, alpha);
            }
        }
    }

    private void SetBeltRevealState(float centerAlpha, float beltReveal)
    {
        for (var i = 0; i < previewMaterialStates.Count; i++)
        {
            var state = previewMaterialStates[i];
            if (state.Role == PreviewMaterialRole.BeltCenter)
            {
                ConfigureBeltPreviewAlpha(state.Material, state.Role, centerAlpha);
                SetBeltRevealProgress(state.Material, 1.0f);
            }
            else if (state.Role == PreviewMaterialRole.Belt)
            {
                ConfigureBeltPreviewAlpha(state.Material, state.Role, beltReveal > 0.0f ? 1.0f : 0.0f);
                SetBeltRevealProgress(state.Material, beltReveal);
            }
        }
    }

    private static void ConfigureBeltPreviewAlpha(Material material, PreviewMaterialRole role, float alpha)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Alpha"))
        {
            material.SetFloat("_Alpha", alpha);
        }

        if (role == PreviewMaterialRole.BeltCenter && material.HasProperty("_RevealProgress"))
        {
            material.SetFloat("_RevealProgress", 1.0f);
        }
    }

    private static void SetBeltRevealProgress(Material material, float revealProgress)
    {
        if (material != null && material.HasProperty("_RevealProgress"))
        {
            material.SetFloat("_RevealProgress", revealProgress);
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

    private PreviewMaterialRole GetPreviewMaterialRole(Material material)
    {
        if (!enableBeltReveal || material == null)
        {
            return PreviewMaterialRole.Body;
        }

        var lookupText = GetMaterialLookupText(material);
        var isBlackBeltCenterMaterial = MatchesAnyKeyword(lookupText, BlackBeltCenterMaterialName);
        if (MatchesAnyKeyword(lookupText, beltCenterMaterialKeywords) || isBlackBeltCenterMaterial)
        {
            return PreviewMaterialRole.BeltCenter;
        }

        var isBlackBeltMaterial = MatchesAnyKeyword(lookupText, BlackBeltMaterialPrefix);
        if (MatchesAnyKeyword(lookupText, beltMaterialKeywords) || isBlackBeltMaterial)
        {
            return PreviewMaterialRole.Belt;
        }

        return PreviewMaterialRole.Body;
    }

    private static string GetMaterialLookupText(Material material)
    {
        var lookupText = material.name;
        AppendTextureName(material, "_BaseMap", ref lookupText);
        AppendTextureName(material, "_MainTex", ref lookupText);
        AppendTextureName(material, "_EmissionMap", ref lookupText);
        return lookupText;
    }

    private static void AppendTextureName(Material material, string propertyName, ref string lookupText)
    {
        if (material == null || !material.HasProperty(propertyName))
        {
            return;
        }

        var texture = material.GetTexture(propertyName);
        if (texture != null && !string.IsNullOrWhiteSpace(texture.name))
        {
            lookupText = $"{lookupText},{texture.name}";
        }
    }

    private static bool MatchesAnyKeyword(string value, string keywords)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(keywords))
        {
            return false;
        }

        var splitKeywords = keywords.Split(',');
        for (var i = 0; i < splitKeywords.Length; i++)
        {
            var keyword = splitKeywords[i].Trim();
            if (keyword.Length > 0 && value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void SetTransformedState(bool transformed, bool invokeEvents)
    {
        isTransformed = transformed;
        UpdateVoiceCommandGate(transformed);

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

    private void UpdateVoiceCommandGate(bool transformed)
    {
        if (recognizer == null)
        {
            return;
        }

        recognizer.ConfigureCommandGate(
            gateVoiceCommandsByForm,
            transformed,
            commandId);
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

    private static bool IsKeyboardKeyPressedThisFrame(KeyCode keyCode)
    {
#if ENABLE_INPUT_SYSTEM
        if (IsKeyboardKeyPressedThisFrameInputSystem(keyCode))
        {
            return true;
        }
#endif

        try
        {
            return Input.GetKeyDown(keyCode);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

#if ENABLE_INPUT_SYSTEM
    private static bool IsKeyboardKeyPressedThisFrameInputSystem(KeyCode keyCode)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        switch (keyCode)
        {
            case KeyCode.H:
                return keyboard.hKey.wasPressedThisFrame;
            case KeyCode.B:
                return keyboard.bKey.wasPressedThisFrame;
            case KeyCode.T:
                return keyboard.tKey.wasPressedThisFrame;
            case KeyCode.Space:
                return keyboard.spaceKey.wasPressedThisFrame;
            case KeyCode.Return:
                return keyboard.enterKey.wasPressedThisFrame;
            case KeyCode.Alpha0:
                return keyboard.digit0Key.wasPressedThisFrame;
            case KeyCode.Alpha1:
                return keyboard.digit1Key.wasPressedThisFrame;
            case KeyCode.Alpha2:
                return keyboard.digit2Key.wasPressedThisFrame;
            case KeyCode.Alpha3:
                return keyboard.digit3Key.wasPressedThisFrame;
            case KeyCode.Alpha4:
                return keyboard.digit4Key.wasPressedThisFrame;
            case KeyCode.Alpha5:
                return keyboard.digit5Key.wasPressedThisFrame;
            case KeyCode.Alpha6:
                return keyboard.digit6Key.wasPressedThisFrame;
            case KeyCode.Alpha7:
                return keyboard.digit7Key.wasPressedThisFrame;
            case KeyCode.Alpha8:
                return keyboard.digit8Key.wasPressedThisFrame;
            case KeyCode.Alpha9:
                return keyboard.digit9Key.wasPressedThisFrame;
            default:
                return false;
        }
    }
#endif

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
