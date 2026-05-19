using UnityEngine;

/// <summary>
/// Horizontal rail for the batter. Assign two scene transforms (left / right bounds).
/// </summary>
public class BatterMovementAxis : MonoBehaviour
{
    [SerializeField] private Transform _left;
    [SerializeField] private Transform _right;
    [Tooltip("A point on the side of the axis where the batter stands (defines \"below\").")]
    [SerializeField] private Transform _belowSideHint;

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

    public Vector2 BelowNormal
    {
        get
        {
            Vector2 n = new Vector2(-Tangent.y, Tangent.x);
            Vector2 mid = (Left + Right) * 0.5f;

            if (_belowSideHint != null)
            {
                Vector2 toHint = (Vector2)_belowSideHint.position - mid;
                if (Vector2.Dot(n, toHint) < 0f)
                    n = -n;
            }

            return n;
        }
    }

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

    /// <summary>Positive when the point is on the batter side of the rail.</summary>
    public float SignedDistanceBelow(Vector2 worldPoint)
    {
        Vector2 onAxis = ClosestPointOnAxis(worldPoint);
        Vector2 offset = worldPoint - onAxis;

        if (_belowSideHint != null)
            return Vector2.Dot(offset, BelowNormal);

        // Without a hint: treat lower world Y as "below" (batter under a horizontal rail).
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

        Vector3 mid = (_left.position + _right.position) * 0.5f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            mid,
            mid + (Vector3)(BelowNormal * 0.5f)
        );
    }
}
