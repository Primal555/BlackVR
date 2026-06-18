using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class JapaneseStreetUrpSetup
{
    private const string AssetRoot = "Assets/Japanese_Street";
    private const string SourceScenePath = AssetRoot + "/Scenes/Showcase.unity";
    private const string ProjectScenePath = "Assets/Scenes/JapaneseStreetVR.unity";
    private const string LiteScenePath = "Assets/Scenes/JapaneseStreetVR_Lite.unity";
    private const string MarkerPath = AssetRoot + "/.kamen-rider-urp-setup.txt";

    [InitializeOnLoadMethod]
    private static void RunOnceAfterImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (File.Exists(ProjectScenePath) && SceneNeedsSanitizing())
            {
                FixJapaneseStreetVrScene();
            }

            if (File.Exists(ProjectScenePath) && !File.Exists(LiteScenePath))
            {
                CreateJapaneseStreetVrLiteScene();
            }

            if (!AssetDatabase.IsValidFolder(AssetRoot) ||
                !File.Exists(SourceScenePath) ||
                File.Exists(MarkerPath))
            {
                return;
            }

            Run();
        };
    }

    [MenuItem("Kamen Rider/Convert Japanese Street To URP")]
    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder(AssetRoot))
        {
            Debug.LogWarning($"Japanese Street asset folder was not found: {AssetRoot}");
            return;
        }

        AssetDatabase.StartAssetEditing();
        try
        {
            ConvertMaterials();
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        CreateProjectScene();
        AddSceneToBuildSettings();
        WriteMarker();

        Debug.Log("Japanese Street setup complete. Open Assets/Scenes/JapaneseStreetVR.unity to use the URP VR scene.");
    }

    [MenuItem("Kamen Rider/Fix Japanese Street VR Scene")]
    public static void FixJapaneseStreetVrScene()
    {
        if (!File.Exists(ProjectScenePath))
        {
            Debug.LogWarning($"Project scene was not found: {ProjectScenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ProjectScenePath, OpenSceneMode.Additive);
        RemoveLegacyCamera(scene);
        AddXrOrigin(scene);
        AddBasicStreetColliders(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, removeScene: true);

        Debug.Log("Japanese Street VR scene cameras were sanitized.");
    }

    [MenuItem("Kamen Rider/Create Japanese Street VR Lite Scene")]
    public static void CreateJapaneseStreetVrLiteScene()
    {
        if (!File.Exists(ProjectScenePath))
        {
            Debug.LogWarning($"Project scene was not found: {ProjectScenePath}");
            return;
        }

        if (File.Exists(LiteScenePath))
        {
            AssetDatabase.DeleteAsset(LiteScenePath);
        }

        AssetDatabase.CopyAsset(ProjectScenePath, LiteScenePath);
        AssetDatabase.ImportAsset(LiteScenePath);

        var scene = EditorSceneManager.OpenScene(LiteScenePath, OpenSceneMode.Additive);
        RemoveLegacyCamera(scene);
        AddXrOrigin(scene);
        OptimizeSceneForVr(scene);
        AddBasicStreetColliders(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, removeScene: true);

        var scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(item => item.path == LiteScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(LiteScenePath, enabled: true));
        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.Log("Created Japanese Street VR Lite scene and moved it to the top of Build Settings.");
    }

    private static void ConvertMaterials()
    {
        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        var particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");

        if (litShader == null)
        {
            Debug.LogError("URP Lit shader was not found. Confirm the Universal Render Pipeline package is installed.");
            return;
        }

        var materialGuids = AssetDatabase.FindAssets("t:Material", new[] { AssetRoot });
        var convertedCount = 0;

        foreach (var guid in materialGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                continue;
            }

            // Skip skybox materials - they should NOT be converted to URP/Lit
            var materialNameLower = material.name.ToLowerInvariant();
            if (materialNameLower.Contains("sky") || path.Contains("/Textures/"))
            {
                continue;
            }

            var snapshot = MaterialSnapshot.Capture(material);

            // Foliage/plant materials use _Albedo instead of _MainTex – migrate it
            if (snapshot.MainTexture == null && material.HasProperty("_Albedo"))
            {
                var albedoTexture = material.GetTexture("_Albedo");
                if (albedoTexture != null)
                {
                    snapshot = MaterialSnapshot.CaptureWithFallback(material, albedoTexture);
                }
            }

            var isParticle = IsParticleMaterial(path, material.name, snapshot.ShaderName);
            var targetShader = isParticle ? particleShader ?? unlitShader ?? litShader : litShader;

            material.shader = targetShader;
            ApplyCommonProperties(material, snapshot);

            if (isParticle)
            {
                ConfigureTransparent(material, additive: snapshot.DstBlend == (float)BlendMode.One);
            }
            else if (snapshot.Mode == 1 || IsFoliageMaterial(path, material.name))
            {
                ConfigureCutout(material, snapshot.Cutoff);
            }
            else if (snapshot.Mode == 2 || snapshot.Mode == 3 || snapshot.BaseColor.a < 0.99f)
            {
                ConfigureTransparent(material, additive: false);
            }
            else
            {
                ConfigureOpaque(material);
            }

            EditorUtility.SetDirty(material);
            convertedCount++;
        }

        Debug.Log($"Converted {convertedCount} Japanese Street materials to URP-compatible shaders.");
    }

    private static void ApplyCommonProperties(Material material, MaterialSnapshot snapshot)
    {
        SetTexture(material, "_BaseMap", snapshot.MainTexture);
        SetTexture(material, "_MainTex", snapshot.MainTexture);
        SetTextureScaleOffset(material, "_BaseMap", snapshot.MainTextureScale, snapshot.MainTextureOffset);
        SetTextureScaleOffset(material, "_MainTex", snapshot.MainTextureScale, snapshot.MainTextureOffset);

        SetColor(material, "_BaseColor", snapshot.BaseColor);
        SetColor(material, "_Color", snapshot.BaseColor);
        SetFloat(material, "_Cutoff", snapshot.Cutoff);

        SetTexture(material, "_BumpMap", snapshot.BumpMap);
        SetFloat(material, "_BumpScale", snapshot.BumpScale);
        SetKeyword(material, "_NORMALMAP", snapshot.BumpMap != null);

        SetTexture(material, "_MetallicGlossMap", snapshot.MetallicGlossMap);
        SetFloat(material, "_Metallic", snapshot.Metallic);
        SetFloat(material, "_Smoothness", snapshot.Smoothness);
        SetKeyword(material, "_METALLICSPECGLOSSMAP", snapshot.MetallicGlossMap != null);

        SetTexture(material, "_EmissionMap", snapshot.EmissionMap);
        SetColor(material, "_EmissionColor", snapshot.EmissionColor);
        var hasEmission = snapshot.EmissionMap != null || snapshot.EmissionColor.maxColorComponent > 0.01f;
        SetKeyword(material, "_EMISSION", hasEmission);
        material.globalIlluminationFlags = hasEmission
            ? MaterialGlobalIlluminationFlags.RealtimeEmissive
            : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
    }

    private static void ConfigureOpaque(Material material)
    {
        material.SetOverrideTag("RenderType", "Opaque");
        SetFloat(material, "_Surface", 0);
        SetFloat(material, "_Blend", 0);
        SetFloat(material, "_AlphaClip", 0);
        SetFloat(material, "_SrcBlend", (float)BlendMode.One);
        SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
        SetFloat(material, "_ZWrite", 1);
        SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", false);
        SetKeyword(material, "_ALPHATEST_ON", false);
        material.renderQueue = -1;
    }

    private static void ConfigureCutout(Material material, float cutoff)
    {
        material.SetOverrideTag("RenderType", "TransparentCutout");
        SetFloat(material, "_Surface", 0);
        SetFloat(material, "_Blend", 0);
        SetFloat(material, "_AlphaClip", 1);
        SetFloat(material, "_Cutoff", Mathf.Clamp01(cutoff <= 0 ? 0.33f : cutoff));
        SetFloat(material, "_SrcBlend", (float)BlendMode.One);
        SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
        SetFloat(material, "_ZWrite", 1);
        SetFloat(material, "_Cull", (float)CullMode.Off);
        SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", false);
        SetKeyword(material, "_ALPHATEST_ON", true);
        material.renderQueue = (int)RenderQueue.AlphaTest;
    }

    private static void ConfigureTransparent(Material material, bool additive)
    {
        material.SetOverrideTag("RenderType", "Transparent");
        SetFloat(material, "_Surface", 1);
        SetFloat(material, "_Blend", additive ? 1 : 0);
        SetFloat(material, "_AlphaClip", 0);
        SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloat(material, "_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        SetFloat(material, "_ZWrite", 0);
        SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", true);
        SetKeyword(material, "_ALPHATEST_ON", false);
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void CreateProjectScene()
    {
        if (!File.Exists(SourceScenePath))
        {
            Debug.LogWarning($"Japanese Street showcase scene was not found: {SourceScenePath}");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        if (!File.Exists(ProjectScenePath))
        {
            AssetDatabase.CopyAsset(SourceScenePath, ProjectScenePath);
            AssetDatabase.ImportAsset(ProjectScenePath);
        }

        var scene = EditorSceneManager.OpenScene(ProjectScenePath, OpenSceneMode.Additive);
        RemoveLegacyCamera(scene);
        AddXrOrigin(scene);
        AddBasicStreetColliders(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, removeScene: true);
    }

    private static void RemoveLegacyCamera(Scene scene)
    {
        var xrRig = FindInScene(scene, "XR Origin") ?? FindInScene(scene, "XR Rig");

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "EventSystem")
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        foreach (var camera in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<Camera>(includeInactive: true))
                     .ToArray())
        {
            if (xrRig != null && camera.transform.IsChildOf(xrRig.transform))
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }

    private static void AddXrOrigin(Scene scene)
    {
        if (FindInScene(scene, "XR Origin") != null || FindInScene(scene, "XR Rig") != null)
        {
            return;
        }

        var prefabPath = File.Exists("Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab")
            ? "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab"
            : "Assets/Samples/XR Interaction Toolkit/3.1.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("No XR Origin prefab was found. The street scene was created without a VR player rig.");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "XR Origin (VR Player)";
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    private static void AddBasicStreetColliders(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var meshFilter in root.GetComponentsInChildren<MeshFilter>(includeInactive: true))
            {
                var objectName = meshFilter.gameObject.name;
                if (!LooksLikeWalkableStreet(objectName) || meshFilter.sharedMesh == null || meshFilter.GetComponent<Collider>() != null)
                {
                    continue;
                }

                meshFilter.gameObject.AddComponent<MeshCollider>();
                EditorUtility.SetDirty(meshFilter.gameObject);
            }
        }
    }

    private static void OptimizeSceneForVr(Scene scene)
    {
        foreach (var reflectionProbe in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<ReflectionProbe>(includeInactive: true)))
        {
            reflectionProbe.enabled = false;
            EditorUtility.SetDirty(reflectionProbe);
        }

        foreach (var lightProbeGroup in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<LightProbeGroup>(includeInactive: true)))
        {
            lightProbeGroup.enabled = false;
            EditorUtility.SetDirty(lightProbeGroup);
        }

        foreach (var light in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<Light>(includeInactive: true)))
        {
            if (light.type != LightType.Directional)
            {
                continue;
            }

            light.shadows = LightShadows.None;
            light.intensity = Mathf.Min(light.intensity, 0.85f);
            EditorUtility.SetDirty(light);
        }
    }

    private static bool LooksLikeWalkableStreet(string objectName)
    {
        var name = objectName.ToLowerInvariant();
        return name.Contains("road") ||
               name.Contains("street") ||
               name.Contains("sidewalk") ||
               name.Contains("asphalt") ||
               name.Contains("crosswalk") ||
               name.Contains("floor");
    }

    private static GameObject FindInScene(Scene scene, string namePart)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(includeInactive: true))
            .FirstOrDefault(transform => transform.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0)
            ?.gameObject;
    }

    private static void AddSceneToBuildSettings()
    {
        if (!File.Exists(ProjectScenePath))
        {
            return;
        }

        var scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(scene => scene.path == ProjectScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ProjectScenePath, enabled: true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void WriteMarker()
    {
        File.WriteAllText(
            MarkerPath,
            $"Japanese Street URP setup completed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}.\n" +
            $"Generated scene: {ProjectScenePath}\n");
        AssetDatabase.ImportAsset(MarkerPath);
    }

    private static bool SceneNeedsSanitizing()
    {
        var sceneText = File.ReadAllText(ProjectScenePath);
        return sceneText.Contains("m_Name: Main Camera");
    }

    private static bool IsParticleMaterial(string path, string materialName, string shaderName)
    {
        var text = $"{path}/{materialName}/{shaderName}".ToLowerInvariant();
        return text.Contains("/vfx/") ||
               text.Contains("particle") ||
               text.Contains("smoke") ||
               text.Contains("steam") ||
               text.Contains("dust") ||
               text.Contains("spark");
    }

    private static bool IsFoliageMaterial(string path, string materialName)
    {
        var text = $"{path}/{materialName}".ToLowerInvariant();
        return text.Contains("leaf") ||
               text.Contains("leaves") ||
               text.Contains("grass") ||
               text.Contains("flower") ||
               text.Contains("plant") ||
               text.Contains("tree") ||
               text.Contains("bush");
    }

    private static void SetTexture(Material material, string property, Texture texture)
    {
        if (texture != null && material.HasProperty(property))
        {
            material.SetTexture(property, texture);
        }
    }

    private static void SetTextureScaleOffset(Material material, string property, Vector2 scale, Vector2 offset)
    {
        if (!material.HasProperty(property))
        {
            return;
        }

        material.SetTextureScale(property, scale);
        material.SetTextureOffset(property, offset);
    }

    private static void SetColor(Material material, string property, Color color)
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, color);
        }
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled)
        {
            material.EnableKeyword(keyword);
        }
        else
        {
            material.DisableKeyword(keyword);
        }
    }

    private readonly struct MaterialSnapshot
    {
        public readonly string ShaderName;
        public readonly Texture MainTexture;
        public readonly Vector2 MainTextureScale;
        public readonly Vector2 MainTextureOffset;
        public readonly Color BaseColor;
        public readonly Texture BumpMap;
        public readonly float BumpScale;
        public readonly Texture MetallicGlossMap;
        public readonly Texture EmissionMap;
        public readonly Color EmissionColor;
        public readonly float Metallic;
        public readonly float Smoothness;
        public readonly float Cutoff;
        public readonly float Mode;
        public readonly float DstBlend;

        private MaterialSnapshot(
            string shaderName,
            Texture mainTexture,
            Vector2 mainTextureScale,
            Vector2 mainTextureOffset,
            Color baseColor,
            Texture bumpMap,
            float bumpScale,
            Texture metallicGlossMap,
            Texture emissionMap,
            Color emissionColor,
            float metallic,
            float smoothness,
            float cutoff,
            float mode,
            float dstBlend)
        {
            ShaderName = shaderName;
            MainTexture = mainTexture;
            MainTextureScale = mainTextureScale;
            MainTextureOffset = mainTextureOffset;
            BaseColor = baseColor;
            BumpMap = bumpMap;
            BumpScale = bumpScale;
            MetallicGlossMap = metallicGlossMap;
            EmissionMap = emissionMap;
            EmissionColor = emissionColor;
            Metallic = metallic;
            Smoothness = smoothness;
            Cutoff = cutoff;
            Mode = mode;
            DstBlend = dstBlend;
        }

        public static MaterialSnapshot Capture(Material material)
        {
            return new MaterialSnapshot(
                material.shader != null ? material.shader.name : string.Empty,
                GetTexture(material, "_MainTex"),
                GetTextureScale(material, "_MainTex"),
                GetTextureOffset(material, "_MainTex"),
                GetColor(material, "_Color", Color.white),
                GetTexture(material, "_BumpMap"),
                GetFloat(material, "_BumpScale", 1),
                GetTexture(material, "_MetallicGlossMap"),
                GetTexture(material, "_EmissionMap"),
                GetColor(material, "_EmissionColor", Color.black),
                GetFloat(material, "_Metallic", 0),
                GetFloat(material, "_Glossiness", GetFloat(material, "_Smoothness", 0.5f)),
                GetFloat(material, "_Cutoff", 0.5f),
                GetFloat(material, "_Mode", 0),
                GetFloat(material, "_DstBlend", 0));
        }

        /// <summary>
        /// Creates a snapshot using a fallback texture for materials that store
        /// their main texture in a non-standard property like _Albedo.
        /// </summary>
        public static MaterialSnapshot CaptureWithFallback(Material material, Texture fallbackMainTexture)
        {
            return new MaterialSnapshot(
                material.shader != null ? material.shader.name : string.Empty,
                fallbackMainTexture,
                GetTextureScale(material, "_MainTex"),
                GetTextureOffset(material, "_MainTex"),
                GetColor(material, "_Color", Color.white),
                GetTexture(material, "_BumpMap") ?? GetTexture(material, "_Normal"),
                GetFloat(material, "_BumpScale", 1),
                GetTexture(material, "_MetallicGlossMap"),
                GetTexture(material, "_EmissionMap"),
                GetColor(material, "_EmissionColor", Color.black),
                GetFloat(material, "_Metallic", 0),
                GetFloat(material, "_Glossiness", GetFloat(material, "_Smoothness", 0.5f)),
                GetFloat(material, "_Cutoff", 0.5f),
                GetFloat(material, "_Mode", 0),
                GetFloat(material, "_DstBlend", 0));
        }

        private static Texture GetTexture(Material material, string property)
        {
            return material.HasProperty(property) ? material.GetTexture(property) : null;
        }

        private static Vector2 GetTextureScale(Material material, string property)
        {
            return material.HasProperty(property) ? material.GetTextureScale(property) : Vector2.one;
        }

        private static Vector2 GetTextureOffset(Material material, string property)
        {
            return material.HasProperty(property) ? material.GetTextureOffset(property) : Vector2.zero;
        }

        private static Color GetColor(Material material, string property, Color fallback)
        {
            return material.HasProperty(property) ? material.GetColor(property) : fallback;
        }

        private static float GetFloat(Material material, string property, float fallback)
        {
            return material.HasProperty(property) ? material.GetFloat(property) : fallback;
        }
    }
}
