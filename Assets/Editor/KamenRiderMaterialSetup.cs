using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class KamenRiderMaterialSetup
{
    private const string BlackModelPath = "Assets/Characters/Black/Models/Black.fbx";
    private const string BlackMaterialFolder = "Assets/Characters/Black/Materials";
    private const string BlackTextureFolder = "Assets/Characters/Black/Textures";
    private const string ShadowMoonModelPath = "Assets/Characters/ShadowMoon/Models/ShadowMoon.fbx";
    private const string ShadowMoonMaterialFolder = "Assets/Characters/ShadowMoon/Materials";
    private const string ShadowMoonTextureFolder = "Assets/Characters/ShadowMoon/Textures";
    private const string SatanSaberModelPath = "Assets/Props/Weapons/ShadowMoon/Models/SatanSaber.fbx";
    private const string ShadowSabersModelPath = "Assets/Props/Weapons/ShadowMoon/Models/ShadowSabers.fbx";
    private const string WeaponMaterialFolder = "Assets/Props/Weapons/ShadowMoon/Materials";
    private const string WeaponTextureFolder = "Assets/Props/Weapons/ShadowMoon/Textures";

    [InitializeOnLoadMethod]
    private static void ScheduleInitialSetup()
    {
        if (!AssetDatabase.IsValidFolder(BlackMaterialFolder))
        {
            EditorApplication.delayCall += SetupMaterialsAndValidate;
        }
    }

    [MenuItem("Kamen Rider/Setup Imported Materials")]
    public static void SetupMaterialsAndValidate()
    {
        ConfigureMaterials(BlackModelPath, BlackMaterialFolder, BlackTextureFolder, BlackMaterials());
        ConfigureMaterials(ShadowMoonModelPath, ShadowMoonMaterialFolder, ShadowMoonTextureFolder, ShadowMoonMaterials());
        ConfigureMaterials(SatanSaberModelPath, WeaponMaterialFolder, WeaponTextureFolder, SatanSaberMaterials());
        ConfigureMaterials(ShadowSabersModelPath, WeaponMaterialFolder, WeaponTextureFolder, ShadowSabersMaterials());
        AssetDatabase.SaveAssets();
        KamenRiderModelImportProcessor.ImportAndValidate();
    }

    private static void ConfigureMaterials(string modelPath, string materialFolder, string textureFolder, IEnumerable<MaterialSpec> materialSpecs)
    {
        EnsureFolder(materialFolder);
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null)
        {
            throw new System.InvalidOperationException($"Model importer not found: {modelPath}");
        }

        foreach (var materialSpec in materialSpecs)
        {
            var material = CreateOrUpdateMaterial(materialFolder, textureFolder, materialSpec);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), materialSpec.SourceName), material);
        }

        importer.SaveAndReimport();
    }

    private static Material CreateOrUpdateMaterial(string materialFolder, string textureFolder, MaterialSpec materialSpec)
    {
        var materialPath = $"{materialFolder}/{materialSpec.AssetName}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            throw new System.InvalidOperationException("Universal Render Pipeline/Lit shader was not found.");
        }

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.name = materialSpec.AssetName;
        material.enableInstancing = true;
        material.SetColor("_BaseColor", materialSpec.Color);
        material.SetColor("_Color", materialSpec.Color);
        material.SetFloat("_Metallic", materialSpec.Metallic);
        material.SetFloat("_Smoothness", materialSpec.Smoothness);

        var texture = string.IsNullOrEmpty(materialSpec.TextureName)
            ? null
            : AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureFolder}/{materialSpec.TextureName}");
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);

        if (materialSpec.Emissive)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", materialSpec.Color * 1.5f);
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }

        ConfigureSurface(material, materialSpec.Color.a < 0.99f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureSurface(Material material, bool transparent)
    {
        if (transparent)
        {
            material.SetFloat("_Surface", 1f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return;
        }

        material.SetFloat("_Surface", 0f);
        material.SetOverrideTag("RenderType", "Opaque");
        material.SetInt("_SrcBlend", (int)BlendMode.One);
        material.SetInt("_DstBlend", (int)BlendMode.Zero);
        material.SetInt("_ZWrite", 1);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = -1;
    }

    private static void EnsureFolder(string folder)
    {
        var segments = folder.Split('/');
        var current = segments[0];
        for (var index = 1; index < segments.Length; index++)
        {
            var next = $"{current}/{segments[index]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[index]);
            }

            current = next;
        }
    }

    private static IEnumerable<MaterialSpec> BlackMaterials()
    {
        yield return Spec("flash1", "FlashRed", null, 1f, 0.23f, 0.25f, emissive: true);
        yield return Spec("flash2", "FlashGreen", null, 0.01f, 0.8f, 0f, emissive: true);
        yield return Spec("flash3", "FlashBlue", null, 0f, 0.56f, 0.8f, emissive: true);
        yield return Spec("キングストーン", "KingStone", "ファン.png", 0.8f, 0.8f, 0.8f, emissive: true);
        yield return Spec("スーツ　マーク", "SuitMark", "胸マーク.jpg", 0.27f, 0.27f, 0.27f);
        yield return Spec("スーツ　灰", "SuitGray", null, 1f, 0.94f, 0.88f, metallic: 0.65f, smoothness: 0.7f);
        yield return Spec("スーツ　黒", "SuitBlack", "マーク.jpg", 0.27f, 0.27f, 0.27f);
        yield return Spec("ベルト　本体", "BeltBody", "本体.png", 0.8f, 0.8f, 0.8f, metallic: 0.4f, smoothness: 0.7f);
        yield return Spec("ベルト　灰", "BeltGray", null, 0.49f, 0.49f, 0.49f, metallic: 0.65f, smoothness: 0.7f);
        yield return Spec("ベルト　白", "BeltWhite", null, 0.8f, 0.8f, 0.8f);
        yield return Spec("ベルト　緑", "BeltGreen", null, 0f, 1f, 0f, emissive: true);
        yield return Spec("ベルト　赤", "BeltRed", null, 0.8f, 0f, 0f, emissive: true);
        yield return Spec("ベルト　青", "BeltBlue", null, 0.07f, 0.18f, 0.8f, emissive: true);
        yield return Spec("ベルト　黄色", "BeltYellow", null, 1f, 1f, 0f, emissive: true);
        yield return Spec("ベルト　黒", "BeltBlack", "metalg.png", 0.03f, 0.03f, 0.03f, metallic: 0.65f, smoothness: 0.75f);
        yield return Spec("中身", "Inner", "中身.jpg", 0.8f, 0.8f, 0.8f);
        yield return Spec("仮面　ランプ", "MaskLamp", null, 1f, 0f, 0f, emissive: true);
        yield return Spec("仮面　灰", "MaskGray", null, 0.77f, 0.77f, 0.77f, metallic: 0.65f, smoothness: 0.7f);
        yield return Spec("仮面　目", "MaskEyes", "eye.jpg", 1f, 1f, 1f, emissive: true, metallic: 0.25f, smoothness: 0.7f);
        yield return Spec("仮面　赤", "MaskRed", null, 1f, 0f, 0f, emissive: true);
        yield return Spec("仮面　透明", "MaskTransparent", null, 0.8f, 0f, 0f, 0.8f);
        yield return Spec("仮面　黒", "MaskBlack", "マーク.jpg", 0.27f, 0.27f, 0.27f);
        yield return Spec("右手", "RightHand", null, 0.66f, 0.66f, 0.66f);
        yield return Spec("右足底", "RightSole", null, 0.75f, 0.75f, 0.75f);
        yield return Spec("左手", "LeftHand", null, 0.66f, 0.66f, 0.66f);
        yield return Spec("左足底", "LeftSole", null, 0.75f, 0.75f, 0.75f);
        yield return Spec("腕　", "Arm", "マーク.jpg", 0.49f, 0.49f, 0.49f);
        yield return Spec("靴", "Boots", "マーク.jpg", 0.27f, 0.27f, 0.27f);
    }

    private static IEnumerable<MaterialSpec> ShadowMoonMaterials()
    {
        yield return Spec("flash1", "FlashRed", null, 1f, 0.23f, 0.25f, emissive: true);
        yield return Spec("flash2", "FlashGreen", null, 0.01f, 0.8f, 0f, emissive: true);
        yield return Spec("flash3", "FlashBlue", null, 0f, 0.56f, 0.8f, emissive: true);
        yield return Spec("キングストーン", "KingStone", "ファン２.png", 0.8f, 0.8f, 0.8f, emissive: true);
        yield return Spec("スーツ　模様", "SuitPattern", "しましま模様.png", 1f, 1f, 1f, metallic: 0.5f, smoothness: 0.65f);
        yield return Spec("スーツ　銀", "SuitSilver", null, 1f, 1f, 1f, metallic: 0.8f, smoothness: 0.75f);
        yield return Spec("スーツ　黒", "SuitBlack", null, 0.05f, 0.05f, 0.05f, metallic: 0.5f, smoothness: 0.65f);
        yield return Spec("ベルト　本体", "BeltBody", "本体.png", 0.8f, 0.8f, 0.8f, metallic: 0.4f, smoothness: 0.7f);
        yield return Spec("ベルト　灰", "BeltGray", null, 0.03f, 0.03f, 0.03f, metallic: 0.65f, smoothness: 0.7f);
        yield return Spec("ベルト　白", "BeltWhite", null, 0.8f, 0.8f, 0.8f);
        yield return Spec("ベルト　緑", "BeltGreen", null, 0f, 1f, 0f, emissive: true);
        yield return Spec("ベルト　赤", "BeltRed", null, 0.8f, 0f, 0f, emissive: true);
        yield return Spec("ベルト　青", "BeltBlue", null, 0.07f, 0.18f, 0.8f, emissive: true);
        yield return Spec("ベルト　黄色", "BeltYellow", null, 1f, 1f, 0f, emissive: true);
        yield return Spec("ベルト　黒", "BeltBlack", null, 0.03f, 0.03f, 0.03f, metallic: 0.65f, smoothness: 0.75f);
        yield return Spec("中身", "Inner", null, 1f, 1f, 1f);
        yield return Spec("足底", "Sole", null, 0.75f, 0.75f, 0.75f);
        yield return Spec("仮面　灰", "MaskGray", null, 0.25f, 0.25f, 0.25f, metallic: 0.7f, smoothness: 0.75f);
        yield return Spec("仮面　目", "MaskEyes", "目　模様.png", 1f, 1f, 1f, emissive: true, metallic: 0.25f, smoothness: 0.7f);
        yield return Spec("仮面　緑", "MaskGreen", null, 0.02f, 1f, 0f, emissive: true);
        yield return Spec("仮面　赤", "MaskRed", null, 1f, 0f, 0f, emissive: true);
        yield return Spec("仮面　透明", "MaskTransparent", null, 0f, 0.8f, 0f, 0.8f);
        yield return Spec("仮面　銀", "MaskSilver", null, 1f, 1f, 1f, metallic: 0.8f, smoothness: 0.75f);
        yield return Spec("仮面　黄", "MaskYellow", null, 1f, 0.81f, 0f, emissive: true);
        yield return Spec("仮面　黒", "MaskBlack", null, 0.05f, 0.05f, 0.05f, metallic: 0.55f, smoothness: 0.65f);
    }

    private static IEnumerable<MaterialSpec> SatanSaberMaterials()
    {
        yield return Spec("刀身", "SatanBlade", null, 1f, 0f, 0f, 0.9f, emissive: true);
        yield return Spec("赤", "SatanRed", null, 0.8f, 0f, 0f, emissive: true);
        yield return Spec("青", "SatanBlue", null, 0.01f, 0f, 0.8f, emissive: true);
        yield return Spec("黄色", "SatanYellow", null, 0.8f, 0.7f, 0f, emissive: true);
    }

    private static IEnumerable<MaterialSpec> ShadowSabersMaterials()
    {
        yield return Spec("ファン", "ShadowFan", "ファン２.png", 0.8f, 0.8f, 0.8f, emissive: true);
        yield return Spec("刀身", "ShadowBlade", null, 1f, 0f, 0f, 0.9f, emissive: true);
        yield return Spec("銀", "ShadowSilver", null, 0.8f, 0.8f, 0.8f, metallic: 0.8f, smoothness: 0.75f);
    }

    private static MaterialSpec Spec(string sourceName, string assetName, string textureName, float red, float green, float blue, float alpha = 1f, bool emissive = false, float metallic = 0f, float smoothness = 0.45f)
    {
        return new MaterialSpec(sourceName, assetName, textureName, new Color(red, green, blue, alpha), emissive, metallic, smoothness);
    }

    private sealed class MaterialSpec
    {
        public MaterialSpec(string sourceName, string assetName, string textureName, Color color, bool emissive, float metallic, float smoothness)
        {
            SourceName = sourceName;
            AssetName = assetName;
            TextureName = textureName;
            Color = color;
            Emissive = emissive;
            Metallic = metallic;
            Smoothness = smoothness;
        }

        public string SourceName { get; }
        public string AssetName { get; }
        public string TextureName { get; }
        public Color Color { get; }
        public bool Emissive { get; }
        public float Metallic { get; }
        public float Smoothness { get; }
    }
}
