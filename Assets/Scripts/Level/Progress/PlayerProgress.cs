using UnityEngine;

public interface IProgressStore
{
    int CurrentLevelIndex { get; set; }
    bool IsLevelCompleted(string levelId);
    void CompleteLevel(string levelId, int nextLevelIndex);
    void ResetCampaign(string[] levelIds);
}

public static class PlayerProgress
{
    private static IProgressStore _store = new PlayerPrefsProgressStore();

    public static IProgressStore Store
    {
        get => _store;
        set => _store = value ?? new PlayerPrefsProgressStore();
    }

    public static int CurrentLevelIndex
    {
        get => _store.CurrentLevelIndex;
        set => _store.CurrentLevelIndex = value;
    }

    public static bool IsLevelCompleted(string levelId) =>
        _store.IsLevelCompleted(levelId);

    public static void CompleteLevel(string levelId, int nextLevelIndex) =>
        _store.CompleteLevel(levelId, nextLevelIndex);

    public static bool HasSaveData() => CurrentLevelIndex > 0;

    public static void ResetCampaign(LevelDatabase database)
    {
        if (database?.Levels == null || database.Levels.Length == 0)
        {
            _store.ResetCampaign(System.Array.Empty<string>());
            return;
        }

        var levelIds = new string[database.Levels.Length];
        for (int i = 0; i < database.Levels.Length; i++)
        {
            LevelConfig level = database.Levels[i];
            levelIds[i] = level != null ? level.LevelId : string.Empty;
        }

        _store.ResetCampaign(levelIds);
    }
}

public class PlayerPrefsProgressStore : IProgressStore
{
    private const string KeyHighestIndex = "progress_highest_index";
    private const string KeyCompletedPrefix = "progress_completed_";

    public int CurrentLevelIndex
    {
        get => PlayerPrefs.GetInt(KeyHighestIndex, 0);
        set
        {
            PlayerPrefs.SetInt(KeyHighestIndex, value);
            PlayerPrefs.Save();
        }
    }

    public bool IsLevelCompleted(string levelId) =>
        !string.IsNullOrEmpty(levelId)
        && PlayerPrefs.GetInt(KeyCompletedPrefix + levelId, 0) == 1;

    public void CompleteLevel(string levelId, int nextLevelIndex)
    {
        if (!string.IsNullOrEmpty(levelId))
        {
            PlayerPrefs.SetInt(KeyCompletedPrefix + levelId, 1);
        }

        CurrentLevelIndex = Mathf.Max(CurrentLevelIndex, nextLevelIndex);
        PlayerPrefs.Save();
    }

    public void ResetCampaign(string[] levelIds)
    {
        PlayerPrefs.DeleteKey(KeyHighestIndex);

        if (levelIds != null)
        {
            foreach (string levelId in levelIds)
            {
                if (!string.IsNullOrEmpty(levelId))
                    PlayerPrefs.DeleteKey(KeyCompletedPrefix + levelId);
            }
        }

        PlayerPrefs.Save();
    }
}
