using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public interface ILevelResultsActions
{
    void Next();
    void Retry();
    void Menu();
}

public interface ILevelResultsFacade
{
    void Bind(ILevelResultsActions actions);
    void Show(LevelResultsSnapshot snapshot, bool canGoNext);
    void Hide();
}

public abstract class LevelResultsFacadeBase : MonoBehaviour, ILevelResultsFacade
{
    private ILevelResultsActions _actions;

    public virtual void Bind(ILevelResultsActions actions)
    {
        _actions = actions;
    }

    public abstract void Show(LevelResultsSnapshot snapshot, bool canGoNext);
    public abstract void Hide();

    public void Next() => _actions?.Next();
    public void Retry() => _actions?.Retry();
    public void Menu() => _actions?.Menu();
}

public class LevelResultsController : MonoBehaviour, ILevelResultsActions
{
    [SerializeField] private LevelResultsFacadeBase _resultsFacade;

    private Action _onNext;
    private Action _onRetry;
    private Action _onMenu;

    private void Awake()
    {
        ResolveFacade();
        _resultsFacade?.Bind(this);
        HideImmediate();
    }

    public void Show(
        LevelResultsSnapshot snapshot,
        bool canGoNext,
        Action onNext,
        Action onRetry,
        Action onMenu
    )
    {
        _onNext = onNext;
        _onRetry = onRetry;
        _onMenu = onMenu;

        Debug.Log($"[Results] {BuildTitle(snapshot)}\n{BuildBody(snapshot)}");

        if (_resultsFacade == null)
            ResolveFacade();

        if (_resultsFacade == null)
        {
            Debug.LogError(
                "[LevelResultsController] Cannot show results: facade is missing."
            );
            return;
        }

        _resultsFacade.Show(snapshot, canGoNext);
    }

    public void HideImmediate()
    {
        _resultsFacade?.Hide();
    }

    public void Next()
    {
        HideImmediate();
        Action callback = _onNext;
        ClearCallbacks();
        callback?.Invoke();
    }

    public void Retry()
    {
        HideImmediate();
        Action callback = _onRetry;
        ClearCallbacks();
        callback?.Invoke();
    }

    public void Menu()
    {
        HideImmediate();
        Action callback = _onMenu;
        ClearCallbacks();
        callback?.Invoke();
    }

    private void ClearCallbacks()
    {
        _onNext = null;
        _onRetry = null;
        _onMenu = null;
    }

    public static string BuildTitle(LevelResultsSnapshot snapshot)
    {
        return snapshot.Outcome == LevelEndOutcome.Success
            ? "Level Complete"
            : "Level Failed";
    }

    public static string BuildBody(LevelResultsSnapshot snapshot)
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine(snapshot.LevelName);
        lines.AppendLine($"Reason: {snapshot.Reason}");
        lines.AppendLine($"Approved: {snapshot.ObjectsApproved}");
        lines.AppendLine($"Actions: {snapshot.ActionsUsed}");
        lines.AppendLine($"Rating: {FormatRating(snapshot.Rating)}");
        lines.AppendLine();

        if (snapshot.GoalLines != null)
        {
            foreach (LevelGoalResultLine line in snapshot.GoalLines)
            {
                lines.AppendLine(
                    $"{(line.Complete ? "[x]" : "[ ]")} "
                    + $"{line.Description} ({line.Current}/{line.Required})"
                );
            }
        }

        return lines.ToString();
    }

    public static string BuildGoalProgressText(LevelGoalResultLine[] lines)
    {
        if (lines == null || lines.Length == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (LevelGoalResultLine line in lines)
        {
            sb.AppendLine(
                $"{(line.Complete ? "[x]" : "[ ]")} "
                + $"{line.Description} ({line.Current}/{line.Required})"
            );
        }

        return sb.ToString();
    }

    public static string BuildGoalProgressText(
        IReadOnlyList<LevelGoalRuntime> goals
    )
    {
        if (goals == null || goals.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (LevelGoalRuntime goal in goals)
        {
            sb.AppendLine(
                $"{(goal.IsComplete ? "[x]" : "[ ]")} "
                + $"{goal.GetDisplayDescription()} "
                + $"({goal.Current}/{goal.Config.RequiredAmount})"
            );
        }

        return sb.ToString();
    }

    public static string FormatRating(int rating) => $"{rating}/3";

    private void ResolveFacade()
    {
        if (_resultsFacade == null)
            _resultsFacade = FindAnyObjectByType<LevelResultsFacadeBase>();
    }
}
