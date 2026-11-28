using Chess2.Core;
using UnityEngine;

/// <summary>Настройки игрока, которые сохраняются между запусками (PlayerPrefs).</summary>
public static class AppSettings
{
    private const string Prefix = "chess2.";

    public static bool Sound
    {
        get => GetBool("sound", true);
        set => SetBool("sound", value);
    }

    public static bool Music
    {
        get => GetBool("music", true);
        set => SetBool("music", value);
    }

    public static BotDifficulty Difficulty
    {
        get => (BotDifficulty)PlayerPrefs.GetInt(Prefix + "difficulty", (int)BotDifficulty.Medium);
        set => PlayerPrefs.SetInt(Prefix + "difficulty", (int)value);
    }

    /// <summary>0 — белые, 1 — чёрные, 2 — случайно.</summary>
    public static int PlayerColor
    {
        get => PlayerPrefs.GetInt(Prefix + "color", 0);
        set => PlayerPrefs.SetInt(Prefix + "color", value);
    }

    /// <summary>Время на партию для каждого игрока, в минутах. 0 — без контроля времени.</summary>
    public static int TimeMinutes
    {
        get => PlayerPrefs.GetInt(Prefix + "time", 0);
        set => PlayerPrefs.SetInt(Prefix + "time", value);
    }

    /// <summary>Последний адрес, к которому подключались по IP.</summary>
    public static string LastAddress
    {
        get => PlayerPrefs.GetString(Prefix + "lastAddress", "127.0.0.1");
        set => PlayerPrefs.SetString(Prefix + "lastAddress", value);
    }

    /// <summary>Варианты времени, которые предлагает меню.</summary>
    public static readonly int[] TimeOptions = { 0, 10, 15, 20 };

    /// <summary>0 — «Корона» (основной), 1 — «Хаос» (все способности сразу, король слабеет).</summary>
    public static int Mode
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "mode", 0), 0, 1);
        set => PlayerPrefs.SetInt(Prefix + "mode", value);
    }

    public static bool QueenMovesAsKnight
    {
        get => GetBool("queenKnight", true);
        set => SetBool("queenKnight", value);
    }

    public static bool EnablePierce
    {
        get => GetBool("pierce", true);
        set => SetBool("pierce", value);
    }

    public static bool PierceThroughOwnPieces
    {
        get => GetBool("pierceOwn", true);
        set => SetBool("pierceOwn", value);
    }

    public static RuleSet BuildRules() => new RuleSet
    {
        WinCondition = Mode == 1 ? WinCondition.Annihilation : WinCondition.KingFall,
        AbilitiesForAll = Mode == 1,
        QueenMovesAsKnight = QueenMovesAsKnight,
        EnablePierce = EnablePierce,
        PierceThroughOwnPieces = PierceThroughOwnPieces,
        TrojanKnight = TrojanKnight
    };

    public static bool TrojanKnight
    {
        get => GetBool("trojan", true);
        set => SetBool("trojan", value);
    }

    #region Графика

    /// <summary>true — полный экран (окно без рамки на весь экран), false — окно.</summary>
    public static bool Fullscreen
    {
        get => GetBool("fullscreen", true);
        set => SetBool("fullscreen", value);
    }

    /// <summary>
    /// Размер окна «1280x720» в оконном режиме. Запоминается, когда окно тянут мышкой.
    /// Пусто — 1280x720 (или меньше, если монитор маленький).
    /// </summary>
    public static string WindowSize
    {
        get => PlayerPrefs.GetString(Prefix + "windowSize", "");
        set => PlayerPrefs.SetString(Prefix + "windowSize", value);
    }

    /// <summary>Разрешение полного экрана «1920x1080». Пусто — разрешение монитора.</summary>
    public static string Resolution
    {
        get => PlayerPrefs.GetString(Prefix + "resolution", "");
        set => PlayerPrefs.SetString(Prefix + "resolution", value);
    }

    /// <summary>Ограничение кадров в секунду. 0 — без ограничения.</summary>
    public static int FpsLimit
    {
        get => PlayerPrefs.GetInt(Prefix + "fps", 60);
        set => PlayerPrefs.SetInt(Prefix + "fps", value);
    }

    public static int[] FpsOptions => Platform.IsMobile ? MobileFps : DesktopFps;

    private static readonly int[] DesktopFps = { 30, 60, 120, 144, 0 };
    private static readonly int[] MobileFps = { 30, 60, 120 }; // «без ограничения» на телефоне = 30 кадров

    /// <summary>Вертикальная синхронизация: кадры под частоту монитора, без разрывов картинки.</summary>
    public static bool VSync
    {
        get => GetBool("vsync", false);
        set => SetBool("vsync", value);
    }

    /// <summary>Яркость: 1 — как есть, 0.5 … 1.5.</summary>
    public static float Brightness
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "brightness", 1f), 0.5f, 1.5f);
        set => PlayerPrefs.SetFloat(Prefix + "brightness", Mathf.Clamp(value, 0.5f, 1.5f));
    }

    #endregion

    #region Звук

    public static float MasterVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "volMaster", 1f));
        set => PlayerPrefs.SetFloat(Prefix + "volMaster", Mathf.Clamp01(value));
    }

    public static float SoundVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "volSound", 0.8f));
        set => PlayerPrefs.SetFloat(Prefix + "volSound", Mathf.Clamp01(value));
    }

    public static float MusicVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "volMusic", 0.6f));
        set => PlayerPrefs.SetFloat(Prefix + "volMusic", Mathf.Clamp01(value));
    }

    #endregion

    #region Игра

    /// <summary>Показывать точки и кольца возможных ходов у выбранной фигуры.</summary>
    public static bool ShowMoveHints
    {
        get => GetBool("moveHints", true);
        set => SetBool("moveHints", value);
    }

    /// <summary>Подсвечивать, откуда и куда был последний ход.</summary>
    public static bool ShowLastMove
    {
        get => GetBool("lastMove", true);
        set => SetBool("lastMove", value);
    }

    /// <summary>Эффекты на доске: искры, взрыв с тряской, падающая корона, вспышка возрождения.</summary>
    public static bool Effects
    {
        get => GetBool("effects", true);
        set => SetBool("effects", value);
    }

    /// <summary>Вибрация на телефоне при ударе, взрыве, мате и возрождении.</summary>
    public static bool Vibration
    {
        get => GetBool("vibration", true);
        set => SetBool("vibration", value);
    }

    /// <summary>Подсказка под доской о том, что значат цвета меток.</summary>
    public static bool ShowHintText
    {
        get => GetBool("hintText", true);
        set => SetBool("hintText", value);
    }

    /// <summary>Язык: -1 — как в системе, 0 — русский, 1 — English.</summary>
    /// <summary>Раскраска доски — номер в BoardTheme.All.</summary>
    public static int BoardTheme
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "boardTheme", 0), 0, global::BoardTheme.All.Length - 1);
        set => PlayerPrefs.SetInt(Prefix + "boardTheme", value);
    }

    public static int Language
    {
        get => PlayerPrefs.GetInt(Prefix + "language", -1);
        set => PlayerPrefs.SetInt(Prefix + "language", value);
    }

    /// <summary>Урок обучения пройден (по id урока).</summary>
    public static bool IsLessonDone(string id) => GetBool("lesson." + id, false);

    public static void MarkLessonDone(string id)
    {
        SetBool("lesson." + id, true);
        Save();
    }

    #endregion

    public static void Save() => PlayerPrefs.Save();

    private static bool GetBool(string key, bool def) => PlayerPrefs.GetInt(Prefix + key, def ? 1 : 0) != 0;
    private static void SetBool(string key, bool value) => PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
}
