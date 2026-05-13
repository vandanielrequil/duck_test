using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class PipelineController : MonoBehaviour
{
    [SerializeField] private List<PipeSlot> _slots = new();
    [SerializeField] public float MoveInterval = 1.5f;
    [SerializeField] public bool IsPaused;
    [SerializeField] private GameStateManager _gameState;
    private Coroutine _tickRoutine;

    public IReadOnlyList<PipeSlot> Slots => _slots;

    private void Awake()
    {
        for (int i = 0; i < _slots.Count; i++)
            _slots[i].Index = i;
    }

    private void Start()
    {
        RefreshSlotBindings();
    }

    private void OnEnable()
    {
        _tickRoutine = StartCoroutine(PipelineTickRoutine());
    }

    private void OnDisable()
    {
        if (_tickRoutine != null) StopCoroutine(_tickRoutine);
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
        if (IsPaused)
            return;
        if (_gameState != null && _gameState.Current != PipeGameState.Playing)
            return;
        if (_slots == null || _slots.Count == 0)
            return;

        PipeSlot head = _slots[_slots.Count - 1];
        PipeObject front = head.OccupiedObject;
        if (front != null)
        {
            head.OccupiedObject = null;
            front.CurrentSlot = null;
            // _inspectorBehaviour.BeginInspection(front);
        }

        for (int i = 0; i < _slots.Count - 1; i++)
            _slots[i].OccupiedObject = _slots[i + 1].OccupiedObject;

        _slots[_slots.Count - 1].OccupiedObject = null;

        RefreshSlotBindings();
    }

    public void RefreshSlotBindings()
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
}
