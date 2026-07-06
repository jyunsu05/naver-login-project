using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

public static class NaverLoginSession
{
    private const string SessionTokenKey = "naver_session_token";

    public static string GetToken()
    {
        return PlayerPrefs.GetString(SessionTokenKey, string.Empty);
    }

    public static void SaveToken(string sessionToken)
    {
        PlayerPrefs.SetString(SessionTokenKey, sessionToken);
        PlayerPrefs.Save();
    }

    public static void ClearToken()
    {
        PlayerPrefs.DeleteKey(SessionTokenKey);
        PlayerPrefs.Save();
    }

    public static IEnumerator PostAuth(string url, string sessionToken, Action<string> onSuccess, Action<string> onError)
    {
        var body = new JObject { ["sessionToken"] = sessionToken }.ToString();
        using var request = new UnityWebRequest(url, "POST");
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        request.uploadHandler = new UploadHandlerRaw(bodyBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {sessionToken}");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke(request.downloadHandler.text);
            yield break;
        }

        onError?.Invoke(request.error);
    }
}
