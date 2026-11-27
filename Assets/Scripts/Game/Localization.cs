using System.Collections.Generic;
using Chess2.Core;
using UnityEngine;

/// <summary>
/// Язык интерфейса: русский или английский. В коде пишем по-русски, а L.T("…") подставляет перевод
/// из словаря (Localization.En.cs). Нет перевода — остаётся русский текст.
/// L.F("Ход {0}", n) — то же с подстановкой значений.
///
/// Язык по умолчанию — как у системы (русский для русского, украинского, белорусского), иначе английский.
/// Меняется в опциях; интерфейс при этом пересобирается (см. MenuManager.ReloadForLanguage).
/// </summary>
public static partial class L
{
    private static bool? sEnglish;

    public static bool English => sEnglish ??= Resolve();

    /// <summary>Перечитать выбранный язык (после смены в опциях) и передать переводчик ядру.</summary>
    public static void Reload()
    {
        sEnglish = Resolve();
        CoreText.T = T;
    }

    /// <summary>Какой язык будет после пересборки (по текущей настройке) — чтобы не пересобирать зря.</summary>
    public static bool WouldBeEnglish => Resolve();

    private static bool Resolve()
    {
        switch (AppSettings.Language)
        {
            case 0: return false;
            case 1: return true;
        }
        SystemLanguage sys = Application.systemLanguage;
        return sys != SystemLanguage.Russian && sys != SystemLanguage.Ukrainian && sys != SystemLanguage.Belarusian;
    }

    public static string T(string ru)
    {
        if (ru == null || !English)
            return ru;
        return En.TryGetValue(ru, out string en) ? en : ru;
    }

    public static string F(string ru, params object[] args) => string.Format(T(ru), args);

    /// <summary>Английские надписи для кнопок-картинок с русским текстом (рисуются плашкой с текстом).</summary>
    public static readonly Dictionary<string, string> ButtonLabels = new Dictionary<string, string>
    {
        { "continue", "Continue" },
        { "menu", "Menu" },
        { "options", "Options" },
        { "exit", "Exit" },
        { "play_bot", "Play vs Bot" },
        { "play_players", "Play vs Players" },
        { "save", "Save" },
        { "search", "Search" },
        { "send", "Send" },
    };
}
