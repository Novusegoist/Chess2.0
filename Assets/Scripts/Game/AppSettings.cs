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

    /// <summary>0 — «Шахматы 2.0» (основной), 1 — «Хаос» (все способности сразу, король слабеет).</summary>
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

    public static void Save() => PlayerPrefs.Save();

    private static bool GetBool(string key, bool def) => PlayerPrefs.GetInt(Prefix + key, def ? 1 : 0) != 0;
    private static void SetBool(string key, bool value) => PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
}
