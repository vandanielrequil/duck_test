using UnityEngine;

public class PipeObject : MonoBehaviour
{
    public PipeObjectData Data;
    public PipeSlot CurrentSlot;
    public float FollowSpeed = 8f;
    // public bool IsMoving;

    private Vector3 _targetPosition;

    private float _visualZ;
    // private Rigidbody2D _rigidbody2D;

    private void Awake()
    {
        _visualZ = transform.position.z;
        // _rigidbody2D = GetComponent<Rigidbody2D>();
    }

    public void MoveTo(Vector3 targetWorldPosition)
    {
        targetWorldPosition.z = _visualZ;
        // if (_rigidbody2D != null)
        // {
        //     _rigidbody2D.MovePosition(targetWorldPosition);
        //     _rigidbody2D.linearVelocity = Vector2.zero;
        //     _rigidbody2D.angularVelocity = 0f;
        // }
        // else
        // {
        _targetPosition = targetWorldPosition;
        // }
    }

    private void Update()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            _targetPosition,
            FollowSpeed * Time.deltaTime
        );
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
