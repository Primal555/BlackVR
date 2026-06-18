using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class KamenRiderModelImportProcessor : AssetPostprocessor
{
    private const string BlackModelPath = "Assets/Characters/Black/Models/Black.fbx";
    private const string ShadowMoonModelPath = "Assets/Characters/ShadowMoon/Models/ShadowMoon.fbx";
    private const string MinamiKotaroModelPath = "Assets/Characters/MinamiKotaro/Models/MinamiKotaro.glb";
    private static readonly string[] CharacterPaths = { BlackModelPath, ShadowMoonModelPath };
    private static readonly HashSet<string> WeaponPaths = new HashSet<string>
    {
        "Assets/Props/Weapons/ShadowMoon/Models/SatanSaber.fbx",
        "Assets/Props/Weapons/ShadowMoon/Models/ShadowSabers.fbx",
    };
    private static readonly HashSet<string> ModelsWithoutUsableTangents = new HashSet<string>
    {
        "Assets/Characters/MinamiKotaro/Models/kotaro.fbx",
        "Assets/Characters/MinamiKotaro/Models/kotarotest.fbx",
        "Assets/Japanese_Street/Models/House/AE_House_01.fbx",
        "Assets/Japanese_Street/Models/House/AE_House_02.fbx",
        "Assets/Japanese_Street/Models/House/AE_House_04.fbx",
        "Assets/Japanese_Street/Models/Modules/House_05/AE_House_05_Door_05.fbx",
        "Assets/Japanese_Street/Models/Props/AE_Electricity_Meter_01.fbx",
        "Assets/Japanese_Street/Models/Props/AE_Flower_Pot_06.fbx",
        "Assets/Japanese_Street/Models/Street/AE_Canopy_01.fbx",
        "Assets/Japanese_Street/Models/Street/AE_Road_Sign_Pole_10.fbx",
        "Assets/Japanese_Street/Models/Street/AE_Traffic_Light_01.fbx",
    };

    public static void ImportAndValidate()
    {
        foreach (var modelPath in CharacterPaths.Concat(WeaponPaths))
        {
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        var invalidCharacterPaths = CharacterPaths.Where(path =>
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            var valid = avatar != null && avatar.isHuman && avatar.isValid;
            Debug.Log($"Humanoid Avatar validation for {path}: {(valid ? "PASS" : "FAIL")}.");
            return !valid;
        }).ToArray();

        if (invalidCharacterPaths.Length > 0)
        {
            throw new System.InvalidOperationException($"Humanoid Avatar validation failed: {string.Join(", ", invalidCharacterPaths)}");
        }
    }

    private void OnPreprocessModel()
    {
        var importer = (ModelImporter)assetImporter;

        if (ModelsWithoutUsableTangents.Contains(assetPath))
        {
            ConfigureMissingNormalModel(importer);
        }

        if (assetPath == BlackModelPath)
        {
            ConfigureHumanoid(importer, false);
            return;
        }

        if (assetPath == ShadowMoonModelPath)
        {
            ConfigureHumanoid(importer, true);
            return;
        }

        if (WeaponPaths.Contains(assetPath))
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation = false;
        }
    }

    private static void ConfigureMissingNormalModel(ModelImporter importer)
    {
        importer.importNormals = ModelImporterNormals.Calculate;
        importer.importTangents = (ModelImporterTangents)4;
    }

    private void OnPostprocessModel(GameObject importedModel)
    {
        if (assetPath != BlackModelPath && assetPath != ShadowMoonModelPath)
        {
            return;
        }

        var avatar = importedModel.GetComponent<Animator>()?.avatar;
        if (avatar == null || !avatar.isHuman || !avatar.isValid)
        {
            Debug.LogError($"Humanoid Avatar validation failed for {assetPath}.");
            return;
        }

        Debug.Log($"Humanoid Avatar validation succeeded for {assetPath}.");
    }

    private static void ConfigureHumanoid(ModelImporter importer, bool isShadowMoon)
    {
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.importBlendShapes = true;

        var description = importer.humanDescription;
        description.human = BuildHumanBones(isShadowMoon).ToArray();
        description.upperArmTwist = 0.5f;
        description.lowerArmTwist = 0.5f;
        description.upperLegTwist = 0.5f;
        description.lowerLegTwist = 0.5f;
        description.armStretch = 0.05f;
        description.legStretch = 0.05f;
        description.feetSpacing = 0f;
        description.hasTranslationDoF = !isShadowMoon;
        importer.humanDescription = description;
    }

    private static IEnumerable<HumanBone> BuildHumanBones(bool isShadowMoon)
    {
        var toeName = isShadowMoon ? "つま先EX" : "つま先ＥＸ";

        yield return Map("Hips", "センター");
        yield return Map("Spine", "上半身");
        yield return Map("Chest", "上半身2");
        yield return Map("Neck", "首");
        yield return Map("Head", "頭");
        yield return Map("LeftShoulder", "肩.L");
        yield return Map("LeftUpperArm", "腕.L");
        yield return Map("LeftLowerArm", "ひじ.L");
        yield return Map("LeftHand", "手首.L");
        yield return Map("RightShoulder", "肩.R");
        yield return Map("RightUpperArm", "腕.R");
        yield return Map("RightLowerArm", "ひじ.R");
        yield return Map("RightHand", "手首.R");
        yield return Map("LeftUpperLeg", "足.L");
        yield return Map("LeftLowerLeg", "ひざ.L");
        yield return Map("LeftFoot", "足首.L");
        yield return Map("LeftToes", $"{toeName}.L");
        yield return Map("RightUpperLeg", "足.R");
        yield return Map("RightLowerLeg", "ひざ.R");
        yield return Map("RightFoot", "足首.R");
        yield return Map("RightToes", $"{toeName}.R");
        yield return Map("Left Thumb Proximal", "親指０.L");
        yield return Map("Left Thumb Intermediate", "親指１.L");
        yield return Map("Left Thumb Distal", "親指２.L");
        yield return Map("Left Index Proximal", "人指１.L");
        yield return Map("Left Index Intermediate", "人指２.L");
        yield return Map("Left Index Distal", "人指３.L");
        yield return Map("Left Middle Proximal", "中指１.L");
        yield return Map("Left Middle Intermediate", "中指２.L");
        yield return Map("Left Middle Distal", "中指３.L");
        yield return Map("Left Ring Proximal", "薬指１.L");
        yield return Map("Left Ring Intermediate", "薬指２.L");
        yield return Map("Left Ring Distal", "薬指３.L");
        yield return Map("Left Little Proximal", "小指１.L");
        yield return Map("Left Little Intermediate", "小指２.L");
        yield return Map("Left Little Distal", "小指３.L");
        yield return Map("Right Thumb Proximal", "親指０.R");
        yield return Map("Right Thumb Intermediate", "親指１.R");
        yield return Map("Right Thumb Distal", "親指２.R");
        yield return Map("Right Index Proximal", "人指１.R");
        yield return Map("Right Index Intermediate", "人指２.R");
        yield return Map("Right Index Distal", "人指３.R");
        yield return Map("Right Middle Proximal", "中指１.R");
        yield return Map("Right Middle Intermediate", "中指２.R");
        yield return Map("Right Middle Distal", "中指３.R");
        yield return Map("Right Ring Proximal", "薬指１.R");
        yield return Map("Right Ring Intermediate", "薬指２.R");
        yield return Map("Right Ring Distal", "薬指３.R");
        yield return Map("Right Little Proximal", "小指１.R");
        yield return Map("Right Little Intermediate", "小指２.R");
        yield return Map("Right Little Distal", "小指３.R");
    }

    private static HumanBone Map(string humanName, string boneName)
    {
        return new HumanBone
        {
            humanName = humanName,
            boneName = boneName,
            limit = new HumanLimit { useDefaultValues = true },
        };
    }
}
