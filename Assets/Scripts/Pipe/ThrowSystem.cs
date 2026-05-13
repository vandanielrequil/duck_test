using UnityEngine;

public class ThrowSystem : MonoBehaviour
{
    [SerializeField] private InteractionResolver _resolver;
    [SerializeField] private PipelineController _pipeline;

    public InteractionResult SimulateImpact(PipeObject thrown, PipeObject target)
    {
        if (_resolver == null)
            return new InteractionResult { Outcome = InteractionOutcome.None };
        return _resolver.Resolve(thrown, target);
    }

    public PipelineController Pipeline => _pipeline;
}
