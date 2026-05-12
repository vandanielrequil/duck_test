using UnityEngine;

public sealed class PipelineDuck : PipelineObject
{
    private static readonly float s_ampY = 0.055f;
    private static readonly float s_rollDeg = 2.2f;
    private static readonly float s_freq = 1.75f;

    private float _phase;

    protected override void Awake()
    {
        base.Awake();
        _phase = Random.Range(0f, Mathf.PI * 2f);
    }

    protected override bool HasSway => true;

    protected override Vector2 EvaluateSway(float time, out float rollDegreesZ)
    {
        float w = time * s_freq + _phase;
        rollDegreesZ = Mathf.Sin(w * 0.88f) * s_rollDeg;
        return new Vector2(0f, Mathf.Sin(w) * s_ampY);
    }
}
