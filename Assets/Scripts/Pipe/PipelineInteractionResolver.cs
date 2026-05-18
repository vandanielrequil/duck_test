using UnityEngine;

public class PipelineInteractionResolver : MonoBehaviour
{
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

    private static InteractionResult DetermineInteraction(
        PipeObject moving,
        PipeObject target
    )
    {
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

    private static InteractionResult ResolveWeightBased(
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

    private static bool HasEmptySlotBehind(PipeObject target)
    {
        if (target.CurrentSlot == null)
            return false;

        PipelineController pipeline =
            FindAnyObjectByType<PipelineController>();

        if (pipeline == null)
            return false;

        int behindIndex = target.CurrentSlot.Index + 1;

        if (behindIndex >= pipeline.Slots.Count)
            return false;

        return pipeline.Slots[behindIndex].OccupiedObject == null;
    }

    private void MoveTargetBehind(PipeObject target)
    {
        if (target.CurrentSlot == null)
            return;

        PipelineController pipeline =
            FindAnyObjectByType<PipelineController>();

        if (pipeline == null)
            return;

        int behindIndex = target.CurrentSlot.Index + 1;

        if (behindIndex >= pipeline.Slots.Count)
            return;

        PipeSlot behindSlot =
            pipeline.Slots[behindIndex] as PipeSlot;

        if (behindSlot == null
            || behindSlot.OccupiedObject != null)
            return;

        PipeSlot fromSlot = target.CurrentSlot;
        fromSlot.ClearOccupant();
        behindSlot.SetOccupant(target);
        target.MoveTo(behindSlot.transform.position);
    }

    private void MoveObjectToSlot(
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
        Vector3 bouncePosition =
            obj.transform.position + Vector3.down;

        obj.MoveTo(bouncePosition);

        if (obj.CurrentSlot != null)
            obj.CurrentSlot.ClearOccupant();

        Destroy(obj.gameObject);
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
