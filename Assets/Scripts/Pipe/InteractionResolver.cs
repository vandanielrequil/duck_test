using UnityEngine;

public enum InteractionOutcome
{
    None,
    Merged,
    Swapped,
    Rejected,
    Ejected,
}

public struct InteractionResult
{
    public InteractionOutcome Outcome;
    public PipeObjectData ResultData;
}

public class InteractionResolver : MonoBehaviour
{
    public InteractionResult Resolve(PipeObject a, PipeObject b)
    {
        if (a == null || b == null || a.Data == null || b.Data == null)
            return new InteractionResult { Outcome = InteractionOutcome.Rejected };

        return new InteractionResult { Outcome = InteractionOutcome.Rejected };
    }
}
