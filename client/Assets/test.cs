using Gree.UnityWebView;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

public class test : MonoBehaviour
{
    [SerializeField] private string loginUrl = "http://127.0.0.1:3000/login";

    private WebViewObject webViewObject;

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

    private void OnWebMessage(string message)
    {
        Debug.Log($"[Naver] 응답 JSON: {message}");

        try
        {
            var json = JObject.Parse(message);
            var user = json["data"]?["user"];

            if (user != null)
            {
                var uid = user["uid"]?.ToString();
                var email = user["email"]?.ToString();
                var name = user["name"]?.ToString();

                Debug.Log($"uid: {uid}\nemail: {email}\nname: {name}");
            }
            else
            {
                Debug.LogWarning("[Naver] user 정보가 응답에 없습니다.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Naver] JSON 파싱 실패: {ex.Message}");
        }

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
