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
    void SetRage(int current, int max);
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
    public abstract void SetRage(int current, int max);

    public void Pause() => _actions?.Pause();
    public void Continue() => _actions?.Continue();
    public void Restart() => _actions?.Restart();
    public void Menu() => _actions?.Menu();
}

public class LevelPauseController : MonoBehaviour, ILevelPauseActions
{
    [SerializeField] private LevelSession _levelSession;
    [SerializeField] private InspectorController _inspector;
    [SerializeField] private LevelPauseFacadeBase _pauseFacade;

    private int _lastRageCurrent = -1;
    private int _lastRageMax = -1;
    private bool _subscribedToAnger;

    private void Awake()
    {
        if (_levelSession == null)
            _levelSession = FindAnyObjectByType<LevelSession>();
        if (_inspector == null)
            _inspector = FindAnyObjectByType<InspectorController>();

        ResolveFacade();
        _pauseFacade?.Bind(this);
    }

    private void OnEnable()
    {
        SubscribeToAnger();
    }

    private void OnDisable()
    {
        UnsubscribeFromAnger();
    }

    private void Start()
    {
        if (_levelSession != null && _levelSession.IsLevelActive)
            ShowGameHud();
        else
            HideAll();
    }

    private void Update()
    {
        if (_levelSession == null || !_levelSession.IsLevelActive)
            return;

        if (_inspector == null)
            return;

        int current = _inspector.Anger.Current;
        int max = _inspector.Anger.Max;
        if (current == _lastRageCurrent && max == _lastRageMax)
            return;

        SetRage(current, max);
    }

    public void ShowGameHud()
    {
        _pauseFacade?.ShowGameHud();
        SyncRage();
    }

    public void HideAll()
    {
        _pauseFacade?.HideAll();
    }

    public void BindInspector(InspectorController inspector)
    {
        if (_inspector == inspector)
        {
            SubscribeToAnger();
            SyncRage();
            return;
        }

        UnsubscribeFromAnger();
        _inspector = inspector;
        SubscribeToAnger();
        SyncRage();
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

    private void SubscribeToAnger()
    {
        if (_subscribedToAnger || _inspector == null)
            return;

        _inspector.Anger.OnAngerChanged += HandleAngerChanged;
        _subscribedToAnger = true;
        Debug.Log("[RageBar] Subscribed to inspector anger.");
    }

    private void UnsubscribeFromAnger()
    {
        if (!_subscribedToAnger || _inspector == null)
            return;

        _inspector.Anger.OnAngerChanged -= HandleAngerChanged;
        _subscribedToAnger = false;
    }

    private void HandleAngerChanged(int current)
    {
        if (_inspector == null)
            return;

        SetRage(current, _inspector.Anger.Max);
    }

    private void SyncRage()
    {
        if (_inspector == null)
            return;

        SetRage(
            _inspector.Anger.Current,
            _inspector.Anger.Max
        );
    }

    private void SetRage(int current, int max)
    {
        _lastRageCurrent = current;
        _lastRageMax = max;
        Debug.Log($"[RageBar] Controller SetRage {current}/{max}");
        _pauseFacade?.SetRage(current, max);
    }
}
