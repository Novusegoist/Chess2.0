// Таблицы лидеров Steam — только в версии для ПК; на телефонах заглушка (Available = false).
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
#define STEAM_SUPPORTED
#endif

using System;
using System.Collections.Generic;
using UnityEngine;
#if STEAM_SUPPORTED
using Steamworks;
#endif

/// <summary>Строка таблицы лидеров.</summary>
public struct LeaderRow
{
    public int Rank;
    public ulong SteamId;
    public int Score;
    public bool IsMe;
}

/// <summary>
/// Таблицы лидеров Steam: рейтинг, лучшая серия задач дня, повержено королей.
/// Таблицы создаются сами при первом обращении (FindOrCreateLeaderboard). Для настоящего AppID их лучше
/// заранее завести в Steamworks (Stats &amp; Achievements → Leaderboards) с теми же именами — см. README.
/// Очки отправляются, только когда значение изменилось (у Steam есть ограничение на частоту отправки).
/// </summary>
public static class SteamLeaderboards
{
    public enum Board { Rating, DailyStreak, Kings }

    public const int Count = 3;

    /// <summary>Имена таблиц в Steam (с приставкой, чтобы не путаться с чужими на тестовом AppID 480).</summary>
    private static readonly string[] Names = { "AshCrown_Rating", "AshCrown_DailyStreak", "AshCrown_Kings" };

    /// <summary>Сейчас ли у игрока значения для таблицы (0 — нечего отправлять).</summary>
    public static int CurrentValue(Board b)
    {
        switch (b)
        {
            case Board.Rating: return GameStats.RatedGames > 0 ? GameStats.Rating : 0;
            case Board.DailyStreak: return DailyChallenge.BestStreak;
            default: return GameStats.KingsFelled;
        }
    }

    /// <summary>Имена игроков подгрузились — список можно перерисовать.</summary>
    public static event Action NamesChanged;

#if STEAM_SUPPORTED
    public static bool Available => SteamService.Instance != null && SteamService.Instance.Ready;

    private static readonly SteamLeaderboard_t?[] sHandles = new SteamLeaderboard_t?[Count];
    private static readonly CallResult<LeaderboardFindResult_t>[] sFind = new CallResult<LeaderboardFindResult_t>[Count];
    private static readonly List<(Action<SteamLeaderboard_t> ok, Action<string> fail)>[] sWaiting =
        { new List<(Action<SteamLeaderboard_t>, Action<string>)>(), new List<(Action<SteamLeaderboard_t>, Action<string>)>(),
          new List<(Action<SteamLeaderboard_t>, Action<string>)>() };
    private static readonly int[] sUploaded = { -1, -1, -1 };
    // CallResult должен жить, пока Steam не ответит, — держим ссылки здесь.
    private static readonly List<object> sPending = new List<object>();
    private static Callback<PersonaStateChange_t> sPersona;

    /// <summary>Отправить в Steam всё, что изменилось с прошлого раза (после партии, задачи дня, при запуске).</summary>
    public static void SyncAll()
    {
        if (!Available)
            return;
        for (int i = 0; i < Count; i++)
        {
            int value = CurrentValue((Board)i);
            if (value <= 0 || value == sUploaded[i])
                continue;
            int board = i;
            sUploaded[i] = value; // до ответа: повторно не шлём
            // Рейтинг и короли — текущее значение (рейтинг может и упасть); серия задач — лучшая.
            var method = (Board)i == Board.DailyStreak
                ? ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest
                : ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodForceUpdate;
            WithBoard((Board)i, h =>
            {
                CallResult<LeaderboardScoreUploaded_t> cr = null;
                cr = CallResult<LeaderboardScoreUploaded_t>.Create((r, ioFail) =>
                {
                    sPending.Remove(cr);
                    if (ioFail || r.m_bSuccess == 0)
                        sUploaded[board] = -1; // попробуем в следующий раз
                });
                sPending.Add(cr);
                cr.Set(SteamUserStats.UploadLeaderboardScore(h, method, value, null, 0));
            }, _ => sUploaded[board] = -1);
        }
    }

    /// <summary>
    /// Загрузить таблицу: friends = false — 10 лучших в мире (и своё место, если ниже),
    /// friends = true — друзья по Steam. done(строки, ошибка или null).
    /// </summary>
    public static void Download(Board board, bool friends, Action<List<LeaderRow>, string> done)
    {
        if (!Available)
        {
            done(new List<LeaderRow>(), L.T("Steam недоступен"));
            return;
        }
        EnsurePersonaCallback();
        WithBoard(board, h =>
        {
            if (friends)
            {
                // Друзей может быть много — 10 лучших и своё место, чтобы список поместился на экран.
                Request(h, ELeaderboardDataRequest.k_ELeaderboardDataRequestFriends, 0, 0, rows =>
                {
                    if (rows.Count > 11)
                    {
                        LeaderRow? me = rows.Find(r => r.IsMe) is var m && m.IsMe ? m : (LeaderRow?)null;
                        rows = rows.GetRange(0, 10);
                        if (me != null && !rows.Exists(r => r.IsMe))
                            rows.Add(me.Value);
                    }
                    done(rows, null);
                }, done);
                return;
            }
            Request(h, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, 1, 10, top =>
            {
                if (top.Exists(r => r.IsMe))
                {
                    done(top, null);
                    return;
                }
                // Своё место — отдельной строкой внизу, если игрок есть в таблице.
                Request(h, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobalAroundUser, 0, 0, me =>
                {
                    top.AddRange(me.FindAll(r => r.IsMe));
                    done(top, null);
                }, (_, __) => done(top, null));
            }, done);
        }, error => done(new List<LeaderRow>(), error));
    }

    /// <summary>Имя игрока Steam (пока не подгрузилось — пустая строка; потом придёт NamesChanged).</summary>
    public static string NameOf(ulong steamId)
    {
        if (!Available)
            return "";
        var id = new CSteamID(steamId);
        string name = SteamFriends.GetFriendPersonaName(id);
        if (string.IsNullOrEmpty(name) || name == "[unknown]")
        {
            SteamFriends.RequestUserInformation(id, true);
            return "…";
        }
        return name;
    }

    private static void Request(SteamLeaderboard_t h, ELeaderboardDataRequest kind, int from, int to,
        Action<List<LeaderRow>> ok, Action<List<LeaderRow>, string> fail)
    {
        CallResult<LeaderboardScoresDownloaded_t> cr = null;
        cr = CallResult<LeaderboardScoresDownloaded_t>.Create((r, ioFail) =>
        {
            sPending.Remove(cr);
            if (ioFail)
            {
                fail(new List<LeaderRow>(), L.T("Steam не ответил, попробуй позже"));
                return;
            }
            var rows = new List<LeaderRow>(r.m_cEntryCount);
            ulong me = SteamUser.GetSteamID().m_SteamID;
            for (int i = 0; i < r.m_cEntryCount; i++)
            {
                if (!SteamUserStats.GetDownloadedLeaderboardEntry(r.m_hSteamLeaderboardEntries, i, out LeaderboardEntry_t e, null, 0))
                    continue;
                rows.Add(new LeaderRow
                {
                    Rank = e.m_nGlobalRank,
                    SteamId = e.m_steamIDUser.m_SteamID,
                    Score = e.m_nScore,
                    IsMe = e.m_steamIDUser.m_SteamID == me
                });
            }
            ok(rows);
        });
        sPending.Add(cr);
        cr.Set(SteamUserStats.DownloadLeaderboardEntries(h, kind, from, to));
    }

    /// <summary>Найти (или создать) таблицу и выполнить действие с ней; пока ищем — действия ждут в очереди.</summary>
    private static void WithBoard(Board board, Action<SteamLeaderboard_t> ok, Action<string> fail)
    {
        int i = (int)board;
        if (sHandles[i] is SteamLeaderboard_t known)
        {
            ok(known);
            return;
        }
        sWaiting[i].Add((ok, fail));
        if (sFind[i] != null)
            return;
        sFind[i] = CallResult<LeaderboardFindResult_t>.Create((r, ioFail) =>
        {
            sFind[i] = null;
            var waiting = new List<(Action<SteamLeaderboard_t> ok, Action<string> fail)>(sWaiting[i]);
            sWaiting[i].Clear();
            bool found = !ioFail && r.m_bLeaderboardFound != 0;
            if (found)
                sHandles[i] = r.m_hSteamLeaderboard;
            foreach (var w in waiting)
            {
                if (found)
                    w.ok(r.m_hSteamLeaderboard);
                else
                    w.fail(L.T("Таблица лидеров недоступна"));
            }
        });
        sFind[i].Set(SteamUserStats.FindOrCreateLeaderboard(Names[i],
            ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending, ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric));
    }

    private static void EnsurePersonaCallback()
    {
        if (sPersona == null)
            sPersona = Callback<PersonaStateChange_t>.Create(_ => NamesChanged?.Invoke());
    }
#else
    public static bool Available => false;

    public static void SyncAll() { }

    public static void Download(Board board, bool friends, Action<List<LeaderRow>, string> done) =>
        done(new List<LeaderRow>(), L.T("Steam есть только в версии для ПК"));

    public static string NameOf(ulong steamId) => "";

    // Событие в заглушке не срабатывает никогда.
    private static void Unused() => NamesChanged?.Invoke();
#endif
}
