using UnityEngine;

/// <summary>
/// Rail for the batter. Assign left / right bounds on the movement line.
/// </summary>
public class BatterMovementAxis : MonoBehaviour
{
    [SerializeField] private Transform _left;
    [SerializeField] private Transform _right;

    public Vector2 Left => _left != null ? (Vector2)_left.position : (Vector2)transform.position;
    public Vector2 Right => _right != null ? (Vector2)_right.position : (Vector2)transform.position;

    public Vector2 Tangent
    {
        get
        {
            Vector2 delta = Right - Left;
            return delta.sqrMagnitude > 0.0001f
                ? delta.normalized
                : Vector2.right;
        }
    }

    public float Length => Vector2.Distance(Left, Right);

    public Vector2 ClosestPointOnAxis(Vector2 worldPoint)
    {
        Vector2 delta = Right - Left;
        if (delta.sqrMagnitude < 0.0001f)
            return Left;

        float t = Vector2.Dot(worldPoint - Left, delta) / delta.sqrMagnitude;
        t = Mathf.Clamp01(t);
        return Left + delta * t;
    }

    public Vector2 ClampOnAxis(Vector2 worldPoint) =>
        ClosestPointOnAxis(worldPoint);

    /// <summary>
    /// Positive when the point is below the movement line (lower world Y than the rail).
    /// </summary>
    public float SignedDistanceBelow(Vector2 worldPoint)
    {
        Vector2 onAxis = ClosestPointOnAxis(worldPoint);
        return onAxis.y - worldPoint.y;
    }

    public bool IsBelowAxis(Vector2 worldPoint, float threshold = 0.05f) =>
        SignedDistanceBelow(worldPoint) > threshold;

    private void OnDrawGizmos()
    {
        if (_left == null || _right == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(_left.position, _right.position);
        Gizmos.DrawSphere(_left.position, 0.12f);
        Gizmos.DrawSphere(_right.position, 0.12f);
    }
}
