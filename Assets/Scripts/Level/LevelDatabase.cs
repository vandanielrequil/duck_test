using UnityEngine;

[CreateAssetMenu(
    fileName = "Campaign",
    menuName = "Level/Level Database"
)]
public class LevelDatabase : ScriptableObject
{
    public LevelConfig[] Levels;
}
