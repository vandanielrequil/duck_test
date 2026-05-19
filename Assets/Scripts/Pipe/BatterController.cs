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
    [SerializeField] private float _belowAxisThreshold = 0.05f;
    [SerializeField] private Transform _stickPivot;
    [SerializeField] private bool _logLaunchFailures = true;

    private Vector2 _velocity;
    private bool _dragging;
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
        if (!IsPointerOnBatter(worldPos))
            return;

        _dragging = true;
        _velocity = Vector2.zero;
    }

    private void UpdateDrag(Vector2 worldPos)
    {
        DriveAlongAxis(worldPos);

        bool below = _axis.IsBelowAxis(worldPos, _belowAxisThreshold);
        PipeObject target = GetObjectAbove();

        if (below && target != null)
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

    private void ReleaseDrag(Vector2 worldPos)
    {
        _throwSystem.HidePreview();

        bool below = _axis.IsBelowAxis(worldPos, _belowAxisThreshold);

        if (below)
            TrySlingshotLaunch(worldPos);
        else if (_logLaunchFailures)
            Debug.Log(
                "[Batter] Released above the rail — no strike. "
                + "Release the finger below the cyan line (toward the batter)."
            );

        _dragging = false;
    }

    private void EndDrag()
    {
        if (!_dragging)
            return;

        _throwSystem.HidePreview();
        _dragging = false;
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
}
