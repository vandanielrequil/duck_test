using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ThrowSystem : MonoBehaviour
{
    [SerializeField] private PipelineController _pipeline;
    [SerializeField] private Camera _camera;
    [SerializeField] private GameStateManager _gameState;

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
            TryLaunchByWeight(obj);
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

    public bool TryLaunchByWeight(PipeObject obj)
    {
        if (obj == null || _pipeline == null || obj.CurrentSlot == null)
            return false;

        int slotsToFly = GetWeight(obj) * GetThrowDirection(obj);
        PipeSlot targetSlot = _pipeline.GetSlotAtOffset(obj, slotsToFly);

        if (targetSlot == null || targetSlot == obj.CurrentSlot)
            return false;

        _pipeline.IsPaused = true;
        _flightRoutine = StartCoroutine(
            FlyToSlotRoutine(obj, targetSlot, Mathf.Abs(slotsToFly))
        );
        return true;
    }

    private IEnumerator FlyToSlotRoutine(
        PipeObject obj,
        PipeSlot targetSlot,
        int distanceSlots
    )
    {
        Vector2 start = obj.transform.position;
        Vector2 end = targetSlot.transform.position;

        if (obj.CurrentSlot != null)
            obj.CurrentSlot.ClearOccupant();

        obj.BeginFlight();

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

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector2 pos = Vector2.Lerp(start, end, t)
                + Vector2.up * (Mathf.Sin(t * Mathf.PI) * arc);
            obj.SetFlightPosition(pos);
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
