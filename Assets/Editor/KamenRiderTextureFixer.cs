using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-time fix for the two TGA textures that fail to load.
/// Clears cached import data and forces reimport.
/// </summary>
public static class KamenRiderTextureFixer
{
    private const string FlowerPath = "Assets/Japanese_Street/Textures/AE_Props/AE_Flower/AE_Flower_A.tga";
    private const string GrassPath = "Assets/Japanese_Street/Textures/AE_Grass_01_A.tga";

    [MenuItem("Kamen Rider/Fix Plant Textures (Reimport)")]
    public static void Run()
    {
        FixTextureImport(FlowerPath);
        FixTextureImport(GrassPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[TextureFixer] Plant textures reimported.");
    }

    private static void FixTextureImport(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"[TextureFixer] File not found: {path}");
            return;
        }

        // Force Unity to reimport
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        Debug.Log($"[TextureFixer] Reimported: {Path.GetFileName(path)}");

        // Verify it loaded
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null)
        {
            Debug.Log($"[TextureFixer] Texture loaded successfully: {tex.name} ({tex.width}x{tex.height})");
        }
        else
        {
            Debug.LogError($"[TextureFixer] Failed to load texture: {path}");
        }
    }
}