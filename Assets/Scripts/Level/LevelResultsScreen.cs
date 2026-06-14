using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class LevelResultsScreen : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _bodyText;
    [FormerlySerializedAs("_continueButton")]
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _menuButton;

    private Action _onNext;
    private Action _onRetry;
    private Action _onMenu;

    private void Awake()
    {
        if (_nextButton != null)
            _nextButton.onClick.AddListener(HandleNext);

        if (_retryButton != null)
            _retryButton.onClick.AddListener(HandleRetry);

        if (_menuButton != null)
            _menuButton.onClick.AddListener(HandleMenu);

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

        if (_titleText != null)
        {
            _titleText.text = snapshot.Outcome == LevelEndOutcome.Success
                ? "Level Complete"
                : "Level Failed";
        }

        if (_bodyText != null)
            _bodyText.text = BuildBody(snapshot);

        Debug.Log($"[Results] {_titleText?.text}\n{BuildBody(snapshot)}");

        SetButtonVisible(
            _nextButton,
            snapshot.Outcome == LevelEndOutcome.Success && canGoNext
        );
        SetButtonVisible(
            _retryButton,
            snapshot.Outcome == LevelEndOutcome.Fail
        );
        SetButtonVisible(_menuButton, true);

        if (_root != null)
            _root.SetActive(true);
        else
            gameObject.SetActive(true);
    }

    public void HideImmediate()
    {
        if (_root != null)
            _root.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private void HandleContinue()
    {
        HideImmediate();
        Action callback = _onNext;
        ClearCallbacks();
        callback?.Invoke();
    }

    private void HandleNext()
    {
        HideImmediate();
        Action callback = _onNext;
        ClearCallbacks();
        callback?.Invoke();
    }

    private void HandleRetry()
    {
        HideImmediate();
        Action callback = _onRetry;
        ClearCallbacks();
        callback?.Invoke();
    }

    private void HandleMenu()
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

    private static void SetButtonVisible(Button button, bool visible)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(visible);
    }

    private static string BuildBody(LevelResultsSnapshot snapshot)
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine(snapshot.LevelName);
        lines.AppendLine($"Reason: {snapshot.Reason}");
        lines.AppendLine($"Approved: {snapshot.ObjectsApproved}");
        lines.AppendLine($"Rage modes: {snapshot.RageModesTriggered}");
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
}
