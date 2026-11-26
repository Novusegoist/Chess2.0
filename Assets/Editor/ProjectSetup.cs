using UnityEditor;

/// <summary>
/// Нужные игре настройки сборки. Выставляются автоматически при загрузке проекта в редакторе,
/// чтобы не зависеть от того, что кто-то поменял в Project Settings руками.
/// </summary>
[InitializeOnLoad]
public static class ProjectSetup
{
    static ProjectSetup()
    {
        // В оконном режиме окно можно тянуть мышкой за край.
        if (!PlayerSettings.resizableWindow)
            PlayerSettings.resizableWindow = true;

        // Alt+Enter переключает окно и полный экран (F11 игра обрабатывает сама).
        if (!PlayerSettings.allowFullscreenSwitch)
            PlayerSettings.allowFullscreenSwitch = true;
    }
}
