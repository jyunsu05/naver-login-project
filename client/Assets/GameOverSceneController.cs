using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverSceneController : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Button homeButton;

    private bool isGoingHome;

    private void Awake()
    {
        ResolveReferences();
        ShowFinalScore();

        if (homeButton != null)
        {
            homeButton.onClick.RemoveListener(GoHome);
            homeButton.onClick.AddListener(GoHome);
        }
    }

    private void ResolveReferences()
    {
        if (scoreText == null)
        {
            scoreText = GameObject.Find("scoreText")?.GetComponent<TMP_Text>()
                ?? GameObject.Find("Score")?.GetComponent<TMP_Text>();
        }

        if (homeButton == null)
        {
            homeButton = GameObject.Find("HomeButton")?.GetComponent<Button>()
                ?? GameObject.Find("Button")?.GetComponent<Button>();
        }

        if (homeButton == null)
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvasItem in canvases)
            {
                if (canvasItem.gameObject.name == "LoginOverlayCanvas")
                {
                    continue;
                }

                foreach (var button in canvasItem.GetComponentsInChildren<Button>(true))
                {
                    var label = button.GetComponentInChildren<TMP_Text>(true)?.text?.Trim();
                    if (label is "Home" or "홈" or "게임시작")
                    {
                        homeButton = button;
                        return;
                    }
                }
            }
        }
    }

    private void ShowFinalScore()
    {
        if (scoreText == null)
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            Canvas sceneCanvas = null;
            foreach (var canvasItem in canvases)
            {
                if (canvasItem.gameObject.name != "LoginOverlayCanvas")
                {
                    sceneCanvas = canvasItem;
                    break;
                }
            }

            if (sceneCanvas == null)
            {
                return;
            }

            var scoreObject = new GameObject("scoreText", typeof(RectTransform));
            scoreObject.transform.SetParent(sceneCanvas.transform, false);

            var rectTransform = scoreObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0f, 80f);
            rectTransform.sizeDelta = new Vector2(800f, 120f);

            scoreText = scoreObject.AddComponent<TextMeshProUGUI>();
            scoreText.fontSize = 72f;
            scoreText.alignment = TextAlignmentOptions.Center;
        }

        scoreText.text = $"획득 점수: {GameSession.Score}";
    }

    private void GoHome()
    {
        if (isGoingHome)
        {
            return;
        }

        StartCoroutine(GoHomeRoutine());
    }

    private IEnumerator GoHomeRoutine()
    {
        isGoingHome = true;

        if (homeButton != null)
        {
            homeButton.interactable = false;
        }

        var overlay = TitleLoginOverlay.Ensure();
        overlay.Show();
        yield return null;

        var hasToken = !string.IsNullOrEmpty(NaverLoginSession.GetToken());

        if (hasToken)
        {
            HighScoreSubmitResult submitResult = null;
            yield return HighScoreClient.SubmitScore(
                GameSession.Score,
                result => submitResult = result,
                error => Debug.LogWarning($"[Score] 최고 점수 저장 실패: {error}"));

            if (submitResult != null)
            {
                GameSession.HighScore = submitResult.HighScore;
            }

            yield return HighScoreClient.FetchHighScore(
                highScore => GameSession.HighScore = highScore,
                error => Debug.LogWarning($"[Score] 최고 점수 조회 실패: {error}"));
        }
        else
        {
            Debug.LogWarning("[Score] 로그인되지 않아 최고 점수를 저장하지 않습니다.");
        }

        overlay.Hide();
        App.Instance.GoToHome();
    }

    private void OnDestroy()
    {
        if (homeButton != null)
        {
            homeButton.onClick.RemoveListener(GoHome);
        }
    }
}
