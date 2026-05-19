using UnityEngine;

public class PipeSlot : MonoBehaviour
{
    public PipeObject OccupiedObject;
    [HideInInspector] public int Index;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position, 0.2f);
    }

    public void ClearOccupant()
    {
        if (OccupiedObject != null && OccupiedObject.CurrentSlot == this)
            OccupiedObject.CurrentSlot = null;
        OccupiedObject = null;
    }

    public void SetOccupant(PipeObject obj)
    {
        if (OccupiedObject != null
            && OccupiedObject != obj
            && OccupiedObject.CurrentSlot == this)
            OccupiedObject.CurrentSlot = null;

        OccupiedObject = obj;

        if (obj != null)
            obj.CurrentSlot = this;
    }
}
