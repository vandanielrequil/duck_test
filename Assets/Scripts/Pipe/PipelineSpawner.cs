using UnityEngine;

public class PipelineSpawner : MonoBehaviour
{
    [SerializeField] private PipelineController _pipeline;
    [SerializeField] private PipeObject _objectPrefab;
    [SerializeField] private float _spawnOffset = 2f;

    private SpawnEntry[] _queue = System.Array.Empty<SpawnEntry>();
    private int _queueIndex;

    public bool QueueFinished =>
        _queue == null || _queueIndex >= _queue.Length;

    public PipeObject ObjectPrefab => _objectPrefab;

    public void BindQueue(SpawnEntry[] queue)
    {
        _queue = queue ?? System.Array.Empty<SpawnEntry>();
        _queueIndex = 0;
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

        if (!TryDequeue(out PipeObjectData data))
            return;

        if (data == null || _objectPrefab == null)
        {
            Debug.LogWarning(
                $"[Spawner] Missing object data or object prefab for {data?.name}"
            );
            return;
        }

        Vector3 spawnPos =
            tail.transform.position + Vector3.right * _spawnOffset;

        PipeObject spawned = Instantiate(
            _objectPrefab,
            spawnPos,
            Quaternion.identity
        );

        spawned.Initialize(data);

        tail.SetOccupant(spawned);
        spawned.MoveTo(tail.transform.position);
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
