#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public class NaverLoginSessionToolsWindow : EditorWindow
{
    const string MenuPath = "Naver Login/Dev Reset Tools";
    const string DefaultServerUrl = "http://127.0.0.1:3000";

    Vector2 _scrollPosition;
    string _lastResetLog = string.Empty;
    bool _closeBrowsersBeforeReset = true;
    bool _ignoreBrowserLock;
    bool _isResetRunning;
    string _serverUrl = DefaultServerUrl;

    [MenuItem(MenuPath)]
    static void OpenWindow()
    {
        var window = GetWindow<NaverLoginSessionToolsWindow>("Naver Dev Reset");
        window.minSize = new Vector2(480, 520);
        window.Show();
    }

    [MenuItem("Naver Login/Session Token Tools")]
    static void OpenLegacyMenu()
    {
        OpenWindow();
    }

    void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        EditorGUILayout.LabelField("Naver Login 개발 초기화", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        DrawTokenStatus();
        EditorGUILayout.Space(12);

        EditorGUILayout.LabelField("저장 위치", EditorStyles.boldLabel);
        DrawStorageInfo();
        EditorGUILayout.Space(12);

        EditorGUILayout.LabelField("세션 토큰", EditorStyles.boldLabel);
        DrawTokenActions();
        EditorGUILayout.Space(12);

        EditorGUILayout.LabelField("전체 초기화 (원클릭)", EditorStyles.boldLabel);
        DrawFullResetSection();

        EditorGUILayout.EndScrollView();
    }

    static void DrawTokenStatus()
    {
        var token = PlayerPrefs.GetString(NaverLoginSession.SessionTokenKey, string.Empty);
        var hasToken = !string.IsNullOrEmpty(token);

        EditorGUILayout.HelpBox(
            hasToken ? "sessionToken 있음" : "sessionToken 없음",
            hasToken ? MessageType.Info : MessageType.Warning);

        using (new EditorGUI.DisabledScope(!hasToken))
        {
            EditorGUILayout.TextField("Key", NaverLoginSession.SessionTokenKey);
            EditorGUILayout.TextField("Token Preview", hasToken ? MaskToken(token) : "(empty)");
            EditorGUILayout.IntField("Length", hasToken ? token.Length : 0);
        }
    }

    static void DrawStorageInfo()
    {
        var companyName = PlayerSettings.companyName;
        var productName = PlayerSettings.productName;
        var persistentPath = Application.persistentDataPath;
        var bridgePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".naver-login-project",
            "local-session.json");

        EditorGUILayout.LabelField("PlayerPrefs", EditorStyles.miniBoldLabel);
        EditorGUILayout.SelectableLabel(
            GetPlayerPrefsLocationLabel(companyName, productName),
            EditorStyles.textField,
            GUILayout.Height(EditorGUIUtility.singleLineHeight * 2));

        EditorGUILayout.LabelField("Browser Bridge", EditorStyles.miniBoldLabel);
        EditorGUILayout.SelectableLabel(bridgePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

        EditorGUILayout.LabelField("Persistent Data", EditorStyles.miniBoldLabel);
        EditorGUILayout.SelectableLabel(persistentPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

        EditorGUILayout.HelpBox(
            "전체 초기화는 Unity sessionToken, DB users, 브라우저 브리지, Chrome/Edge 네이버·로컬서버 쿠키/스토리지(서비스 동의 포함)를 삭제합니다.",
            MessageType.None);
    }

    void DrawTokenActions()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("저장 위치 열기", GUILayout.Height(28)))
            {
                OpenStorageLocation();
            }

            if (GUILayout.Button("sessionToken만 삭제", GUILayout.Height(28)))
            {
                DeleteSessionToken();
            }

            if (GUILayout.Button("현재 유저만 초기화", GUILayout.Height(28)))
            {
                ResetCurrentUserOnServer();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("브라우저 동의 쿠키 삭제", GUILayout.Height(24)))
            {
                RevokeBrowserConsent();
            }

            if (GUILayout.Button("토큰 복사", GUILayout.Height(24)))
            {
                CopyTokenToClipboard();
            }

            if (GUILayout.Button("새로고침", GUILayout.Height(24)))
            {
                Repaint();
            }
        }
    }

    void DrawFullResetSection()
    {
        EditorGUILayout.HelpBox(
            "전체 초기화 = Unity/DB/브라우저 네이버 로그인을 모두 제거합니다.\n" +
            "1) Unity sessionToken 삭제\n" +
            "2) Naver access_token 폐기 (grant_type=delete)\n" +
            "3) MySQL users 테이블 전체 삭제\n" +
            "4) 브라우저 로그인 브리지 파일 삭제\n" +
            "5) Chrome/Edge 자동 종료 후 naver·로컬서버 쿠키/스토리지 삭제 (서비스 동의 포함)\n\n" +
            "완료 후 네이버 OAuth 동의 화면 + 서비스 이용 동의가 다시 표시됩니다.",
            MessageType.Warning);

        _serverUrl = EditorGUILayout.TextField("Server URL", _serverUrl);
        _closeBrowsersBeforeReset = EditorGUILayout.ToggleLeft(
            "Chrome/Edge 자동 종료 후 쿠키 삭제 (권장)",
            _closeBrowsersBeforeReset);
        _ignoreBrowserLock = EditorGUILayout.ToggleLeft(
            "쿠키 삭제 실패해도 계속 (브라우저 로그아웃 미완료 가능)",
            _ignoreBrowserLock);

        using (new EditorGUI.DisabledScope(_isResetRunning))
        {
            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
            };

            if (GUILayout.Button("전체 초기화 실행", buttonStyle, GUILayout.Height(36)))
            {
                RunFullReset();
            }
        }

        if (_isResetRunning)
        {
            EditorGUILayout.HelpBox("초기화 실행 중...", MessageType.Info);
        }

        if (!string.IsNullOrEmpty(_lastResetLog))
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("마지막 실행 결과", EditorStyles.miniBoldLabel);
            EditorGUILayout.TextArea(_lastResetLog, GUILayout.MinHeight(140));
        }
    }

    void RunFullReset()
    {
        if (!EditorUtility.DisplayDialog(
                "전체 초기화",
                "다음 작업을 자동으로 실행합니다.\n\n" +
                "- Unity sessionToken 삭제\n" +
                "- Naver access_token 폐기 (grant_type=delete)\n" +
                "- DB users 테이블 비우기\n" +
                "- 브라우저 브리지 파일 삭제\n" +
                "- Chrome/Edge 종료 후 naver·로컬서버 쿠키/스토리지 삭제 (서비스 동의 포함)\n\n" +
                "브라우저 네이버 로그인도 해제됩니다.\n" +
                "다음 로그인부터 네이버 OAuth 동의 + 서비스 이용 동의가 다시 표시됩니다.\n\n" +
                "계속할까요?",
                "실행",
                "취소"))
        {
            return;
        }

        _isResetRunning = true;
        _lastResetLog = string.Empty;

        try
        {
            var log = new StringBuilder();
            log.AppendLine("[1/2] Unity sessionToken 삭제");
            ClearUnitySessionToken(log);

            log.AppendLine();
            log.AppendLine("[2/2] 서버/DB/브라우저 초기화");
            var resultText = RunNodeFullResetScript(log);

            _lastResetLog = log.ToString();
            Repaint();

            var success = resultText.Contains("\"success\": true", StringComparison.Ordinal)
                || resultText.Contains("\"success\":true", StringComparison.Ordinal);
            var needsBrowserClosed = resultText.Contains("needsBrowserClosed", StringComparison.Ordinal);
            var browserLogoutComplete = resultText.Contains("\"browserLogoutComplete\": true", StringComparison.Ordinal)
                || resultText.Contains("\"browserLogoutComplete\":true", StringComparison.Ordinal);

            if (success && browserLogoutComplete)
            {
                EditorUtility.DisplayDialog(
                    "전체 초기화",
                    "전체 초기화가 완료되었습니다.\n브라우저 네이버 로그인도 해제되었습니다.",
                    "확인");
            }
            else if (success)
            {
                EditorUtility.DisplayDialog(
                    "부분 완료",
                    "Unity/DB는 초기화되었지만 브라우저 네이버 로그아웃이 완료되지 않았습니다.\n" +
                    "Chrome/Edge를 닫고 다시 실행하세요.",
                    "확인");
            }
            else if (needsBrowserClosed)
            {
                EditorUtility.DisplayDialog(
                    "브라우저 닫기 필요",
                    "Chrome/Edge를 모두 닫은 뒤 다시 실행하거나,\n" +
                    "'브라우저가 열려 있어도 계속' 옵션을 켜고 실행하세요.",
                    "확인");
            }
            else
            {
                EditorUtility.DisplayDialog("전체 초기화", "일부 단계가 실패했습니다. 아래 로그를 확인하세요.", "확인");
            }
        }
        catch (Exception ex)
        {
            _lastResetLog += Environment.NewLine + ex;
            UnityEngine.Debug.LogError($"[Naver] 전체 초기화 실패: {ex}");
            EditorUtility.DisplayDialog("전체 초기화 실패", ex.Message, "확인");
        }
        finally
        {
            _isResetRunning = false;
            Repaint();
        }
    }

    static void ClearUnitySessionToken(StringBuilder log)
    {
        var hadToken = !string.IsNullOrEmpty(PlayerPrefs.GetString(NaverLoginSession.SessionTokenKey, string.Empty));
        PlayerPrefs.DeleteKey(NaverLoginSession.SessionTokenKey);
        PlayerPrefs.DeleteKey("naver_login_consent_accepted");
        PlayerPrefs.Save();
        log.AppendLine(hadToken ? "Unity sessionToken 삭제 완료" : "Unity sessionToken 없음 (건너뜀)");
    }

    string RunNodeFullResetScript(StringBuilder log)
    {
        var scriptPath = GetDevFullResetScriptPath();
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("dev-full-reset.js를 찾을 수 없습니다.", scriptPath);
        }

        var outputFile = Path.Combine(Path.GetTempPath(), $"naver-reset-{Guid.NewGuid():N}.json");
        var args = $"\"{scriptPath}\" --output=\"{outputFile}\"";
        if (_ignoreBrowserLock)
        {
            args += " --ignore-browser-lock";
        }

        if (!_closeBrowsersBeforeReset)
        {
            args += " --keep-browsers-open";
        }

        var nodePath = ResolveNodeExecutablePath();
        log.AppendLine($"node: {nodePath}");
        log.AppendLine($"script: {scriptPath}");
        log.AppendLine($"output: {outputFile}");

        var startInfo = new ProcessStartInfo
        {
            FileName = nodePath,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(scriptPath),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("node 프로세스를 시작하지 못했습니다.");
        }

        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        log.AppendLine($"node exit code: {process.ExitCode}");
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            log.AppendLine("stderr:");
            log.AppendLine(stderr.Trim());
        }

        string resultText = string.Empty;
        try
        {
            if (!File.Exists(outputFile))
            {
                throw new InvalidOperationException(
                    "초기화 결과 파일이 생성되지 않았습니다. node와 sarver/dev-full-reset.js를 확인하세요.");
            }

            resultText = File.ReadAllText(outputFile, Encoding.UTF8).Trim();
            if (!string.IsNullOrWhiteSpace(resultText))
            {
                log.AppendLine(resultText);
            }
        }
        finally
        {
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }
        }

        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(resultText))
        {
            throw new InvalidOperationException("전체 초기화 스크립트가 실패했습니다.");
        }

        return resultText;
    }

    static string ResolveNodeExecutablePath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var folder in pathEnv.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            var candidate = Path.Combine(folder.Trim(), "node.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        const string defaultNode = @"C:\Program Files\nodejs\node.exe";
        if (File.Exists(defaultNode))
        {
            return defaultNode;
        }

        return "node";
    }

    static string GetDevFullResetScriptPath()
    {
        var clientRoot = Directory.GetParent(Application.dataPath)?.FullName;
        var repoRoot = string.IsNullOrEmpty(clientRoot)
            ? Directory.GetCurrentDirectory()
            : Directory.GetParent(clientRoot)?.FullName;

        return Path.GetFullPath(Path.Combine(repoRoot ?? Directory.GetCurrentDirectory(), "sarver", "dev-full-reset.js"));
    }

    static void OpenStorageLocation()
    {
#if UNITY_EDITOR_WIN
        var companyName = PlayerSettings.companyName;
        var productName = PlayerSettings.productName;
        var registryPath = $@"Computer\HKEY_CURRENT_USER\Software\{companyName}\{productName}";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "regedit.exe",
                Arguments = $"/m {registryPath}",
                UseShellExecute = true,
            });
            UnityEngine.Debug.Log($"[Naver] PlayerPrefs 레지스트리 위치를 열었습니다: {registryPath}");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[Naver] 레지스트리 열기 실패: {ex.Message}");
        }
#elif UNITY_EDITOR_OSX
        var plistPath = GetMacPlayerPrefsPlistPath(PlayerSettings.companyName, PlayerSettings.productName);
        if (File.Exists(plistPath))
        {
            EditorUtility.RevealInFinder(plistPath);
            UnityEngine.Debug.Log($"[Naver] PlayerPrefs plist를 열었습니다: {plistPath}");
        }
        else
        {
            EditorUtility.RevealInFinder(Path.GetDirectoryName(plistPath));
            UnityEngine.Debug.LogWarning($"[Naver] plist 파일이 아직 없습니다. Preferences 폴더를 열었습니다: {plistPath}");
        }
#endif

        var persistentPath = Application.persistentDataPath;
        if (!Directory.Exists(persistentPath))
        {
            Directory.CreateDirectory(persistentPath);
        }

        EditorUtility.RevealInFinder(persistentPath);
        UnityEngine.Debug.Log($"[Naver] PersistentData 폴더를 열었습니다: {persistentPath}");
    }

    static string GetPlayerPrefsLocationLabel(string companyName, string productName)
    {
#if UNITY_EDITOR_WIN
        return $@"HKEY_CURRENT_USER\Software\{companyName}\{productName}";
#elif UNITY_EDITOR_OSX
        return GetMacPlayerPrefsPlistPath(companyName, productName);
#else
        return $"PlayerPrefs key: {NaverLoginSession.SessionTokenKey}";
#endif
    }

    static string GetMacPlayerPrefsPlistPath(string companyName, string productName)
    {
        var preferencesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Personal),
            "Library/Preferences");
        return Path.Combine(preferencesDirectory, $"unity.{companyName}.{productName}.plist");
    }

    static void DeleteSessionToken()
    {
        var token = PlayerPrefs.GetString(NaverLoginSession.SessionTokenKey, string.Empty);
        if (string.IsNullOrEmpty(token))
        {
            EditorUtility.DisplayDialog("Session Token", "삭제할 sessionToken이 없습니다.", "확인");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Session Token 삭제",
                "저장된 sessionToken을 삭제할까요?\n다음 실행 시 네이버 OAuth가 다시 필요합니다.",
                "삭제",
                "취소"))
        {
            return;
        }

        PlayerPrefs.DeleteKey(NaverLoginSession.SessionTokenKey);
        PlayerPrefs.Save();

        UnityEngine.Debug.Log("[Naver] Editor에서 sessionToken을 삭제했습니다.");
        EditorUtility.DisplayDialog("Session Token", "sessionToken을 삭제했습니다.", "확인");
    }

    void ResetCurrentUserOnServer()
    {
        var token = PlayerPrefs.GetString(NaverLoginSession.SessionTokenKey, string.Empty);
        if (string.IsNullOrEmpty(token))
        {
            EditorUtility.DisplayDialog("현재 유저 초기화", "저장된 sessionToken이 없습니다.", "확인");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "현재 유저만 초기화",
                "서버에서 다음을 실행합니다.\n\n" +
                "- Naver access_token 폐기 (grant_type=delete)\n" +
                "- 해당 users DB 행 삭제\n" +
                "- 브라우저 브리지 삭제\n" +
                "- Unity sessionToken 삭제\n\n" +
                "MD의 /debug/reset과 같은 개념입니다.\n" +
                "브라우저 쿠키는 삭제하지 않습니다.",
                "실행",
                "취소"))
        {
            return;
        }

        try
        {
            var url = $"{_serverUrl.TrimEnd('/')}/auth/dev/reset";
            var body = new JObject { ["sessionToken"] = token }.ToString();
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = 15000;

            var bodyBytes = Encoding.UTF8.GetBytes(body);
            using (var stream = request.GetRequestStream())
            {
                stream.Write(bodyBytes, 0, bodyBytes.Length);
            }

            string responseText;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                responseText = reader.ReadToEnd();
            }

            PlayerPrefs.DeleteKey(NaverLoginSession.SessionTokenKey);
            PlayerPrefs.Save();

            UnityEngine.Debug.Log($"[Naver] 현재 유저 초기화 응답: {responseText}");
            EditorUtility.DisplayDialog(
                "현재 유저 초기화",
                "서버에서 해당 유저를 초기화했습니다.\nUnity sessionToken도 삭제했습니다.",
                "확인");
        }
        catch (WebException ex)
        {
            var details = ex.Message;
            if (ex.Response is HttpWebResponse errorResponse)
            {
                using var reader = new StreamReader(errorResponse.GetResponseStream());
                details = reader.ReadToEnd();
            }

            UnityEngine.Debug.LogError($"[Naver] 현재 유저 초기화 실패: {details}");
            EditorUtility.DisplayDialog("현재 유저 초기화 실패", details, "확인");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[Naver] 현재 유저 초기화 실패: {ex}");
            EditorUtility.DisplayDialog("현재 유저 초기화 실패", ex.Message, "확인");
        }
    }

    void RevokeBrowserConsent()
    {
        var revokeUrl = $"{_serverUrl.TrimEnd('/')}/login/consent/revoke";
        Application.OpenURL(revokeUrl);
        UnityEngine.Debug.Log($"[Naver] 브라우저 동의 쿠키 삭제 URL 열기: {revokeUrl}");
        EditorUtility.DisplayDialog(
            "브라우저 동의 쿠키",
            "Chrome에서 동의 쿠키 삭제 페이지를 열었습니다.\n다음 네이버 로그인 시 브라우저에서 동의 화면이 다시 표시됩니다.",
            "확인");
    }

    static void CopyTokenToClipboard()
    {
        var token = PlayerPrefs.GetString(NaverLoginSession.SessionTokenKey, string.Empty);
        if (string.IsNullOrEmpty(token))
        {
            EditorUtility.DisplayDialog("Session Token", "복사할 sessionToken이 없습니다.", "확인");
            return;
        }

        EditorGUIUtility.systemCopyBuffer = token;
        UnityEngine.Debug.Log("[Naver] sessionToken을 클립보드에 복사했습니다.");
    }

    static string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return string.Empty;
        }

        if (token.Length <= 12)
        {
            return new string('*', token.Length);
        }

        return $"{token[..6]}...{token[^6..]}";
    }
}
#endif
