using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class BatterController : MonoBehaviour
{
    [SerializeField] private BatterMovementAxis _axis;
    [SerializeField] private PipelineController _pipeline;
    [SerializeField] private ThrowSystem _throwSystem;
    [SerializeField] private Camera _camera;
    [SerializeField] private GameStateManager _gameState;

    [Header("Movement")]
    [SerializeField] private float _thrustForce = 24f;
    [SerializeField] private float _maxSpeed = 8f;
    [SerializeField] private float _velocityDamping = 4f;

    [Header("Targeting")]
    [SerializeField] private float _maxTargetHorizontalDistance = 1.25f;

    [Header("Slingshot")]
    [SerializeField] private Transform _stickPivot;
    [SerializeField] private bool _logLaunchFailures = true;

    [Header("Input zones (relative to axis Y)")]
    [Tooltip(
        "How far BELOW the rail the pointer can be and still count as movement. "
        + "Pointer above the rail is always movement."
    )]
    [Min(0f)]
    [SerializeField] private float _movementZoneDepthBelowAxis = 0.3f;

    [Tooltip(
        "How far BELOW the rail the pointer must reach to switch into strike-aim mode."
    )]
    [Min(0f)]
    [SerializeField] private float _strikeZoneDepthBelowAxis = 0.6f;

    private enum DragMode
    {
        None,
        Move,
        Strike,
    }

    private Vector2 _velocity;
    private bool _dragging;
    private DragMode _mode;
    private Collider2D _collider;

    public BatterMovementAxis Axis => _axis;
    public bool IsDragging => _dragging;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();

        if (_camera == null)
            _camera = Camera.main;

        if (_pipeline == null)
            _pipeline = FindAnyObjectByType<PipelineController>();

        if (_throwSystem == null)
            _throwSystem = FindAnyObjectByType<ThrowSystem>();
    }

    private void Start()
    {
        if (_axis != null)
            SnapToAxis(ClosestPointOnAxis(transform.position));

        AutoFitTargetRadius();
    }

    private void AutoFitTargetRadius()
    {
        if (_pipeline == null || _pipeline.Slots.Count < 2)
            return;

        float spacing = Vector2.Distance(
            _pipeline.Slots[0].transform.position,
            _pipeline.Slots[1].transform.position
        );

        _maxTargetHorizontalDistance = Mathf.Max(
            _maxTargetHorizontalDistance,
            spacing * 0.55f
        );
    }

    public void SetMovementAxis(BatterMovementAxis axis)
    {
        _axis = axis;

        if (_axis != null && isActiveAndEnabled)
            SnapToAxis(_axis.ClosestPointOnAxis(transform.position));
    }

    private void Update()
    {
        if (_axis == null || _throwSystem == null)
            return;

        Pointer pointer = Pointer.current;
        if (pointer == null)
            return;

        if (!IsPlayableState())
        {
            EndDrag();
            return;
        }

        if (_throwSystem.IsBusy)
        {
            EndDrag();
            return;
        }

        Vector2 screenPos = pointer.position.ReadValue();
        Vector2 worldPos = ScreenToWorld(screenPos);

        if (pointer.press.wasPressedThisFrame)
            TryBeginDrag(worldPos);

        if (_dragging)
        {
            if (pointer.press.isPressed)
                UpdateDrag(worldPos);
            else if (pointer.press.wasReleasedThisFrame)
                ReleaseDrag(worldPos);
        }
    }

    private void FixedUpdate()
    {
        if (!_dragging || _axis == null)
            return;

        ApplyAxisDamping();
    }

    private bool IsPlayableState() =>
        _gameState == null
        || _gameState.Current == PipeGameState.Playing;

    private void TryBeginDrag(Vector2 worldPos)
    {
        DragMode zoneMode = GetZoneMode(worldPos);

        if (zoneMode == DragMode.Strike)
        {
            BeginDrag(DragMode.Strike);
            return;
        }

        if (zoneMode == DragMode.Move && IsPointerOnBatter(worldPos))
        {
            BeginDrag(DragMode.Move);
            return;
        }
    }

    private void BeginDrag(DragMode mode)
    {
        _dragging = true;
        _mode = mode;
        _velocity = Vector2.zero;
    }

    private void UpdateDrag(Vector2 worldPos)
    {
        DragMode zoneMode = GetZoneMode(worldPos);
        if (zoneMode != DragMode.None)
            _mode = zoneMode;

        if (_mode == DragMode.Move)
        {
            DriveAlongAxis(worldPos);
            _throwSystem.HidePreview();
            AimStick(_axis.Tangent);
            return;
        }

        if (_mode == DragMode.Strike)
        {
            BrakeMovement();

            PipeObject target = GetObjectAbove();
            if (target != null)
            {
                Vector2 pull = (Vector2)target.transform.position - worldPos;
                _throwSystem.PreviewLaunch(target, pull);
                AimStick(pull);
            }
            else
            {
                _throwSystem.HidePreview();
                AimStick(_axis.Tangent);
            }
        }
    }

    private void ReleaseDrag(Vector2 worldPos)
    {
        _throwSystem.HidePreview();

        DragMode zoneMode = GetZoneMode(worldPos);
        if (zoneMode != DragMode.None)
            _mode = zoneMode;

        if (_mode == DragMode.Strike)
            TrySlingshotLaunch(worldPos);
        else if (_logLaunchFailures && _mode == DragMode.Move)
            Debug.Log(
                "[Batter] Drag ended in movement zone — no strike."
            );

        _dragging = false;
        _mode = DragMode.None;
    }

    private void EndDrag()
    {
        if (!_dragging)
            return;

        _throwSystem.HidePreview();
        _dragging = false;
        _mode = DragMode.None;
    }

    private DragMode GetZoneMode(Vector2 worldPos)
    {
        float depth = _axis.SignedDistanceBelow(worldPos);

        if (depth >= _strikeZoneDepthBelowAxis)
            return DragMode.Strike;

        if (depth <= _movementZoneDepthBelowAxis)
            return DragMode.Move;

        return DragMode.None;
    }

    private void TrySlingshotLaunch(Vector2 worldPos)
    {
        PipeObject target = GetObjectAbove();
        if (target == null)
        {
            if (_logLaunchFailures)
                Debug.Log(
                    "[Batter] No pipe object above — move batter under a slot occupant "
                    + $"(max X distance {_maxTargetHorizontalDistance:F2})."
                );
            return;
        }

        if (target.CurrentSlot == null)
            return;

        Vector2 pull = (Vector2)target.transform.position - worldPos;
        _throwSystem.TryLaunchFromPull(
            target,
            pull,
            _logLaunchFailures
        );
    }

    private void BrakeMovement()
    {
        float damp = Mathf.Clamp01(_velocityDamping * Time.deltaTime * 2.5f);
        _velocity *= 1f - damp;
    }

    private void DriveAlongAxis(Vector2 worldPos)
    {
        Vector2 onAxis = ClosestPointOnAxis(worldPos);
        Vector2 toTarget = onAxis - (Vector2)transform.position;
        float along = Vector2.Dot(toTarget, _axis.Tangent);
        Vector2 alongVector = _axis.Tangent * along;

        _velocity += alongVector * (_thrustForce * Time.deltaTime);
        _velocity = Vector2.ClampMagnitude(_velocity, _maxSpeed);

        Vector2 next = (Vector2)transform.position + _velocity * Time.deltaTime;
        SnapToAxis(ClosestPointOnAxis(next));
    }

    private void ApplyAxisDamping()
    {
        float damp = Mathf.Clamp01(_velocityDamping * Time.fixedDeltaTime);
        _velocity *= 1f - damp;
    }

    private PipeObject GetObjectAbove()
    {
        if (_pipeline == null)
            return null;

        return _pipeline.GetOccupantNearWorldX(
            transform.position.x,
            _maxTargetHorizontalDistance
        );
    }

    private bool IsPointerOnBatter(Vector2 worldPos)
    {
        if (_collider == null)
            return Vector2.Distance(worldPos, transform.position) < 1f;

        return _collider.OverlapPoint(worldPos);
    }

    private Vector2 ClosestPointOnAxis(Vector2 worldPos) =>
        _axis.ClosestPointOnAxis(worldPos);

    private void SnapToAxis(Vector2 onAxis)
    {
        Vector3 p = onAxis;
        p.z = transform.position.z;
        transform.position = p;
    }

    private void AimStick(Vector2 direction)
    {
        if (_stickPivot == null || direction.sqrMagnitude < 0.0001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        _stickPivot.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        float depth = Mathf.Abs(_camera.transform.position.z);
        Vector3 world = _camera.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, depth)
        );
        return world;
    }

    private void OnDrawGizmosSelected()
    {
        if (_axis == null)
            return;

        Vector2 onAxis = _axis.ClosestPointOnAxis(transform.position);
        Vector2 tangent = _axis.Tangent;
        Vector2 halfRail = tangent * (_axis.Length * 0.5f);

        Vector2 moveStart = onAxis - halfRail - Vector2.up * _movementZoneDepthBelowAxis;
        Vector2 moveEnd = onAxis + halfRail - Vector2.up * _movementZoneDepthBelowAxis;
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
        Gizmos.DrawLine(moveStart, moveEnd);

        Vector2 strikeStart = onAxis - halfRail - Vector2.up * _strikeZoneDepthBelowAxis;
        Vector2 strikeEnd = onAxis + halfRail - Vector2.up * _strikeZoneDepthBelowAxis;
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
        Gizmos.DrawLine(strikeStart, strikeEnd);
    }
}
