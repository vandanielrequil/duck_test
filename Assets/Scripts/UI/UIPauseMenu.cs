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

    [Header("Pause menu buttons")]
    [FormerlySerializedAs("_continueButtonName")]
    [SerializeField] private string _resumeButtonName = "Resume";
    [SerializeField] private string _restartButtonName = "Restart";
    [SerializeField] private string _menuButtonName = "Menu";

    private Button _pauseButton;
    private Button _resumeButton;
    private Button _restartButton;
    private Button _menuButton;

    private void Awake()
    {
        if (_gameHudDocument != null)
            UseGameHudDocument(_gameHudDocument);

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
        UnbindPauseButton();

        _gameHudDocument = document;
        _pauseButton = FindButton(
            GetRoot(_gameHudDocument),
            _pauseButtonName
        );

        BindPauseButton();
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

        UseGameHudDocument(_gameHudDocument);
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
}
