using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class test : MonoBehaviour
{
    [SerializeField] private string loginUrl = "http://127.0.0.1:3000/auth/naver";
    [SerializeField] private string devLoginUrl = "http://127.0.0.1:3000/login/dev";
    [SerializeField] private string authMeUrl = "http://127.0.0.1:3000/auth/me";
    [SerializeField] private string authRefreshUrl = "http://127.0.0.1:3000/auth/refresh";
    [SerializeField] private string logoutApiUrl = "http://127.0.0.1:3000/auth/logout";
    [SerializeField] private string authBridgeUrl = "http://127.0.0.1:3000/auth/dev/session";
    [SerializeField] private string loginSuccessUrl = "http://127.0.0.1:3000/login/success";

    [Header("Browser Login")]
    [SerializeField] private float browserLoginPollIntervalSeconds = 2f;
    [SerializeField] private int browserLoginPollMaxAttempts = 150;

    [Header("UI Buttons")]
    [SerializeField] private Button naverLoginButton;
    [SerializeField] private Button devLoginButton;
    [SerializeField] private Button syncLoginButton;
    [SerializeField] private Button logoutButton;

    private NaverLoginCallbackListener callbackListener;
    private bool isLoggingIn;
    private bool isLoggedIn;
    private bool homeSceneRequested;

    public bool IsLoginInProgress => isLoggingIn;
    private string loggedInUserLabel = "로그인 필요";
    private Coroutine browserLoginPollRoutine;

    private void Awake()
    {
        EnsureCallbackListener();

        if (SceneManager.GetActiveScene().name == GameSceneNames.Title)
        {
            ResolveButtons();
            BindUIButtons();
        }
    }

    public void BindUIButtons()
    {
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            {
                var label = button.GetComponentInChildren<TMP_Text>(true)?.text?.Trim();
                switch (label)
                {
                    case "네이버 로그인":
                        BindButton(ref naverLoginButton, button, OnNaverLoginClicked);
                        break;
                    case "개발용 로그인":
                        BindButton(ref devLoginButton, button, OpenDevLogin);
                        break;
                    case "웹뷰 닫기":
                    case "로그인 동기화":
                        BindButton(ref syncLoginButton, button, SyncLoginFromBrowser);
                        break;
                    case "로그아웃":
                        BindButton(ref logoutButton, button, Logout);
                        break;
                }
            }
        }

        if (naverLoginButton == null)
        {
            var fallback = GameObject.Find("NaverLoginButton")?.GetComponent<Button>()
                ?? GameObject.Find("Button")?.GetComponent<Button>();
            if (fallback != null)
            {
                BindButton(ref naverLoginButton, fallback, OnNaverLoginClicked);
            }
        }

        if (devLoginButton == null)
        {
            var fallback = GameObject.Find("DevLoginButton")?.GetComponent<Button>();
            if (fallback != null)
            {
                BindButton(ref devLoginButton, fallback, OpenDevLogin);
            }
        }

        if (syncLoginButton == null)
        {
            var fallback = GameObject.Find("SyncLoginButton")?.GetComponent<Button>()
                ?? GameObject.Find("CloseWebViewButton")?.GetComponent<Button>();
            if (fallback != null)
            {
                BindButton(ref syncLoginButton, fallback, SyncLoginFromBrowser);
            }
        }
    }

    private void ResolveButtons()
    {
        if (naverLoginButton == null)
        {
            naverLoginButton = GameObject.Find("NaverLoginButton")?.GetComponent<Button>()
                ?? GameObject.Find("Button")?.GetComponent<Button>();
        }

        if (devLoginButton == null)
        {
            devLoginButton = GameObject.Find("DevLoginButton")?.GetComponent<Button>();
        }

        if (syncLoginButton == null)
        {
            syncLoginButton = GameObject.Find("SyncLoginButton")?.GetComponent<Button>()
                ?? GameObject.Find("CloseWebViewButton")?.GetComponent<Button>();
        }

        if (logoutButton == null)
        {
            logoutButton = GameObject.Find("LogoutButton")?.GetComponent<Button>();
        }
    }

    private static void BindButton(ref Button field, Button button, UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        field = button;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void EnsureCallbackListener()
    {
        callbackListener = FindAnyObjectByType<NaverLoginCallbackListener>();
        if (callbackListener != null)
        {
            callbackListener.OnTokenReceived -= HandleCallbackToken;
            callbackListener.OnTokenReceived += HandleCallbackToken;
            callbackListener.OnErrorReceived -= HandleCallbackError;
            callbackListener.OnErrorReceived += HandleCallbackError;
            return;
        }

        var listenerObject = new GameObject("NaverLoginCallbackListener");
        callbackListener = listenerObject.AddComponent<NaverLoginCallbackListener>();
        callbackListener.OnTokenReceived += HandleCallbackToken;
        callbackListener.OnErrorReceived += HandleCallbackError;
    }

    private void HandleCallbackToken(string sessionToken)
    {
        NaverLoginSession.SaveToken(sessionToken);
        StartCoroutine(CallAuthMe(sessionToken, openOAuthOnFail: false));
    }

    private void HandleCallbackError(string message)
    {
        UnityEngine.Debug.LogWarning($"[Naver] OAuth 콜백 오류: {message}");
    }

    private IEnumerator TryAutoLogin()
    {
        var sessionToken = NaverLoginSession.GetToken();

        if (string.IsNullOrEmpty(sessionToken))
        {
            yield return TryFetchBrowserBridgeSession(token => sessionToken = token);
        }

        if (string.IsNullOrEmpty(sessionToken))
        {
            yield break;
        }

        yield return CallAuthMe(sessionToken, openOAuthOnFail: false);
    }

    private IEnumerator TryFetchBrowserBridgeSession(Action<string> onTokenFound)
    {
        var bridgeToken = string.Empty;

        yield return NaverLoginSession.GetJson(
            authBridgeUrl,
            responseText =>
            {
                try
                {
                    var json = JObject.Parse(responseText);
                    var success = json["success"]?.Value<bool>() ?? false;
                    if (!success)
                    {
                        return;
                    }

                    bridgeToken = json["data"]?["sessionToken"]?.ToString();
                    if (!string.IsNullOrEmpty(bridgeToken))
                    {
                        NaverLoginSession.SaveToken(bridgeToken);
                        UnityEngine.Debug.Log("[Naver] 브라우저 로그인 sessionToken을 PlayerPrefs에 저장했습니다");
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[Naver] 브라우저 세션 브리지 파싱 실패: {ex.Message}");
                }
            },
            _ => { });

        if (!string.IsNullOrEmpty(bridgeToken))
        {
            onTokenFound?.Invoke(bridgeToken);
        }
    }

    private Coroutine titleEntryLoginRoutine;
    private Coroutine buttonLoginRoutine;

    public void BeginTitleEntryLogin()
    {
        if (titleEntryLoginRoutine != null)
        {
            StopCoroutine(titleEntryLoginRoutine);
        }

        titleEntryLoginRoutine = StartCoroutine(TitleEntryLoginRoutine());
    }

    private IEnumerator TitleEntryLoginRoutine()
    {
        TitleLoginOverlay.Ensure().Show();
        yield return null;

        if (isLoggedIn)
        {
            TitleLoginOverlay.Ensure().Hide();
            titleEntryLoginRoutine = null;
            yield break;
        }

        UnityEngine.Debug.Log("[Naver] Title 진입 자동 로그인 시도");
        yield return TryAutoLogin();

        if (isLoggedIn)
        {
            TitleLoginOverlay.Ensure().Hide();
            titleEntryLoginRoutine = null;
            yield break;
        }

        UnityEngine.Debug.Log("[Naver] Title 진입 자동 로그인 실패 - 브라우저 로그인으로 전환");
        ContinueNaverLogin();

        while (isLoggingIn)
        {
            yield return null;
        }

        EndLoginOverlayIfIdle();
        titleEntryLoginRoutine = null;
    }

    private void OnNaverLoginClicked()
    {
        if (isLoggingIn)
        {
            return;
        }

        if (buttonLoginRoutine != null)
        {
            StopCoroutine(buttonLoginRoutine);
        }

        buttonLoginRoutine = StartCoroutine(TryNaverLoginOnButtonClicked());
    }

    private IEnumerator TryNaverLoginOnButtonClicked()
    {
        TitleLoginOverlay.Ensure().Show();
        yield return null;

        if (isLoggedIn)
        {
            TitleLoginOverlay.Ensure().Hide();
            yield break;
        }

        UnityEngine.Debug.Log("[Naver] 네이버 자동 로그인 시도");
        yield return TryAutoLogin();

        if (isLoggedIn)
        {
            TitleLoginOverlay.Ensure().Hide();
            buttonLoginRoutine = null;
            yield break;
        }

        UnityEngine.Debug.Log("[Naver] 자동 로그인 실패 - 브라우저 로그인으로 전환");
        ContinueNaverLogin();

        while (isLoggingIn)
        {
            yield return null;
        }

        EndLoginOverlayIfIdle();
        buttonLoginRoutine = null;
    }

    private void EndLoginOverlayIfIdle()
    {
        if (isLoggingIn || isLoggedIn)
        {
            return;
        }

        if (SceneManager.GetActiveScene().name != GameSceneNames.Title)
        {
            return;
        }

        TitleLoginOverlay.Ensure().Hide();
    }

    private void ContinueNaverLogin()
    {
        var sessionToken = NaverLoginSession.GetToken();

        StopBrowserLoginPolling();
        OpenInChrome(loginUrl);

        if (!string.IsNullOrEmpty(sessionToken))
        {
            StartCoroutine(CallAuthMe(sessionToken, openOAuthOnFail: false));
            return;
        }

        StartBrowserLoginPolling();
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
        OpenInChrome(devLoginUrl);
    }

    public void SyncLoginFromBrowser()
    {
        if (isLoggingIn)
        {
            return;
        }

        TitleLoginOverlay.Ensure().Show();
        StartCoroutine(SyncFromBrowserRoutine());
    }

    private IEnumerator SyncFromBrowserRoutine()
    {
        var sessionToken = NaverLoginSession.GetToken();
        if (!string.IsNullOrEmpty(sessionToken))
        {
            yield return CallAuthMe(sessionToken, openOAuthOnFail: false);
            EndLoginOverlayIfIdle();
            yield break;
        }

        var bridgeToken = string.Empty;
        yield return TryFetchBrowserBridgeSession(token => bridgeToken = token);

        if (!string.IsNullOrEmpty(bridgeToken))
        {
            yield return CallAuthMe(bridgeToken, openOAuthOnFail: false);
            EndLoginOverlayIfIdle();
            yield break;
        }

        loggedInUserLabel = "브라우저 로그인 정보 없음";
        UnityEngine.Debug.LogWarning("[Naver] 동기화할 브라우저 로그인 세션이 없습니다. 네이버 로그인 버튼으로 브라우저를 여세요.");
        EndLoginOverlayIfIdle();
    }

    public void Logout()
    {
        StopBrowserLoginPolling();

        var sessionToken = NaverLoginSession.GetToken();
        if (!string.IsNullOrEmpty(sessionToken))
        {
            StartCoroutine(SendLogout(sessionToken));
        }
        else
        {
            NaverLoginSession.ClearToken();
            SetLoggedInState(false, null);
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
                SetLoggedInState(false, null);
                UnityEngine.Debug.Log("[Naver] 로그아웃 완료");
            },
            error => UnityEngine.Debug.LogWarning($"[Naver] 로그아웃 요청 실패: {error}"));

        yield return NaverLoginSession.DeleteJson(
            authBridgeUrl,
            () => UnityEngine.Debug.Log("[Naver] 브라우저 세션 브리지 삭제"),
            error => UnityEngine.Debug.LogWarning($"[Naver] 브라우저 세션 브리지 삭제 실패: {error}"));
    }

    private IEnumerator CallAuthMe(string sessionToken, bool openOAuthOnFail)
    {
        isLoggingIn = true;
        TitleLoginOverlay.Ensure().Show();
        var completed = false;
        var failed = false;

        yield return NaverLoginSession.PostAuth(
            authMeUrl,
            sessionToken,
            responseText =>
            {
                UnityEngine.Debug.Log($"[Naver] /auth/me 응답: {responseText}");
                HandleLoginPayload(responseText);
                completed = true;
            },
            error =>
            {
                UnityEngine.Debug.LogWarning($"[Naver] /auth/me 실패: {error}");
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
                UnityEngine.Debug.Log($"[Naver] /auth/refresh 응답: {responseText}");
                HandleLoginPayload(responseText);
                completed = true;
            },
            error =>
            {
                UnityEngine.Debug.LogWarning($"[Naver] /auth/refresh 실패: {error}");
                failed = true;
            });

        if (failed || !completed)
        {
            NaverLoginSession.ClearToken();
            SetLoggedInState(false, null);
            if (openOAuthOnFail)
            {
                OpenOAuthInBrowser();
            }
        }
    }

    private void SetLoggedInState(bool loggedIn, JToken user)
    {
        isLoggedIn = loggedIn;

        if (!loggedIn || user == null)
        {
            loggedInUserLabel = "로그인 필요";
            homeSceneRequested = false;
            return;
        }

        var name = user["name"]?.ToString();
        var email = user["email"]?.ToString();
        loggedInUserLabel = string.IsNullOrEmpty(name)
            ? "로그인됨"
            : $"로그인됨: {name} ({email})";
    }

    private void NavigateToHomeScene()
    {
        if (homeSceneRequested)
        {
            return;
        }

        if (SceneManager.GetActiveScene().name == GameSceneNames.Home)
        {
            return;
        }

        homeSceneRequested = true;
        App.Instance.GoToHome();
    }

    private void OpenOAuthInBrowser()
    {
        StopBrowserLoginPolling();
        OpenInChrome(loginUrl);
        StartBrowserLoginPolling();
    }

    private void OpenInChrome(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (TryOpenChrome(url))
        {
            UnityEngine.Debug.Log($"[Naver] Chrome에서 열기: {url}");
            return;
        }
#endif

        Application.OpenURL(url);
        UnityEngine.Debug.Log($"[Naver] 기본 브라우저에서 열기: {url}");
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private static bool TryOpenChrome(string url)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var chromeCandidates = new[]
        {
            Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe"),
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
        };

        foreach (var chromePath in chromeCandidates)
        {
            if (!File.Exists(chromePath))
            {
                continue;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = chromePath,
                    Arguments = $"\"{url}\"",
                    UseShellExecute = true,
                });
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Naver] Chrome 실행 실패 ({chromePath}): {ex.Message}");
            }
        }

        return false;
    }
#endif

    private void StartBrowserLoginPolling()
    {
        StopBrowserLoginPolling();
        browserLoginPollRoutine = StartCoroutine(PollBrowserLoginRoutine());
    }

    private void StopBrowserLoginPolling()
    {
        if (browserLoginPollRoutine != null)
        {
            StopCoroutine(browserLoginPollRoutine);
            browserLoginPollRoutine = null;
        }

        isLoggingIn = false;
    }

    private IEnumerator PollBrowserLoginRoutine()
    {
        loggedInUserLabel = "브라우저에서 로그인해 주세요...";
        isLoggingIn = true;
        TitleLoginOverlay.Ensure().Show();

        for (var attempt = 0; attempt < browserLoginPollMaxAttempts; attempt++)
        {
            if (isLoggedIn)
            {
                break;
            }

            var bridgeToken = string.Empty;
            yield return TryFetchBrowserBridgeSession(token => bridgeToken = token);

            if (!string.IsNullOrEmpty(bridgeToken))
            {
                yield return CallAuthMe(bridgeToken, openOAuthOnFail: false);
                break;
            }

            yield return new WaitForSeconds(browserLoginPollIntervalSeconds);
        }

        if (!isLoggedIn)
        {
            loggedInUserLabel = "로그인 필요";
            UnityEngine.Debug.LogWarning("[Naver] 브라우저 로그인 대기 시간이 초과되었습니다. 로그인 동기화 버튼을 눌러 주세요.");
        }

        isLoggingIn = false;
        if (!isLoggedIn)
        {
            EndLoginOverlayIfIdle();
        }

        browserLoginPollRoutine = null;
    }

    private void HandleLoginPayload(string message)
    {
        try
        {
            var json = JObject.Parse(message);
            var success = json["success"]?.Value<bool>() ?? false;

            if (!success)
            {
                UnityEngine.Debug.LogWarning($"[Naver] 로그인 실패: {json["message"]}");
                SetLoggedInState(false, null);
                return;
            }

            var sessionToken = json["data"]?["sessionToken"]?.ToString();
            if (!string.IsNullOrEmpty(sessionToken))
            {
                NaverLoginSession.SaveToken(sessionToken);
                UnityEngine.Debug.Log("[Naver] sessionToken 저장 완료");
            }

            var loginType = json["data"]?["loginType"]?.ToString();
            var user = json["data"]?["user"];

            if (user != null)
            {
                SetLoggedInState(true, user);
                StopBrowserLoginPolling();
                TitleLoginOverlay.Ensure().Hide();
                NavigateToHomeScene();
                var uid = user["uid"]?.ToString();
                var email = user["email"]?.ToString();
                var name = user["name"]?.ToString();
                UnityEngine.Debug.Log($"loginType: {loginType}\nuid: {uid}\nemail: {email}\nname: {name}");
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[Naver] JSON 파싱 실패: {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        StopBrowserLoginPolling();

        if (callbackListener != null)
        {
            callbackListener.OnTokenReceived -= HandleCallbackToken;
            callbackListener.OnErrorReceived -= HandleCallbackError;
        }

        UnbindButton(naverLoginButton, OnNaverLoginClicked);
        UnbindButton(devLoginButton, OpenDevLogin);
        UnbindButton(syncLoginButton, SyncLoginFromBrowser);
        UnbindButton(logoutButton, Logout);
    }

    private static void UnbindButton(Button button, UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
    }
}
