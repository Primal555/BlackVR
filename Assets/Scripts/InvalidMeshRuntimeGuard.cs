using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Finds runtime meshes with NaN/Infinity bounds before they poison the render submission.
/// This is intentionally conservative: valid meshes are untouched, unreadable asset meshes
/// are only reported, and meshes with invalid vertices have their renderer disabled.
/// </summary>
[DefaultExecutionOrder(-10000)]
public sealed class InvalidMeshRuntimeGuard : MonoBehaviour
{
    private const bool AutoInstall = false;
    private const int MaxStartupScans = 240;
    private static InvalidMeshRuntimeGuard _instance;

    private readonly HashSet<int> _reportedRenderers = new HashSet<int>();
    private readonly HashSet<int> _reportedMeshes = new HashSet<int>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!AutoInstall)
        {
            return;
        }

        if (_instance != null)
        {
            return;
        }

        var guardObject = new GameObject(nameof(InvalidMeshRuntimeGuard));
        guardObject.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(guardObject);
        _instance = guardObject.AddComponent<InvalidMeshRuntimeGuard>();
    }
#endif

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(ScanStartupFrames());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ScanAfterSceneLoad());
    }

    private IEnumerator ScanAfterSceneLoad()
    {
        yield return null;
        yield return null;
        ScanAllRenderers();
    }

    private IEnumerator ScanStartupFrames()
    {
        for (var i = 0; i < MaxStartupScans; i++)
        {
            ScanAllRenderers();
            yield return null;
        }
    }

    private void ScanAllRenderers()
    {
        var renderers = FindObjectsOfType<Renderer>(true);
        foreach (var rendererComponent in renderers)
        {
            if (rendererComponent == null || !rendererComponent.enabled)
            {
                continue;
            }

            if (rendererComponent is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                ValidateSkinnedMeshRenderer(skinnedMeshRenderer);
                continue;
            }

            if (rendererComponent is MeshRenderer)
            {
                var meshFilter = rendererComponent.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    ValidateMeshRenderer(rendererComponent, meshFilter.sharedMesh);
                }
            }
        }
    }

    private void ValidateSkinnedMeshRenderer(SkinnedMeshRenderer rendererComponent)
    {
        var mesh = rendererComponent.sharedMesh;
        var meshWasFixed = ValidateMesh(rendererComponent, mesh);

        if (!IsFinite(rendererComponent.localBounds))
        {
            if (mesh != null && IsFinite(mesh.bounds))
            {
                rendererComponent.localBounds = mesh.bounds;
                Report(rendererComponent, mesh, "fixed invalid SkinnedMeshRenderer.localBounds from shared mesh bounds");
                return;
            }

            if (!meshWasFixed)
            {
                DisableRenderer(rendererComponent, mesh, "invalid SkinnedMeshRenderer.localBounds and no valid mesh bounds fallback");
            }
        }
    }

    private void ValidateMeshRenderer(Renderer rendererComponent, Mesh mesh)
    {
        ValidateMesh(rendererComponent, mesh);
    }

    private bool ValidateMesh(Renderer rendererComponent, Mesh mesh)
    {
        if (mesh == null || IsFinite(mesh.bounds))
        {
            return false;
        }

        var verticesState = GetVerticesState(mesh);
        if (verticesState == VerticesState.Valid)
        {
            mesh.RecalculateBounds();
            if (IsFinite(mesh.bounds))
            {
                Report(rendererComponent, mesh, "recalculated invalid mesh bounds");
                return true;
            }
        }

        if (verticesState == VerticesState.Invalid)
        {
            DisableRenderer(rendererComponent, mesh, "mesh contains NaN/Infinity vertices");
            return false;
        }

        Report(rendererComponent, mesh, "mesh bounds are invalid, but vertices are unreadable; reimport or replace this mesh asset");
        return false;
    }

    private VerticesState GetVerticesState(Mesh mesh)
    {
        try
        {
            var vertices = mesh.vertices;
            for (var i = 0; i < vertices.Length; i++)
            {
                if (!IsFinite(vertices[i]))
                {
                    return VerticesState.Invalid;
                }
            }

            return VerticesState.Valid;
        }
        catch
        {
            return VerticesState.Unreadable;
        }
    }

    private void DisableRenderer(Renderer rendererComponent, Mesh mesh, string reason)
    {
        rendererComponent.enabled = false;
        Report(rendererComponent, mesh, "disabled renderer: " + reason);
    }

    private void Report(Renderer rendererComponent, Mesh mesh, string action)
    {
        var rendererId = rendererComponent.GetInstanceID();
        var meshId = mesh != null ? mesh.GetInstanceID() : 0;
        if (!_reportedRenderers.Add(rendererId) && (meshId == 0 || !_reportedMeshes.Add(meshId)))
        {
            return;
        }

        var meshName = mesh != null && !string.IsNullOrEmpty(mesh.name) ? mesh.name : "<empty mesh name>";
        var bounds = mesh != null ? mesh.bounds.ToString() : "<no mesh>";
        Debug.LogWarning(
            $"[InvalidMeshRuntimeGuard] {action}. Object='{GetPath(rendererComponent.transform)}', Renderer='{rendererComponent.GetType().Name}', Mesh='{meshName}', Bounds={bounds}",
            rendererComponent);
    }

    private static string GetPath(Transform transform)
    {
        var builder = new StringBuilder(transform.name);
        var current = transform.parent;
        while (current != null)
        {
            builder.Insert(0, current.name + "/");
            current = current.parent;
        }

        return builder.ToString();
    }

    private static bool IsFinite(Bounds bounds)
    {
        return IsFinite(bounds.center) && IsFinite(bounds.size) && IsFinite(bounds.min) && IsFinite(bounds.max);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private enum VerticesState
    {
        Valid,
        Invalid,
        Unreadable
    }
}
