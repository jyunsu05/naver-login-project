using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HomeSceneController : MonoBehaviour
{
    private const string OverlayCanvasName = "LoginOverlayCanvas";

    [SerializeField] private Button startGameButton;
    [SerializeField] private TMP_Text highScoreText;

    private void Awake()
    {
        ResolveStartButton();
        ResolveHighScoreText();

        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveListener(StartGame);
            startGameButton.onClick.AddListener(StartGame);
        }
    }

    private void Start()
    {
        UpdateHighScoreText(GameSession.HighScore);
        StartCoroutine(LoadHighScoreRoutine());
    }

    private static Canvas FindSceneCanvas()
    {
        var activeScene = SceneManager.GetActiveScene();

        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas.gameObject.name == OverlayCanvasName)
            {
                continue;
            }

            if (canvas.gameObject.scene == activeScene)
            {
                return canvas;
            }
        }

        return null;
    }

    private void ResolveStartButton()
    {
        if (startGameButton != null)
        {
            return;
        }

        var canvas = FindSceneCanvas();
        if (canvas == null)
        {
            return;
        }

        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
        {
            var label = button.GetComponentInChildren<TMP_Text>(true)?.text?.Trim();
            if (label is "게임시작" or "게임 시작" or "다시시작")
            {
                startGameButton = button;
                return;
            }
        }
    }

    private void ResolveHighScoreText()
    {
        if (highScoreText != null)
        {
            return;
        }

        var canvas = FindSceneCanvas();
        if (canvas == null)
        {
            return;
        }

        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.transform.parent != null && text.transform.parent.GetComponent<Button>() != null)
            {
                continue;
            }

            var label = text.text?.Trim();
            if (label != null && label.StartsWith("최고 점수"))
            {
                highScoreText = text;
                return;
            }
        }
    }

    private IEnumerator LoadHighScoreRoutine()
    {
        yield return HighScoreClient.FetchHighScore(
            highScore => UpdateHighScoreText(highScore),
            error =>
            {
                Debug.LogWarning($"[Score] 최고 점수 조회 실패: {error}");
                UpdateHighScoreText(GameSession.HighScore);
            });
    }

    private void UpdateHighScoreText(int highScore)
    {
        if (highScoreText == null)
        {
            ResolveHighScoreText();
        }

        if (highScoreText == null)
        {
            return;
        }

        highScoreText.text = $"최고 점수\n{highScore}";
    }

    private void StartGame()
    {
        App.Instance.GoToGame();
    }

    private void OnDestroy()
    {
        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveListener(StartGame);
        }
    }
}
