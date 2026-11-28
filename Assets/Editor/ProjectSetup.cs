using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Нужные игре настройки сборки. Выставляются автоматически при загрузке проекта в редакторе,
/// чтобы не зависеть от того, что кто-то поменял в Project Settings руками.
/// </summary>
[InitializeOnLoad]
public static class ProjectSetup
{
    private const string IconDir = "Assets/Art/Icon/";
    private const string IconPath = IconDir + "icon.png";
    private const string CursorPath = "Assets/Resources/UI/cursor.png";

    /// <summary>Название игры: заголовок окна, магазины, папка настроек и сохранений.</summary>
    public const string ProductName = "Ash & Crown";
    public const string CompanyName = "VoidRunar";

    /// <summary>Идентификатор приложения в Google Play и App Store. После первой публикации не меняется.</summary>
    public const string BundleId = "com.voidrunar.ashandcrown";

    /// <summary>Первая версия для магазинов. Дальше версию повышают вручную в Player Settings.</summary>
    private const string FirstVersion = "0.9.0";

    static ProjectSetup()
    {
        if (PlayerSettings.productName != ProductName)
            PlayerSettings.productName = ProductName;
        if (PlayerSettings.companyName != CompanyName)
            PlayerSettings.companyName = CompanyName;
        ApplyFirstVersion();

        // В оконном режиме окно можно тянуть мышкой за край.
        if (!PlayerSettings.resizableWindow)
            PlayerSettings.resizableWindow = true;

        // Alt+Enter переключает окно и полный экран (F11 игра обрабатывает сама).
        if (!PlayerSettings.allowFullscreenSwitch)
            PlayerSettings.allowFullscreenSwitch = true;

        ApplyMobile();

        // Иконку ставим, когда ассеты уже импортированы.
        EditorApplication.delayCall += UseBuiltInRenderer;
        EditorApplication.delayCall += ApplyIcon;
        EditorApplication.delayCall += ApplyStoreIcons;
        EditorApplication.delayCall += ApplyCursor;
    }

    /// <summary>Версия выставляется один раз — пока стоит заводская (0.1 / 0.1.0 / 1.0).</summary>
    private static void ApplyFirstVersion()
    {
        string v = PlayerSettings.bundleVersion;
        if (v != "0.1" && v != "0.1.0" && v != "1.0" && !string.IsNullOrEmpty(v))
            return;
        PlayerSettings.bundleVersion = FirstVersion;
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.iOS.buildNumber = "1";
    }

    /// <summary>
    /// Телефоны (Google Play и App Store): только вертикальная ориентация, IL2CPP и ARM64 (так требует Google Play),
    /// сборка в .aab, разрешение на интернет (игра по сети), урезание неиспользуемого кода (меньше сборка).
    /// </summary>
    private static void ApplyMobile()
    {
        // На телефоне игра вертикальная: доска сверху, панели и кнопки под ней.
        if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

        // Standalone — это macOS: по идентификатору там хранятся настройки и сохранения игры.
        foreach (NamedBuildTarget target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone })
        {
            string id = PlayerSettings.GetApplicationIdentifier(target);
            // Заводской или временный идентификатор — меняем на настоящий.
            if (string.IsNullOrEmpty(id) || id.Contains("DefaultCompany") || id.Contains("unity.template")
                || id == "com.ashandcrown.game")
                PlayerSettings.SetApplicationIdentifier(target, BundleId);
        }

        if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        if (!PlayerSettings.Android.forceInternetPermission)
            PlayerSettings.Android.forceInternetPermission = true;
        if (!EditorUserBuildSettings.buildAppBundle)
            EditorUserBuildSettings.buildAppBundle = true; // Google Play принимает только .aab

        // Неиспользуемый код вырезается из сборки — она меньше и быстрее грузится.
        // Medium безопасен: рефлексию игра не использует, Input System сам защищает свои типы (link.xml).
        foreach (NamedBuildTarget target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            if (PlayerSettings.GetManagedStrippingLevel(target) < ManagedStrippingLevel.Medium)
                PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Medium);
        if (!PlayerSettings.stripEngineCode)
            PlayerSettings.stripEngineCode = true;

        // На iPad приложение с одной ориентацией должно занимать весь экран.
        if (!PlayerSettings.iOS.requiresFullScreen)
            PlayerSettings.iOS.requiresFullScreen = true;
    }

    /// <summary>
    /// Игра целиком нарисована интерфейсом, камера только заливает фон — 3D-рендер URP ей не нужен.
    /// Встроенный рендер легче (особенно на телефонах) и не сыплет в консоль предупреждениями URP
    /// «exceeds previous array size», которые появляются, когда в одном сеансе работают
    /// симулятор телефона и окно Game (у них разные лимиты URP).
    /// </summary>
    private static void UseBuiltInRenderer()
    {
        bool changed = false;
        if (GraphicsSettings.defaultRenderPipeline != null)
        {
            GraphicsSettings.defaultRenderPipeline = null;
            changed = true;
        }

        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            if (QualitySettings.GetRenderPipelineAssetAt(i) == null)
                continue;
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = null;
            changed = true;
        }
        QualitySettings.SetQualityLevel(current, false);

        if (changed)
            AssetDatabase.SaveAssets();
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

    /// <summary>
    /// iOS: иконка без прозрачности (App Store отклоняет иконки с прозрачными углами).
    /// Android: адаптивная иконка из двух слоёв — система сама обрежет её кругом, квадратом и т.п.
    /// </summary>
    private static void ApplyStoreIcons()
    {
        var opaque = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "icon_opaque.png");
        if (opaque != null)
        {
            int count = PlayerSettings.GetIconSizes(NamedBuildTarget.iOS, IconKind.Application).Length;
            Texture2D[] current = PlayerSettings.GetIcons(NamedBuildTarget.iOS, IconKind.Application);
            if (count > 0 && (current.Length == 0 || current[0] != opaque))
            {
                var icons = new Texture2D[count];
                for (int i = 0; i < count; i++)
                    icons[i] = opaque;
                PlayerSettings.SetIcons(NamedBuildTarget.iOS, icons, IconKind.Application);
            }
        }

#if UNITY_ANDROID
        var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "android_bg.png");
        var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "android_fg.png");
        if (bg != null && fg != null)
        {
            var kind = UnityEditor.Android.AndroidPlatformIconKind.Adaptive;
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            if (icons.Length > 0 && icons[0].GetTexture(1) != fg)
            {
                foreach (PlatformIcon i in icons)
                    i.SetTextures(bg, fg); // слой 0 — фон, слой 1 — передний план
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }
        }
#endif
        AssetDatabase.SaveAssets();
    }
}
