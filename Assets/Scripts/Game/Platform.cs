using UnityEngine;

/// <summary>
/// Где запущена игра: ПК (Windows/Mac/Linux) или телефон (Android/iOS).
/// Определяется при сборке, поэтому в редакторе с выбранной платформой Android/iOS
/// сразу видно мобильное меню и опции.
/// </summary>
public static class Platform
{
#if UNITY_ANDROID || UNITY_IOS
    public static bool IsMobile => true;
#else
    public static bool IsMobile => false;
#endif

    public static bool IsDesktop => !IsMobile;

    /// <summary>
    /// Вертикальная раскладка: только на телефоне (в сборке игра там всегда вертикальная).
    /// На ПК — всегда горизонтальная. В редакторе (платформа Android/iOS) — по форме окна: широкое окно
    /// Game показывает ПК-раскладку, вертикальный симулятор — телефонную. UnityEngine.Device тут
    /// не годится: пока открыт симулятор, он и в окне Game отвечает «телефон».
    /// </summary>
#if UNITY_EDITOR
    public static bool Portrait => IsMobile && Screen.height > Screen.width;
#else
    public static bool Portrait => IsMobile;
#endif

    /// <summary>Кнопка «Выход»: на iOS приложения сами не закрываются (так требует App Store).</summary>
#if UNITY_IOS
    public static bool CanQuit => false;
#else
    public static bool CanQuit => true;
#endif
}
