using System.Collections;
using UnityEngine;

public class ThrowSystem : MonoBehaviour
{
    [SerializeField] private PipelineController _pipeline;
    [SerializeField] private Camera _camera;
    [SerializeField] private GameStateManager _gameState;

    [Header("Batter aim")]
    [Tooltip("Pull length at which reach is 1 slot (before ThrowPower).")]
    [SerializeField] private float _minPullWorld = 0.35f;

    [Tooltip("Pull length for maximum reach in the aimed direction.")]
    [SerializeField] private float _maxPullWorld = 3.5f;

    [Tooltip("Pipeline tangent alignment needed to pick left/right.")]
    [SerializeField] private float _minAlongAlignment = 0.12f;

    [Header("Ballistics")]
    [SerializeField] private float _gravity = 14f;
    [SerializeField] private float _flightTimePerSlot = 0.14f;
    [SerializeField] private float _minFlightTime = 0.35f;
    [SerializeField] private int _trajectorySteps = 24;

    [Header("Flight arc (clear neighbors)")]
    [Tooltip("Extra vertical lift so objects pass over heads.")]
    [SerializeField] private float _flightArcClearance = 0.28f;

    [Tooltip("More arc per slot traveled along the pipeline.")]
    [SerializeField] private float _arcPerSlotStep = 0.04f;

    [Header("Weight visuals")]
    [SerializeField] private float _arcHeightPerWeight = 0.05f;
    [SerializeField] private float _durationPerWeight = 0.22f;

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

        if (!TryGetLaunchTarget(obj, pull, out PipeLaunchTarget target, out string failReason))
        {
            if (logFailures)
                Debug.Log($"[Throw] Launch rejected: {failReason}");
            return false;
        }

        HidePreview();
        _pipeline.IsPaused = true;
        _flightRoutine =
            StartCoroutine(FlyAndLandRoutine(obj, target));

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
        if (!TryGetLaunchTarget(obj, pull, out PipeLaunchTarget target, out failReason))
            return false;

        if (target.IsHomerun)
        {
            failReason = "launch is a homerun, not a slot offset";
            return false;
        }

        slotOffset = target.SignedSlotOffset;
        return true;
    }

    public bool TryGetLaunchTarget(
        PipeObject obj,
        Vector2 pull,
        out PipeLaunchTarget target,
        out string failReason
    )
    {
        target = default;
        failReason = null;

        if (obj == null || _pipeline == null)
        {
            failReason = "no object or pipeline";
            return false;
        }

        if (obj.CurrentSlot == null)
        {
            failReason = "object has no slot";
            return false;
        }

        int currentIndex = obj.CurrentSlot.Index;
        int slotCount = _pipeline.Slots.Count;

        if (!TryGetStrikeDirection(pull, out int direction, out failReason))
            return false;

        int reach = ComputeReachSlots(obj, pull.magnitude, currentIndex, direction);

        if (reach <= 0)
        {
            failReason = "no reach in that direction";
            return false;
        }

        int targetIndex = currentIndex + direction * reach;

        if (targetIndex < 0)
        {
            if (!_pipeline.TryGetHomerunLandingPoints(
                    out Vector2 leftBeyond,
                    out _))
            {
                failReason = "homerun left not available";
                return false;
            }

            target = PipeLaunchTarget.ForHomerun(true, leftBeyond);
            return true;
        }

        if (targetIndex >= slotCount)
        {
            if (!_pipeline.TryGetHomerunLandingPoints(
                    out _,
                    out Vector2 rightBeyond))
            {
                failReason = "homerun right not available";
                return false;
            }

            target = PipeLaunchTarget.ForHomerun(false, rightBeyond);
            return true;
        }

        PipeSlot slot = _pipeline.Slots[targetIndex];
        target = PipeLaunchTarget.ForSlot(
            targetIndex - currentIndex,
            slot,
            slot.transform.position
        );
        return true;
    }

    private bool TryGetStrikeDirection(
        Vector2 pull,
        out int direction,
        out string failReason
    )
    {
        direction = 0;
        failReason = null;

        Vector2 pullDir = GetPullDirection(pull);
        Vector2 tangent = _pipeline.GetPipelineTangent();
        float along = Vector2.Dot(pullDir, tangent);

        if (along >= _minAlongAlignment)
        {
            direction = 1;
            return true;
        }

        if (along <= -_minAlongAlignment)
        {
            direction = -1;
            return true;
        }

        Vector2 fingerSide = -pull;
        if (fingerSide.sqrMagnitude < 0.0001f)
        {
            failReason = "aim direction unclear";
            return false;
        }

        float fingerAngle =
            Mathf.Atan2(fingerSide.x, -fingerSide.y) * Mathf.Rad2Deg;
        direction = fingerAngle >= 0f ? 1 : -1;
        return true;
    }

    private int ComputeReachSlots(
        PipeObject obj,
        float pullMag,
        int currentIndex,
        int direction
    )
    {
        int slotCount = _pipeline.Slots.Count;
        int slotsAlongPipe = direction > 0
            ? slotCount - 1 - currentIndex
            : currentIndex;

        // +1 so a full strike can send the ball past the end (homerun).
        int maxReach = slotsAlongPipe + 1;

        float power = GetThrowPower(obj);
        float minPull = _minPullWorld / power;
        float maxPull = _maxPullWorld / power;
        float clampedMag = Mathf.Max(pullMag, minPull * 0.25f);
        float t = Mathf.InverseLerp(minPull, maxPull, clampedMag);

        return Mathf.Max(
            1,
            Mathf.RoundToInt(Mathf.Lerp(1f, maxReach, t))
        );
    }

    private static Vector2 GetPullDirection(Vector2 pull)
    {
        if (pull.sqrMagnitude > 0.0001f)
            return pull.normalized;

        return Vector2.down;
    }

    private static float GetThrowPower(PipeObject obj) =>
        obj?.State?.ObjectData != null
            ? Mathf.Max(0.1f, obj.State.ObjectData.ThrowPower)
            : 1f;

    public void PreviewLaunch(PipeObject obj, Vector2 pull)
    {
        if (obj == null || _trajectoryLine == null || _pipeline == null)
            return;

        if (!TryGetLaunchTarget(obj, pull, out PipeLaunchTarget launch, out _))
        {
            HidePreview();
            return;
        }

        Vector2 anchor = obj.transform.position;

        FillTrajectoryPoints(
            BuildFlightParams(
                obj,
                launch.FlightDistanceSteps,
                anchor,
                launch.LandingPosition
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

    private FlightParams BuildFlightParams(
        PipeObject obj,
        int distanceSteps,
        Vector2 start,
        Vector2 end
    )
    {
        int weight = GetWeight(obj);
        float launchAccel = GetLaunchAccel(obj);
        int steps = Mathf.Max(1, distanceSteps);

        float duration = Mathf.Max(
            _minFlightTime,
            _flightTimePerSlot * steps
        );
        duration /= launchAccel;
        duration *= 1f + (weight - 1) * _durationPerWeight;

        float gravity =
            _gravity / (launchAccel * Mathf.Sqrt(weight));

        float arcBoost =
            _flightArcClearance
            + _arcPerSlotStep * steps
            + _arcHeightPerWeight * (weight - 1);

        return new FlightParams(
            duration,
            gravity,
            ComputeBallisticVelocity(
                start,
                end,
                duration,
                gravity
            ),
            arcBoost
        );
    }

    private static int GetWeight(PipeObject obj) =>
        Mathf.Max(1, obj?.State?.ObjectData?.Weight ?? 1);

    private static float GetLaunchAccel(PipeObject obj) =>
        Mathf.Max(
            0.1f,
            obj?.State?.ObjectData?.LaunchAccelModifier ?? 1f
        );

    private IEnumerator FlyAndLandRoutine(
        PipeObject obj,
        PipeLaunchTarget launch
    )
    {
        Vector2 start = obj.transform.position;
        Vector2 end = launch.LandingPosition;

        if (obj.CurrentSlot != null)
            obj.CurrentSlot.ClearOccupant();

        obj.BeginFlight();

        FlightParams flight =
            BuildFlightParams(
                obj,
                launch.FlightDistanceSteps,
                start,
                end
            );

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
                pos
                + Vector2.up * (
                    Mathf.Sin(phase * Mathf.PI) * flight.ArcBoost
                )
            );

            yield return null;
        }

        InteractionResult result;

        if (launch.IsHomerun)
        {
            obj.SetFlightPosition(end);
            result = Resolver != null
                ? Resolver.ResolveHomerun(obj, launch.IsHomerunLeft)
                : InteractionResult.None;
        }
        else
        {
            obj.SetFlightPosition(end);
            obj.EndFlight();

            result = Resolver != null && launch.TargetSlot != null
                ? Resolver.ResolveInteraction(obj, launch.TargetSlot)
                : InteractionResult.None;

            _pipeline.MoveOcupasToNewSlot();
        }

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

            float phase = (float)i / _trajectorySteps;
            p += Vector2.up * (
                Mathf.Sin(phase * Mathf.PI) * flight.ArcBoost
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
        public readonly float ArcBoost;

        public FlightParams(
            float duration,
            float gravity,
            Vector2 velocity,
            float arcBoost
        )
        {
            Duration = duration;
            Gravity = gravity;
            Velocity = velocity;
            ArcBoost = arcBoost;
        }
    }
}
