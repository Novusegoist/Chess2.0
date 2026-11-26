using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Нужные игре настройки сборки. Выставляются автоматически при загрузке проекта в редакторе,
/// чтобы не зависеть от того, что кто-то поменял в Project Settings руками.
/// </summary>
[InitializeOnLoad]
public static class ProjectSetup
{
    private const string IconPath = "Assets/Art/Icon/icon.png";
    private const string CursorPath = "Assets/Resources/UI/cursor.png";

    public const string ProductName = "Пепел и корона";

    static ProjectSetup()
    {
        // Название игры: заголовок окна, Steam, папка настроек.
        if (PlayerSettings.productName != ProductName)
            PlayerSettings.productName = ProductName;

        // В оконном режиме окно можно тянуть мышкой за край.
        if (!PlayerSettings.resizableWindow)
            PlayerSettings.resizableWindow = true;

        // Alt+Enter переключает окно и полный экран (F11 игра обрабатывает сама).
        if (!PlayerSettings.allowFullscreenSwitch)
            PlayerSettings.allowFullscreenSwitch = true;

        // Иконку ставим, когда ассеты уже импортированы.
        EditorApplication.delayCall += ApplyIcon;
        EditorApplication.delayCall += ApplyCursor;
    }

    /// <summary>Курсор по умолчанию в сборке — сразу при запуске, ещё до загрузки сцены.</summary>
    private static void ApplyCursor()
    {
        var cursor = AssetDatabase.LoadAssetAtPath<Texture2D>(CursorPath);
        if (cursor == null || PlayerSettings.defaultCursor == cursor)
            return;
        PlayerSettings.defaultCursor = cursor;
        PlayerSettings.cursorHotspot = new Vector2(1, 1);
        AssetDatabase.SaveAssets();
    }

    /// <summary>Иконка игры по умолчанию (для всех платформ), если её ещё не задали.</summary>
    private static void ApplyIcon()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null)
            return;
        Texture2D[] current = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
        if (current.Length > 0 && current[0] == icon)
            return;
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        AssetDatabase.SaveAssets();
    }
}
