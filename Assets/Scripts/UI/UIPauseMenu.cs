using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

public class UIPauseMenu : LevelPauseFacadeBase
{
    [FormerlySerializedAs("_pauseButtonDocument")]
    [SerializeField] private UIDocument _gameHudDocument;
    [SerializeField] private UIDocument _pauseMenuDocument;

    [Header("Game HUD")]
    [SerializeField] private string _pauseButtonName = "PauseButton";
    [SerializeField] private string _rageBarName = "RageBar";

    [Header("Pause menu buttons")]
    [FormerlySerializedAs("_continueButtonName")]
    [SerializeField] private string _resumeButtonName = "Resume";
    [SerializeField] private string _restartButtonName = "Restart";
    [SerializeField] private string _menuButtonName = "Menu";

    private Button _pauseButton;
    private ProgressBar _rageBar;
    private Button _resumeButton;
    private Button _restartButton;
    private Button _menuButton;
    private int _rageCurrent;
    private int _rageMax = 6;

    private void Awake()
    {
        if (_pauseMenuDocument != null)
            UsePauseMenuDocument(_pauseMenuDocument);
    }

    private void OnDestroy()
    {
        UnbindPauseButton();
        UnbindPauseMenuButtons();
    }

    public void UseGameHudDocument(UIDocument document)
    {
        _gameHudDocument = document;
        BindGameHudDocument();
    }

    private void BindGameHudDocument()
    {
        UnbindPauseButton();

        VisualElement root = GetRoot(_gameHudDocument);
        _pauseButton = FindButton(
            root,
            _pauseButtonName
        );
        _rageBar = FindProgressBar(
            root,
            _rageBarName
        );
        Debug.Log(
            $"[RageBar] UseGameHudDocument found={_rageBar != null} "
            + $"name={_rageBarName} rootChildren={root?.childCount ?? -1}"
        );

        BindPauseButton();
        ApplyRage();
    }

    public void UsePauseMenuDocument(UIDocument document)
    {
        UnbindPauseMenuButtons();

        _pauseMenuDocument = document;
        VisualElement root = GetRoot(_pauseMenuDocument);
        _resumeButton = FindButton(root, _resumeButtonName, "Resume");
        _restartButton = FindButton(root, _restartButtonName);
        _menuButton = FindButton(root, _menuButtonName);

        BindPauseMenuButtons();
    }

    public override void ShowGameHud()
    {
        HidePauseMenuDocument();
        ShowGameHudDocument();
    }

    public override void ShowPauseMenu()
    {
        HideGameHudDocument();
        ShowPauseMenuDocument();
    }

    public override void HideAll()
    {
        HideGameHudDocument();
        HidePauseMenuDocument();
    }

    public override void SetRage(int current, int max)
    {
        _rageCurrent = Mathf.Max(0, current);
        _rageMax = Mathf.Max(1, max);
        Debug.Log($"[RageBar] Facade SetRage {_rageCurrent}/{_rageMax}");
        ApplyRage();
    }

    private void BindPauseButton()
    {
        if (_pauseButton != null)
            _pauseButton.clicked += Pause;
    }

    private void UnbindPauseButton()
    {
        if (_pauseButton != null)
            _pauseButton.clicked -= Pause;

        _pauseButton = null;
        _rageBar = null;
    }

    private void BindPauseMenuButtons()
    {
        if (_resumeButton != null)
            _resumeButton.clicked += Continue;
        if (_restartButton != null)
            _restartButton.clicked += Restart;
        if (_menuButton != null)
            _menuButton.clicked += Menu;
    }

    private void UnbindPauseMenuButtons()
    {
        if (_resumeButton != null)
            _resumeButton.clicked -= Continue;
        if (_restartButton != null)
            _restartButton.clicked -= Restart;
        if (_menuButton != null)
            _menuButton.clicked -= Menu;

        _resumeButton = null;
        _restartButton = null;
        _menuButton = null;
    }

    private void ShowGameHudDocument()
    {
        if (_gameHudDocument == null)
            return;

        if (!_gameHudDocument.gameObject.activeSelf)
            _gameHudDocument.gameObject.SetActive(true);

        BindGameHudDocument();
        ScheduleGameHudRebind();
        ApplyRage();
    }

    private void HideGameHudDocument()
    {
        if (_gameHudDocument != null)
            _gameHudDocument.gameObject.SetActive(false);
    }

    private void ShowPauseMenuDocument()
    {
        if (_pauseMenuDocument == null)
            return;

        if (!_pauseMenuDocument.gameObject.activeSelf)
            _pauseMenuDocument.gameObject.SetActive(true);

        UsePauseMenuDocument(_pauseMenuDocument);
    }

    private void HidePauseMenuDocument()
    {
        if (_pauseMenuDocument != null)
            _pauseMenuDocument.gameObject.SetActive(false);
    }

    private static VisualElement GetRoot(UIDocument document) =>
        document != null ? document.rootVisualElement : null;

    private static Button FindButton(
        VisualElement root,
        string buttonName
    )
    {
        if (root == null || string.IsNullOrEmpty(buttonName))
            return null;

        return root.Q<Button>(buttonName);
    }

    private static ProgressBar FindProgressBar(
        VisualElement root,
        string progressBarName
    )
    {
        if (root == null || string.IsNullOrEmpty(progressBarName))
            return null;

        return root.Q<ProgressBar>(progressBarName);
    }

    private static Button FindButton(
        VisualElement root,
        string buttonName,
        string fallbackButtonName
    )
    {
        Button button = FindButton(root, buttonName);
        if (button != null)
            return button;

        return FindButton(root, fallbackButtonName);
    }

    private void ScheduleGameHudRebind()
    {
        VisualElement root = GetRoot(_gameHudDocument);
        if (root == null)
            return;

        root.schedule.Execute(() =>
        {
            BindGameHudDocument();
            ApplyRage();
        }).ExecuteLater(0);
    }

    private void ApplyRage()
    {
        if (_rageBar == null)
        {
            Debug.LogWarning("[RageBar] ApplyRage skipped: RageBar not found.");
            return;
        }

        int value = Mathf.Clamp(_rageCurrent, 0, _rageMax);
        _rageBar.lowValue = 0f;
        _rageBar.highValue = _rageMax;
        _rageBar.value = value;
        _rageBar.title = $"{value}/{_rageMax}";
        Debug.Log(
            $"[RageBar] Applied value={_rageBar.value} "
            + $"range={_rageBar.lowValue}-{_rageBar.highValue} "
            + $"title={_rageBar.title}"
        );
    }
}
