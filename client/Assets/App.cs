using UnityEngine;
using UnityEngine.SceneManagement;

public class App : MonoBehaviour
{
    public static App Instance { get; private set; }

    private test login;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureLogin();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == GameSceneNames.App)
        {
            GoToTitle();
            return;
        }

        if (SceneManager.GetActiveScene().name == GameSceneNames.Title)
        {
            SetupTitleScene();
            return;
        }

        if (SceneManager.GetActiveScene().name == GameSceneNames.Home)
        {
            SetupHomeScene();
            return;
        }

        if (SceneManager.GetActiveScene().name == GameSceneNames.Game)
        {
            SetupGameScene();
            return;
        }

        if (SceneManager.GetActiveScene().name == GameSceneNames.GameOver)
        {
            SetupGameOverScene();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case GameSceneNames.Title:
                SetupTitleScene();
                break;
            case GameSceneNames.Home:
                SetupHomeScene();
                break;
            case GameSceneNames.Game:
                SetupGameScene();
                break;
            case GameSceneNames.GameOver:
                SetupGameOverScene();
                break;
        }
    }

    private void EnsureLogin()
    {
        login = GetComponent<test>();
        if (login == null)
        {
            login = gameObject.AddComponent<test>();
        }

        TitleLoginOverlay.Ensure();
    }

    private void SetupTitleScene()
    {
        EnsureLogin();
        login.BindUIButtons();
        login.BeginTitleEntryLogin();
    }

    private void SetupHomeScene()
    {
        EnsureSceneController<HomeSceneController>();
    }

    private void SetupGameScene()
    {
        EnsureSceneController<GameSceneController>();
    }

    private void SetupGameOverScene()
    {
        EnsureSceneController<GameOverSceneController>();
    }

    private static void EnsureSceneController<T>() where T : MonoBehaviour
    {
        if (Object.FindAnyObjectByType<T>() != null)
        {
            return;
        }

        var controllerObject = new GameObject(typeof(T).Name);
        controllerObject.AddComponent<T>();
    }

    public void GoToTitle()
    {
        LoadScene(GameSceneNames.Title);
    }

    public void GoToHome()
    {
        LoadScene(GameSceneNames.Home);
    }

    public void GoToGame()
    {
        GameSession.ResetScore();
        LoadScene(GameSceneNames.Game);
    }

    public void GoToGameOver()
    {
        LoadScene(GameSceneNames.GameOver);
    }

    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }

        if (SceneManager.GetActiveScene().name == sceneName)
        {
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
