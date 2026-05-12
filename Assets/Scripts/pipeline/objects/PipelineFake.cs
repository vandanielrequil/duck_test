using UnityEngine;

public class PipelineFake : PipelineObject
{
    [SerializeField] private PipelineFakeData _data;

    private float _ampY;
    private float _rollDeg;
    private float _freq;
    private float _phase;

    protected override void Awake()
    {
        base.Awake();
        RollSwayParameters();
    }

    public void RollSwayParameters()
    {
        if (_data != null)
        {
            _ampY = Random.Range(_data.swayAmpYMin, _data.swayAmpYMax);
            _rollDeg = Random.Range(_data.swayRollDegMin, _data.swayRollDegMax);
            _freq = Random.Range(_data.swayFreqMin, _data.swayFreqMax);
        }
        else
        {
            _ampY = Random.Range(0.05f, 0.12f);
            _rollDeg = Random.Range(2f, 5f);
            _freq = Random.Range(1.3f, 3f);
        }

        _phase = Random.Range(0f, Mathf.PI * 2f);
    }

    protected override bool HasSway => true;

    protected override Vector2 EvaluateSway(float time, out float rollDegreesZ)
    {
        float w = time * _freq + _phase;
        rollDegreesZ = Mathf.Sin(w * 0.9f) * _rollDeg;
        return new Vector2(0f, Mathf.Sin(w) * _ampY);
    }
}
