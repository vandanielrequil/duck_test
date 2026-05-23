using UnityEngine;

public class PipeObject : MonoBehaviour
{
    public PipeObjectData Data;
    public PipeSlot CurrentSlot;
    public PipeObject hardcodedMerge;

    public int Duckiness =>
        Data != null ? Data.BaseDuckiness : 0;

    [SerializeField] private float _followSpeed = 8f;

    private Vector2 _targetPosition;
    private float _visualZ;
    private bool _followEnabled = true;

    private void Awake()
    {
        _visualZ = transform.position.z;
        _targetPosition = transform.position;
    }

    public void MoveTo(Vector3 targetWorldPosition)
    {
        targetWorldPosition.z = _visualZ;
        _targetPosition = targetWorldPosition;
        _followEnabled = true;
    }

    public void BeginAim() => _followEnabled = false;

    public void CancelAim()
    {
        if (CurrentSlot != null)
            MoveTo(CurrentSlot.transform.position);
        else
            _followEnabled = true;
    }

    public void SetAimPosition(Vector2 worldPosition) =>
        SetWorldPosition(worldPosition);

    public void BeginFlight() => _followEnabled = false;

    public void SetFlightPosition(Vector2 worldPosition) =>
        SetWorldPosition(worldPosition);

    public void EndFlight()
    {
        _targetPosition = transform.position;
        _followEnabled = true;
    }

    private void SetWorldPosition(Vector2 worldPosition)
    {
        transform.position = new Vector3(
            worldPosition.x,
            worldPosition.y,
            _visualZ
        );
    }

    private void Update()
    {
        if (!_followEnabled)
            return;

        transform.position = Vector3.Lerp(
            transform.position,
            _targetPosition,
            _followSpeed * Time.deltaTime
        );
    }

    public void OnInspect()
    {
    }

    public void OnEject()
    {
    }

    public PipeObject MergeWith(PipeObject other)
    {
        PipeSlot slot = CurrentSlot;
        Vector3 spawnPos = transform.position;

        if (slot != null)
            slot.ClearOccupant();

        if (other != null && other.CurrentSlot != null)
            other.CurrentSlot.ClearOccupant();

        if (hardcodedMerge == null)
        {
            Debug.LogError("Hardcoded merge is not set");
            Destroy(gameObject);
            if (other != null)
                Destroy(other.gameObject);
            return null;
        }

        PipeObject merged = Instantiate(
            hardcodedMerge,
            spawnPos,
            Quaternion.identity
        );

        if (slot != null)
        {
            slot.SetOccupant(merged);
            merged.MoveTo(slot.transform.position);
        }

        if (other != null)
            Destroy(other.gameObject);

        Destroy(gameObject);
        return merged;
    }
}
