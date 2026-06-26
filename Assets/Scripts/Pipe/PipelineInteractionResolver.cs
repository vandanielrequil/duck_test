using System.Collections;
using UnityEngine;

public class PipelineInteractionResolver : MonoBehaviour
{
    [SerializeField] private PipelineController _pipeline;

    private void Awake()
    {
        if (_pipeline == null)
            _pipeline = FindAnyObjectByType<PipelineController>();
    }

    public InteractionResult ResolveInteraction(
        PipeObject movingObject,
        PipeSlot targetSlot
    )
    {
        if (movingObject == null || targetSlot == null)
            return InteractionResult.None;

        PipeObject targetObject = targetSlot.OccupiedObject;
        if (targetObject == movingObject)
            targetObject = null;

        if (targetObject == null)
        {
            MoveObjectToSlot(movingObject, targetSlot);
            return InteractionResult.Move;
        }

        InteractionResult kind =
            DetermineInteraction(movingObject, targetObject);

        Debug.Log($"Interaction: {kind}");

        switch (kind)
        {
            case InteractionResult.Merge:
                MergeObjects(movingObject, targetObject);
                break;

            case InteractionResult.Shove:
                ShoveObject(targetObject);
                MoveObjectToSlot(movingObject, targetSlot);
                break;

            case InteractionResult.Move:
                MoveTargetBehind(targetObject);
                MoveObjectToSlot(movingObject, targetSlot);
                break;

            case InteractionResult.Bounce:
                BounceObject(movingObject);
                break;
        }

        return kind;
    }

    public bool TryGetDuckPaintSlideContinuation(
        PipeObject movingObject,
        PipeSlot landingSlot,
        int previousSlideHits,
        out PipeSlot nextSlot,
        out int flyDistance
    )
    {
        nextSlot = null;
        flyDistance = 0;

        if (movingObject == null
            || landingSlot == null
            || _pipeline == null)
            return false;

        PipeObject blocker = landingSlot.OccupiedObject;
        if (blocker == null || blocker == movingObject)
            return false;

        if (!IsDuckPaintPair(movingObject, blocker))
            return false;

        flyDistance = previousSlideHits == 0
            ? GetWeight(movingObject)
            : 1;

        int direction = GetThrowDirection(movingObject);
        if (!TryGetSlideTargetSlot(
                landingSlot.Index,
                flyDistance,
                direction,
                out nextSlot,
                out flyDistance
            ))
            return false;

        Debug.Log(
            $"Interaction: Duck/Paint slide #{previousSlideHits + 1}, "
            + $"distance={flyDistance}, direction={direction}"
        );
        return true;
    }

    private bool TryGetSlideTargetSlot(
        int fromIndex,
        int requestedDistance,
        int direction,
        out PipeSlot targetSlot,
        out int actualDistance
    )
    {
        targetSlot = null;
        actualDistance = 0;

        if (_pipeline?.Slots == null || requestedDistance <= 0)
            return false;

        int slotsCount = _pipeline.Slots.Count;
        int targetIndex = fromIndex;

        for (int step = 0; step < requestedDistance; step++)
        {
            int nextIndex = targetIndex + direction;
            if (nextIndex < 0 || nextIndex >= slotsCount)
                break;

            targetIndex = nextIndex;
            actualDistance++;
        }

        if (actualDistance <= 0 || targetIndex == fromIndex)
            return false;

        targetSlot = _pipeline.Slots[targetIndex];
        return targetSlot != null;
    }

    public InteractionResult ResolveHomerun(
        PipeObject movingObject,
        bool toLeft
    )
    {
        if (movingObject == null)
            return InteractionResult.None;

        if (movingObject.CurrentSlot != null)
            movingObject.CurrentSlot.ClearOccupant();

        InteractionResult kind = toLeft
            ? InteractionResult.HomerunLeft
            : InteractionResult.HomerunRight;

        Debug.Log($"Interaction: ResolveHomerun {kind}");

        Destroy(movingObject.gameObject);
        return kind;
    }

    private InteractionResult DetermineInteraction(
        PipeObject moving,
        PipeObject target
    )
    {
        if (moving.State?.ObjectData == null
            || target.State?.ObjectData == null)
            return InteractionResult.Bounce;

        PipeArchetype m = moving.State.ObjectData.Archetype;
        PipeArchetype t = target.State.ObjectData.Archetype;

        if (CanMerge(moving, target))
            return InteractionResult.Merge;

        if (IsWeightBasedPair(m, t))
        {
            Debug.Log("DetermineInteraction - WeightBasedPair debug");
            return ResolveWeightBased(moving, target);
        }

        if (IsShovePair(m, t))
        {
            Debug.Log("DetermineInteraction - ShovePair debug");
            return ResolveWeightBased(moving, target);
        }

        if (IsDuckPaintPair(moving, target))
            return ResolveWeightBased(moving, target);

        Debug.Log("DetermineInteraction - Bounce debug");
        return InteractionResult.Bounce;
    }

    private static bool IsMergePair(
        PipeArchetype moving,
        PipeArchetype target
    )
    {
        return (moving == PipeArchetype.Fake
                && target == PipeArchetype.Modifier)
            || (moving == PipeArchetype.Modifier
                && target == PipeArchetype.Fake);
    }

    private static bool IsShovePair(
        PipeArchetype moving,
        PipeArchetype target
    )
    {
        return (moving == PipeArchetype.Duck
                && target == PipeArchetype.Modifier)
            || (moving == PipeArchetype.Duck
                && target == PipeArchetype.Duck)
            || (moving == PipeArchetype.Modifier
                && target == PipeArchetype.Modifier)
            || (moving == PipeArchetype.Fake
                && target == PipeArchetype.Modifier)
            || (moving == PipeArchetype.Fake
                && target == PipeArchetype.Duck);
    }

    private static bool IsWeightBasedPair(
        PipeArchetype moving,
        PipeArchetype target
    )
    {
        return (moving == PipeArchetype.Fake
                && target == PipeArchetype.Duck)
            || (moving == PipeArchetype.Duck
                && target == PipeArchetype.Fake)
            || (moving == PipeArchetype.Fake
                && target == PipeArchetype.Fake);
    }

    private InteractionResult ResolveWeightBased(
        PipeObject moving,
        PipeObject target
    )
    {
        int movingWeight = GetWeight(moving);
        int targetWeight = GetWeight(target);

        if (movingWeight < targetWeight) {
            Debug.Log($"Shove debug {movingWeight} > {targetWeight}");
            return InteractionResult.Shove;
        }

        if (movingWeight > targetWeight)
        {
            Debug.Log($"Bounce debug {movingWeight} < {targetWeight}");
            return InteractionResult.Bounce;
        }

        if (HasEmptySlotBehind(target))
            return InteractionResult.Move;

        return InteractionResult.Shove;
    }

    private static int GetWeight(PipeObject obj) =>
        Mathf.Max(1, obj?.State?.ObjectData?.Weight ?? 1);

    private static int GetThrowDirection(PipeObject obj) =>
        obj?.State?.ObjectData?.Archetype == PipeArchetype.Duck ? -1 : 1;

    private static bool IsPaint(PipeObject obj) =>
        obj?.State?.ObjectData?.Archetype == PipeArchetype.Modifier
        && obj.State.ObjectData.ModifierType == PipeModifierType.Paint;

    private static bool IsDuckPaintPair(
        PipeObject moving,
        PipeObject target
    )
    {
        if (moving?.State?.ObjectData == null
            || target?.State?.ObjectData == null)
            return false;

        bool movingIsDuck =
            moving.State.ObjectData.Archetype == PipeArchetype.Duck;
        bool targetIsDuck =
            target.State.ObjectData.Archetype == PipeArchetype.Duck;

        return (movingIsDuck && IsPaint(target))
            || (IsPaint(moving) && targetIsDuck);
    }

    private bool HasEmptySlotBehind(PipeObject target)
    {
        if (_pipeline == null || target.CurrentSlot == null)
            return false;

        int behindIndex = target.CurrentSlot.Index + 1;

        if (behindIndex >= _pipeline.Slots.Count)
            return false;

        return _pipeline.Slots[behindIndex].OccupiedObject == null;
    }

    private void MoveTargetBehind(PipeObject target)
    {
        if (_pipeline == null || target.CurrentSlot == null)
            return;

        int behindIndex = target.CurrentSlot.Index + 1;

        if (behindIndex >= _pipeline.Slots.Count)
            return;

        PipeSlot behindSlot = _pipeline.Slots[behindIndex];

        if (behindSlot.OccupiedObject != null)
            return;

        PipeSlot fromSlot = target.CurrentSlot;
        fromSlot.ClearOccupant();
        behindSlot.SetOccupant(target);
        target.MoveTo(behindSlot.transform.position);
    }

    private static void MoveObjectToSlot(
        PipeObject obj,
        PipeSlot slot
    )
    {
        if (obj.CurrentSlot != null)
            obj.CurrentSlot.ClearOccupant();

        slot.SetOccupant(obj);
        obj.MoveTo(slot.transform.position);
    }

    private void MergeObjects(
        PipeObject moving,
        PipeObject target
    )
    {
        if (!TryGetFakeAndModifier(
                moving,
                target,
                out PipeObject fake,
                out PipeObject modifier
            ))
        {
            BounceObject(moving);
            return;
        }

        PipeSlot resultSlot = target.CurrentSlot;

        if (!fake.TryApplyModifier(
                modifier,
                out _,
                out _
            ))
        {
            BounceObject(moving);
            return;
        }

        if (fake.CurrentSlot != null)
            fake.CurrentSlot.ClearOccupant();

        if (modifier.CurrentSlot != null)
            modifier.CurrentSlot.ClearOccupant();

        if (resultSlot != null)
        {
            resultSlot.SetOccupant(fake);
            fake.MoveTo(resultSlot.transform.position);
        }

        Destroy(modifier.gameObject);
    }

    private static bool CanMerge(
        PipeObject moving,
        PipeObject target
    ) =>
        TryGetFakeAndModifier(
            moving,
            target,
            out PipeObject fake,
            out PipeObject modifier
        )
        && fake.State.TryAddModifier(
            modifier.State.ObjectData.ModifierType,
            fake.State.ObjectData,
            modifier.State.ObjectData,
            out _
        );

    private static bool TryGetFakeAndModifier(
        PipeObject a,
        PipeObject b,
        out PipeObject fake,
        out PipeObject modifier
    )
    {
        fake = null;
        modifier = null;

        if (a?.State?.ObjectData == null
            || b?.State?.ObjectData == null)
            return false;

        if (a.State.ObjectData.Archetype == PipeArchetype.Fake
            && b.State.ObjectData.Archetype == PipeArchetype.Modifier)
        {
            fake = a;
            modifier = b;
            return true;
        }

        if (b.State.ObjectData.Archetype == PipeArchetype.Fake
            && a.State.ObjectData.Archetype == PipeArchetype.Modifier)
        {
            fake = b;
            modifier = a;
            return true;
        }

        return false;
    }

    private void ShoveObject(PipeObject target)
    {
        if (target.CurrentSlot != null)
            target.CurrentSlot.ClearOccupant();

        Destroy(target.gameObject);
    }

    private void BounceObject(PipeObject obj)
    {
        if (obj.CurrentSlot != null)
            obj.CurrentSlot.ClearOccupant();

        if (_pipeline != null)
            _pipeline.IsPaused = true;

        obj.MoveTo(obj.transform.position + Vector3.down);
        StartCoroutine(DestroyAfterBounce(obj, 0.35f));
    }

    private IEnumerator DestroyAfterBounce(
        PipeObject obj,
        float delay
    )
    {
        yield return new WaitForSeconds(delay);

        if (obj != null)
            Destroy(obj.gameObject);

        if (_pipeline != null)
            _pipeline.IsPaused = false;
    }
}

public enum InteractionResult
{
    None,
    Move,
    Merge,
    Shove,
    Bounce,
    HomerunLeft,
    HomerunRight
}
