using System;
using UnityEngine;

public class LevelResultsScreen : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private UnityEngine.UI.Text _titleText;
    [SerializeField] private UnityEngine.UI.Text _bodyText;
    [SerializeField] private UnityEngine.UI.Button _continueButton;

    private Action _onContinue;

    private void Awake()
    {
        if (_continueButton != null)
            _continueButton.onClick.AddListener(HandleContinue);

        HideImmediate();
    }

    public void Show(LevelResultsSnapshot snapshot, Action onContinue)
    {
        _onContinue = onContinue;

        if (_titleText != null)
        {
            _titleText.text = snapshot.Outcome == LevelEndOutcome.Success
                ? "Level Complete"
                : "Level Failed";
        }

        if (_bodyText != null)
            _bodyText.text = BuildBody(snapshot);

        Debug.Log($"[Results] {_titleText?.text}\n{BuildBody(snapshot)}");

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
        Action callback = _onContinue;
        _onContinue = null;
        callback?.Invoke();
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
