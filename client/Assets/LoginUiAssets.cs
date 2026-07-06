using UnityEngine;

public class LoginUiAssets : ScriptableObject
{
    public Sprite loginIconSprite;
    public Material loginIconMaterial;

    private static LoginUiAssets instance;

    public static LoginUiAssets Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            instance = Resources.Load<LoginUiAssets>("LoginUiAssets");
            return instance;
        }
    }
}
