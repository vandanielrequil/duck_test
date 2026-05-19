using System.Collections;
using UnityEngine;

public class ThrowSystem : MonoBehaviour
{
    [SerializeField] private PipelineController _pipeline;
    [SerializeField] private Camera _camera;
    [SerializeField] private GameStateManager _gameState;

    [Header("Slingshot")]
    [SerializeField] private float _minPullWorld = 0.35f;
    [SerializeField] private float _maxPullWorld = 3.5f;
    [SerializeField] private float _minAimAlignment = 0.25f;

    [Header("Ballistics")]
    [SerializeField] private float _gravity = 14f;
    [SerializeField] private float _flightTimePerSlot = 0.14f;
    [SerializeField] private float _minFlightTime = 0.35f;
    [SerializeField] private int _trajectorySteps = 24;

    [Header("Preview")]
    [SerializeField] private LineRenderer _trajectoryLine;
    [SerializeField] private float _lineZ = -1f;

    private Coroutine _flightRoutine;
    private Vector3[] _trajectoryPoints;

    public bool IsBusy => _flightRoutine != null;

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

    private bool IsPlayableState() =>
        _gameState == null
        || _gameState.Current == PipeGameState.Playing;

    /// <summary>
    /// Launch from pull vector (anchor = object position, pull = anchor - finger).
    /// </summary>
    public bool TryLaunchFromPull(PipeObject obj, Vector2 pull)
    {
        return TryLaunchFromPull(obj, pull, logFailures: false);
    }

    public bool TryLaunchFromPull(
        PipeObject obj,
        Vector2 pull,
        bool logFailures
    )
    {
        if (!IsPlayableState())
        {
            if (logFailures)
                Debug.Log("[Throw] Not in Playing state.");
            return false;
        }

        if (IsBusy)
        {
            if (logFailures)
                Debug.Log("[Throw] Previous flight still in progress.");
            return false;
        }

        if (obj == null || _pipeline == null)
            return false;

        if (obj.CurrentSlot == null)
        {
            if (logFailures)
                Debug.Log("[Throw] Object has no slot.");
            return false;
        }

        if (!TryGetSlotOffset(obj, pull, out int slotOffset, out string failReason))
        {
            if (logFailures)
                Debug.Log($"[Throw] Launch rejected: {failReason}");
            return false;
        }

        HidePreview();
        _pipeline.IsPaused = true;
        _flightRoutine =
            StartCoroutine(FlyAndLandRoutine(obj, slotOffset));

        return true;
    }

    public bool TryGetSlotOffset(
        PipeObject obj,
        Vector2 pull,
        out int slotOffset,
        out string failReason
    )
    {
        slotOffset = 0;
        failReason = null;

        if (obj == null || _pipeline == null)
        {
            failReason = "no object or pipeline";
            return false;
        }

        float pullMag = pull.magnitude;
        float power = GetThrowPower(obj);
        float minPull = _minPullWorld / power;
        float maxPull = _maxPullWorld * power;

        if (pullMag < minPull)
        {
            failReason =
                $"pull too short ({pullMag:F2} < {minPull:F2}) — drag finger farther from the object";
            return false;
        }

        Vector2 backward = _pipeline.GetPipelineBackward();
        float align = Vector2.Dot(pull / pullMag, backward);

        if (align < _minAimAlignment)
        {
            failReason =
                $"bad aim (alignment {align:F2} < {_minAimAlignment:F2}) — pull toward the tail of the pipeline (yellow arrow in axis gizmo)";
            return false;
        }

        int maxOffset = _pipeline.GetMaxBackwardSlots(obj);
        if (maxOffset <= 0)
        {
            failReason = "object is already at the last slot";
            return false;
        }

        float t = Mathf.InverseLerp(minPull, maxPull, pullMag) * align;
        slotOffset = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Lerp(1f, maxOffset, t)),
            1,
            maxOffset
        );
        return true;
    }

    public void PreviewLaunch(PipeObject obj, Vector2 pull)
    {
        if (obj == null || _trajectoryLine == null || _pipeline == null)
            return;

        int offset = ComputeSlotOffset(obj, pull);
        if (offset <= 0)
        {
            HidePreview();
            return;
        }

        PipeSlot target =
            _pipeline.GetSlotAtOffset(obj, offset);

        if (target == null)
        {
            HidePreview();
            return;
        }

        Vector2 anchor = obj.transform.position;

        FillTrajectoryPoints(
            BuildFlightParams(
                obj,
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

    public void HidePreview()
    {
        if (_trajectoryLine != null)
            _trajectoryLine.enabled = false;
    }

    private int ComputeSlotOffset(PipeObject obj, Vector2 pull) =>
        TryGetSlotOffset(obj, pull, out int offset, out _)
            ? offset
            : 0;

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
