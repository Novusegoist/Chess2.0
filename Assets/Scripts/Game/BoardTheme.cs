using System;
using UnityEngine;

/// <summary>
/// Раскраски доски на выбор (опции → «Доска»). Метки ходов (точки, кольца, подсветка последнего хода)
/// одинаковые во всех темах, поэтому цвета клеток подобраны так, чтобы метки на них читались.
/// </summary>
public sealed class BoardTheme
{
    /// <summary>Название — ключ перевода (L.T).</summary>
    public readonly string Name;
    public readonly Color Light;
    public readonly Color Dark;

    private BoardTheme(string name, Color32 light, Color32 dark)
    {
        Name = name;
        Light = light;
        Dark = dark;
    }

    public static readonly BoardTheme[] All =
    {
        new BoardTheme("Дерево", new Color32(230, 220, 187, 255), new Color32(176, 136, 98, 255)),
        new BoardTheme("Пепел", new Color32(206, 202, 194, 255), new Color32(110, 104, 100, 255)),
        new BoardTheme("Ночь", new Color32(178, 198, 218, 255), new Color32(68, 98, 130, 255)),
        new BoardTheme("Лёд", new Color32(228, 240, 246, 255), new Color32(128, 166, 192, 255)),
        new BoardTheme("Луг", new Color32(236, 236, 210, 255), new Color32(118, 150, 86, 255)),
        new BoardTheme("Аметист", new Color32(228, 214, 236, 255), new Color32(134, 100, 164, 255)),
    };

    public static BoardTheme Current => All[AppSettings.BoardTheme];

    /// <summary>Тему сменили в опциях — доска перекрашивается сразу.</summary>
    public static event Action Changed;

    public static void Select(int index)
    {
        AppSettings.BoardTheme = (index % All.Length + All.Length) % All.Length;
        Changed?.Invoke();
    }
}
