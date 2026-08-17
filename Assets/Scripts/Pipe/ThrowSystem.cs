using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ThrowSystem : MonoBehaviour
{
    [SerializeField] private PipelineController _pipeline;
    [SerializeField] private Camera _camera;
    [SerializeField] private GameStateManager _gameState;
    [SerializeField] private LevelManager _levelManager;

    [Header("Flight")]
    [SerializeField] private float _flightTimePerSlot = 0.14f;
    [SerializeField] private float _minFlightTime = 0.25f;
    [SerializeField] private float _arcPerSlot = 0.2f;

    private Coroutine _flightRoutine;

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

        if (_levelManager == null)
            _levelManager = FindAnyObjectByType<LevelManager>();
    }

    private void Update()
    {
        if (!IsPlayableState() || IsBusy)
            return;

        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame)
            return;

        PipeObject obj = GetClickedObject(
            pointer.position.ReadValue()
        );

        if (obj != null)
        {
            if (obj.IsClickOnCooldown)
                return;

            _levelManager?.RegisterAction();
            obj.RegisterClick();

            if (IsKit(obj))
                TryJumpInPlace(obj);
            else
                TryLaunchByWeight(obj);
        }
    }

    private bool IsPlayableState() =>
        _gameState == null
        || _gameState.Current == PipeGameState.Playing;

    private PipeObject GetClickedObject(Vector2 screenPosition)
    {
        if (_camera == null)
            return null;

        float depth = Mathf.Abs(_camera.transform.position.z);
        Vector3 world = _camera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, depth)
        );

        Collider2D[] hits = Physics2D.OverlapPointAll(world);
        foreach (Collider2D hit in hits)
        {
            PipeObject obj = hit.GetComponentInParent<PipeObject>();
            if (obj != null)
                return obj;
        }

        return null;
    }

    private static bool IsKit(PipeObject obj) =>
        obj?.State?.ObjectData?.Archetype == PipeArchetype.Modifier
        && obj.State.ObjectData.ModifierType == PipeModifierType.Kit;

    private void TryJumpInPlace(PipeObject obj)
    {
        if (obj == null || _flightRoutine != null)
            return;

        _flightRoutine = StartCoroutine(JumpInPlaceRoutine(obj));
    }

    private IEnumerator JumpInPlaceRoutine(PipeObject obj)
    {
        if (obj == null)
        {
            _flightRoutine = null;
            yield break;
        }

        _pipeline.IsPaused = true;

        Vector2 origin = obj.transform.position;
        float jumpHeight = _arcPerSlot * GetFlightArcMultiplier(obj);
        float duration = _minFlightTime;
        float elapsed = 0f;

        obj.BeginFlight();
        bool apexNotified = false;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (!apexNotified && t >= 0.5f)
            {
                apexNotified = true;
                obj.NotifyFlightApex();
            }

            Vector2 pos = origin + Vector2.up * (Mathf.Sin(t * Mathf.PI) * jumpHeight);
            obj.SetFlightPosition(pos);
            yield return null;
        }

        obj.SetFlightPosition(origin);
        obj.EndFlight();
        _pipeline.IsPaused = false;
        _flightRoutine = null;
    }

    public bool TryLaunchByWeight(PipeObject obj)
    {
        if (obj == null || _pipeline == null || obj.CurrentSlot == null)
            return false;

        int fromIndex = obj.CurrentSlot.Index;
        int slotsToFly = GetWeight(obj) * GetThrowDirection(obj);
        PipeSlot targetSlot = _pipeline.GetSlotAtOffset(obj, slotsToFly);

        if (targetSlot == null || targetSlot == obj.CurrentSlot)
            return false;

        _pipeline.IsPaused = true;
        _flightRoutine = StartCoroutine(
            FlyToSlotRoutine(obj, targetSlot, Mathf.Abs(slotsToFly), fromIndex)
        );
        return true;
    }

    private IEnumerator FlyToSlotRoutine(
        PipeObject obj,
        PipeSlot targetSlot,
        int distanceSlots,
        int fromIndex = -1
    )
    {
        if (obj.CurrentSlot != null)
            obj.CurrentSlot.ClearOccupant();

        obj.BeginFlight();

        Vector2 start = obj.transform.position;
        PipeSlot currentTarget = targetSlot;
        int currentDistance = distanceSlots;
        int slideHits = 0;

        while (true)
        {
            Vector2 end = currentTarget.transform.position;

            yield return FlyArcSegment(
                obj,
                start,
                end,
                currentDistance
            );

            if (Resolver != null
                && Resolver.TryGetDuckPaintSlideContinuation(
                    obj,
                    currentTarget,
                    slideHits,
                    out PipeSlot nextSlot,
                    out int nextDistance
                ))
            {
                slideHits++;
                start = end;
                currentTarget = nextSlot;
                currentDistance = nextDistance;
                continue;
            }

            obj.SetFlightPosition(end);
            obj.EndFlight();

            // If the object landed in the eject zone (slot index <= eject slot),
            // the next pipeline tick will ClearOccupant it, leaving it floating.
            // Destroy it now as a homerun instead.
            PipeSlot ejectSlot = _pipeline.GetEjectSlot();
            if (ejectSlot != null && currentTarget.Index <= ejectSlot.Index)
            {
                if (obj.CurrentSlot != null)
                    obj.CurrentSlot.ClearOccupant();
                Destroy(obj.gameObject);
                _pipeline.IsPaused = false;
                _flightRoutine = null;
                yield break;
            }

            // Check for KitKnock before resolving so we can get the kit ref
            PipeObject kitTarget = currentTarget.OccupiedObject;

            InteractionResult result =
                Resolver != null
                    ? Resolver.ResolveInteraction(obj, currentTarget)
                    : InteractionResult.None;

            _pipeline.MoveOcupasToNewSlot();

            if (result == InteractionResult.KitKnock && kitTarget != null)
            {
                yield return LaunchKnockedKit(
                    kitTarget,
                    obj,
                    fromIndex >= 0 ? fromIndex : obj.CurrentSlot?.Index ?? 0
                );
            }
            else if (result != InteractionResult.Bounce)
            {
                _pipeline.IsPaused = false;
            }

            _flightRoutine = null;
            yield break;
        }
    }

    private IEnumerator LaunchKnockedKit(
        PipeObject kit,
        PipeObject striker,
        int strikerFromIndex
    )
    {
        if (Resolver == null || kit == null)
        {
            _pipeline.IsPaused = false;
            yield break;
        }

        PipeSlot kitCurrentSlot = kit.CurrentSlot;
        if (kitCurrentSlot == null)
        {
            _pipeline.IsPaused = false;
            yield break;
        }

        if (!Resolver.TryResolveKitKnock(
                striker,
                kitCurrentSlot,
                strikerFromIndex,
                out PipeSlot kitTarget,
                out int kitDistance
            ))
        {
            // Kit has nowhere to fly — destroy it, then place striker in its slot.
            kitCurrentSlot.ClearOccupant();
            Object.Destroy(kit.gameObject);
            kitCurrentSlot.SetOccupant(striker);
            striker.MoveTo(kitCurrentSlot.transform.position);
            _pipeline.IsPaused = false;
            yield break;
        }

        // Atomic swap: manually disconnect Kit from its slot so SetOccupant
        // doesn't null out Kit's CurrentSlot via the displacement path,
        // then assign striker to that slot.
        kit.CurrentSlot = null;
        kitCurrentSlot.OccupiedObject = null;

        int originalDirection = strikerFromIndex < kitCurrentSlot.Index ? 1 : -1;
        PipeSlot reboundSlot = _pipeline.GetSlotAtOffset(
            kitCurrentSlot.Index,
            1,
            -originalDirection
        );

        kitCurrentSlot.SetOccupant(striker);
        striker.MoveTo(kitCurrentSlot.transform.position);

        // --- Kit and striker fly simultaneously ---
        kit.BeginFlight();
        Vector2 kitStart = kit.transform.position;

        Coroutine kitFlight = StartCoroutine(
            FlyArcSegment(kit, kitStart, kitTarget.transform.position, kitDistance)
        );

        Coroutine strikerFlight = null;
        if (reboundSlot != null)
        {
            striker.CurrentSlot.ClearOccupant();
            striker.BeginFlight();
            Vector2 strikerStart = striker.transform.position;
            strikerFlight = StartCoroutine(
                FlyArcSegment(striker, strikerStart, reboundSlot.transform.position, 1)
            );
        }

        yield return kitFlight;
        if (strikerFlight != null)
            yield return strikerFlight;

        // --- Resolve both landings ---
        kit.EndFlight();
        InteractionResult kitResult = Resolver.ResolveInteraction(kit, kitTarget);

        InteractionResult reboundResult = InteractionResult.None;
        if (reboundSlot != null)
        {
            striker.EndFlight();
            reboundResult = Resolver.ResolveInteraction(striker, reboundSlot);
        }

        _pipeline.MoveOcupasToNewSlot();

        if (kitResult != InteractionResult.Bounce
            && reboundResult != InteractionResult.Bounce)
        {
            _pipeline.IsPaused = false;
        }
    }

    private IEnumerator FlyArcSegment(
        PipeObject obj,
        Vector2 start,
        Vector2 end,
        int distanceSlots
    )
    {
        float speed = GetFlightSpeedMultiplier(obj);
        float duration = Mathf.Max(
            _minFlightTime,
            _flightTimePerSlot * Mathf.Max(1, distanceSlots)
        ) / speed;
        float arc =
            _arcPerSlot
            * Mathf.Max(1, distanceSlots)
            * GetFlightArcMultiplier(obj);
        float elapsed = 0f;
        bool apexNotified = false;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (!apexNotified && t >= 0.5f)
            {
                apexNotified = true;
                obj.NotifyFlightApex();
            }

            Vector2 pos = Vector2.Lerp(start, end, t)
                + Vector2.up * (Mathf.Sin(t * Mathf.PI) * arc);
            obj.SetFlightPosition(pos);
            yield return null;
        }

        if (!apexNotified)
            obj.NotifyFlightApex();

        obj.SetFlightPosition(end);
    }

    // Ducks fly left (toward the inspector, lower slot index); everything
    // else flies right (toward the tail, higher slot index).
    private static int GetThrowDirection(PipeObject obj) =>
        obj?.State?.ObjectData?.Archetype == PipeArchetype.Duck ? -1 : 1;

    private static int GetWeight(PipeObject obj) =>
        Mathf.Max(1, obj?.State?.ObjectData?.Weight ?? 1);

    private static float GetFlightSpeedMultiplier(PipeObject obj) =>
        Mathf.Max(
            0.1f,
            obj?.State?.ObjectData?.FlightSpeedMultiplier ?? 1f
        );

    private static float GetFlightArcMultiplier(PipeObject obj) =>
        Mathf.Max(
            0f,
            obj?.State?.ObjectData?.FlightArcMultiplier ?? 1f
        );
}
