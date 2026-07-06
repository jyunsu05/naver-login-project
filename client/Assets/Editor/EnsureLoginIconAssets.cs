#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class EnsureLoginIconAssets
{
    private const string ShaderPath = "Assets/Shaders/UI-White.shader";
    private const string ShaderMetaPath = "Assets/Shaders/UI-White.shader.meta";
    private const string MaterialPath = "Assets/Materials/LoginIconWhite.mat";
    private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";

    static EnsureLoginIconAssets()
    {
        EditorApplication.delayCall += EnsureAssets;
    }

    private static void EnsureAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EnsureShaderMeta();
        AssetDatabase.Refresh();

        var material = EnsureMaterial();
        if (material != null)
        {
            AssignMaterialToTitleScene(material);
        }
    }

    private static void EnsureShaderMeta()
    {
        if (!File.Exists(ShaderPath) || File.Exists(ShaderMetaPath))
        {
            return;
        }

        var guid = System.Guid.NewGuid().ToString("N");
        var content =
            "fileFormatVersion: 2\n" +
            $"guid: {guid}\n" +
            "ShaderImporter:\n" +
            "  externalObjects: {}\n" +
            "  defaultTextures: []\n" +
            "  nonModifiableTextures: []\n" +
            "  userData: \n" +
            "  assetBundleName: \n" +
            "  assetBundleVariant: \n";

        File.WriteAllText(ShaderMetaPath, content);
    }

    private static Material EnsureMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
        {
            return material;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            Debug.LogWarning("[App] UI/White shader를 찾을 수 없어 LoginIconWhite 머티리얼을 만들지 못했습니다.");
            return null;
        }

        material = new Material(shader)
        {
            name = "LoginIconWhite",
            color = Color.white,
        };

        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[App] {MaterialPath} 머티리얼을 생성했습니다.");

        return material;
    }

    private static void AssignMaterialToTitleScene(Material material)
    {
        if (!File.Exists(TitleScenePath))
        {
            return;
        }

        var scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        var loginIcon = GameObject.Find("LoginIcon")?.GetComponent<Image>();
        if (loginIcon == null)
        {
            return;
        }

        if (loginIcon.material == material)
        {
            return;
        }

        loginIcon.material = material;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[App] TitleScene LoginIcon에 LoginIconWhite 머티리얼을 연결했습니다.");
    }
}
#endif
