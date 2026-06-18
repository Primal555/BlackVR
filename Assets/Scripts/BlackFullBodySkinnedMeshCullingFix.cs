using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BlackFullBodySkinnedMeshCullingFix : MonoBehaviour
{
    [SerializeField] private Vector3 minimumLocalBoundsExtents = new Vector3(2f, 2f, 2f);
    [SerializeField] private bool updateWhenOffscreen = true;
    [SerializeField] private int startupRefreshFrames = 10;

    private void OnEnable()
    {
        Apply();
        StartCoroutine(RefreshDuringStartup());
    }

    private IEnumerator RefreshDuringStartup()
    {
        for (var i = 0; i < startupRefreshFrames; i++)
        {
            yield return null;
            Apply();
        }
    }

    private void Apply()
    {
        var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var rendererComponent in renderers)
        {
            rendererComponent.updateWhenOffscreen = updateWhenOffscreen;

            var bounds = rendererComponent.localBounds;
            bounds.extents = Vector3.Max(bounds.extents, minimumLocalBoundsExtents);
            rendererComponent.localBounds = bounds;
        }
    }
}
