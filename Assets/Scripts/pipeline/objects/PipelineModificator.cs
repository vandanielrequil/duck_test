using UnityEngine;

public class PipelineModificator : PipelineObject
{
    protected override bool HasSway => false;

    protected override Vector2 EvaluateSway(float time, out float rollDegreesZ)
    {
        rollDegreesZ = 0f;
        return Vector2.zero;
    }
}
