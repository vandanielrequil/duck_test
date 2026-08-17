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
    private SpriteRenderer _poseRenderer;
    private GameObject _visualInstance;
    private float _lastClickTime = float.NegativeInfinity;
    private PipeObjectPose _pose = PipeObjectPose.Idle;

    public bool IsClickOnCooldown =>
        State?.ObjectData != null
        && State.ObjectData.ClickCooldown > 0f
        && Time.time - _lastClickTime < State.ObjectData.ClickCooldown;

    public void RegisterClick() => _lastClickTime = Time.time;

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
        _pose = PipeObjectPose.Idle;
        ApplyVisual();
    }

    public void Initialize(PipeObjectState state)
    {
        State = state;
        _pose = PipeObjectPose.Idle;
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

            _poseRenderer = _spriteRenderer;
            ApplyCurrentPose();
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
        _poseRenderer = _visualInstance.GetComponentInChildren<SpriteRenderer>();
        ApplyCurrentPose();
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

    public void BeginFlight()
    {
        _followEnabled = false;
        SetPose(PipeObjectPose.Fly);
    }

    public void NotifyFlightApex()
    {
        if (_pose == PipeObjectPose.Fly)
            SetPose(PipeObjectPose.Landing);
    }

    public void SetFlightPosition(Vector2 worldPosition) =>
        SetWorldPosition(worldPosition);

    public void EndFlight()
    {
        _targetPosition = transform.position;
        _followEnabled = true;
        SetPose(PipeObjectPose.Idle);
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
        SetPose(PipeObjectPose.Happy);
    }

    public void PlaySurprised()
    {
        SetPose(PipeObjectPose.Surprised);
    }

    private void SetPose(PipeObjectPose pose)
    {
        _pose = pose;
        ApplyCurrentPose();
    }

    private void ApplyCurrentPose()
    {
        if (_poseRenderer == null)
            return;

        Sprite sprite = ResolvePoseSprite(_pose);
        if (sprite != null)
            _poseRenderer.sprite = sprite;
    }

    private Sprite ResolvePoseSprite(PipeObjectPose pose)
    {
        PipeObjectData data = State?.ObjectData;
        if (data == null)
            return null;

        switch (pose)
        {
            case PipeObjectPose.Fly:
                return data.FlySprite != null ? data.FlySprite : data.IdleSprite;
            case PipeObjectPose.Landing:
                return data.LandingSprite != null
                    ? data.LandingSprite
                    : data.IdleSprite;
            case PipeObjectPose.Happy:
                return PickRandomSprite(data.HappySprites) ?? data.IdleSprite;
            case PipeObjectPose.Surprised:
                return PickRandomSprite(data.SurprisedSprites) ?? data.IdleSprite;
            default:
                return data.IdleSprite;
        }
    }

    private static Sprite PickRandomSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        int count = 0;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                count++;
        }

        if (count == 0)
            return null;

        int pick = Random.Range(0, count);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
                continue;
            if (pick == 0)
                return sprites[i];
            pick--;
        }

        return null;
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
    public PipeObjectData MergeInputC { get; }
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
        : this(data, null, null, null, false, false)
    {
    }

    private PipeObjectState(
        PipeObjectData data,
        PipeObjectData mergeInputA,
        PipeObjectData mergeInputB,
        PipeObjectData mergeInputC,
        bool hasPaint,
        bool hasKit
    )
    {
        ObjectData = data;
        MergeInputA = mergeInputA;
        MergeInputB = mergeInputB;
        MergeInputC = mergeInputC;
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

        PipeObjectData modifierData = inputB;
        PipeObjectData mergeA;
        PipeObjectData mergeB;
        PipeObjectData mergeC;

        if (MergeInputA == null && MergeInputB == null)
        {
            mergeA = inputA;
            mergeB = modifierData;
            mergeC = null;
        }
        else if (MergeInputC == null)
        {
            mergeA = MergeInputA;
            mergeB = MergeInputB;
            mergeC = modifierData;
        }
        else
            return false;

        next = new PipeObjectState(
            ObjectData,
            mergeA,
            mergeB,
            mergeC,
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

public enum PipeObjectPose
{
    Idle,
    Fly,
    Landing,
    Happy,
    Surprised,
}
