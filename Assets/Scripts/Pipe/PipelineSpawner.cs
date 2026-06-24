using UnityEngine;

public class PipelineSpawner : MonoBehaviour
{
    [SerializeField] private PipelineController _pipeline;
    [SerializeField] private PipeObject _objectPrefab;
    [SerializeField] private float _spawnOffset = 2f;

    private SpawnEntry[] _queue = System.Array.Empty<SpawnEntry>();
    private int _queueIndex;
    private int _queueStartSlotIndex = -1;

    public bool QueueFinished =>
        _queue == null || _queueIndex >= _queue.Length;

    public PipeObject ObjectPrefab => _objectPrefab;

    public void BindQueue(SpawnEntry[] queue, int queueStartSlotIndex = -1)
    {
        _queue = queue ?? System.Array.Empty<SpawnEntry>();
        _queueIndex = 0;
        _queueStartSlotIndex = queueStartSlotIndex;
    }

    // Pre-fills the pipeline at level start so the queue head sits at the
    // configured start slot, with following objects filling toward the tail.
    // Spawning afterwards still happens at the tail and shifts left.
    public void PrefillQueueStart()
    {
        if (_queueStartSlotIndex < 0)
            return;

        if (_pipeline == null
            || _pipeline.Slots == null
            || _pipeline.Slots.Count == 0)
            return;

        var slots = _pipeline.Slots;
        int lastIndex = slots.Count - 1;
        int start = Mathf.Clamp(_queueStartSlotIndex, 0, lastIndex);

        for (int index = start; index <= lastIndex; index++)
        {
            if (QueueFinished)
                break;

            SpawnIntoSlot(slots[index], slideIn: false);
        }
    }

    public void SpawnNext()
    {
        if (QueueFinished)
            return;

        if (_pipeline == null
            || _pipeline.Slots == null
            || _pipeline.Slots.Count == 0)
            return;

        PipeSlot tail = _pipeline.Slots[_pipeline.Slots.Count - 1];

        if (tail.OccupiedObject != null)
        {
            Debug.LogWarning("[Spawner] Tail slot occupied.");
            return;
        }

        SpawnIntoSlot(tail, slideIn: true);
    }

    private void SpawnIntoSlot(PipeSlot slot, bool slideIn)
    {
        if (slot == null || slot.OccupiedObject != null)
            return;

        if (!TryDequeue(out PipeObjectData data))
            return;

        if (data == null || _objectPrefab == null)
        {
            Debug.LogWarning(
                $"[Spawner] Missing object data or object prefab for {data?.name}"
            );
            return;
        }

        Vector3 spawnPos = slideIn
            ? slot.transform.position + Vector3.right * _spawnOffset
            : slot.transform.position;

        PipeObject spawned = Instantiate(
            _objectPrefab,
            spawnPos,
            Quaternion.identity
        );

        spawned.Initialize(data);

        slot.SetOccupant(spawned);
        spawned.MoveTo(slot.transform.position);
    }

    public bool TryDequeue(out PipeObjectData data)
    {
        data = null;

        if (QueueFinished)
            return false;

        data = _queue[_queueIndex].Object;
        _queueIndex++;
        return data != null;
    }

    public void SpawnRandom()
    {
        SpawnNext();
    }
}
