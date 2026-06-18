using System;
using UnityEditor;
using UnityEngine;

public sealed class BlackTranslationDofImporter : AssetPostprocessor
{
    private const string BlackModelPath = "Assets/Characters/Black/Models/Black.fbx";

    private void OnPreprocessModel()
    {
        if (!IsBlackModel(assetPath))
        {
            return;
        }

        ConfigureImporter((ModelImporter)assetImporter);
    }

    [MenuItem("Kamen Rider/Fix Black Translation DoF and Reimport")]
    public static void FixBlackTranslationDofAndReimport()
    {
        var importer = AssetImporter.GetAtPath(BlackModelPath) as ModelImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"Could not find ModelImporter at {BlackModelPath}.");
        }

        ConfigureImporter(importer);
        importer.SaveAndReimport();
        AssetDatabase.Refresh();

        var refreshedImporter = AssetImporter.GetAtPath(BlackModelPath) as ModelImporter;
        var humanDescription = refreshedImporter != null ? refreshedImporter.humanDescription : default;
        Debug.Log(
            $"[BlackTranslationDoF] Reimported {BlackModelPath}. " +
            $"hasTranslationDoF={humanDescription.hasTranslationDoF}");
    }

    private static void ConfigureImporter(ModelImporter importer)
    {
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

        var humanDescription = importer.humanDescription;
        humanDescription.hasTranslationDoF = true;
        importer.humanDescription = humanDescription;
    }

    private static bool IsBlackModel(string path)
    {
        return string.Equals(
            path.Replace('\\', '/'),
            BlackModelPath,
            StringComparison.OrdinalIgnoreCase);
    }
}
