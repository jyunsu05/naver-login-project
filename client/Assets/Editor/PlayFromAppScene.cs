#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PlayFromAppScene
{
    private const string AppScenePath = "Assets/Scenes/App.unity";

    static PlayFromAppScene()
    {
        EditorApplication.delayCall += ApplyPlayModeStartScene;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            ApplyPlayModeStartScene();
        }
    }

    private static void ApplyPlayModeStartScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        var appScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(AppScenePath);
        if (appScene == null)
        {
            Debug.LogWarning($"[App] Play 시작 씬을 찾을 수 없습니다: {AppScenePath}");
            return;
        }

        EditorSceneManager.playModeStartScene = appScene;
    }
}
#endif
