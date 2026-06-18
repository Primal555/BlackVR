using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates a URP-compatible Skybox/Panoramic material and fixes the skybox
/// references in JapaneseStreetVR scenes.
///
/// This uses Unity Editor API (not raw YAML for the material) to properly
/// create a Skybox material that works in URP single-pass instanced stereo.
/// </summary>
public static class KamenRiderSkyboxFixer
{
    private const string ScenePathVr = "Assets/Scenes/JapaneseStreetVR.unity";
    private const string ScenePathVrLite = "Assets/Scenes/JapaneseStreetVR_Lite.unity";
    private const string SkyMaterialPath = "Assets/Japanese_Street/Textures/Sky.mat";
    private const string HdrTexturePath = "Assets/Japanese_Street/Textures/kloofendal_48d_partly_cloudy_4k.hdr";

    [InitializeOnLoadMethod]
    private static void AutoFixOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (SceneNeedsFix(ScenePathVr) || SceneNeedsFix(ScenePathVrLite))
            {
                Run();
            }
        };
    }

    [MenuItem("Kamen Rider/Fix Skybox For VR")]
    public static void Run()
    {
        var skyMaterial = CreateSkyMaterial();
        if (skyMaterial == null)
        {
            Debug.LogError("[SkyboxFixer] Failed to create skybox material.");
            return;
        }

        FixSceneSkyboxRef(ScenePathVr, skyMaterial);
        FixSceneSkyboxRef(ScenePathVrLite, skyMaterial);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SkyboxFixer] Skybox fix completed. Both VR scenes now use URP Panoramic skybox.");
    }

    /// <summary>
    /// Creates/recreates Sky.mat using the Skybox/Panoramic shader with the
    /// kloofendal HDR texture. Uses Unity Editor API so the material is
    /// serialized correctly.
    /// </summary>
    private static Material CreateSkyMaterial()
    {
        // Delete any broken Sky.mat first
        if (File.Exists(SkyMaterialPath))
        {
            AssetDatabase.DeleteAsset(SkyMaterialPath);
            AssetDatabase.Refresh();
        }

        var shader = Shader.Find("Skybox/Panoramic");
        if (shader == null)
        {
            Debug.LogError("[SkyboxFixer] Skybox/Panoramic shader not found!");
            return null;
        }

        var hdrTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(HdrTexturePath);
        if (hdrTexture == null)
        {
            Debug.LogWarning($"[SkyboxFixer] HDR texture not found at {HdrTexturePath}. Will use solid color fallback.");
        }

        // Ensure directory exists
        var dir = Path.GetDirectoryName(SkyMaterialPath);
        if (!AssetDatabase.IsValidFolder(dir))
        {
            var parent = Path.GetDirectoryName(dir);
            var folder = Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                Debug.LogError("[SkyboxFixer] Textures folder not found.");
                return null;
            }
            AssetDatabase.CreateFolder(parent, folder);
        }

        // Create material via Editor API
        var material = new Material(shader);
        material.name = "Sky";

        if (hdrTexture != null)
        {
            material.SetTexture("_MainTex", hdrTexture);
            material.SetFloat("_Exposure", 0.66f);
            material.SetFloat("_Rotation", 0f);
            material.SetFloat("_Mapping", 0f); // 0 = Latitude Longitude Layout
            material.SetInt("_ImageType", 0);   // 0 = 2D
        }

        material.SetColor("_Tint", Color.white);
        material.SetColor("_Color", Color.white);

        // Save as asset so it gets a proper .meta file and GUID
        AssetDatabase.CreateAsset(material, SkyMaterialPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Verify it was created
        var created = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (created != null && created.shader == shader)
        {
            Debug.Log($"[SkyboxFixer] Created Sky.mat with Skybox/Panoramic shader. GUID: {AssetDatabase.AssetPathToGUID(SkyMaterialPath)}");
        }
        else
        {
            Debug.LogError("[SkyboxFixer] Failed to verify created Sky.mat!");
        }

        return created;
    }

    /// <summary>
    /// Fixes the skybox material reference in the scene file via text
    /// replacement. We avoid EditorSceneManager because it corrupts
    /// XR Origin/locomotion components during save.
    /// </summary>
    private static void FixSceneSkyboxRef(string scenePath, Material newSkyMaterial)
    {
        if (!File.Exists(scenePath)) return;

        var text = File.ReadAllText(scenePath);
        var newGuid = AssetDatabase.AssetPathToGUID(SkyMaterialPath);

        // The old Sky.mat also has the same GUID since we recreated it.
        // Check if scene already references the new correct GUID
        if (text.Contains($"guid: {newGuid}"))
        {
            Debug.Log($"[SkyboxFixer] Scene {Path.GetFileName(scenePath)} already uses correct skybox.");
            return;
        }

        // If scene references a dead skybox, replace the line
        // Pattern: m_SkyboxMaterial: {fileID: 2100000, guid: ANYTHING, type: 2}
        var oldLine = System.Text.RegularExpressions.Regex.Match(text,
            @"m_SkyboxMaterial:\s*\{fileID:\s*2100000,\s*guid:\s*[a-f0-9]+,\s*type:\s*\d+\s*\}");
        if (!oldLine.Success)
        {
            Debug.LogWarning($"[SkyboxFixer] Could not find m_SkyboxMaterial line in {Path.GetFileName(scenePath)}");
            return;
        }

        var newLine = $"m_SkyboxMaterial: {{fileID: 2100000, guid: {newGuid}, type: 2}}";
        var newText = text.Replace(oldLine.Value, newLine);

        if (newText == text)
        {
            Debug.LogWarning($"[SkyboxFixer] No change made to {Path.GetFileName(scenePath)}");
            return;
        }

        // Backup and write
        var backup = scenePath + ".skyfix_backup";
        File.Copy(scenePath, backup, overwrite: true);
        File.WriteAllText(scenePath, newText);
        AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);

        // Verify
        var verify = File.ReadAllText(scenePath);
        if (verify.Contains($"guid: {newGuid}"))
        {
            Debug.Log($"[SkyboxFixer] Fixed skybox in {Path.GetFileName(scenePath)}");
            File.Delete(backup);
        }
        else
        {
            Debug.LogError($"[SkyboxFixer] Fix verification failed for {Path.GetFileName(scenePath)}. Restoring.");
            File.Copy(backup, scenePath, overwrite: true);
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);
            File.Delete(backup);
        }
    }

    private static bool SceneNeedsFix(string scenePath)
    {
        if (!File.Exists(scenePath)) return false;
        var text = File.ReadAllText(scenePath);
        // Scene is broken if it references something other than our Sky.mat GUID
        var correctGuid = AssetDatabase.AssetPathToGUID(SkyMaterialPath);
        if (string.IsNullOrEmpty(correctGuid)) return false;
        return !text.Contains($"guid: {correctGuid}");
    }
}