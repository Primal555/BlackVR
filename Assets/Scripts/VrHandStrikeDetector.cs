using UnityEngine;

[DefaultExecutionOrder(11000)]
[DisallowMultipleComponent]
public sealed class VrHandStrikeDetector : MonoBehaviour
{
    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.LTouch;
    [SerializeField] private Vector3 localHitOffset = Vector3.zero;
    [SerializeField, Min(0.01f)] private float hitRadius = 0.09f;
    [SerializeField, Min(0.1f)] private float minimumStrikeSpeed = 1.5f;
    [SerializeField, Min(0)] private float rearmSpeed = 0.65f;
    [SerializeField, Min(0)] private float strikeCooldown = 0.3f;
    [SerializeField, Min(0.1f)] private float maxSampleDistance = 0.6f;
    [SerializeField] private LayerMask hitLayers = ~0;

    private readonly RaycastHit[] sweepHits = new RaycastHit[64];
    private readonly Collider[] overlaps = new Collider[64];
    private Vector3 previousTrackingPosition;
    private bool hasPreviousPosition;
    private bool armed;
    private float nextStrikeTime;

    public void Configure(OVRCameraRig rig, Transform player, OVRInput.Controller hand)
    {
        cameraRig = rig;
        playerRoot = player;
        controller = hand;
    }

    private void OnEnable()
    {
        hasPreviousPosition = false;
        armed = false;
    }

    private void OnApplicationFocus(bool focus)
    {
        hasPreviousPosition = false;
        armed = false;
    }

    private void LateUpdate()
    {
        if (cameraRig == null || cameraRig.trackingSpace == null || playerRoot == null ||
            !OVRInput.GetControllerPositionTracked(controller) ||
            !OVRInput.GetControllerOrientationTracked(controller))
        {
            hasPreviousPosition = false;
            armed = false;
            return;
        }

        var space = cameraRig.trackingSpace;
        var point = transform.TransformPoint(localHitOffset);
        var localPoint = space.InverseTransformPoint(point);
        var velocity = OVRInput.GetLocalControllerVelocity(controller);
        var speed = velocity.magnitude;
        if (speed < Mathf.Min(rearmSpeed, minimumStrikeSpeed))
            armed = true;

        // Reconstruct both samples in today's tracking space: joystick movement and
        // snap turns must not become a punch sweep through the world.
        var from = space.TransformPoint(previousTrackingPosition);
        var delta = point - from;
        var validSample = hasPreviousPosition && delta.magnitude <= maxSampleDistance && Time.deltaTime <= 0.1f;
        previousTrackingPosition = localPoint;
        hasPreviousPosition = true;
        if (!validSample)
        {
            armed = false;
            return;
        }
        if (!armed || speed < minimumStrikeSpeed || Time.time < nextStrikeTime)
            return;

        Collider closest = null;
        Vector3 hitPoint = point;
        float nearest = float.PositiveInfinity;
        var count = Physics.OverlapSphereNonAlloc(from, hitRadius, overlaps, hitLayers, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length)
            return;
        for (var i = 0; i < count; i++)
        {
            if (IsPlayer(overlaps[i])) continue;
            // Never hit through a wall when the hand starts inside it.
            if (overlaps[i].GetComponentInParent<ShadowMoonHitReceiver>() == null)
            {
                armed = false;
                return;
            }
            closest = overlaps[i];
            hitPoint = closest.ClosestPoint(from);
            nearest = 0;
        }

        if (delta.sqrMagnitude > 0.000001f)
        {
            count = Physics.SphereCastNonAlloc(from, hitRadius, delta.normalized, sweepHits,
                delta.magnitude, hitLayers, QueryTriggerInteraction.Ignore);
            if (count == sweepHits.Length)
                return;
            for (var i = 0; i < count; i++)
            {
                var hit = sweepHits[i];
                if (IsPlayer(hit.collider) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                closest = hit.collider;
                hitPoint = hit.point;
            }
        }
        if (closest == null)
            return;
        var target = closest.GetComponentInParent<ShadowMoonHitReceiver>();
        // Contact consumes the swing even when a wall blocks it or the target is
        // cooling down. A hand already passing through a surface cannot hit later.
        armed = false;
        if (target != null && target.TryHit(hitPoint, space.TransformDirection(velocity)))
        {
            nextStrikeTime = Time.time + strikeCooldown;
        }
    }

    private bool IsPlayer(Collider collider)
    {
        return collider == null || collider.transform.IsChildOf(playerRoot);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.TransformPoint(localHitOffset), hitRadius);
    }
}
