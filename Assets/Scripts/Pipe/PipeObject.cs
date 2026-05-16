using UnityEngine;
using UnityEngine.InputSystem;

public class PipeObject : MonoBehaviour
{
    public PipeObjectData Data;
    public PipeSlot CurrentSlot;

    [SerializeField] private float _followSpeed = 8f;
    // [SerializeField] private float _moveScale = 1.05f;
    // [SerializeField] private float _idleScale = 1f;

    private Vector2 _targetPosition;
    private float _visualZ;

    private Vector2 _dragStart;

    [SerializeField] private PipelineController _pipeline;

    private void Awake()
    {
        _pipeline = FindAnyObjectByType<PipelineController>();

        _visualZ = transform.position.z;
        _targetPosition = transform.position;
    }

    public void MoveTo(Vector3 targetWorldPosition)
    {
        targetWorldPosition.z = _visualZ;
        _targetPosition = targetWorldPosition;

        // transform.localScale = Vector3.one * _moveScale;
    }

    private void Update()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            _targetPosition,
            _followSpeed * Time.deltaTime
        );

        // transform.localScale = Vector3.Lerp(
        //     transform.localScale,
        //     Vector3.one * _idleScale,
        //     10f * Time.deltaTime
        // );
    }

    private void OnMouseDown()
    {
        _dragStart = Pointer.current.position.ReadValue();
    }

    private void OnMouseUp()
    {
        Vector2 delta =
            Pointer.current.position.ReadValue() - _dragStart;

        if (delta.y > 50f)
        {
            _pipeline.TryMoveObjectBackward(this, 2);
        }
        else if (delta.y < -50f)
        {
            if (CurrentSlot != null)
            {
                CurrentSlot.ClearOccupant();
            }

            Destroy(gameObject);
        }
    }

    public void OnInspect()
    {
    }

    public void OnEject()
    {
    }

    public void MergeWith(PipeObject other)
    {
    }
}