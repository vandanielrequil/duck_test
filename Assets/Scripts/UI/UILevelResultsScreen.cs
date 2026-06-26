using UnityEngine;
using UnityEngine.UIElements;

public class UILevelResultsScreen : LevelResultsFacadeBase
{
    [SerializeField] private UIDocument _document;

    [Header("Text element names")]
    [SerializeField] private string _titleName = "Title";
    [SerializeField] private string _bodyName = "Body";
    [SerializeField] private string _taskProgressName = "TaskProgress";
    [SerializeField] private string _actionsUsedName = "ActionsUsed";
    [SerializeField] private string _ratingName = "Rating";

    [Header("Button names")]
    [SerializeField] private string _nextButtonName = "Next";
    [SerializeField] private string _retryButtonName = "Retry";
    [SerializeField] private string _menuButtonName = "Menu";

    private TextElement _title;
    private TextElement _body;
    private TextElement _taskProgress;
    private TextElement _actionsUsed;
    private TextElement _rating;
    private Button _nextButton;
    private Button _retryButton;
    private Button _menuButton;

    private void Awake()
    {
        if (_document != null)
            UseDocument(_document);
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    public void UseDocument(UIDocument document)
    {
        UnbindButtons();

        _document = document;
        VisualElement root = GetRoot(_document);

        _title = FindText(root, _titleName);
        _body = FindText(root, _bodyName);
        _taskProgress = FindText(root, _taskProgressName);
        _actionsUsed = FindText(root, _actionsUsedName);
        _rating = FindText(root, _ratingName);
        _nextButton = FindButton(root, _nextButtonName);
        _retryButton = FindButton(root, _retryButtonName);
        _menuButton = FindButton(root, _menuButtonName);

        BindButtons();
    }

    public override void Show(LevelResultsSnapshot snapshot, bool canGoNext)
    {
        ShowDocument();

        if (_title != null)
            _title.text = LevelResultsController.BuildTitle(snapshot);

        if (_body != null)
            _body.text = LevelResultsController.BuildBody(snapshot);

        if (_taskProgress != null)
            _taskProgress.text = LevelResultsController.BuildGoalProgressText(
                snapshot.GoalLines
            );

        if (_actionsUsed != null)
            _actionsUsed.text = $"Actions: {snapshot.ActionsUsed}";

        if (_rating != null)
            _rating.text =
                $"Rating: {LevelResultsController.FormatRating(snapshot.Rating)}";

        SetVisible(
            _nextButton,
            snapshot.Outcome == LevelEndOutcome.Success && canGoNext
        );
        // SetVisible(_retryButton, snapshot.Outcome == LevelEndOutcome.Fail);
        SetVisible(_retryButton, true);
        SetVisible(_menuButton, true);
    }

    public override void Hide()
    {
        HideDocument();
    }

    private void BindButtons()
    {
        if (_nextButton != null)
            _nextButton.clicked += Next;
        if (_retryButton != null)
            _retryButton.clicked += Retry;
        if (_menuButton != null)
            _menuButton.clicked += Menu;
    }

    private void UnbindButtons()
    {
        if (_nextButton != null)
            _nextButton.clicked -= Next;
        if (_retryButton != null)
            _retryButton.clicked -= Retry;
        if (_menuButton != null)
            _menuButton.clicked -= Menu;

        _nextButton = null;
        _retryButton = null;
        _menuButton = null;
    }

    private void ShowDocument()
    {
        if (_document == null)
            return;

        if (!_document.gameObject.activeSelf)
            _document.gameObject.SetActive(true);

        UseDocument(_document);
    }

    private void HideDocument()
    {
        if (_document != null)
            _document.gameObject.SetActive(false);
    }

    private static VisualElement GetRoot(UIDocument document) =>
        document != null ? document.rootVisualElement : null;

    private static TextElement FindText(
        VisualElement root,
        string elementName
    )
    {
        if (root == null || string.IsNullOrEmpty(elementName))
            return null;

        return root.Q<TextElement>(elementName);
    }

    private static Button FindButton(
        VisualElement root,
        string buttonName
    )
    {
        if (root == null || string.IsNullOrEmpty(buttonName))
            return null;

        return root.Q<Button>(buttonName);
    }

    private static void SetVisible(VisualElement element, bool visible)
    {
        if (element != null)
            element.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
    }
}
