using System.Collections.Generic;
using UnityEngine;

public class PipelineSpawner : MonoBehaviour
{
    [SerializeField] private PipelineController _pipeline;
    [SerializeField] private List<PipeObject> _prefabs;

    [SerializeField] private float _spawnOffset = 2f;

    public void SpawnRandom()
    {
        PipeSlot tail =
            _pipeline.Slots[_pipeline.Slots.Count - 1];

        if (tail.OccupiedObject != null)
        {
            Debug.Log("GAME OVER");
            return;
        }

        int index = Random.Range(0, _prefabs.Count);

        Vector3 spawnPos =
            tail.transform.position + Vector3.right * _spawnOffset;

        PipeObject spawned =
            Instantiate(
                _prefabs[index],
                spawnPos,
                Quaternion.identity
            );

        tail.SetOccupant(spawned);

        spawned.MoveTo(tail.transform.position);
    }
}