using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class NaverLoginCallbackListener : MonoBehaviour
{
    public const int CallbackPort = 7777;
    public const string CallbackPath = "/naver-login/";

    private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
    private HttpListener _listener;
    private Thread _listenerThread;
    private bool _isRunning;

    public event Action<string> OnTokenReceived;
    public event Action<string> OnErrorReceived;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        StartListener();
    }

    private void Update()
    {
        lock (_mainThreadQueue)
        {
            while (_mainThreadQueue.Count > 0)
            {
                _mainThreadQueue.Dequeue()?.Invoke();
            }
        }
    }

    private void OnDestroy()
    {
        StopListener();
    }

    public void StartListener()
    {
        if (_isRunning)
        {
            return;
        }

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{CallbackPort}/");
            _listener.Start();
            _isRunning = true;
            _listenerThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "NaverLoginCallbackListener",
            };
            _listenerThread.Start();
            Debug.Log($"[Naver] Unity 콜백 리스너 시작: http://127.0.0.1:{CallbackPort}{CallbackPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Naver] 콜백 리스너 시작 실패: {ex.Message}");
        }
    }

    public void StopListener()
    {
        _isRunning = false;

        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Naver] 콜백 리스너 종료 중 경고: {ex.Message}");
        }

        _listener = null;
        _listenerThread = null;
    }

    private void ListenLoop()
    {
        while (_isRunning && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = _listener.GetContext();
                HandleRequest(context);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (Exception ex)
            {
                EnqueueMain(() => Debug.LogError($"[Naver] 콜백 처리 오류: {ex.Message}"));
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        var path = request.Url.AbsolutePath ?? string.Empty;

        if (!path.StartsWith("/naver-login", StringComparison.OrdinalIgnoreCase))
        {
            WriteResponse(response, 404, "Not Found");
            return;
        }

        var token = request.QueryString["token"];
        var error = request.QueryString["error"];

        if (!string.IsNullOrEmpty(token))
        {
            EnqueueMain(() =>
            {
                Debug.Log("[Naver] Unity 콜백에서 sessionToken 수신");
                OnTokenReceived?.Invoke(token);
            });
            WriteResponse(response, 200, "<html><body><h1>로그인 완료</h1><p>Unity로 돌아가세요.</p></body></html>");
            return;
        }

        var message = string.IsNullOrEmpty(error) ? "알 수 없는 오류" : error;
        EnqueueMain(() =>
        {
            Debug.LogWarning($"[Naver] Unity 콜백 오류: {message}");
            OnErrorReceived?.Invoke(message);
        });
        WriteResponse(response, 400, $"<html><body><h1>로그인 실패</h1><p>{message}</p></body></html>");
    }

    private static void WriteResponse(HttpListenerResponse response, int statusCode, string body)
    {
        var buffer = Encoding.UTF8.GetBytes(body);
        response.StatusCode = statusCode;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }

    private void EnqueueMain(Action action)
    {
        lock (_mainThreadQueue)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }
}

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
