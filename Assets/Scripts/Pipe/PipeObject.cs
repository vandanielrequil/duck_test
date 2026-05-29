using UnityEngine;

public class PipeObject : MonoBehaviour
{
    public PipeObjectState State { get; private set; }
    public PipeSlot CurrentSlot;

    public int Duckiness =>
        State != null ? State.Duckiness : 0;

    private const float FallbackFollowSpeed = 8f;
    private Vector2 _targetPosition;
    private float _visualZ;
    private bool _followEnabled = true;
    private SpriteRenderer _spriteRenderer;
    private GameObject _visualInstance;

    private void Awake()
    {
        _visualZ = transform.position.z;
        _targetPosition = transform.position;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void Initialize(
        PipeObjectData data
    )
    {
        State = new PipeObjectState(data);

        ApplyVisual();
    }

    public void Initialize(PipeObjectState state)
    {
        State = state;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (_visualInstance != null)
            Destroy(_visualInstance);

        if (_spriteRenderer == null)
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        GameObject visualPrefab = State?.GetVisualPrefab();

        if (visualPrefab == null)
        {
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = true;

            return;
        }

        if (_spriteRenderer != null)
            _spriteRenderer.enabled = false;

        _visualInstance = Instantiate(
            visualPrefab,
            transform
        );
        _visualInstance.transform.localPosition = Vector3.zero;
        _visualInstance.transform.localRotation = Quaternion.identity;
        _visualInstance.transform.localScale = Vector3.one;
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
            GetFollowSpeed() * Time.deltaTime
        );
    }

    private float GetFollowSpeed() =>
        State?.ObjectData != null
            ? Mathf.Max(0f, State.ObjectData.FollowSpeed)
            : FallbackFollowSpeed;

    public void OnInspect()
    {
    }

    public void OnEject()
    {
    }

    public bool TryApplyModifier(
        PipeObject modifier,
        out PipeObjectData inputA,
        out PipeObjectData inputB
    )
    {
        inputA = State?.ObjectData;
        inputB = modifier?.State?.ObjectData;

        if (State == null || modifier?.State?.ObjectData == null)
            return false;

        if (!State.TryAddModifier(
                modifier.State.ObjectData.ModifierType,
                inputA,
                inputB,
                out PipeObjectState next
            ))
            return false;

        Initialize(next);
        return true;
    }
}

public class PipeObjectState
{
    public PipeObjectData ObjectData { get; }
    public PipeObjectData MergeInputA { get; }
    public PipeObjectData MergeInputB { get; }
    public bool HasPaint { get; }
    public bool HasKit { get; }

    public int Duckiness
    {
        get
        {
            if (ObjectData == null)
                return 0;

            if (ObjectData.Archetype != PipeArchetype.Fake)
                return ObjectData.BaseDuckiness;

            int modifiers = 0;
            if (HasPaint) modifiers++;
            if (HasKit) modifiers++;
            return modifiers;
        }
    }

    public PipeObjectState(PipeObjectData data)
        : this(data, null, null, false, false)
    {
    }

    private PipeObjectState(
        PipeObjectData data,
        PipeObjectData mergeInputA,
        PipeObjectData mergeInputB,
        bool hasPaint,
        bool hasKit
    )
    {
        ObjectData = data;
        MergeInputA = mergeInputA;
        MergeInputB = mergeInputB;
        HasPaint = hasPaint;
        HasKit = hasKit;
    }

    public bool TryAddModifier(
        PipeModifierType modifier,
        PipeObjectData inputA,
        PipeObjectData inputB,
        out PipeObjectState next
    )
    {
        next = null;

        if (ObjectData == null || ObjectData.Archetype != PipeArchetype.Fake)
            return false;

        bool hasPaint = HasPaint;
        bool hasKit = HasKit;

        switch (modifier)
        {
            case PipeModifierType.Paint:
                if (hasPaint)
                    return false;
                hasPaint = true;
                break;

            case PipeModifierType.Kit:
                if (hasKit)
                    return false;
                hasKit = true;
                break;

            default:
                return false;
        }

        next = new PipeObjectState(
            ObjectData,
            inputA,
            inputB,
            hasPaint,
            hasKit
        );
        return true;
    }

    public GameObject GetVisualPrefab()
    {
        if (ObjectData == null)
            return null;

        if (ObjectData.Archetype != PipeArchetype.Fake)
            return ObjectData.DisplayVisualPrefab;

        if (HasPaint && HasKit)
            return ObjectData.FakePaintKitVisualPrefab != null
                ? ObjectData.FakePaintKitVisualPrefab
                : ObjectData.DisplayVisualPrefab;

        if (HasPaint)
            return ObjectData.FakePaintVisualPrefab != null
                ? ObjectData.FakePaintVisualPrefab
                : ObjectData.DisplayVisualPrefab;

        if (HasKit)
            return ObjectData.FakeKitVisualPrefab != null
                ? ObjectData.FakeKitVisualPrefab
                : ObjectData.DisplayVisualPrefab;

        return ObjectData.FakeVisualPrefab != null
            ? ObjectData.FakeVisualPrefab
            : ObjectData.DisplayVisualPrefab;
    }
}
