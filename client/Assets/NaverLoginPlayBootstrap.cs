using UnityEngine;

public static class NaverLoginPlayBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureRuntimeObjects()
    {
        if (Object.FindAnyObjectByType<test>() == null)
        {
            var testObject = new GameObject("test");
            testObject.AddComponent<test>();
            Debug.Log("[Naver] Play 모드용 test 오브젝트를 자동 생성했습니다.");
        }

        if (Object.FindAnyObjectByType<NaverLoginCallbackListener>() == null)
        {
            var listenerObject = new GameObject("NaverLoginCallbackListener");
            listenerObject.AddComponent<NaverLoginCallbackListener>();
            Debug.Log("[Naver] Play 모드용 콜백 리스너를 자동 생성했습니다.");
        }
    }
}
