using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PipelineController : MonoBehaviour, IPipelineControl
{
    [SerializeField] private List<PipeSlot> _slots = new();
    [Tooltip("Slot index where inspection starts. Slot 0 stays empty (e.g. off-screen).")]
    [SerializeField] private int _inspectionSlotIndex = 1;
    public float MoveInterval = 1.5f;
    public bool IsPaused { get; set; }
    [SerializeField] private GameStateManager _gameState;
    private Coroutine _tickRoutine;
    [SerializeField] private PipelineSpawner _spawner;
    [SerializeField] private float _homerunBeyondSpacing = 1f;

    private InspectorController _inspector;
    private bool _stopped = true;

    public IReadOnlyList<PipeSlot> Slots => _slots;
    public PipelineInteractionResolver InteractionResolver;

    public event Action OnPipelineDrained;

    private void Awake()
    {
        for (int i = 0; i < _slots.Count; i++)
            _slots[i].Index = i;
    }

    private void OnEnable()
    {
        _tickRoutine = StartCoroutine(PipelineTickRoutine());
    }

    private void OnDisable()
    {
        if (_tickRoutine != null)
            StopCoroutine(_tickRoutine);
    }

    public void ResetForLevel(
        LevelConfig config,
        InspectorController inspector,
        PipelineSpawner spawner
    )
    {
        _inspector = inspector;
        _spawner = spawner;
        _stopped = false;
        IsPaused = false;

        if (config != null)
            MoveInterval = config.MoveInterval;

        ClearAllSlots();
        MoveOcupasToNewSlot();
    }

    public void StopPipeline()
    {
        _stopped = true;
        IsPaused = true;
    }

    public bool AllSlotsEmpty
    {
        get
        {
            if (_slots == null)
                return true;

            foreach (PipeSlot slot in _slots)
            {
                if (slot.OccupiedObject != null)
                    return false;
            }

            return true;
        }
    }

    private void ClearAllSlots()
    {
        if (_slots == null)
            return;

        foreach (PipeSlot slot in _slots)
        {
            if (slot.OccupiedObject != null)
                Destroy(slot.OccupiedObject.gameObject);

            slot.ClearOccupant();
        }
    }

    private IEnumerator PipelineTickRoutine()
    {
        while (enabled)
        {
            yield return new WaitForSeconds(MoveInterval);
            TickPipeline();
        }
    }

    public void TickPipeline()
    {
        if (_stopped)
            return;

        if (IsPaused)
            return;

        if (_inspector != null && _inspector.IsBusy)
            return;

        if (_gameState != null
            && _gameState.Current != PipeGameState.Playing)
            return;

        if (_slots == null || _slots.Count == 0)
            return;

        int inspectIndex = GetInspectionSlotIndex();
        PipeObject toInspect = _slots[inspectIndex].OccupiedObject;

        if (toInspect != null)
            _slots[inspectIndex].ClearOccupant();

        ShiftSlotsTowardInspector(inspectIndex);

        if (toInspect != null)
        {
            if (_inspector != null)
            {
                _inspector.BeginInspection(
                    toInspect,
                    CompleteTickAfterInspection
                );
                return;
            }

            Destroy(toInspect.gameObject);
        }

        CompleteTick();
    }

    private int GetInspectionSlotIndex()
    {
        if (_slots == null || _slots.Count == 0)
            return 0;

        return Mathf.Clamp(_inspectionSlotIndex, 0, _slots.Count - 1);
    }

    public PipeSlot GetEjectSlot()
    {
        if (_slots == null || _slots.Count == 0)
            return null;

        int ejectIndex = GetInspectionSlotIndex() - 1;
        if (ejectIndex < 0)
            return null;

        return _slots[ejectIndex];
    }

    private void ShiftSlotsTowardInspector(int inspectIndex)
    {
        for (int i = inspectIndex; i < _slots.Count - 1; i++)
            _slots[i].SetOccupant(_slots[i + 1].OccupiedObject);

        _slots[^1].ClearOccupant();

        for (int i = 0; i < inspectIndex; i++)
            _slots[i].ClearOccupant();
    }

    private void CompleteTickAfterInspection() => CompleteTick();

    private void CompleteTick()
    {
        if (_stopped)
            return;

        if (_spawner != null)
            _spawner.SpawnNext();

        MoveOcupasToNewSlot();
        TryNotifyPipelineDrained();
    }

    private void TryNotifyPipelineDrained()
    {
        if (_spawner == null || !_spawner.QueueFinished)
            return;

        if (!AllSlotsEmpty)
            return;

        if (_inspector != null && _inspector.IsBusy)
            return;

        OnPipelineDrained?.Invoke();
    }

    public void MoveOcupasToNewSlot()
    {
        foreach (PipeSlot slot in _slots)
        {
            PipeObject obj = slot.OccupiedObject;
            if (obj == null)
                continue;

            obj.CurrentSlot = slot;
            obj.MoveTo(slot.transform.position);
        }
    }

    public Vector2 GetPipelineTangent()
    {
        if (_slots == null || _slots.Count < 2)
            return Vector2.right;

        Vector2 first = _slots[0].transform.position;
        Vector2 last = _slots[^1].transform.position;
        return (last - first).normalized;
    }

    public Vector2 GetPipelineBackward() => GetPipelineTangent();

    public float GetAverageSlotSpacing()
    {
        if (_slots == null || _slots.Count < 2)
            return 1f;

        float total = 0f;
        int segments = 0;

        for (int i = 1; i < _slots.Count; i++)
        {
            total += Vector2.Distance(
                _slots[i - 1].transform.position,
                _slots[i].transform.position
            );
            segments++;
        }

        return segments > 0 ? total / segments : 1f;
    }

    public bool TryGetHomerunLandingPoints(
        out Vector2 leftBeyond,
        out Vector2 rightBeyond
    )
    {
        leftBeyond = Vector2.zero;
        rightBeyond = Vector2.zero;

        if (_slots == null || _slots.Count == 0)
            return false;

        Vector2 tangent = GetPipelineTangent();
        float beyond = GetAverageSlotSpacing() * _homerunBeyondSpacing;

        Vector2 first = _slots[0].transform.position;
        Vector2 last = _slots[^1].transform.position;

        leftBeyond = first - tangent * beyond;
        rightBeyond = last + tangent * beyond;
        return true;
    }

    public int GetMaxBackwardSlots(PipeObject obj)
    {
        if (obj?.CurrentSlot == null || _slots == null)
            return 0;

        return _slots.Count - 1 - obj.CurrentSlot.Index;
    }

    public int GetMaxForwardSlots(PipeObject obj)
    {
        if (obj?.CurrentSlot == null || _slots == null)
            return 0;

        return obj.CurrentSlot.Index;
    }

    public int GetMaxReachSlots(PipeObject obj)
    {
        if (obj?.CurrentSlot == null)
            return 0;

        return Mathf.Max(
            GetMaxForwardSlots(obj),
            GetMaxBackwardSlots(obj)
        );
    }

    public PipeSlot GetSlotAtOffset(
        PipeObject obj,
        int signedOffset
    )
    {
        if (obj?.CurrentSlot == null || _slots == null)
            return null;

        int targetIndex = obj.CurrentSlot.Index + signedOffset;

        if (targetIndex < 0 || targetIndex >= _slots.Count)
            return null;

        return _slots[targetIndex];
    }

    public PipeObject GetOccupantNearWorldX(
        float worldX,
        float maxHorizontalDistance
    )
    {
        if (_slots == null || _slots.Count == 0)
            return null;

        PipeObject best = null;
        float bestDist = maxHorizontalDistance;

        foreach (PipeSlot slot in _slots)
        {
            PipeObject obj = slot.OccupiedObject;
            if (obj == null)
                continue;

            float dist = Mathf.Abs(obj.transform.position.x - worldX);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = obj;
            }
        }

        return best;
    }
}
