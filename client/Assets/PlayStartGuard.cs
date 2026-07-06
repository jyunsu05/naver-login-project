using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayStartGuard
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsurePlayStartsFromApp()
    {
#if UNITY_EDITOR
        if (SceneManager.GetActiveScene().name == GameSceneNames.App)
        {
            return;
        }

        if (Object.FindAnyObjectByType<App>() != null)
        {
            return;
        }

        Debug.LogWarning(
            $"[App] '{SceneManager.GetActiveScene().name}' 씬에서 Play되어 App 씬으로 이동합니다.");

        SceneManager.LoadScene(GameSceneNames.App);
#endif
    }
}
