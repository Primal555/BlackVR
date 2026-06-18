using UnityEngine;

[DisallowMultipleComponent]
public sealed class MetaMovementCrouchBlendDriver : MonoBehaviour
{
    [SerializeField] private Animator[] _animators = new Animator[0];
    [SerializeField] private Transform _headTransform;
    [SerializeField] private Transform _heightReference;
    [SerializeField] private bool _calibrateStandingHeightOnStart = true;
    [SerializeField] private float _standingHeadHeight = 1.65f;
    [SerializeField, Range(0.4f, 1.0f)] private float _enterCrouchEyeHeightRatio = 0.86f;
    [SerializeField, Range(0.4f, 1.0f)] private float _exitCrouchEyeHeightRatio = 0.9f;
    [SerializeField, Range(0.2f, 0.95f)] private float _fullCrouchEyeHeightRatio = 0.55f;
    [SerializeField] private float _smoothSpeed = 10.0f;
    [SerializeField, Range(0.0f, 1.0f)] private float _layerFadeStart = 0.15f;
    [SerializeField, Range(0.0f, 1.0f)] private float _maxLayerWeight = 1.0f;
    [SerializeField] private string _crouchAmountParameter = "CrouchAmount";
    [SerializeField] private string _isCrouchingParameter = "IsCrouching";
    [SerializeField] private string _moveSpeedParameter = "MoveSpeed";
    [SerializeField] private string _horizontalParameter = "Horizontal";
    [SerializeField] private string _verticalParameter = "Vertical";
    [SerializeField] private string _crouchLayer = "Crouch Locomotion";

    public float StandingEyeHeight => _standingHeadHeight;
    public float CrouchAmount => _crouchAmount;
    public bool IsCrouching => _isCrouching;

    private int _crouchAmountId;
    private int _isCrouchingId;
    private int _moveSpeedId;
    private int _horizontalId;
    private int _verticalId;
    private int[] _layerIndices;
    private float _crouchAmount;
    private bool _isCrouching;

    private void Awake()
    {
        CacheParameterIds();
        CacheLayerIndices();
    }

    private void Start()
    {
        if (_headTransform == null)
        {
            _headTransform = FindHeadTransform();
        }

        if (_heightReference == null)
        {
            _heightReference = transform;
        }

        if (_calibrateStandingHeightOnStart)
        {
            RecalibrateStandingHeight();
        }
    }

    private void LateUpdate()
    {
        if (_headTransform == null || _animators == null || _animators.Length == 0)
        {
            return;
        }

        UpdateCrouchState();
        ApplyAnimatorParameters();
    }

    [ContextMenu("Recalibrate Standing Height")]
    public void RecalibrateStandingHeight()
    {
        _standingHeadHeight = Mathf.Max(0.1f, CurrentHeadHeight());
    }

    private void OnDisable()
    {
        if (_animators == null)
        {
            return;
        }

        for (var i = 0; i < _animators.Length; i++)
        {
            var animator = _animators[i];
            if (animator == null)
            {
                continue;
            }

            SetFloatIfPresent(animator, _crouchAmountId, 0.0f);
            SetBoolIfPresent(animator, _isCrouchingId, false);
            SetLayerWeight(animator, i, 0.0f);
        }
    }

    private void UpdateCrouchState()
    {
        var standingHeight = Mathf.Max(0.1f, _standingHeadHeight);
        var eyeHeightRatio = Mathf.Clamp(CurrentHeadHeight() / standingHeight, 0.0f, 1.5f);
        var enterRatio = Mathf.Clamp01(_enterCrouchEyeHeightRatio);
        var exitRatio = Mathf.Max(enterRatio, Mathf.Clamp01(_exitCrouchEyeHeightRatio));
        var fullRatio = Mathf.Min(enterRatio - 0.01f, Mathf.Clamp01(_fullCrouchEyeHeightRatio));

        if (_isCrouching)
        {
            _isCrouching = eyeHeightRatio < exitRatio;
        }
        else
        {
            _isCrouching = eyeHeightRatio <= enterRatio;
        }

        var targetAmount = _isCrouching
            ? Mathf.Clamp01((enterRatio - eyeHeightRatio) / Mathf.Max(0.01f, enterRatio - fullRatio))
            : 0.0f;
        var t = 1.0f - Mathf.Exp(-Mathf.Max(0.0f, _smoothSpeed) * Time.deltaTime);
        _crouchAmount = Mathf.Lerp(_crouchAmount, targetAmount, t);

        if (!_isCrouching && _crouchAmount < 0.001f)
        {
            _crouchAmount = 0.0f;
        }
    }

    private void ApplyAnimatorParameters()
    {
        for (var i = 0; i < _animators.Length; i++)
        {
            var animator = _animators[i];
            if (animator == null || !animator.isActiveAndEnabled)
            {
                continue;
            }

            var moveSpeed = ReadMoveSpeed(animator);
            SetFloatIfPresent(animator, _crouchAmountId, _crouchAmount);
            SetBoolIfPresent(animator, _isCrouchingId, _isCrouching || _crouchAmount > 0.05f);
            SetFloatIfPresent(animator, _moveSpeedId, moveSpeed);

            var layerWeight = Mathf.SmoothStep(
                0.0f,
                _maxLayerWeight,
                Mathf.InverseLerp(_layerFadeStart, 1.0f, _crouchAmount));
            SetLayerWeight(animator, i, layerWeight);
        }
    }

    private float ReadMoveSpeed(Animator animator)
    {
        var horizontal = GetFloatIfPresent(animator, _horizontalId);
        var vertical = GetFloatIfPresent(animator, _verticalId);
        return Mathf.Clamp01(new Vector2(horizontal, vertical).magnitude);
    }

    private float CurrentHeadHeight()
    {
        var referenceY = _heightReference != null ? _heightReference.position.y : 0.0f;
        return _headTransform.position.y - referenceY;
    }

    private static Transform FindHeadTransform()
    {
        var camera = Camera.main;
        return camera != null ? camera.transform : null;
    }

    private void CacheParameterIds()
    {
        _crouchAmountId = Animator.StringToHash(_crouchAmountParameter);
        _isCrouchingId = Animator.StringToHash(_isCrouchingParameter);
        _moveSpeedId = Animator.StringToHash(_moveSpeedParameter);
        _horizontalId = Animator.StringToHash(_horizontalParameter);
        _verticalId = Animator.StringToHash(_verticalParameter);
    }

    private void CacheLayerIndices()
    {
        if (_animators == null)
        {
            _layerIndices = null;
            return;
        }

        _layerIndices = new int[_animators.Length];
        for (var i = 0; i < _animators.Length; i++)
        {
            _layerIndices[i] = FindLayerIndex(_animators[i], _crouchLayer);
        }
    }

    private void SetLayerWeight(Animator animator, int animatorIndex, float weight)
    {
        var layerIndex = _layerIndices != null && animatorIndex < _layerIndices.Length
            ? _layerIndices[animatorIndex]
            : -1;
        if (layerIndex >= 0 && layerIndex < animator.layerCount)
        {
            animator.SetLayerWeight(layerIndex, weight);
        }
    }

    private static int FindLayerIndex(Animator animator, string layerName)
    {
        if (animator == null)
        {
            return -1;
        }

        for (var i = 0; i < animator.layerCount; i++)
        {
            if (animator.GetLayerName(i) == layerName)
            {
                return i;
            }
        }

        return -1;
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

    private static float GetFloatIfPresent(Animator animator, int parameterId)
    {
        return HasParameter(animator, parameterId) ? animator.GetFloat(parameterId) : 0.0f;
    }
}
