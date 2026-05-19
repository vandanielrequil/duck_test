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

    private InteractionResult DetermineInteraction(
        PipeObject moving,
        PipeObject target
    )
    {
        if (moving.Data == null || target.Data == null)
            return InteractionResult.Bounce;

        PipeArchetype m = moving.Data.Archetype;
        PipeArchetype t = target.Data.Archetype;

        if (IsMergePair(m, t))
            return InteractionResult.Merge;

        if (IsShovePair(m, t))
            return InteractionResult.Shove;

        if (IsWeightBasedPair(m, t))
            return ResolveWeightBased(moving, target);

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
                && target == PipeArchetype.Modifier);
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
        int movingWeight = moving.Data.Weight;
        int targetWeight = target.Data.Weight;

        if (movingWeight > targetWeight)
            return InteractionResult.Shove;

        if (movingWeight < targetWeight)
            return InteractionResult.Bounce;

        if (HasEmptySlotBehind(target))
            return InteractionResult.Move;

        return InteractionResult.Shove;
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

    private static void MergeObjects(
        PipeObject moving,
        PipeObject target
    )
    {
        target.MergeWith(moving);
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
    Bounce
}
