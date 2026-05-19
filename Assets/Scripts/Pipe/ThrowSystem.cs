using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ThrowSystem : MonoBehaviour
{
    [SerializeField] private PipelineController _pipeline;
    [SerializeField] private Camera _camera;
    [SerializeField] private GameStateManager _gameState;

    [Header("Slingshot")]
    [SerializeField] private float _minPullWorld = 0.35f;
    [SerializeField] private float _maxPullWorld = 3.5f;
    [SerializeField] private float _aimStretchFactor = 0.35f;
    [SerializeField] private float _minAimAlignment = 0.25f;
    [SerializeField] private float _pickRadius = 0.6f;

    [Header("Ballistics")]
    [SerializeField] private float _gravity = 14f;
    [SerializeField] private float _flightTimePerSlot = 0.14f;
    [SerializeField] private float _minFlightTime = 0.35f;
    [SerializeField] private int _trajectorySteps = 24;

    [Header("Preview")]
    [SerializeField] private LineRenderer _trajectoryLine;
    [SerializeField] private float _lineZ = -1f;

    private PipeObject _aimingObject;
    private Vector2 _aimAnchor;
    private Coroutine _flightRoutine;
    private Vector3[] _trajectoryPoints;

    private bool IsFlying => _flightRoutine != null;

    private PipelineInteractionResolver Resolver =>
        _pipeline != null
            ? _pipeline.InteractionResolver
            : null;

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;

        if (_pipeline == null)
            _pipeline = FindAnyObjectByType<PipelineController>();

        _trajectoryPoints =
            new Vector3[_trajectorySteps + 1];

        if (_trajectoryLine != null)
            _trajectoryLine.enabled = false;
    }

    private void Update()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null || _pipeline == null)
            return;

        if (_aimingObject != null)
        {
            if (pointer.press.isPressed)
                UpdateAim(pointer.position.ReadValue());

            if (pointer.press.wasReleasedThisFrame)
                ReleaseAim(pointer.position.ReadValue());

            return;
        }

        if (IsFlying || !IsPlayableState())
            return;

        if (pointer.press.wasPressedThisFrame)
            TryBeginAim(pointer.position.ReadValue());
    }

    private bool IsPlayableState() =>
        _gameState == null
        || _gameState.Current == PipeGameState.Playing;

    private void TryBeginAim(Vector2 screenPos)
    {
        if (!IsPlayableState())
            return;

        PipeObject hit = PickPipeObject(screenPos);
        if (hit == null || hit.CurrentSlot == null)
            return;

        _aimingObject = hit;
        _aimAnchor = hit.transform.position;

        _pipeline.IsPaused = true;
        hit.BeginAim();
    }

    private void UpdateAim(Vector2 screenPos)
    {
        Vector2 pull = _aimAnchor - ScreenToWorld(screenPos);

        _aimingObject.SetAimPosition(
            _aimAnchor - pull * _aimStretchFactor
        );

        DrawTrajectoryPreview(_aimAnchor, pull);
    }

    private void ReleaseAim(Vector2 screenPos)
    {
        PipeObject obj = _aimingObject;
        Vector2 pull = _aimAnchor - ScreenToWorld(screenPos);
        int slotOffset = ComputeSlotOffset(obj, pull);

        ClearAim();

        if (slotOffset <= 0)
        {
            obj.CancelAim();
            _pipeline.IsPaused = false;
            return;
        }

        _flightRoutine =
            StartCoroutine(FlyAndLandRoutine(obj, slotOffset));
    }

    private void ClearAim()
    {
        _aimingObject = null;
        HideTrajectory();
    }

    private int ComputeSlotOffset(PipeObject obj, Vector2 pull)
    {
        float pullMag = pull.magnitude;
        float power = GetThrowPower(obj);
        float minPull = _minPullWorld / power;
        float maxPull = _maxPullWorld * power;

        if (pullMag < minPull)
            return 0;

        float align = Vector2.Dot(
            pull / pullMag,
            _pipeline.GetPipelineBackward()
        );

        if (align < _minAimAlignment)
            return 0;

        int maxOffset = _pipeline.GetMaxBackwardSlots(obj);
        if (maxOffset <= 0)
            return 0;

        float t = Mathf.InverseLerp(
            minPull,
            maxPull,
            pullMag
        ) * align;

        return Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Lerp(1f, maxOffset, t)),
            1,
            maxOffset
        );
    }

    private static float GetThrowPower(PipeObject obj) =>
        obj?.Data != null ? obj.Data.ThrowPower : 1f;

    private FlightParams BuildFlightParams(
        PipeObject obj,
        int slotOffset,
        Vector2 start,
        Vector2 end
    )
    {
        float launchAccel = Mathf.Max(
            0.1f,
            obj.Data?.LaunchAccelModifier ?? 1f
        );
        int weight = Mathf.Max(1, obj.Data?.Weight ?? 1);

        float duration = Mathf.Max(
            _minFlightTime,
            _flightTimePerSlot * slotOffset
        );
        duration *= Mathf.Sqrt(weight) / launchAccel;

        float gravity = _gravity * weight / launchAccel;

        return new FlightParams(
            duration,
            gravity,
            ComputeBallisticVelocity(
                start,
                end,
                duration,
                gravity
            )
        );
    }

    private IEnumerator FlyAndLandRoutine(
        PipeObject obj,
        int slotOffset
    )
    {
        PipeSlot targetSlot =
            _pipeline.GetSlotAtOffset(obj, slotOffset);

        if (targetSlot == null)
        {
            obj.CancelAim();
            _pipeline.IsPaused = false;
            _flightRoutine = null;
            yield break;
        }

        Vector2 start = obj.transform.position;
        Vector2 end = targetSlot.transform.position;

        if (obj.CurrentSlot != null)
            obj.CurrentSlot.ClearOccupant();

        obj.BeginFlight();

        FlightParams flight =
            BuildFlightParams(obj, slotOffset, start, end);

        float elapsed = 0f;

        while (elapsed < flight.Duration)
        {
            elapsed += Time.deltaTime;

            Vector2 pos = SampleBallistic(
                start,
                flight.Velocity,
                flight.Gravity,
                elapsed
            );

            float phase =
                Mathf.Clamp01(elapsed / flight.Duration);
            obj.SetFlightPosition(
                pos + Vector2.up * (Mathf.Sin(phase * Mathf.PI) * 0.06f)
            );

            yield return null;
        }

        obj.SetFlightPosition(end);
        obj.EndFlight();

        InteractionResult result =
            Resolver != null
                ? Resolver.ResolveInteraction(obj, targetSlot)
                : InteractionResult.None;

        _pipeline.MoveOcupasToNewSlot();

        if (result != InteractionResult.Bounce)
            _pipeline.IsPaused = false;

        _flightRoutine = null;
    }

    private void DrawTrajectoryPreview(Vector2 anchor, Vector2 pull)
    {
        if (_trajectoryLine == null)
            return;

        int offset = ComputeSlotOffset(_aimingObject, pull);
        if (offset <= 0)
        {
            HideTrajectory();
            return;
        }

        PipeSlot target =
            _pipeline.GetSlotAtOffset(_aimingObject, offset);

        if (target == null)
        {
            HideTrajectory();
            return;
        }

        FillTrajectoryPoints(
            BuildFlightParams(
                _aimingObject,
                offset,
                anchor,
                target.transform.position
            ),
            anchor
        );

        _trajectoryLine.positionCount = _trajectoryPoints.Length;
        _trajectoryLine.SetPositions(_trajectoryPoints);
        _trajectoryLine.enabled = true;
    }

    private void FillTrajectoryPoints(
        FlightParams flight,
        Vector2 start
    )
    {
        for (int i = 0; i <= _trajectorySteps; i++)
        {
            float time =
                (float)i / _trajectorySteps * flight.Duration;

            Vector2 p = SampleBallistic(
                start,
                flight.Velocity,
                flight.Gravity,
                time
            );

            _trajectoryPoints[i] =
                new Vector3(p.x, p.y, _lineZ);
        }
    }

    private void HideTrajectory()
    {
        if (_trajectoryLine != null)
            _trajectoryLine.enabled = false;
    }

    private static Vector2 SampleBallistic(
        Vector2 start,
        Vector2 velocity,
        float gravity,
        float time
    )
    {
        return start
            + velocity * time
            + Vector2.down * (0.5f * gravity * time * time);
    }

    private static Vector2 ComputeBallisticVelocity(
        Vector2 start,
        Vector2 end,
        float duration,
        float gravity
    )
    {
        Vector2 delta = end - start;
        float t = Mathf.Max(0.05f, duration);

        return new Vector2(
            delta.x / t,
            (delta.y + 0.5f * gravity * t * t) / t
        );
    }

    private PipeObject PickPipeObject(Vector2 screenPos)
    {
        Vector2 world = ScreenToWorld(screenPos);

        Collider2D hit =
            Physics2D.OverlapCircle(world, _pickRadius);

        if (hit != null)
        {
            PipeObject picked =
                hit.GetComponentInParent<PipeObject>();

            if (picked != null)
                return picked;
        }

        PipeObject best = null;
        float bestDist = _pickRadius;

        foreach (PipeSlot slot in _pipeline.Slots)
        {
            PipeObject obj = slot.OccupiedObject;
            if (obj == null)
                continue;

            float dist = Vector2.Distance(
                world,
                obj.transform.position
            );

            if (dist < bestDist)
            {
                bestDist = dist;
                best = obj;
            }
        }

        return best;
    }

    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        float depth = Mathf.Abs(
            _camera.transform.position.z
        );

        Vector3 world = _camera.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, depth)
        );

        return world;
    }

    private readonly struct FlightParams
    {
        public readonly float Duration;
        public readonly float Gravity;
        public readonly Vector2 Velocity;

        public FlightParams(
            float duration,
            float gravity,
            Vector2 velocity
        )
        {
            Duration = duration;
            Gravity = gravity;
            Velocity = velocity;
        }
    }
}
