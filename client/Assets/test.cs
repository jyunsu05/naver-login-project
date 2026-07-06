using System.Collections;
using Gree.UnityWebView;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

public class test : MonoBehaviour
{
    [SerializeField] private string loginUrl = "http://127.0.0.1:3000/auth/naver";
    [SerializeField] private string devLoginUrl = "http://127.0.0.1:3000/login/dev";
    [SerializeField] private string authMeUrl = "http://127.0.0.1:3000/auth/me";
    [SerializeField] private string authRefreshUrl = "http://127.0.0.1:3000/auth/refresh";
    [SerializeField] private string logoutApiUrl = "http://127.0.0.1:3000/auth/logout";

    private WebViewObject webViewObject;
    private bool startupAutoLoginAttempted;
    private bool isLoggingIn;

    private void Start()
    {
        var buttonObject = GameObject.Find("Button");
        if (buttonObject != null)
        {
            var button = buttonObject.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(OnNaverLoginClicked);
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
        yield return CallAuthMe(sessionToken, openOAuthOnFail: false);
    }

    private void OnNaverLoginClicked()
    {
        if (isLoggingIn)
        {
            return;
        }

        var sessionToken = NaverLoginSession.GetToken();
        if (!string.IsNullOrEmpty(sessionToken))
        {
            StartCoroutine(CallAuthMe(sessionToken, openOAuthOnFail: true));
            return;
        }

        OpenOAuth();
    }

    public void OpenNaverLogin()
    {
        OnNaverLoginClicked();
    }

    public void Login()
    {
        OnNaverLoginClicked();
    }

    public void OpenDevLogin()
    {
        OpenUrl(devLoginUrl);
    }

    public void CloseWebView()
    {
        if (webViewObject == null)
        {
            return;
        }

        webViewObject.SetVisibility(false);
    }

    public void Logout()
    {
        var sessionToken = NaverLoginSession.GetToken();
        if (!string.IsNullOrEmpty(sessionToken))
        {
            StartCoroutine(SendLogout(sessionToken));
        }
        else
        {
            NaverLoginSession.ClearToken();
        }
    }

    private IEnumerator CallAuthMe(string sessionToken, bool openOAuthOnFail)
    {
        isLoggingIn = true;
        var completed = false;
        var failed = false;

        yield return NaverLoginSession.PostAuth(
            authMeUrl,
            sessionToken,
            responseText =>
            {
                Debug.Log($"[Naver] /auth/me 응답: {responseText}");
                HandleLoginPayload(responseText);
                completed = true;
            },
            error =>
            {
                Debug.LogWarning($"[Naver] /auth/me 실패: {error}");
                failed = true;
            });

        if (failed)
        {
            yield return CallAuthRefresh(sessionToken, openOAuthOnFail);
            isLoggingIn = false;
            yield break;
        }

        if (!completed)
        {
            isLoggingIn = false;
            yield break;
        }

        isLoggingIn = false;
    }

    private IEnumerator CallAuthRefresh(string sessionToken, bool openOAuthOnFail)
    {
        var completed = false;
        var failed = false;

        yield return NaverLoginSession.PostAuth(
            authRefreshUrl,
            sessionToken,
            responseText =>
            {
                Debug.Log($"[Naver] /auth/refresh 응답: {responseText}");
                HandleLoginPayload(responseText);
                completed = true;
            },
            error =>
            {
                Debug.LogWarning($"[Naver] /auth/refresh 실패: {error}");
                failed = true;
            });

        if (failed || !completed)
        {
            NaverLoginSession.ClearToken();
            if (openOAuthOnFail)
            {
                OpenOAuth();
            }
        }
    }

    private IEnumerator SendLogout(string sessionToken)
    {
        yield return NaverLoginSession.PostAuth(
            logoutApiUrl,
            sessionToken,
            _ =>
            {
                NaverLoginSession.ClearToken();
                Debug.Log("[Naver] 로그아웃 완료");
            },
            error => Debug.LogWarning($"[Naver] 로그아웃 요청 실패: {error}"));
    }

    private void OpenOAuth()
    {
        OpenUrl(loginUrl);
    }

    private void OpenUrl(string url)
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
        webViewObject.LoadURL(url);
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
        Debug.Log($"[Naver] WebView 메시지: {message}");
        HandleLoginPayload(message);
        CloseWebView();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        const int width = 320;
        const int height = 56;
        const int x = 20;
        var y = 20;

        GUI.backgroundColor = new Color(0.01f, 0.78f, 0.35f);
        if (GUI.Button(new Rect(x, y, width, height), "네이버 로그인"))
        {
            OnNaverLoginClicked();
        }

        y += height + 12;
        GUI.backgroundColor = new Color(0.2f, 0.45f, 0.95f);
        if (GUI.Button(new Rect(x, y, width, height), "개발용 로그인"))
        {
            OpenDevLogin();
        }

        y += height + 12;
        GUI.backgroundColor = new Color(0.85f, 0.3f, 0.25f);
        if (GUI.Button(new Rect(x, y, width, height), "웹뷰 닫기"))
        {
            CloseWebView();
        }

        y += height + 12;
        GUI.backgroundColor = Color.gray;
        if (GUI.Button(new Rect(x, y, width, height), "로그아웃"))
        {
            Logout();
        }

        GUI.backgroundColor = Color.white;
    }

    private void OnDestroy()
    {
        var buttonObject = GameObject.Find("Button");
        if (buttonObject != null)
        {
            var button = buttonObject.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveListener(OnNaverLoginClicked);
            }
        }
    }
}
