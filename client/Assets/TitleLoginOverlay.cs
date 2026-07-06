using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleLoginOverlay : MonoBehaviour
{
    private const float IconSpinSpeed = 360f;
    private const string OverlayCanvasName = "LoginOverlayCanvas";

    private static TitleLoginOverlay instance;
    private static Sprite loadingIconSprite;
    private static Material loadingIconMaterial;

    private GameObject sceneLoginRoot;
    private Transform sceneLoginIcon;
    private bool isVisible;
    private readonly List<(GameObject gameObject, bool wasActive)> hiddenCanvasChildren = new();

    public static TitleLoginOverlay Ensure()
    {
        if (instance != null)
        {
            return instance;
        }

        if (App.Instance != null)
        {
            instance = App.Instance.GetComponent<TitleLoginOverlay>();
            if (instance == null)
            {
                instance = App.Instance.gameObject.AddComponent<TitleLoginOverlay>();
            }

            return instance;
        }

        instance = FindAnyObjectByType<TitleLoginOverlay>();
        if (instance != null)
        {
            return instance;
        }

        var controllerObject = new GameObject(nameof(TitleLoginOverlay));
        instance = controllerObject.AddComponent<TitleLoginOverlay>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        if (!isVisible || sceneLoginIcon == null)
        {
            return;
        }

        sceneLoginIcon.Rotate(0f, 0f, -IconSpinSpeed * Time.deltaTime);
    }

    private static Sprite GetLoadingIconSprite()
    {
        if (loadingIconSprite != null)
        {
            return loadingIconSprite;
        }

        var assets = LoginUiAssets.Instance;
        if (assets != null && assets.loginIconSprite != null)
        {
            loadingIconSprite = assets.loginIconSprite;
            return loadingIconSprite;
        }

        var sprites = Resources.LoadAll<Sprite>("LoginIcon");
        foreach (var sprite in sprites)
        {
            if (loadingIconSprite == null
                || sprite.rect.width * sprite.rect.height > loadingIconSprite.rect.width * loadingIconSprite.rect.height)
            {
                loadingIconSprite = sprite;
            }
        }

        return loadingIconSprite;
    }

    private static Material GetLoadingIconMaterial()
    {
        if (loadingIconMaterial != null)
        {
            return loadingIconMaterial;
        }

        var assets = LoginUiAssets.Instance;
        if (assets != null && assets.loginIconMaterial != null)
        {
            loadingIconMaterial = assets.loginIconMaterial;
            return loadingIconMaterial;
        }

        loadingIconMaterial = Resources.Load<Material>("LoginIconWhite");
        return loadingIconMaterial;
    }

    private static Canvas FindSceneCanvas()
    {
        var activeScene = SceneManager.GetActiveScene();

        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas.gameObject.name == OverlayCanvasName)
            {
                continue;
            }

            if (canvas.gameObject.scene == activeScene)
            {
                return canvas;
            }
        }

        return null;
    }

    private static void DestroyLegacyOverlayCanvas()
    {
        var legacy = GameObject.Find(OverlayCanvasName);
        if (legacy != null)
        {
            Destroy(legacy);
        }
    }

    private static bool TryFindSceneLogin(Canvas canvas, out GameObject loginRoot, out Transform loginIcon)
    {
        loginRoot = null;
        loginIcon = null;

        if (canvas == null)
        {
            return false;
        }

        foreach (var transform in canvas.GetComponentsInChildren<Transform>(true))
        {
            if (loginRoot == null && transform.name == "Login")
            {
                loginRoot = transform.gameObject;
            }

            if (loginIcon == null && transform.name == "LoginIcon")
            {
                loginIcon = transform;
            }
        }

        return loginRoot != null && loginIcon != null;
    }

    private static GameObject CreateTitleStyleLogin(Transform parent)
    {
        var loginRoot = new GameObject("Login", typeof(RectTransform));
        loginRoot.transform.SetParent(parent, false);
        loginRoot.layer = parent.gameObject.layer;

        var loginRect = loginRoot.GetComponent<RectTransform>();
        loginRect.anchorMin = new Vector2(0.5f, 0.5f);
        loginRect.anchorMax = new Vector2(0.5f, 0.5f);
        loginRect.anchoredPosition = Vector2.zero;
        loginRect.sizeDelta = new Vector2(100f, 100f);
        loginRect.localScale = Vector3.one;

        var backgroundObject = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.transform.SetParent(loginRoot.transform, false);
        backgroundObject.layer = parent.gameObject.layer;

        var backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = new Vector2(0f, 0.6517f);
        backgroundRect.sizeDelta = new Vector2(1112.0898f, 1941.1919f);
        backgroundRect.localScale = Vector3.one;

        var backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = new Color(0f, 0f, 0f, 0.48235294f);
        backgroundImage.raycastTarget = true;

        var iconObject = new GameObject("LoginIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(loginRoot.transform, false);
        iconObject.layer = parent.gameObject.layer;

        var iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(512f, 512f);
        iconRect.localScale = Vector3.one;

        var iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = GetLoadingIconSprite();
        iconImage.color = Color.white;
        iconImage.raycastTarget = true;

        var iconMaterial = GetLoadingIconMaterial();
        if (iconMaterial != null)
        {
            iconImage.material = iconMaterial;
        }

        loginRoot.SetActive(false);
        return loginRoot;
    }

    private void EnsureSceneLogin(Canvas canvas)
    {
        if (sceneLoginRoot != null && sceneLoginRoot.scene == canvas.gameObject.scene)
        {
            return;
        }

        if (sceneLoginRoot != null)
        {
            Destroy(sceneLoginRoot);
            sceneLoginRoot = null;
            sceneLoginIcon = null;
        }

        if (TryFindSceneLogin(canvas, out var existingRoot, out var existingIcon))
        {
            sceneLoginRoot = existingRoot;
            sceneLoginIcon = existingIcon;
            return;
        }

        sceneLoginRoot = CreateTitleStyleLogin(canvas.transform);
        sceneLoginIcon = sceneLoginRoot.transform.Find("LoginIcon");
    }

    private static void PrepareSceneLoginLayout(GameObject loginRoot)
    {
        if (loginRoot == null)
        {
            return;
        }

        var loginRect = loginRoot.GetComponent<RectTransform>();
        if (loginRect == null)
        {
            return;
        }

        loginRect.anchorMin = Vector2.zero;
        loginRect.anchorMax = Vector2.one;
        loginRect.offsetMin = Vector2.zero;
        loginRect.offsetMax = Vector2.zero;
        loginRect.localScale = Vector3.one;
    }

    private void HideSceneUi(Canvas sceneCanvas, GameObject exceptRoot)
    {
        hiddenCanvasChildren.Clear();
        if (sceneCanvas == null)
        {
            return;
        }

        foreach (Transform child in sceneCanvas.transform)
        {
            if (exceptRoot != null && child.gameObject == exceptRoot)
            {
                continue;
            }

            hiddenCanvasChildren.Add((child.gameObject, child.gameObject.activeSelf));
            child.gameObject.SetActive(false);
        }
    }

    private void RestoreSceneUi()
    {
        foreach (var (gameObject, wasActive) in hiddenCanvasChildren)
        {
            if (gameObject != null)
            {
                gameObject.SetActive(wasActive);
            }
        }

        hiddenCanvasChildren.Clear();
    }

    public void PlayEntrySequence()
    {
        Show();
    }

    public void SyncVisibility(bool loginInProgress)
    {
        if (loginInProgress)
        {
            Show();
            return;
        }

        Hide();
    }

    public void Show()
    {
        DestroyLegacyOverlayCanvas();

        var sceneCanvas = FindSceneCanvas();
        if (sceneCanvas == null)
        {
            Debug.LogWarning("[Login] Canvas를 찾지 못했습니다.");
            return;
        }

        EnsureSceneLogin(sceneCanvas);
        if (sceneLoginRoot == null)
        {
            Debug.LogWarning("[Login] Login UI를 준비하지 못했습니다.");
            return;
        }

        HideSceneUi(sceneCanvas, sceneLoginRoot);
        PrepareSceneLoginLayout(sceneLoginRoot);
        sceneLoginRoot.SetActive(true);
        sceneLoginRoot.transform.SetAsLastSibling();
        isVisible = true;
    }

    public void Hide()
    {
        isVisible = false;

        if (sceneLoginRoot != null)
        {
            sceneLoginRoot.SetActive(false);
        }

        RestoreSceneUi();
    }

    public IEnumerator ShowForSeconds(float seconds)
    {
        Show();
        yield return null;
        yield return new WaitForSecondsRealtime(Mathf.Max(seconds, 0.35f));
        Hide();
    }
}
