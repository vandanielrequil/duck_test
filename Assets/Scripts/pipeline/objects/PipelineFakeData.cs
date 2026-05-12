using UnityEngine;

[CreateAssetMenu(fileName = "PipelineFakeData", menuName = "Pipeline/Pipeline Fake Data")]
public class PipelineFakeData : ScriptableObject
{
    [Header("Диапазоны для случайной раскачки при спавне этого типа")]
    public float swayAmpYMin = 0.04f;
    public float swayAmpYMax = 0.14f;

    public float swayRollDegMin = 1.5f;
    public float swayRollDegMax = 5f;

    [Min(0.05f)] public float swayFreqMin = 1.2f;
    [Min(0.05f)] public float swayFreqMax = 3.5f;

    private void OnValidate()
    {
        if (swayAmpYMax < swayAmpYMin)
            swayAmpYMax = swayAmpYMin;
        if (swayRollDegMax < swayRollDegMin)
            swayRollDegMax = swayRollDegMin;
        if (swayFreqMax < swayFreqMin)
            swayFreqMax = swayFreqMin;
    }
}
