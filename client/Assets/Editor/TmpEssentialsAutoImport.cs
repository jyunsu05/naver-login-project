#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TmpEssentialsAutoImport
{
    static TmpEssentialsAutoImport()
    {
        EditorApplication.delayCall += TryImport;
    }

    static void TryImport()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        bool settingsMissing = !File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset");
        bool spriteAssetsMissing = !Directory.Exists("Assets/TextMesh Pro/Resources/Sprite Assets");

        if (!settingsMissing && !spriteAssetsMissing)
        {
            return;
        }

        Debug.Log("TMP Essential Resources를 가져옵니다...");
        TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
    }
}
#endif
