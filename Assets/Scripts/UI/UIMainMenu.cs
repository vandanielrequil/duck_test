using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UIMainMenu : LevelMenuFacadeBase
{
    [SerializeField] private UIDocument _document;
    [SerializeField] private UIDocument _loadLevelDocument;
    [SerializeField] private LevelSession _levelSession;

    [Header("Layout names")]
    [SerializeField] private string _mainMenuName = "MainMenu";
    [SerializeField] private string _optionsName = "Options";
    [SerializeField] private string _credentialsName = "Credentials";

    [Header("Button names")]
    [SerializeField] private string _newGameButtonName = "NewGameButton";
    [SerializeField] private string _continueButtonName = "ContinueButton";
    [Space(6)]
    [SerializeField] private string _loadLevelButtonName = "LoadLevelButton";
    [SerializeField] private string _optionsButtonName = "OptionsButton";
    [SerializeField] private string _exitGameButtonName = "ExitGameButton";
    [SerializeField] private string _credentialsButtonName = "CredentialsButton";

    [Header("Load level document")]
    [SerializeField] private string _levelButtonPrefix = "Level_";
    [Tooltip("Level_1 loads level index 0 when this is 1.")]
    [SerializeField] private int _levelNumberToIndexOffset = 1;

    private VisualElement _mainMenu;
    private VisualElement _options;
    private VisualElement _credentials;

    private Button _newGameButton;
    private Button _continueButton;
    private Button _loadLevelButton;
    private Button _optionsButton;
    private Button _exitGameButton;
    private Button _credentialsButton;

    private readonly List<LevelButtonBinding> _levelButtonBindings = new();

    private void Awake()
    {
        if (_levelSession == null)
            _levelSession = FindAnyObjectByType<LevelSession>();

        if (_document != null)
            UseDocument(_document);

        if (_loadLevelDocument != null)
            UseLoadLevelDocument(_loadLevelDocument);
    }

    private void OnDestroy()
    {
        UnbindButtons();
        UnbindLevelButtons();
    }

    public void UseDocument(UIDocument document)
    {
        UnbindButtons();

        _document = document;
        VisualElement root = _document != null
            ? _document.rootVisualElement
            : null;

        _mainMenu = FindElement(root, _mainMenuName);
        _options = FindElement(root, _optionsName);
        _credentials = FindElement(root, _credentialsName);

        _newGameButton = FindButton(root, _newGameButtonName);
        _continueButton = FindButton(root, _continueButtonName);
        _loadLevelButton = FindButton(root, _loadLevelButtonName);
        _optionsButton = FindButton(root, _optionsButtonName);
        _exitGameButton = FindButton(root, _exitGameButtonName);
        _credentialsButton = FindButton(root, _credentialsButtonName);

        BindButtons();
    }

    public void UseLoadLevelDocument(UIDocument document)
    {
        UnbindLevelButtons();

        _loadLevelDocument = document;
        VisualElement root = _loadLevelDocument != null
            ? _loadLevelDocument.rootVisualElement
            : null;

        BindLevelButtons(root);
    }

    public override void ShowMainMenu()
    {
        HideLoadLevelDocument();
        ShowMainDocument();
        HideLayouts();
        Show(_mainMenu);
        RefreshMainMenuState();
    }

    public override void ShowLoadLevel()
    {
        HideMainDocument();
        ShowLoadLevelDocument();
        HideLayouts();
        RefreshLevelButtons();
    }

    public override void ShowOptions()
    {
        HideLoadLevelDocument();
        ShowMainDocument();
        HideLayouts();
        Show(_options);
    }

    public override void ShowCredentials()
    {
        HideLoadLevelDocument();
        ShowMainDocument();
        HideLayouts();
        Show(_credentials);
    }

    public override void HideAll()
    {
        HideLayouts();
        HideMainDocument();
        HideLoadLevelDocument();
    }

    private void HideLayouts()
    {
        Hide(_mainMenu);
        Hide(_options);
        Hide(_credentials);
    }

    private void BindButtons()
    {
        if (_newGameButton != null)
            _newGameButton.clicked += NewGame;
        if (_continueButton != null)
            _continueButton.clicked += Continue;
        if (_loadLevelButton != null)
            _loadLevelButton.clicked += LoadLevel;
        if (_optionsButton != null)
            _optionsButton.clicked += Options;
        if (_exitGameButton != null)
            _exitGameButton.clicked += ExitGame;
        if (_credentialsButton != null)
            _credentialsButton.clicked += Credentials;
    }

    private void UnbindButtons()
    {
        if (_newGameButton != null)
            _newGameButton.clicked -= NewGame;
        if (_continueButton != null)
            _continueButton.clicked -= Continue;
        if (_loadLevelButton != null)
            _loadLevelButton.clicked -= LoadLevel;
        if (_optionsButton != null)
            _optionsButton.clicked -= Options;
        if (_exitGameButton != null)
            _exitGameButton.clicked -= ExitGame;
        if (_credentialsButton != null)
            _credentialsButton.clicked -= Credentials;
    }

    private void BindLevelButtons(VisualElement root)
    {
        if (root == null)
            return;

        List<Button> buttons = root.Query<Button>().ToList();
        foreach (Button button in buttons)
        {
            if (!TryGetLevelIndex(button.name, out int levelIndex))
                continue;

            string label = button.text;
            Action callback = () => LoadLevel(levelIndex);
            button.clicked += callback;
            _levelButtonBindings.Add(
                new LevelButtonBinding(button, callback, levelIndex, label)
            );
        }
    }

    private void UnbindLevelButtons()
    {
        foreach (LevelButtonBinding binding in _levelButtonBindings)
            binding.Button.clicked -= binding.Callback;

        _levelButtonBindings.Clear();
    }

    private bool TryGetLevelIndex(string buttonName, out int levelIndex)
    {
        levelIndex = -1;

        if (string.IsNullOrEmpty(buttonName)
            || string.IsNullOrEmpty(_levelButtonPrefix)
            || !buttonName.StartsWith(_levelButtonPrefix))
            return false;

        string numberPart = buttonName.Substring(_levelButtonPrefix.Length);
        if (!int.TryParse(numberPart, out int levelNumber))
            return false;

        levelIndex = levelNumber - _levelNumberToIndexOffset;
        return levelIndex >= 0;
    }

    private void ShowMainDocument()
    {
        if (_document == null)
            return;

        if (!_document.gameObject.activeSelf)
            _document.gameObject.SetActive(true);

        if (_mainMenu == null
            && _options == null
            && _credentials == null)
        {
            UseDocument(_document);
        }
    }

    private void HideMainDocument()
    {
        if (_document != null)
            _document.gameObject.SetActive(false);
    }

    private void ShowLoadLevelDocument()
    {
        if (_loadLevelDocument == null)
            return;

        if (!_loadLevelDocument.gameObject.activeSelf)
            _loadLevelDocument.gameObject.SetActive(true);

        // Re-bind on every show: deactivating the UIDocument rebuilds its
        // visual tree, orphaning previously bound level buttons. The tree may
        // also be rebuilt at end of frame, so rebind again via schedule.
        UseLoadLevelDocument(_loadLevelDocument);
        RefreshLevelButtons();
        ScheduleLoadLevelRebind();
    }

    private void HideLoadLevelDocument()
    {
        if (_loadLevelDocument != null)
            _loadLevelDocument.gameObject.SetActive(false);
    }

    private void ScheduleLoadLevelRebind()
    {
        VisualElement root = _loadLevelDocument != null
            ? _loadLevelDocument.rootVisualElement
            : null;

        if (root == null)
            return;

        root.schedule.Execute(() =>
        {
            if (_loadLevelDocument == null
                || !_loadLevelDocument.gameObject.activeSelf)
                return;

            UseLoadLevelDocument(_loadLevelDocument);
            RefreshLevelButtons();
        }).ExecuteLater(0);
    }

    private static VisualElement FindElement(
        VisualElement root,
        string elementName
    )
    {
        if (root == null || string.IsNullOrEmpty(elementName))
            return null;

        return root.Q<VisualElement>(elementName);
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

    private static void Show(VisualElement element)
    {
        if (element != null)
            element.style.display = DisplayStyle.Flex;
    }

    private static void Hide(VisualElement element)
    {
        if (element != null)
            element.style.display = DisplayStyle.None;
    }

    private void RefreshMainMenuState()
    {
        bool hasSave = PlayerProgress.HasSaveData();

        if (_continueButton != null)
            SetVisible(_continueButton, hasSave);
    }

    private void RefreshLevelButtons()
    {
        foreach (LevelButtonBinding binding in _levelButtonBindings)
        {
            bool unlocked = _levelSession == null
                || _levelSession.IsLevelUnlocked(binding.LevelIndex);

            binding.Button.SetEnabled(unlocked);
            binding.Button.text = unlocked
                ? binding.Label
                : $"{binding.Label}\n(Locked)";
        }
    }

    private static void SetVisible(VisualElement element, bool visible)
    {
        if (element != null)
            element.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
    }

    private readonly struct LevelButtonBinding
    {
        public readonly Button Button;
        public readonly Action Callback;
        public readonly int LevelIndex;
        public readonly string Label;

        public LevelButtonBinding(
            Button button,
            Action callback,
            int levelIndex,
            string label
        )
        {
            Button = button;
            Callback = callback;
            LevelIndex = levelIndex;
            Label = label ?? string.Empty;
        }
    }
}
