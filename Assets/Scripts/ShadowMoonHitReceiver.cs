using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(AudioSource))]
public sealed class ShadowMoonHitReceiver : MonoBehaviour
{
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform visualPivot;
    [SerializeField] private ParticleSystem hitParticles;
    [SerializeField] private AudioClip hitClip = null;
    [SerializeField, Min(0)] private float hitCooldown = 0.18f;
    [SerializeField, Min(0)] private float knockbackSpeed = 0.45f;
    [SerializeField, Range(0, 30)] private float recoilDegrees = 10f;
    [SerializeField, Min(0.05f)] private float recoilDuration = 0.25f;
    [SerializeField, Min(0)] private float bumpSpeedThreshold = 0.8f;
    [SerializeField] private UnityEvent onHit = new UnityEvent();
    [SerializeField] private UnityEvent onPlayerBump = new UnityEvent();

    public Vector3 LastHitPoint { get; private set; }
    public float LastHitSpeed { get; private set; }

    private Rigidbody body;
    private AudioSource source;
    private Quaternion neutralRotation;
    private Vector3 recoilAxis;
    private float recoilStrength;
    private float recoilStarted = float.NegativeInfinity;
    private float nextHitTime;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    public void Configure(Transform player, Transform visuals, ParticleSystem particles)
    {
        playerRoot = player;
        visualPivot = visuals;
        hitParticles = particles;
    }

    public void BindPlayer(Transform player)
    {
        playerRoot = player;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        source = GetComponent<AudioSource>();
        spawnPosition = body.position;
        spawnRotation = body.rotation;
        neutralRotation = visualPivot != null ? visualPivot.localRotation : Quaternion.identity;
    }

    public bool TryHit(Vector3 point, Vector3 velocity)
    {
        if (!isActiveAndEnabled || Time.time < nextHitTime || velocity.sqrMagnitude < 0.001f)
            return false;

        nextHitTime = Time.time + hitCooldown;
        LastHitPoint = point;
        LastHitSpeed = velocity.magnitude;
        var strength = Mathf.Clamp(LastHitSpeed / 3f, 0.5f, 1.5f);
        var direction = Vector3.ProjectOnPlane(velocity, Vector3.up).normalized;
        if (direction.sqrMagnitude < 0.01f)
            direction = -transform.forward;
        BeginRecoil(direction, strength);
        if (!body.isKinematic)
            body.AddForce(direction * (knockbackSpeed * strength), ForceMode.VelocityChange);

        if (hitParticles != null)
        {
            if (!hitParticles.isPlaying)
                hitParticles.Play(false);
            // Emit in world space so previous sparks do not move with the next impact.
            var emission = new ParticleSystem.EmitParams { position = point };
            for (var i = 0; i < 12; i++)
            {
                emission.velocity = (Random.onUnitSphere + Vector3.up) * Random.Range(0.3f, 1f);
                hitParticles.Emit(emission, 1);
            }
        }
        if (hitClip != null)
            source.PlayOneShot(hitClip);
        onHit.Invoke();
        return true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (playerRoot == null || collision.rigidbody == null || Time.time < nextHitTime)
            return;
        if (!collision.rigidbody.transform.IsChildOf(playerRoot) ||
            collision.relativeVelocity.magnitude < bumpSpeedThreshold)
            return;

        nextHitTime = Time.time + hitCooldown;
        var direction = Vector3.ProjectOnPlane(transform.position - playerRoot.position, Vector3.up).normalized;
        BeginRecoil(direction, 0.35f);
        onPlayerBump.Invoke();
    }

    private void BeginRecoil(Vector3 direction, float strength)
    {
        recoilAxis = transform.InverseTransformDirection(Vector3.Cross(Vector3.up, direction));
        recoilStrength = strength;
        recoilStarted = Time.time;
    }

    private void LateUpdate()
    {
        if (visualPivot == null)
            return;
        var phase = Mathf.Clamp01((Time.time - recoilStarted) / Mathf.Max(0.05f, recoilDuration));
        var angle = Mathf.Sin(phase * Mathf.PI) * recoilDegrees * recoilStrength;
        visualPivot.localRotation = Quaternion.AngleAxis(angle, recoilAxis) * neutralRotation;
    }

    private void OnDisable()
    {
        if (visualPivot != null)
            visualPivot.localRotation = neutralRotation;
    }

    [ContextMenu("Test Hit (Play Mode)")]
    public void TestHit()
    {
        if (Application.isPlaying)
            TryHit(transform.position + Vector3.up * 1.2f, -transform.forward * 3f);
    }

    [ContextMenu("Reset Target (Play Mode)")]
    public void ResetTarget()
    {
        if (!Application.isPlaying || body == null)
            return;
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = spawnPosition;
        body.rotation = spawnRotation;
        recoilStarted = float.NegativeInfinity;
        nextHitTime = 0;
        if (hitParticles != null)
            hitParticles.Clear();
    }
}
