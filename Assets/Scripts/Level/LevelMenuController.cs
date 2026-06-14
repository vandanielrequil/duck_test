using UnityEngine;

public interface ILevelMenuActions
{
    void NewGame();
    void Continue();
    void LoadLevel();
    void LoadLevel(int index);
    void Options();
    void ExitGame();
    void Credentials();
}

public interface ILevelMenuFacade
{
    void Bind(ILevelMenuActions actions);
    void ShowMainMenu();
    void ShowLoadLevel();
    void ShowOptions();
    void ShowCredentials();
    void HideAll();
}

public abstract class LevelMenuFacadeBase : MonoBehaviour, ILevelMenuFacade
{
    private ILevelMenuActions _actions;

    public virtual void Bind(ILevelMenuActions actions)
    {
        _actions = actions;
    }

    public abstract void ShowMainMenu();
    public abstract void ShowLoadLevel();
    public abstract void ShowOptions();
    public abstract void ShowCredentials();
    public abstract void HideAll();

    public void NewGame() => _actions?.NewGame();
    public void Continue() => _actions?.Continue();
    public void LoadLevel() => _actions?.LoadLevel();
    public void LoadLevel(int index) => _actions?.LoadLevel(index);
    public void Options() => _actions?.Options();
    public void ExitGame() => _actions?.ExitGame();
    public void Credentials() => _actions?.Credentials();
}

public class LevelMenuController : MonoBehaviour, ILevelMenuActions
{
    [SerializeField] private LevelSession _levelSession;
    [SerializeField] private MonoBehaviour _menuFacadeBehaviour;

    private ILevelMenuFacade _menuFacade;

    private void Awake()
    {
        if (_levelSession == null)
            _levelSession = FindAnyObjectByType<LevelSession>();

        ResolveFacade();
        _menuFacade?.Bind(this);
    }

    private void Start()
    {
        if (_levelSession == null || !_levelSession.IsLevelActive)
            ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        _menuFacade?.ShowMainMenu();
    }

    public void HideAll()
    {
        _menuFacade?.HideAll();
    }

    public void NewGame()
    {
        HideAll();
        Debug.Log("New game requested");
        _levelSession?.StartLevel(0);
    }

    public void Continue()
    {
        StartCurrentLevel();
    }

    public void StartCurrentLevel()
    {
        HideAll();
        _levelSession?.StartCurrentLevel();
    }

    public void StartLevel(int index)
    {
        if (_levelSession != null && !_levelSession.IsLevelUnlocked(index))
            return;

        HideAll();
        _levelSession?.StartLevel(index);
    }

    public void LoadLevel()
    {
        _menuFacade?.ShowLoadLevel();
    }

    public void LoadLevel(int index)
    {
        StartLevel(index);
    }

    public void Options()
    {
        _menuFacade?.ShowOptions();
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        Debug.Log("[LevelMenu] Exit game requested. (Editor)");
#else
        Application.Quit();
#endif
    }

    public void Credentials()
    {
        _menuFacade?.ShowCredentials();
    }

    public void BackToMenu()
    {
        if (_levelSession != null)
            _levelSession.ReturnToMenu();
        else
            ShowMainMenu();
    }

    private void ResolveFacade()
    {
        if (_menuFacadeBehaviour == null)
            _menuFacadeBehaviour = FindAnyObjectByType<LevelMenuFacadeBase>();

        _menuFacade = _menuFacadeBehaviour as ILevelMenuFacade;

        if (_menuFacadeBehaviour != null && _menuFacade == null)
        {
            Debug.LogError(
                "[LevelMenuController] Menu facade behaviour must implement "
                + nameof(ILevelMenuFacade)
            );
        }
    }
}
