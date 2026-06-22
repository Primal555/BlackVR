using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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

    [Header("Extension Events")]
    [SerializeField] private UnityEvent onTransformed;

    private bool isTransformed;
    private readonly Dictionary<Renderer, bool> originalRendererStates = new Dictionary<Renderer, bool>();

    private void Awake()
    {
        if (recognizer == null)
        {
            recognizer = GetComponent<VoiceTemplateCommandRecognizer>();
        }

        CacheOriginalRendererStates();
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

    private void OnDisable()
    {
        if (recognizer != null)
        {
            recognizer.CommandRecognized -= HandleCommandRecognized;
        }
    }

    public void TransformNow()
    {
        if (transformOnlyOnce && isTransformed)
        {
            return;
        }

        SetTransformedState(true, invokeEvents: true);
    }

    public void ResetToBefore()
    {
        SetTransformedState(false, invokeEvents: false);
    }

    private void HandleCommandRecognized(string recognizedCommandId)
    {
        if (!string.Equals(recognizedCommandId, commandId, StringComparison.Ordinal))
        {
            return;
        }

        TransformNow();
    }

    private void SetTransformedState(bool transformed, bool invokeEvents)
    {
        isTransformed = transformed;

        for (var i = 0; i < beforeModels.Length; i++)
        {
            SetVisible(beforeModels[i], !transformed);
        }

        SetVisible(transformedModel, transformed);

        if (transformed && invokeEvents)
        {
            onTransformed?.Invoke();
        }
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
}
