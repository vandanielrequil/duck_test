using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class PipelineController : MonoBehaviour
{
    [SerializeField] private List<PipeSlot> _slots = new();
    public float MoveInterval = 1.5f;
    public bool IsPaused;
    [SerializeField] private GameStateManager _gameState;
    private Coroutine _tickRoutine;
    [SerializeField] private PipelineSpawner _spawner;
    public IReadOnlyList<PipeSlot> Slots => _slots;
    public PipelineInteractionResolver InteractionResolver;

    private void Awake()
    {
        for (int i = 0; i < _slots.Count; i++)
            _slots[i].Index = i;
    }

    private void Start()
    {
        MoveOcupasToNewSlot();
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

        PipeObject removed = _slots[0].OccupiedObject;

        if (removed != null)
        {
            Destroy(removed.gameObject); // TODO probably some animation from object should be called
        }

        // TODO shifted array have no sense, do in one cycle
        List<PipeObject> shifted = new();

        for (int i = 1; i < _slots.Count; i++)
        {
            shifted.Add(_slots[i].OccupiedObject);
        }

        shifted.Add(null);

        for (int i = 0; i < _slots.Count; i++)
        {
            _slots[i].SetOccupant(shifted[i]);
        }
        ////

        _slots[^1].ClearOccupant(); // last obj

        _spawner.SpawnRandom();

        MoveOcupasToNewSlot();
    }

    public void MoveOcupasToNewSlot() // TODO this probably should be combined with array above
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

    public bool TryMoveObjectBackward(
        PipeObject obj,
        int distance
    )
    {
        if (obj.CurrentSlot == null)
            return false;

        int currentIndex = obj.CurrentSlot.Index;

        int targetIndex =
            Mathf.Min(
                _slots.Count - 1,
                currentIndex + distance
            );

        PipeSlot targetSlot = _slots[targetIndex];

        InteractionResolver.ResolveInteraction(obj, targetSlot);

        MoveOcupasToNewSlot();


        return true;
    }
}
