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

        // empty slot
        if (targetObject == null)
        {
            MoveObjectToSlot(movingObject, targetSlot);
            return InteractionResult.Move;
        }

        if (CanMerge(movingObject, targetObject))
        {
            MergeObjects(movingObject, targetObject);
            return InteractionResult.Merge;
        }

        if (CanShove(movingObject, targetObject))
        {
            ShoveObject(targetObject);

            MoveObjectToSlot(movingObject, targetSlot);

            return InteractionResult.Shove;
        }

        BounceObject(movingObject);

        return InteractionResult.Bounce;
    }

    private void MoveObjectToSlot(
        PipeObject obj,
        PipeSlot slot
    )
    {
        if (obj.CurrentSlot != null)
        {
            obj.CurrentSlot.ClearOccupant();
        }

        slot.SetOccupant(obj);

        obj.MoveTo(slot.transform.position);
    }

    private bool CanMerge(
        PipeObject moving,
        PipeObject target
    )
    {
        return moving.Data.Tags.HasFlag(
                PipeObjectTag.Paint
            )
            &&
            target.Data.Archetype ==
            PipeArchetype.Fake;
    }

    private void MergeObjects(
        PipeObject moving,
        PipeObject target
    )
    {
        target.MergeWith(moving);

        if (moving.CurrentSlot != null)
        {
            moving.CurrentSlot.ClearOccupant();
        }

        Destroy(moving.gameObject);
    }

    private bool CanShove(
        PipeObject moving,
        PipeObject target
    )
    {
        return moving.Data.Weight >
               target.Data.Weight;
    }

    private void ShoveObject(PipeObject target)
    {
        if (target.CurrentSlot != null)
        {
            target.CurrentSlot.ClearOccupant();
        }

        Destroy(target.gameObject);
    }

    private void BounceObject(PipeObject obj)
    {
        Vector3 bouncePosition =
            obj.transform.position +
            Vector3.down * 1f;

        obj.MoveTo(bouncePosition);
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