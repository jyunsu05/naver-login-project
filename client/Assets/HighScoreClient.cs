using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class HighScoreClient
{
    private const string HighScoreUrl = "http://127.0.0.1:3000/scores/high";

    public static IEnumerator SubmitScore(int score, Action<HighScoreSubmitResult> onSuccess, Action<string> onError)
    {
        var sessionToken = NaverLoginSession.GetToken();
        if (string.IsNullOrEmpty(sessionToken))
        {
            onError?.Invoke("로그인이 필요합니다.");
            yield break;
        }

        var body = new JObject
        {
            ["score"] = score,
        };

        yield return NaverLoginSession.PostAuthJson(
            HighScoreUrl,
            sessionToken,
            body,
            json =>
            {
                try
                {
                    var result = ParseSubmitResponse(json);
                    GameSession.HighScore = result.HighScore;
                    onSuccess?.Invoke(result);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex.Message);
                }
            },
            onError);
    }

    public static IEnumerator FetchHighScore(Action<int> onSuccess, Action<string> onError)
    {
        var sessionToken = NaverLoginSession.GetToken();
        if (string.IsNullOrEmpty(sessionToken))
        {
            onSuccess?.Invoke(0);
            yield break;
        }

        yield return NaverLoginSession.GetAuthJson(
            HighScoreUrl,
            sessionToken,
            json =>
            {
                try
                {
                    var highScore = ParseHighScore(json);
                    GameSession.HighScore = highScore;
                    onSuccess?.Invoke(highScore);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex.Message);
                }
            },
            onError);
    }

    private static int ParseHighScore(string json)
    {
        var root = JObject.Parse(json);
        if (root["success"]?.Value<bool>() != true)
        {
            throw new Exception(root["message"]?.ToString() ?? "최고 점수 조회 실패");
        }

        return root["data"]?["highScore"]?.Value<int>() ?? 0;
    }

    private static HighScoreSubmitResult ParseSubmitResponse(string json)
    {
        var root = JObject.Parse(json);
        if (root["success"]?.Value<bool>() != true)
        {
            throw new Exception(root["message"]?.ToString() ?? "최고 점수 저장 실패");
        }

        var data = root["data"];
        return new HighScoreSubmitResult
        {
            HighScore = data?["highScore"]?.Value<int>() ?? 0,
            PreviousHighScore = data?["previousHighScore"]?.Value<int>() ?? 0,
            IsNewRecord = data?["isNewRecord"]?.Value<bool>() ?? false,
        };
    }
}

public class HighScoreSubmitResult
{
    public int HighScore;
    public int PreviousHighScore;
    public bool IsNewRecord;
}
