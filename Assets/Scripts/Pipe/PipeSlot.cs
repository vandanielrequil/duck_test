using UnityEngine;

public class PipeSlot : MonoBehaviour
{
    public PipeObject OccupiedObject;
    [HideInInspector] public int Index;

    public void ClearOccupant()
    {
        if (OccupiedObject != null && OccupiedObject.CurrentSlot == this)
            OccupiedObject.CurrentSlot = null;
        OccupiedObject = null;
    }

    public void SetOccupant(PipeObject obj)
    {
        OccupiedObject = obj;
        if (obj != null)
            obj.CurrentSlot = this;
    }
}
