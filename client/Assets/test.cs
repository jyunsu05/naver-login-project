using System.Collections;
using Gree.UnityWebView;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

public class test : MonoBehaviour
{
    [SerializeField] private string loginUrl = "http://127.0.0.1:3000/auth/naver";
    [SerializeField] private string authMeUrl = "http://127.0.0.1:3000/auth/me";

    private WebViewObject webViewObject;
    private bool startupAutoLoginAttempted;

    private void Start()
    {
        var buttonObject = GameObject.Find("Button");
        if (buttonObject != null)
        {
            var button = buttonObject.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(OpenNaverLogin);
            }
        }

        if (!startupAutoLoginAttempted)
        {
            startupAutoLoginAttempted = true;
            StartCoroutine(TryStartupAutoLogin());
        }
    }

    private IEnumerator TryStartupAutoLogin()
    {
        var sessionToken = NaverLoginSession.GetToken();
        if (string.IsNullOrEmpty(sessionToken))
        {
            Debug.Log("[Naver] 저장된 sessionToken 없음 - OAuth 필요 시 버튼 사용");
            yield break;
        }

        Debug.Log("[Naver] 앱 시작 시 /auth/me 자동 로그인 시도");
        yield return NaverLoginSession.PostAuth(
            authMeUrl,
            sessionToken,
            responseText =>
            {
                Debug.Log($"[Naver] /auth/me 응답: {responseText}");
                HandleLoginPayload(responseText);
            },
            error => Debug.LogWarning($"[Naver] /auth/me 실패: {error}"));
    }

    public void OpenNaverLogin()
    {
        if (webViewObject == null)
        {
            webViewObject = new GameObject("NaverWebView").AddComponent<WebViewObject>();
        }

        webViewObject.Init(
            cb: OnWebMessage,
            err: message => Debug.LogError($"[Naver] WebView error: {message}"),
            httpErr: message => Debug.LogError($"[Naver] HTTP error: {message}"),
            ld: message => Debug.Log($"[Naver] Loaded: {message}"),
            started: message => Debug.Log($"[Naver] Started: {message}"),
            hooked: message => Debug.Log($"[Naver] Hooked: {message}"),
            transparent: false,
            zoom: false,
            ua: "Mozilla/5.0"
        );

        webViewObject.SetMargins(0, 0, 0, 0);
        webViewObject.SetVisibility(true);
        webViewObject.LoadURL(loginUrl);
    }

    public void Login()
    {
        OpenNaverLogin();
    }

    private void HandleLoginPayload(string message)
    {
        try
        {
            var json = JObject.Parse(message);
            var success = json["success"]?.Value<bool>() ?? false;

            if (!success)
            {
                Debug.LogWarning($"[Naver] 로그인 실패: {json["message"]}");
                return;
            }

            var sessionToken = json["data"]?["sessionToken"]?.ToString();
            if (!string.IsNullOrEmpty(sessionToken))
            {
                NaverLoginSession.SaveToken(sessionToken);
                Debug.Log("[Naver] sessionToken 저장 완료");
            }

            var loginType = json["data"]?["loginType"]?.ToString();
            var user = json["data"]?["user"];

            if (user != null)
            {
                var uid = user["uid"]?.ToString();
                var email = user["email"]?.ToString();
                var name = user["name"]?.ToString();
                Debug.Log($"loginType: {loginType}\nuid: {uid}\nemail: {email}\nname: {name}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Naver] JSON 파싱 실패: {ex.Message}");
        }
    }

    private void OnWebMessage(string message)
    {
        Debug.Log($"[Naver] 응답 JSON: {message}");
        HandleLoginPayload(message);

        if (webViewObject != null)
        {
            webViewObject.SetVisibility(false);
        }
    }

    private void OnDestroy()
    {
        var buttonObject = GameObject.Find("Button");
        if (buttonObject != null)
        {
            var button = buttonObject.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveListener(OpenNaverLogin);
            }
        }
    }
}
