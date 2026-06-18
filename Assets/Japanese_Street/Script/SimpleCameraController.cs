using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnityTemplateProjects
{
    public class SimpleCameraController : MonoBehaviour
    {
        class CameraState
        {
            public float yaw;
            public float pitch;
            public float roll;
            public float x;
            public float y;
            public float z;

            public void SetFromTransform(Transform t)
            {
                pitch = t.eulerAngles.x;
                yaw = t.eulerAngles.y;
                roll = t.eulerAngles.z;
                x = t.position.x;
                y = t.position.y;
                z = t.position.z;
            }

            public void Translate(Vector3 translation)
            {
                Vector3 rotatedTranslation = Quaternion.Euler(pitch, yaw, roll) * translation;

                x += rotatedTranslation.x;
                y += rotatedTranslation.y;
                z += rotatedTranslation.z;
            }

            public void LerpTowards(CameraState target, float positionLerpPct, float rotationLerpPct)
            {
                yaw = Mathf.Lerp(yaw, target.yaw, rotationLerpPct);
                pitch = Mathf.Lerp(pitch, target.pitch, rotationLerpPct);
                roll = Mathf.Lerp(roll, target.roll, rotationLerpPct);

                x = Mathf.Lerp(x, target.x, positionLerpPct);
                y = Mathf.Lerp(y, target.y, positionLerpPct);
                z = Mathf.Lerp(z, target.z, positionLerpPct);
            }

            public void UpdateTransform(Transform t)
            {
                t.eulerAngles = new Vector3(pitch, yaw, roll);
                t.position = new Vector3(x, y, z);
            }
        }

        CameraState m_TargetCameraState = new CameraState();
        CameraState m_InterpolatingCameraState = new CameraState();

        [Header("Movement Settings")]
        [Tooltip("Exponential boost factor on translation, controllable by mouse wheel.")]
        public float boost = 3.5f;

        [Tooltip("Time it takes to interpolate camera position 99% of the way to the target."), Range(0.001f, 1f)]
        public float positionLerpTime = 0.2f;

        [Header("Rotation Settings")]
        [Tooltip("X = Change in mouse position.\nY = Multiplicative factor for camera rotation.")]
        public AnimationCurve mouseSensitivityCurve = new AnimationCurve(new Keyframe(0f, 0.5f, 0f, 5f), new Keyframe(1f, 2.5f, 0f, 0f));

        [Tooltip("Time it takes to interpolate camera rotation 99% of the way to the target."), Range(0.001f, 1f)]
        public float rotationLerpTime = 0.01f;

        [Tooltip("Whether or not to invert our Y axis for mouse input to rotation.")]
        public bool invertY = false;

        void OnEnable()
        {
            m_TargetCameraState.SetFromTransform(transform);
            m_InterpolatingCameraState.SetFromTransform(transform);
        }

        Vector3 GetInputTranslationDirection()
        {
            Vector3 direction = new Vector3();
            if (IsForwardPressed())
            {
                direction += Vector3.forward;
            }
            if (IsBackPressed())
            {
                direction += Vector3.back;
            }
            if (IsLeftPressed())
            {
                direction += Vector3.left;
            }
            if (IsRightPressed())
            {
                direction += Vector3.right;
            }
            if (IsDownPressed())
            {
                direction += Vector3.down;
            }
            if (IsUpPressed())
            {
                direction += Vector3.up;
            }
            return direction;
        }

        void Update()
        {
            if (IsEscapePressed())
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }

            if (IsRightMousePressedThisFrame())
            {
                Cursor.lockState = CursorLockMode.Locked;
            }

            if (IsRightMouseReleasedThisFrame())
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            if (IsRightMousePressed())
            {
                var mouseDelta = GetMouseDelta();
                var mouseMovement = new Vector2(mouseDelta.x, mouseDelta.y * (invertY ? 1 : -1));
                var mouseSensitivityFactor = mouseSensitivityCurve.Evaluate(mouseMovement.magnitude);

                m_TargetCameraState.yaw += mouseMovement.x * mouseSensitivityFactor;
                m_TargetCameraState.pitch += mouseMovement.y * mouseSensitivityFactor;
            }

            var translation = GetInputTranslationDirection() * Time.deltaTime;

            if (IsShiftPressed())
            {
                translation *= 10.0f;
            }

            boost += GetScrollDelta() * 0.2f;
            translation *= Mathf.Pow(2.0f, boost);

            m_TargetCameraState.Translate(translation);

            var positionLerpPct = 1f - Mathf.Exp((Mathf.Log(1f - 0.99f) / positionLerpTime) * Time.deltaTime);
            var rotationLerpPct = 1f - Mathf.Exp((Mathf.Log(1f - 0.99f) / rotationLerpTime) * Time.deltaTime);
            m_InterpolatingCameraState.LerpTowards(m_TargetCameraState, positionLerpPct, rotationLerpPct);

            m_InterpolatingCameraState.UpdateTransform(transform);
        }

#if ENABLE_INPUT_SYSTEM
        static bool IsForwardPressed() => Keyboard.current != null && Keyboard.current.wKey.isPressed;
        static bool IsBackPressed() => Keyboard.current != null && Keyboard.current.sKey.isPressed;
        static bool IsLeftPressed() => Keyboard.current != null && Keyboard.current.aKey.isPressed;
        static bool IsRightPressed() => Keyboard.current != null && Keyboard.current.dKey.isPressed;
        static bool IsDownPressed() => Keyboard.current != null && Keyboard.current.qKey.isPressed;
        static bool IsUpPressed() => Keyboard.current != null && Keyboard.current.eKey.isPressed;
        static bool IsEscapePressed() => Keyboard.current != null && Keyboard.current.escapeKey.isPressed;
        static bool IsShiftPressed() => Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        static bool IsRightMousePressedThisFrame() => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        static bool IsRightMouseReleasedThisFrame() => Mouse.current != null && Mouse.current.rightButton.wasReleasedThisFrame;
        static bool IsRightMousePressed() => Mouse.current != null && Mouse.current.rightButton.isPressed;
        static Vector2 GetMouseDelta() => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        static float GetScrollDelta() => Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
        static bool IsForwardPressed() => Input.GetKey(KeyCode.W);
        static bool IsBackPressed() => Input.GetKey(KeyCode.S);
        static bool IsLeftPressed() => Input.GetKey(KeyCode.A);
        static bool IsRightPressed() => Input.GetKey(KeyCode.D);
        static bool IsDownPressed() => Input.GetKey(KeyCode.Q);
        static bool IsUpPressed() => Input.GetKey(KeyCode.E);
        static bool IsEscapePressed() => Input.GetKey(KeyCode.Escape);
        static bool IsShiftPressed() => Input.GetKey(KeyCode.LeftShift);
        static bool IsRightMousePressedThisFrame() => Input.GetMouseButtonDown(1);
        static bool IsRightMouseReleasedThisFrame() => Input.GetMouseButtonUp(1);
        static bool IsRightMousePressed() => Input.GetMouseButton(1);
        static Vector2 GetMouseDelta() => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        static float GetScrollDelta() => Input.mouseScrollDelta.y;
#endif
    }
}
