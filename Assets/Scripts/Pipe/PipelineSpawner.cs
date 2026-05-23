using UnityEngine;

public class PipelineSpawner : MonoBehaviour
{
    [SerializeField] private PipelineController _pipeline;
    [SerializeField] private float _spawnOffset = 2f;

    private SpawnEntry[] _queue = System.Array.Empty<SpawnEntry>();
    private int _queueIndex;

    public bool QueueFinished =>
        _queue == null || _queueIndex >= _queue.Length;

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

        if (data == null || data.SpawnPrefab == null)
        {
            Debug.LogWarning(
                $"[Spawner] Missing spawn prefab for {data?.name}"
            );
            return;
        }

        Vector3 spawnPos =
            tail.transform.position + Vector3.right * _spawnOffset;

        GameObject instance = Instantiate(
            data.SpawnPrefab,
            spawnPos,
            Quaternion.identity
        );

        PipeObject spawned = instance.GetComponent<PipeObject>();
        if (spawned == null)
        {
            Debug.LogWarning(
                $"[Spawner] Prefab {data.SpawnPrefab.name} has no PipeObject."
            );
            Destroy(instance);
            return;
        }

        if (spawned.Data == null)
            spawned.Data = data;

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
