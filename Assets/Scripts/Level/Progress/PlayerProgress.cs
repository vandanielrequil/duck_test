public interface IProgressStore
{
    int CurrentLevelIndex { get; set; }
    bool IsLevelCompleted(string levelId);
    void CompleteLevel(string levelId, int nextLevelIndex);
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
}

public class PlayerPrefsProgressStore : IProgressStore
{
    private const string KeyHighestIndex = "progress_highest_index";
    private const string KeyCompletedPrefix = "progress_completed_";

    public int CurrentLevelIndex
    {
        get => UnityEngine.PlayerPrefs.GetInt(KeyHighestIndex, 0);
        set
        {
            UnityEngine.PlayerPrefs.SetInt(KeyHighestIndex, value);
            UnityEngine.PlayerPrefs.Save();
        }
    }

    public bool IsLevelCompleted(string levelId) =>
        !string.IsNullOrEmpty(levelId)
        && UnityEngine.PlayerPrefs.GetInt(
            KeyCompletedPrefix + levelId,
            0
        ) == 1;

    public void CompleteLevel(string levelId, int nextLevelIndex)
    {
        if (!string.IsNullOrEmpty(levelId))
        {
            UnityEngine.PlayerPrefs.SetInt(
                KeyCompletedPrefix + levelId,
                1
            );
        }

        CurrentLevelIndex = UnityEngine.Mathf.Max(
            CurrentLevelIndex,
            nextLevelIndex
        );
        UnityEngine.PlayerPrefs.Save();
    }
}
