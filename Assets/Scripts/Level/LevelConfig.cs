using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelConfig",
    menuName = "Level/Level Config"
)]
public class LevelConfig : ScriptableObject
{
    [Header("Meta")]
    public string LevelId = "level_01";
    public string DisplayName = "Level 1";

    [Header("Pipeline")]
    public float MoveInterval = 1.5f;

    [Header("Spawn queue")]
    public SpawnEntry[] SpawnQueue = System.Array.Empty<SpawnEntry>();

    [Header("Goals")]
    public LevelGoalData[] Goals = System.Array.Empty<LevelGoalData>();

    [Header("Inspector")]
    public float BaseInspectionDuration = 5f;
    public int AngerThresholdTier1 = 3;
    public int AngerThresholdTier2 = 6;
    public int AngerMax = 6;

    [Header("End rules")]
    public LevelEndRules EndRules = LevelEndRules.Default;
}

[System.Serializable]
public struct SpawnEntry
{
    public PipeObjectData Object;
}
