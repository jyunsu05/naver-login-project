using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSceneController : MonoBehaviour
{
    private const float GameDurationSeconds = 10f;
    private const int PointsPerClick = 10;

    [SerializeField] private Slider timerSlider;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Button clickButton;

    private float remainingTime;
    private int score;
    private bool isGameOver;

    private void Awake()
    {
        ResolveReferences();
        BindClickButton();
    }

    private void Start()
    {
        remainingTime = GameDurationSeconds;
        score = 0;
        isGameOver = false;
        GameSession.Score = 0;
        UpdateScoreText();
        ResetTimer();
    }

    private void Update()
    {
        if (isGameOver)
        {
            return;
        }

        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            UpdateTimer();
            EndGame();
            return;
        }

        UpdateTimer();
    }

    private void ResolveReferences()
    {
        if (timerSlider == null)
        {
            timerSlider = GameObject.Find("Timer")?.GetComponent<Slider>()
                ?? GameObject.Find("Slider")?.GetComponent<Slider>();
        }

        if (scoreText == null)
        {
            scoreText = GameObject.Find("scoreText")?.GetComponent<TMP_Text>()
                ?? GameObject.Find("Score")?.GetComponent<TMP_Text>();
        }

        if (clickButton == null)
        {
            clickButton = GameObject.Find("clickButton")?.GetComponent<Button>()
                ?? GameObject.Find("Button")?.GetComponent<Button>();
        }
    }

    private void BindClickButton()
    {
        if (clickButton == null)
        {
            return;
        }

        clickButton.onClick.RemoveListener(OnClickButtonPressed);
        clickButton.onClick.AddListener(OnClickButtonPressed);
    }

    private void ResetTimer()
    {
        if (timerSlider == null)
        {
            return;
        }

        timerSlider.interactable = false;
        timerSlider.minValue = 0f;
        timerSlider.maxValue = 100f;
        timerSlider.value = timerSlider.maxValue;
    }

    private void UpdateTimer()
    {
        if (timerSlider == null)
        {
            return;
        }

        var normalizedTime = remainingTime / GameDurationSeconds;
        timerSlider.value = timerSlider.maxValue * normalizedTime;
    }

    private void OnClickButtonPressed()
    {
        if (isGameOver)
        {
            return;
        }

        score += PointsPerClick;
        GameSession.Score = score;
        UpdateScoreText();
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = score.ToString();
        }
    }

    private void EndGame()
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;
        GameSession.Score = score;

        if (clickButton != null)
        {
            clickButton.interactable = false;
        }

        App.Instance.GoToGameOver();
    }

    private void OnDestroy()
    {
        if (clickButton != null)
        {
            clickButton.onClick.RemoveListener(OnClickButtonPressed);
        }
    }
}
