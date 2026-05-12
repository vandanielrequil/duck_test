using UnityEngine;

public abstract class PipelineObject : MonoBehaviour
{
    public const float DefaultDriftSpeed = 1.8f;

    private static float s_driftSpeed = DefaultDriftSpeed;

    public static float DriftSpeed
    {
        get => s_driftSpeed;
        set => s_driftSpeed = value;
    }

    private Vector2 _flowXY;
    private float _lockedZ;
    private Quaternion _baseRotation;
    private bool _initialized;
    private Rigidbody2D _rigidbody2D;

    protected Vector2 FlowPositionXY => _flowXY;

    protected virtual void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        CaptureBaseline();
    }

    protected virtual void OnEnable()
    {
        if (!_initialized)
            CaptureBaseline();
    }

    private void CaptureBaseline()
    {
        Vector3 p = transform.position;
        _flowXY = new Vector2(p.x, p.y);
        _lockedZ = p.z;
        _baseRotation = transform.rotation;
        _initialized = true;
    }

    protected virtual void Update()
    {
        if (_rigidbody2D == null || !_rigidbody2D.simulated)
            Step(Time.deltaTime, Time.time);
    }

    protected virtual void FixedUpdate()
    {
        if (_rigidbody2D != null && _rigidbody2D.simulated)
            Step(Time.fixedDeltaTime, Time.fixedTime);
    }

    private void Step(float deltaTime, float swayTime)
    {
        _flowXY.x -= s_driftSpeed * deltaTime;
        ApplyTransform(swayTime);
    }

    private void ApplyTransform(float swayTime)
    {
        float rollZ = 0f;
        Vector2 sway = HasSway ? EvaluateSway(swayTime, out rollZ) : Vector2.zero;

        Vector3 pos = new Vector3(_flowXY.x + sway.x, _flowXY.y + sway.y, _lockedZ);
        Quaternion rot = HasSway ? _baseRotation * Quaternion.Euler(0f, 0f, rollZ) : _baseRotation;

        if (_rigidbody2D != null)
        {
            _rigidbody2D.MovePosition(pos);
            _rigidbody2D.MoveRotation(rot.eulerAngles.z);
            _rigidbody2D.linearVelocity = Vector2.zero;
            _rigidbody2D.angularVelocity = 0f;
        }
        else
        {
            transform.SetPositionAndRotation(pos, rot);
        }
    }

    protected abstract bool HasSway { get; }

    protected abstract Vector2 EvaluateSway(float time, out float rollDegreesZ);
}
