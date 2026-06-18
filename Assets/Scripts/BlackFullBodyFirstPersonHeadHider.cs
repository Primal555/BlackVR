using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class BlackFullBodyFirstPersonHeadHider : MonoBehaviour
{
    [SerializeField] private string hiddenHeadLayerName = "HiddenMesh";
    [SerializeField] private string noDrawMaterialResource = "FirstPersonNoDraw";
    [SerializeField] private string[] headMaterialNameTokens =
    {
        "Mask",
        "flash",
        "\u4EEE\u9762"
    };

    private readonly List<GameObject> headClones = new List<GameObject>();
    private bool applied;

    private void OnEnable()
    {
        if (applied)
        {
            return;
        }

        Apply();
    }

    private void Apply()
    {
        var hiddenLayer = LayerMask.NameToLayer(hiddenHeadLayerName);
        if (hiddenLayer < 0)
        {
            Debug.LogWarning($"[{nameof(BlackFullBodyFirstPersonHeadHider)}] Layer '{hiddenHeadLayerName}' was not found.");
            return;
        }

        var noDrawMaterial = Resources.Load<Material>(noDrawMaterialResource);
        if (noDrawMaterial == null)
        {
            Debug.LogWarning($"[{nameof(BlackFullBodyFirstPersonHeadHider)}] Resources material '{noDrawMaterialResource}' was not found.");
            return;
        }

        ClearExistingClones();
        applied = true;

        foreach (var rendererComponent in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (IsHeadClone(rendererComponent))
            {
                continue;
            }

            SplitHeadMaterials(rendererComponent, noDrawMaterial, hiddenLayer);
        }

        HideLayerFromFirstPersonCameras(hiddenLayer);
    }

    private void SplitHeadMaterials(SkinnedMeshRenderer source, Material noDrawMaterial, int hiddenLayer)
    {
        var sourceMaterials = source.sharedMaterials;
        if (sourceMaterials == null || sourceMaterials.Length == 0)
        {
            return;
        }

        var hasHeadMaterial = false;
        var bodyMaterials = new Material[sourceMaterials.Length];
        var headMaterials = new Material[sourceMaterials.Length];

        for (var i = 0; i < sourceMaterials.Length; i++)
        {
            var material = sourceMaterials[i];
            if (IsHeadMaterial(material))
            {
                hasHeadMaterial = true;
                bodyMaterials[i] = noDrawMaterial;
                headMaterials[i] = material;
            }
            else
            {
                bodyMaterials[i] = material;
                headMaterials[i] = noDrawMaterial;
            }
        }

        if (!hasHeadMaterial)
        {
            return;
        }

        source.sharedMaterials = bodyMaterials;

        var cloneObject = new GameObject($"{source.name}_FirstPersonHiddenHead");
        cloneObject.layer = hiddenLayer;
        cloneObject.transform.SetParent(source.transform, false);
        cloneObject.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;

        var clone = cloneObject.AddComponent<SkinnedMeshRenderer>();
        clone.sharedMesh = source.sharedMesh;
        clone.sharedMaterials = headMaterials;
        clone.bones = source.bones;
        clone.rootBone = source.rootBone;
        clone.localBounds = source.localBounds;
        clone.updateWhenOffscreen = source.updateWhenOffscreen;
        clone.quality = source.quality;
        clone.skinnedMotionVectors = source.skinnedMotionVectors;
        clone.shadowCastingMode = source.shadowCastingMode;
        clone.receiveShadows = source.receiveShadows;
        clone.lightProbeUsage = source.lightProbeUsage;
        clone.reflectionProbeUsage = source.reflectionProbeUsage;
        clone.probeAnchor = source.probeAnchor;
        clone.motionVectorGenerationMode = source.motionVectorGenerationMode;
        clone.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
        clone.rendererPriority = source.rendererPriority;

        headClones.Add(cloneObject);
    }

    private bool IsHeadMaterial(Material material)
    {
        if (material == null)
        {
            return false;
        }

        var materialName = material.name;
        foreach (var token in headMaterialNameTokens)
        {
            if (!string.IsNullOrWhiteSpace(token) &&
                materialName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void HideLayerFromFirstPersonCameras(int hiddenLayer)
    {
        var hiddenLayerMask = 1 << hiddenLayer;
        foreach (var cameraComponent in FindObjectsOfType<Camera>(true))
        {
            if (IsFirstPersonCamera(cameraComponent))
            {
                cameraComponent.cullingMask &= ~hiddenLayerMask;
            }
        }
    }

    private static bool IsFirstPersonCamera(Camera cameraComponent)
    {
        if (cameraComponent == null)
        {
            return false;
        }

        for (var current = cameraComponent.transform; current != null; current = current.parent)
        {
            var objectName = current.name;
            if (objectName.IndexOf("EyeAnchor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("OVRCameraRig", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return cameraComponent.CompareTag("MainCamera");
    }

    private bool IsHeadClone(SkinnedMeshRenderer rendererComponent)
    {
        return rendererComponent != null && headClones.Contains(rendererComponent.gameObject);
    }

    private void ClearExistingClones()
    {
        foreach (var clone in headClones)
        {
            if (clone != null)
            {
                Destroy(clone);
            }
        }

        headClones.Clear();
    }
}
