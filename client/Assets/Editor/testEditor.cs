using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(test))]
public class testEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Naver Login"))
        {
            ((test)target).OpenNaverLogin();
        }
    }
}
