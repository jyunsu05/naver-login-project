using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(test))]
public class testEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        var testComponent = (test)target;

        if (GUILayout.Button("버튼 다시 연결"))
        {
            testComponent.BindUIButtons();
            EditorUtility.SetDirty(testComponent);
            Debug.Log("[Naver] Canvas 버튼을 라벨/이름 기준으로 다시 연결했습니다.");
        }

        if (GUILayout.Button("네이버 로그인 테스트"))
        {
            testComponent.OpenNaverLogin();
        }
    }
}
