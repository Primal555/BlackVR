using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the MinamiKotaro model as Humanoid with auto bone detection.
/// Also provides debug logging to verify the bone mapping.
/// </summary>
public static class KamenRiderMinamiKotaroImport
{
    private const string ModelPath = "Assets/Characters/MinamiKotaro/Models/MinamiKotaro.fbx";
    private const string KotaroFbxPlayerModelPath = "Assets/Characters/MinamiKotaro/Models/kotarotest.fbx";

    [MenuItem("Kamen Rider/Import MinamiKotaro As Humanoid")]
    public static void Run()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[MinamiKotaro] Model not found at: {ModelPath}");
            return;
        }

        // Use Black's Avatar to ensure identical bone alignment
        var blackAvatar = AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/Black/Models/Black.fbx")
            .OfType<Avatar>().FirstOrDefault();

        if (blackAvatar != null && blackAvatar.isValid && blackAvatar.isHuman)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = blackAvatar;
            Debug.Log("[MinamiKotaro] Using Black's Avatar as bone reference.");
        }
        else
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            Debug.LogWarning("[MinamiKotaro] Black's Avatar not available, using auto-detection.");
        }

        importer.animationType = ModelImporterAnimationType.Human;
        importer.importAnimation = false;
        importer.importBlendShapes = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

        importer.SaveAndReimport();

        // Debug: dump the avatar's bone mapping
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model != null)
        {
            var avatar = model.GetComponent<Animator>()?.avatar;
            if (avatar != null && avatar.isValid)
            {
                Debug.Log($"[MinamiKotaro] Avatar valid. Human bones:");
                var desc = avatar.humanDescription;
                foreach (var bone in desc.human)
                {
                    Debug.Log($"  {bone.humanName} -> {bone.boneName}");
                }
            }
            else
            {
                Debug.LogWarning("[MinamiKotaro] Avatar invalid - check Rig configuration in Inspector.");
                // Try without SaveAndReimport cache
                AssetDatabase.Refresh();
                importer.SaveAndReimport();
            }
        }

        LogBoneHierarchy(model);

        Debug.Log("[MinamiKotaro] Done. If arms are deformed, check the Console for bone mapping and manually adjust in the Rig inspector.");
    }

    public static void ConfigureKotaroFbxPlayerModel()
    {
        var importer = AssetImporter.GetAtPath(KotaroFbxPlayerModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[MinamiKotaro] Model not found at: {KotaroFbxPlayerModelPath}");
            return;
        }

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.importBlendShapes = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importNormals = ModelImporterNormals.Calculate;
        importer.importTangents = (ModelImporterTangents)4;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

        importer.SaveAndReimport();
        Debug.Log($"[MinamiKotaro] Configured player FBX model: {KotaroFbxPlayerModelPath}");
    }

    /// <summary>
    /// Logs all transform children (bone hierarchy) for manual verification.
    /// </summary>
    private static void LogBoneHierarchy(GameObject model, string indent = "", int maxDepth = 3)
    {
        if (model == null) return;
        if (indent.Length > maxDepth * 2) return;

        foreach (Transform child in model.transform)
        {
            Debug.Log($"[MinamiKotaro] Bone: {indent}{child.name}");
            LogBoneHierarchy(child.gameObject, indent + "  ", maxDepth);
        }
    }
}
