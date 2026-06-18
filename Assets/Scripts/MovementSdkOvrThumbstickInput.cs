using Oculus.Movement.Locomotion;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MovementSdkOvrThumbstickInput : MonoBehaviour
{
    [SerializeField] private MovementSDKLocomotion _locomotion;
    [SerializeField] private OVRInput.Axis2D _moveAxis = OVRInput.Axis2D.PrimaryThumbstick;
    [SerializeField] private OVRInput.Axis2D _fallbackMoveAxis = OVRInput.Axis2D.SecondaryThumbstick;
    [SerializeField, Range(0.0f, 0.5f)] private float _deadZone = 0.12f;
    [SerializeField] private bool _invertX = false;
    [SerializeField] private bool _invertY = false;

    [Header("Animator")]
    [SerializeField] private Animator[] _animators = new Animator[0];
    [SerializeField] private bool _writeAnimatorParameters = true;
    [SerializeField] private string _horizontalParameter = "Horizontal";
    [SerializeField] private string _verticalParameter = "Vertical";
    [SerializeField] private string _moveSpeedParameter = "MoveSpeed";
    [SerializeField] private string _isMovingParameter = "IsMoving";

    private int _horizontalId;
    private int _verticalId;
    private int _moveSpeedId;
    private int _isMovingId;

    private void Awake()
    {
        if (_locomotion == null)
        {
            _locomotion = GetComponent<MovementSDKLocomotion>();
        }

        CacheParameterIds();
    }

    private void FixedUpdate()
    {
        var input = ReadMoveInput();
        if (_locomotion != null)
        {
            _locomotion.UserInput = input;
        }

        if (_writeAnimatorParameters)
        {
            ApplyAnimatorParameters(input);
        }
    }

    private void OnDisable()
    {
        if (_locomotion != null)
        {
            _locomotion.UserInput = Vector2.zero;
        }

        ApplyAnimatorParameters(Vector2.zero);
    }

    private Vector2 ReadMoveInput()
    {
        var input = OVRInput.Get(_moveAxis);
        if (input.sqrMagnitude <= _deadZone * _deadZone)
        {
            var fallback = OVRInput.Get(_fallbackMoveAxis);
            if (fallback.sqrMagnitude > input.sqrMagnitude)
            {
                input = fallback;
            }
        }

        if (input.sqrMagnitude <= _deadZone * _deadZone)
        {
            return Vector2.zero;
        }

        if (_invertX)
        {
            input.x = -input.x;
        }

        if (_invertY)
        {
            input.y = -input.y;
        }

        return Vector2.ClampMagnitude(input, 1.0f);
    }

    private void CacheParameterIds()
    {
        _horizontalId = Animator.StringToHash(_horizontalParameter);
        _verticalId = Animator.StringToHash(_verticalParameter);
        _moveSpeedId = Animator.StringToHash(_moveSpeedParameter);
        _isMovingId = Animator.StringToHash(_isMovingParameter);
    }

    private void ApplyAnimatorParameters(Vector2 input)
    {
        if (_animators == null)
        {
            return;
        }

        var speed = Mathf.Clamp01(input.magnitude);
        var isMoving = speed > 0.001f;
        foreach (var animator in _animators)
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                continue;
            }

            SetFloatIfPresent(animator, _horizontalId, input.x);
            SetFloatIfPresent(animator, _verticalId, input.y);
            SetFloatIfPresent(animator, _moveSpeedId, speed);
            SetBoolIfPresent(animator, _isMovingId, isMoving);
        }
    }

    private static bool HasParameter(Animator animator, int parameterId)
    {
        for (var i = 0; i < animator.parameterCount; i++)
        {
            if (animator.GetParameter(i).nameHash == parameterId)
            {
                return true;
            }
        }

        return false;
    }

    private static void SetFloatIfPresent(Animator animator, int parameterId, float value)
    {
        if (HasParameter(animator, parameterId))
        {
            animator.SetFloat(parameterId, value);
        }
    }

    private static void SetBoolIfPresent(Animator animator, int parameterId, bool value)
    {
        if (HasParameter(animator, parameterId))
        {
            animator.SetBool(parameterId, value);
        }
    }
}
