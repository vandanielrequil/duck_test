using UnityEngine;

public interface ILevelPauseActions
{
    void Pause();
    void Continue();
    void Restart();
    void Menu();
}

public interface ILevelPauseFacade
{
    void Bind(ILevelPauseActions actions);
    void ShowGameHud();
    void ShowPauseMenu();
    void HideAll();
}

public abstract class LevelPauseFacadeBase : MonoBehaviour, ILevelPauseFacade
{
    private ILevelPauseActions _actions;

    public virtual void Bind(ILevelPauseActions actions)
    {
        _actions = actions;
    }

    public abstract void ShowGameHud();
    public abstract void ShowPauseMenu();
    public abstract void HideAll();

    public void Pause() => _actions?.Pause();
    public void Continue() => _actions?.Continue();
    public void Restart() => _actions?.Restart();
    public void Menu() => _actions?.Menu();
}

public class LevelPauseController : MonoBehaviour, ILevelPauseActions
{
    [SerializeField] private LevelSession _levelSession;
    [SerializeField] private LevelPauseFacadeBase _pauseFacade;

    private void Awake()
    {
        if (_levelSession == null)
            _levelSession = FindAnyObjectByType<LevelSession>();

        ResolveFacade();
        _pauseFacade?.Bind(this);
    }

    private void Start()
    {
        if (_levelSession != null && _levelSession.IsLevelActive)
            ShowGameHud();
        else
            HideAll();
    }

    public void ShowGameHud()
    {
        _pauseFacade?.ShowGameHud();
    }

    public void HideAll()
    {
        _pauseFacade?.HideAll();
    }

    public void Pause()
    {
        if (_levelSession == null || !_levelSession.IsLevelActive)
            return;

        _levelSession.PauseLevel();
        _pauseFacade?.ShowPauseMenu();
    }

    public void Continue()
    {
        if (_levelSession == null || !_levelSession.IsLevelActive)
            return;

        _levelSession.ResumeLevel();
        _pauseFacade?.ShowGameHud();
    }

    public void Restart()
    {
        _pauseFacade?.HideAll();
        _levelSession?.RetryCurrentLevel();
    }

    public void Menu()
    {
        _pauseFacade?.HideAll();
        _levelSession?.ReturnToMenu();
    }

    private void ResolveFacade()
    {
        if (_pauseFacade == null)
            _pauseFacade = FindAnyObjectByType<LevelPauseFacadeBase>();
    }
}
