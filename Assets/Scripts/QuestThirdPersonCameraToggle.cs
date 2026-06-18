using UnityEngine;

[DefaultExecutionOrder(10020)]
public sealed class QuestThirdPersonCameraToggle : MonoBehaviour
{
    void Awake()
    {
        DisableAndRemoveDebugCameras();
    }

    void OnEnable()
    {
        DisableAndRemoveDebugCameras();
    }

    void DisableAndRemoveDebugCameras()
    {
        foreach (var camera in GetComponentsInChildren<Camera>(true))
        {
            if (camera.name == "Third Person Debug Camera")
            {
                Destroy(camera.gameObject);
            }
        }

        enabled = false;
    }
}
