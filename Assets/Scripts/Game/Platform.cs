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
    /// На ПК — всегда горизонтальная. UnityEngine.Device отвечает «телефон» и в симуляторе телефона,
    /// а в обычном окне Game — «ПК», поэтому в редакторе с платформой Android окно Game остаётся
    /// горизонтальным. Повёрнут ли симулятор, не важно.
    /// </summary>
    public static bool Portrait => IsMobile && UnityEngine.Device.Application.isMobilePlatform;

    /// <summary>Кнопка «Выход»: на iOS приложения сами не закрываются (так требует App Store).</summary>
#if UNITY_IOS
    public static bool CanQuit => false;
#else
    public static bool CanQuit => true;
#endif
}
