#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EnsureLoginUiAssets
{
    private const string AssetPath = "Assets/Resources/LoginUiAssets.asset";
    private const string IconTexturePath = "Assets/free-icon-loading-3305803.png";
    private const string IconMaterialPath = "Assets/Materials/LoginIconWhite.mat";

    static EnsureLoginUiAssets()
    {
        EditorApplication.delayCall += Ensure;
    }

    private static void Ensure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        var assets = AssetDatabase.LoadAssetAtPath<LoginUiAssets>(AssetPath);
        var iconSprite = LoadMainSprite(IconTexturePath);
        var iconMaterial = AssetDatabase.LoadAssetAtPath<Material>(IconMaterialPath);

        if (assets != null
            && assets.loginIconSprite == iconSprite
            && assets.loginIconMaterial == iconMaterial)
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        if (assets == null)
        {
            assets = ScriptableObject.CreateInstance<LoginUiAssets>();
            AssetDatabase.CreateAsset(assets, AssetPath);
        }

        assets.loginIconSprite = iconSprite;
        assets.loginIconMaterial = iconMaterial;

        EditorUtility.SetDirty(assets);
        AssetDatabase.SaveAssets();
    }

    private static Sprite LoadMainSprite(string texturePath)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().ToArray();
        if (sprites.Length == 0)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        }

        if (sprites.Length == 1)
        {
            return sprites[0];
        }

        return sprites.OrderByDescending(sprite => sprite.rect.width * sprite.rect.height).First();
    }
}
#endif
