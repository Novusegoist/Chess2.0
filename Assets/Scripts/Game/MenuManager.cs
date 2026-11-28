using System;
using System.Collections;
using System.Collections.Generic;
using Chess2.Core;
using Chess2.Core.Net;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Меню игры: главный экран, настройка партии с ботом, вдвоём и по сети (IP), опции, пауза, конец партии.
/// Всё строится из кода по картинкам из Resources/UI.
///
/// При запуске любой сцены без GameManager создаётся объект «Chess2» с GameManager и MenuManager
/// (см. Bootstrap) — сцену настраивать не нужно.
/// </summary>
[RequireComponent(typeof(GameManager))]
public class MenuManager : MonoBehaviour
{
    private enum Screen { None, Main, BotSetup, Players, LocalSetup, HostSetup, Join, Waiting, Options, Pause, GameOver, Lessons, Stats, Achievements, History }

    private const float BigButtonHeight = 84f;

    private GameManager mGame;
    private Canvas mCanvas;
    private readonly Dictionary<Screen, GameObject> mScreens = new Dictionary<Screen, GameObject>();
    private Screen mCurrent = Screen.None;
    private Screen mOptionsReturn = Screen.Main;
    private GameObject mHud;
    private Text mGameOverText;
    private Text mGameOverInfo;
    private Text mGameOverRating;
    private Text mStatsRating;
    private RectTransform mStatsGraph;
    private MenuButton mAgainButton;
    private MenuButton mRestartButton;
    private MenuButton mResignButton;
    private MenuButton mContinueButton;
    private MenuButton mDailyButton;
    private MenuButton mReviewButton;   // «Разбор партии» на экране конца партии
    private readonly List<Text> mStatsCells = new List<Text>();   // 6 строк × 3 числа
    private Text mStatsSummary;
    private MenuButton mStatsReset;
    private bool mStatsResetArmed;
    private readonly List<(Achievements.Def def, Text title, Text desc)> mAchItems =
        new List<(Achievements.Def, Text, Text)>();
    private Text mAchHeader;                                 // «Сбросить» нажали один раз — ждём подтверждения   // «Продолжить» в главном меню — есть сохранённая партия
    private readonly List<MenuButton> mLessonButtons = new List<MenuButton>();

    // Сеть: подключение, пока партия ещё не началась (дальше соединением владеет GameManager).
    private NetMatch mPendingNet;
    private Text mWaitTitle;
    private Text mWaitText;
    private Text mWaitStatus;
    private MenuButton mWaitAction;       // «Пригласить ещё раз» (только Steam-хост)
    private Text mHostTitle;
    private bool mHostViaSteam;           // экран настроек хоста открыт для игры через Steam
    private InputField mAddressField;
    private Text mJoinError;
    private readonly List<MenuButton> mHostColorButtons = new List<MenuButton>();
    private readonly List<MenuButton> mHostTimeButtons = new List<MenuButton>();
    private readonly List<MenuButton> mHostModeButtons = new List<MenuButton>();
    private Text mHostModeHint;

    // Группы «выбери один».
    private readonly List<MenuButton> mDifficultyButtons = new List<MenuButton>();
    private readonly List<MenuButton> mColorButtons = new List<MenuButton>();
    private readonly List<MenuButton> mBotModeButtons = new List<MenuButton>();
    private readonly List<MenuButton> mLocalModeButtons = new List<MenuButton>();
    private readonly List<MenuButton> mBotTimeButtons = new List<MenuButton>();
    private readonly List<MenuButton> mLocalTimeButtons = new List<MenuButton>();
    private Text mBotModeHint;
    private Text mLocalModeHint;

    // Опции.
    private readonly OptionsScreen mOptions = new OptionsScreen();
    private readonly List<MenuButton> mSoundButtons = new List<MenuButton>();
    private readonly List<MenuButton> mMusicButtons = new List<MenuButton>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    internal static void Bootstrap()
    {
        L.Reload();
        NetMatch.LocalRating = GameStats.Rating; // сопернику по сети при подключении
        // Steam живёт отдельно от интерфейса: при смене языка интерфейс пересобирается, а Steam — нет.
        if (SteamService.Instance == null)
        {
            var steam = new GameObject("Steam");
            DontDestroyOnLoad(steam);
            steam.AddComponent<SteamService>();
        }

        var game = FindAnyObjectByType<GameManager>();
        if (game == null)
            game = new GameObject("Chess2").AddComponent<GameManager>();
        if (game.GetComponent<AppPerformance>() == null)
            game.gameObject.AddComponent<AppPerformance>();
        if (game.GetComponent<SoundManager>() == null)
            game.gameObject.AddComponent<SoundManager>();
        if (game.GetComponent<MenuManager>() == null)
            game.gameObject.AddComponent<MenuManager>();
    }

    void Awake()
    {
        mGame = GetComponent<GameManager>();
        mGame.GameEnded += OnGameEnded;
        mGame.RematchOffered += OnRematchOffered;
        mGame.NetGameStarted += OnNetGameStarted;
        mGame.NetClosed += OnNetClosed;
        mGame.LessonMenuRequested += OpenLessons;
        mGame.ReviewClosed += OnReviewClosed;
    }

    void Start()
    {
        UiKit.EnsureEventSystem();
        BuildUi();
        Show(Screen.Main);
        if (sReopenOptions)
        {
            // Язык сменили в опциях главного меню — возвращаемся туда же.
            sReopenOptions = false;
            OpenOptions(Screen.Main);
        }

        SteamService steam = SteamService.Instance;
        if (steam != null && steam.Ready)
        {
            steam.InviteAccepted += JoinSteamLobby;
            // Игру запустили из приглашения друга в Steam.
            if (steam.TryTakePendingInvite(out ulong lobby))
                JoinSteamLobby(lobby);
        }
    }

    void OnDestroy()
    {
        if (mGame != null)
        {
            mGame.GameEnded -= OnGameEnded;
            mGame.RematchOffered -= OnRematchOffered;
            mGame.NetGameStarted -= OnNetGameStarted;
            mGame.NetClosed -= OnNetClosed;
            mGame.LessonMenuRequested -= OpenLessons;
            mGame.ReviewClosed -= OnReviewClosed;
        }
        mPendingNet?.Close();
        if (SteamService.Instance != null)
            SteamService.Instance.InviteAccepted -= JoinSteamLobby;
    }

    void Update()
    {
        PollPendingNet();
#if UNITY_EDITOR
        ReloadIfOrientationChanged();
#endif

        if (!EscapePressed())
            return;

        switch (mCurrent)
        {
            case Screen.None:
                if (mGame.Reviewing)
                    mGame.EndReview();
                else
                    OpenPause();
                break;
            case Screen.Pause: ClosePause(); break;
            case Screen.Options: CloseOptions(); break;
            case Screen.BotSetup:
            case Screen.Lessons:
            case Screen.Stats:
            case Screen.Players: Show(Screen.Main); break;
            case Screen.Achievements:
            case Screen.History: Show(Screen.Stats); break;
            case Screen.LocalSetup:
            case Screen.HostSetup:
            case Screen.Join: Show(Screen.Players); break;
            case Screen.Waiting: CancelNet(); break;
        }
    }

    #region Переходы

    private void Show(Screen screen)
    {
        mCurrent = screen;
        foreach (var kv in mScreens)
            kv.Value.SetActive(kv.Key == screen);

        bool inGame = mGame.State != null;
        mHud.SetActive(inGame && screen == Screen.None);
        mGame.Paused = inGame && screen != Screen.None && screen != Screen.GameOver;

        if (screen == Screen.BotSetup || screen == Screen.LocalSetup || screen == Screen.HostSetup)
            RefreshSetup();
        if (screen == Screen.Pause)
        {
            // В сетевой партии «заново» нельзя — только сдаться или реванш после конца.
            mRestartButton.gameObject.SetActive(mGame.Net == null);
            mResignButton.gameObject.SetActive(mGame.State != null && !mGame.State.IsOver && mGame.Lesson == null);
        }
        if (screen == Screen.Options)
            mOptions.Refresh();
        if (screen == Screen.Main)
        {
            RefreshAudioIcons(); // звук могли переключить в опциях
            mContinueButton.gameObject.SetActive(SaveStore.Exists);
            mDailyButton.gameObject.SetActive(DailyChallenge.Available);
            mDailyButton.SetText(DailyChallenge.SolvedToday ? L.T("Задача решена") : L.T("Задача дня"));
        }
        if (screen == Screen.Lessons)
            RefreshLessons();
        if (screen == Screen.Stats)
            RefreshStats();
        if (screen == Screen.GameOver)
            mReviewButton.gameObject.SetActive(mGame.CanReview);
        if (screen == Screen.Achievements)
            RefreshAchievements();
        if (screen == Screen.History)
            RefreshHistory();
    }

    private void StartBotGame()
    {
        int color = AppSettings.PlayerColor;
        if (color == 2)
            color = UnityEngine.Random.Range(0, 2);
        Side botSide = color == 0 ? Side.Black : Side.White;

        AppSettings.Save();
        mGame.StartGame(true, botSide, AppSettings.Difficulty, AppSettings.BuildRules(), AppSettings.TimeMinutes);
        Show(Screen.None);
    }

    private void StartLocalGame()
    {
        AppSettings.Save();
        mGame.StartGame(false, Side.Black, AppSettings.Difficulty, AppSettings.BuildRules(), AppSettings.TimeMinutes);
        Show(Screen.None);
    }

    /// <summary>«Продолжить»: вернуться к сохранённой партии.</summary>
    private void ContinueSaved()
    {
        if (mGame.ResumeSaved())
            Show(Screen.None);
        else
            mContinueButton.gameObject.SetActive(false);
    }

    private void OpenLessons()
    {
        bool daily = mGame.Lesson != null && mGame.Lesson.IsDaily;
        mGame.StopGame();
        Show(daily ? Screen.Main : Screen.Lessons);
    }

    /// <summary>«Задача дня»: сегодняшняя головоломка.</summary>
    private void StartDaily()
    {
        Lesson daily = DailyChallenge.BuildTodayLesson();
        if (daily == null)
            return;
        mGame.StartLesson(daily);
        Show(Screen.None);
    }

    private void StartLesson(Lesson lesson)
    {
        mGame.StartLesson(lesson);
        Show(Screen.None);
    }

    private void RefreshLessons()
    {
        for (int i = 0; i < mLessonButtons.Count; i++)
        {
            Lesson l = Tutorial.Lessons[i];
            mLessonButtons[i].SetText((i + 1) + ". " + L.T(l.Title) + (AppSettings.IsLessonDone(l.Id) ? L.T("  (пройден)") : ""));
        }
    }

    private void OpenPause()
    {
        if (mGame.State != null)
            Show(Screen.Pause);
    }

    private void ClosePause() => Show(Screen.None);

    private void OpenOptions(Screen returnTo)
    {
        mOptionsReturn = returnTo;
        Show(Screen.Options);
    }

    private void CloseOptions()
    {
        AppSettings.Save();
        Show(mOptionsReturn);
    }

    private void ToMainMenu()
    {
        mGame.StopGame();
        if (mLanguagePending)
        {
            ReloadForLanguage(false);
            return;
        }
        Show(Screen.Main);
    }

    #region Смена языка

    private static bool sReopenOptions;
    private bool mLanguagePending;

    /// <summary>
    /// Язык сменили в опциях. Вне партии интерфейс пересобирается сразу,
    /// в партии — когда игрок выйдет в главное меню (партия при этом сохранится).
    /// </summary>
    private void OnLanguageChanged()
    {
        bool changed = L.English != L.WouldBeEnglish;
        if (mGame.State == null)
        {
            if (changed)
                ReloadForLanguage(true);
            return;
        }
        mLanguagePending = changed;
        mOptions.ShowLanguagePending(changed);
    }

#if UNITY_EDITOR
    private bool? mBuiltPortrait;
    private float mOrientationChangedAt = -1f;

    /// <summary>
    /// Только в редакторе: переключился между окном Game и симулятором телефона (или повернул симулятор) —
    /// интерфейс собирается заново под новую форму экрана. Партия сохраняется, её можно продолжить.
    /// Сетевую игру не трогаем. В сборке форма экрана не меняется: ПК — горизонтально, телефон — вертикально.
    /// </summary>
    private void ReloadIfOrientationChanged()
    {
        bool portrait = Platform.Portrait;
        mBuiltPortrait ??= portrait;
        if (portrait == mBuiltPortrait || mGame.Net != null || mPendingNet != null)
        {
            mOrientationChangedAt = -1f;
            return;
        }
        // Пока окно меняет размер, форма может мелькать — ждём, пока устоится.
        if (mOrientationChangedAt < 0)
            mOrientationChangedAt = Time.unscaledTime;
        else if (Time.unscaledTime - mOrientationChangedAt > 0.3f)
            ReloadForLanguage(false);
    }
#endif

    /// <summary>Пересобрать весь интерфейс на новом языке (Steam, сохранения и настройки не трогаются).</summary>
    private void ReloadForLanguage(bool reopenOptions)
    {
        mGame.StopGame(); // партия сохраняется
        AppSettings.Save();
        sReopenOptions = reopenOptions;
        L.Reload();
        new GameObject("Language Reload").AddComponent<LanguageReload>();
        Destroy(gameObject); // объект игры целиком: меню, доска, звук; через кадр он соберётся заново
    }

    #endregion

    private void OnGameEnded(GameState state)
    {
        mGameOverText.text = mGame.ResultText(state);
        bool net = mGame.Net != null && mGame.Net.Transport.Status == TransportStatus.Connected;
        mAgainButton.SetText(mGame.Net != null ? L.T("Реванш") : L.T("Ещё раз"));
        mAgainButton.gameObject.SetActive(mGame.Net == null || net);
        mGameOverInfo.text = mGame.Net != null && mGame.Net.RemoteWantsRematch ? L.T("Соперник предлагает реванш!") : "";
        int? change = mGame.LastRatingChange;
        mGameOverRating.text = change == null ? ""
            : L.F("Рейтинг: {0}", GameStats.Rating) + "  " + RatingDelta(change.Value);
        StartCoroutine(ShowGameOverLater());
    }

    private void OnReviewClosed()
    {
        if (mGame.ViewingRecord)
        {
            // Разбор прошлой партии закрыли — обратно к списку.
            mGame.StopGame();
            Show(Screen.History);
        }
        else
            Show(Screen.GameOver);
    }

    private void PlayAgain()
    {
        if (mGame.Net != null)
        {
            mGame.RequestRematch();
            mGameOverInfo.text = L.T("Ждём ответа соперника…");
            return;
        }
        mGame.Restart();
        Show(Screen.None);
    }

    private void OnRematchOffered()
    {
        if (mGameOverInfo != null)
            mGameOverInfo.text = L.T("Соперник предлагает реванш!");
    }

    private void OnNetGameStarted()
    {
        if (mCurrent == Screen.GameOver || mCurrent == Screen.Waiting)
            Show(Screen.None);
    }

    private void OnNetClosed(string reason)
    {
        if (mGameOverInfo == null)
            return;
        mGameOverInfo.text = reason;
        mAgainButton.gameObject.SetActive(false);
    }

    #endregion

    #region Сеть

    private void StartHosting()
    {
        if (mHostViaSteam)
        {
            StartSteamHosting();
            return;
        }
        AppSettings.Save();
        var template = new NetSetup
        {
            HostColorChoice = AppSettings.PlayerColor,
            Mode = AppSettings.Mode,
            TimeMinutes = AppSettings.TimeMinutes,
            QueenMovesAsKnight = AppSettings.QueenMovesAsKnight,
            EnablePierce = AppSettings.EnablePierce,
            PierceThroughOwnPieces = AppSettings.PierceThroughOwnPieces,
            TrojanKnight = AppSettings.TrojanKnight
        };
        mPendingNet?.Close();
        mPendingNet = NetMatch.Host(TcpTransport.DefaultPort, template);
        mWaitAction.gameObject.SetActive(false);

        var addresses = NetInfo.LocalAddresses();
        mWaitTitle.text = L.T("Ждём соперника…");
        mWaitText.text = L.T("Скажи сопернику один из адресов и порт ") + TcpTransport.DefaultPort + ":\n\n"
                         + (addresses.Count > 0 ? string.Join("\n", addresses) : L.T("(не удалось узнать адреса)"))
                         + L.T("\n\nНа этом же компьютере — 127.0.0.1. Через интернет нужен Radmin VPN\nили открытый порт на роутере.");
        mWaitStatus.text = "";
        Show(Screen.Waiting);
    }

    private void StartJoining()
    {
        mJoinError.text = "";
        if (!NetInfo.TryParseEndpoint(mAddressField.text, out string host, out int port))
        {
            mJoinError.text = L.T("Введи адрес, например 192.168.1.5 или 26.12.34.56:7777");
            return;
        }
        AppSettings.LastAddress = mAddressField.text.Trim();
        AppSettings.Save();

        mPendingNet?.Close();
        mPendingNet = NetMatch.Join(host, port);
        mWaitAction.gameObject.SetActive(false);
        mWaitTitle.text = L.T("Подключаемся…");
        mWaitText.text = L.F("Адрес: {0}, порт {1}", host, port);
        mWaitStatus.text = "";
        Show(Screen.Waiting);
    }

    #region Steam

    private static NetSetup TemplateFromSettings() => new NetSetup
    {
        HostColorChoice = AppSettings.PlayerColor,
        Mode = AppSettings.Mode,
        TimeMinutes = AppSettings.TimeMinutes,
        QueenMovesAsKnight = AppSettings.QueenMovesAsKnight,
        EnablePierce = AppSettings.EnablePierce,
        PierceThroughOwnPieces = AppSettings.PierceThroughOwnPieces,
        TrojanKnight = AppSettings.TrojanKnight
    };

    /// <summary>«Играть с другом в Steam»: если Steam недоступен — объясняем, иначе настройки партии.</summary>
    private void OpenSteamSetup()
    {
        SteamService steam = SteamService.Instance;
        if (steam == null || !steam.Ready)
        {
            mWaitTitle.text = L.T("Steam недоступен");
            mWaitText.text = (steam?.InitError ?? L.T("Steam не запущен"))
                             + L.T("\n\nЗапусти Steam, войди в аккаунт и перезапусти игру.\nА пока можно играть по IP.");
            mWaitStatus.text = "";
            mWaitAction.gameObject.SetActive(false);
            Show(Screen.Waiting);
            return;
        }
        mHostViaSteam = true;
        mHostTitle.text = L.T("Игра с другом в Steam");
        Show(Screen.HostSetup);
    }

    /// <summary>Хост: создать лобби и пригласить друга.</summary>
    private void StartSteamHosting()
    {
        AppSettings.Save();
        mPendingNet?.Close();
        INetTransport transport = SteamService.Instance.HostLobby();
        mPendingNet = NetMatch.Create(NetRole.Host, transport, TemplateFromSettings());

        mWaitTitle.text = L.T("Пригласи друга");
        mWaitText.text = L.T("Открылось окно Steam: выбери друга и нажми «Пригласить».\n\n")
                         + L.T("Друг примет приглашение в Steam — игра у него запустится и подключится сама.\n")
                         + L.T("Окно не открылось? Нажми «Пригласить» ниже или Shift+Tab.\n\n")
                         + L.T("(Оверлей Steam работает в собранной игре; в редакторе Unity его обычно нет.)");
        mWaitStatus.text = L.T("Создаём лобби…");
        mWaitAction.gameObject.SetActive(true);
        Show(Screen.Waiting);
    }

    /// <summary>Гость: друг пригласил, мы приняли — входим в его лобби.</summary>
    private void JoinSteamLobby(ulong lobby)
    {
        // Посреди своей партии чужое приглашение не принимаем.
        if (mGame.State != null && !mGame.State.IsOver)
        {
            mGame.ShowBanner(L.T("Приглашение в Steam: сначала закончи текущую партию"));
            return;
        }
        mGame.StopGame();
        mPendingNet?.Close();
        INetTransport transport = SteamService.Instance.JoinLobby(lobby);
        mPendingNet = NetMatch.Create(NetRole.Guest, transport);

        mWaitTitle.text = L.T("Подключаемся к другу…");
        mWaitText.text = L.T("Входим в лобби Steam.");
        mWaitStatus.text = "";
        mWaitAction.gameObject.SetActive(false);
        Show(Screen.Waiting);
    }

    #endregion

    private void PollPendingNet()
    {
        if (mPendingNet == null)
            return;

        if (mPendingNet.Transport.Status == TransportStatus.Connected && mWaitStatus != null)
        {
            string friend = SteamService.Instance != null ? SteamService.Instance.OpponentName(mPendingNet.Transport) : "";
            mWaitStatus.text = (friend.Length > 0 ? friend + L.T(" на связи") : L.T("Соединение есть")) + L.T(", договариваемся о партии…");
        }
        else if (SteamService.Instance != null && SteamService.Instance.IsLobbyReady(mPendingNet.Transport) && mWaitStatus != null)
            mWaitStatus.text = L.T("Лобби создано, ждём друга…");

        foreach (NetEvent e in mPendingNet.Poll())
        {
            if (e.Kind == NetEventKind.GameStarted)
            {
                NetMatch net = mPendingNet;
                mPendingNet = null; // дальше соединением владеет GameManager
                mGame.StartNetGame(net);
                Show(Screen.None);
                return;
            }
            if (e.Kind == NetEventKind.Disconnected)
            {
                mWaitStatus.text = string.IsNullOrEmpty(e.Text) ? L.T("Соединение закрыто") : e.Text;
                mPendingNet.Close();
                mPendingNet = null;
                return;
            }
        }
    }

    private void CancelNet()
    {
        mPendingNet?.Close();
        mPendingNet = null;
        Show(Screen.Players);
    }

    private IEnumerator ShowGameOverLater()
    {
        // Даём досмотреть последний ход.
        yield return new WaitForSeconds(1.2f);
        if (mGame.State != null && mGame.State.IsOver && mCurrent == Screen.None)
            Show(Screen.GameOver);
    }

    private static void Quit()
    {
        AppSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Сборка экранов

    private void BuildUi()
    {
        mCanvas = UiKit.CreateCanvas("Chess2 Menu Canvas", transform, 10);

        BuildHud();
        mScreens[Screen.Main] = BuildMain();
        mScreens[Screen.BotSetup] = BuildBotSetup();
        mScreens[Screen.Players] = BuildPlayers();
        mScreens[Screen.LocalSetup] = BuildLocalSetup();
        mScreens[Screen.HostSetup] = BuildHostSetup();
        mScreens[Screen.Join] = BuildJoin();
        mScreens[Screen.Waiting] = BuildWaiting();
        mScreens[Screen.Options] = mOptions.Build(mCanvas.transform, CloseOptions,
            (parent, text, size, offset) => Title(parent, text, size, offset), BottomBar);
        mOptions.LanguageChanged += OnLanguageChanged;
        mScreens[Screen.Pause] = BuildPause();
        mScreens[Screen.GameOver] = BuildGameOver();
        mScreens[Screen.Lessons] = BuildLessons();
        mScreens[Screen.Stats] = BuildStats();
        mScreens[Screen.Achievements] = BuildAchievements();
        mScreens[Screen.History] = BuildHistory();
    }

    /// <summary>Кнопка «Меню» поверх доски во время партии.</summary>
    private void BuildHud()
    {
        var rt = UiKit.CreateSafeRoot(mCanvas.transform, "HUD");
        mHud = rt.gameObject;

        var btn = UiKit.CreateImageButton(rt, "menu", 56, OpenPause);
        var brt = (RectTransform)btn.transform;
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0, 1);
        brt.anchoredPosition = new Vector2(24, -24);
    }

    private GameObject BuildMain()
    {
        var root = ScreenRoot("Main", UiKit.Background);

        Text title = Title(root, L.T("ПЕПЕЛ И КОРОНА"), 96, new Vector2(0, -110));
        var sub = UiKit.CreateText("Subtitle", root, L.T("фигуры не умирают — они слабеют"), 30, new Color(0.8f, 0.85f, 0.8f, 1f));
        Place(sub.transform, new Vector2(0.5f, 1), new Vector2(0, -200));
        UiKit.FitText(sub, 1000, 40); // строка занимает 200..240 от верха

        // Кнопки — сразу под подзаголовком (от верха экрана), чтобы при любом соотношении сторон
        // и с кнопкой «Продолжить» они не наезжали на текст. Шесть кнопок занимают до ~580 из 900.
        var col = UiKit.CreateColumn("Buttons", root, 14);
        Place(col, new Vector2(0.5f, 1), new Vector2(0, -258));

        // Телефон (вертикально): кнопки крупнее, а заголовок и кнопки — единым блоком посередине экрана.
        float h = Platform.Portrait ? 110f : BigButtonHeight;
        if (Platform.Portrait)
        {
            col.GetComponent<VerticalLayoutGroup>().spacing = 16;
            Place(title.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 450));
            Place(sub.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 360));
            Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, -60));
        }
        mContinueButton = UiKit.CreateImageButton(col, "continue", h, ContinueSaved);
        UiKit.CreateImageButton(col, "play_bot", h, () => Show(Screen.BotSetup));
        UiKit.CreateImageButton(col, "play_players", h, () => Show(Screen.Players));
        UiKit.CreatePlateButton(col, L.T("Обучение"), new Vector2(361 * h / BigButtonHeight, h), () => Show(Screen.Lessons),
            Mathf.RoundToInt(38 * h / BigButtonHeight));
        UiKit.CreateImageButton(col, "options", h, () => OpenOptions(Screen.Main));
        if (Platform.CanQuit)
            UiKit.CreateImageButton(col, "exit", h, Quit);

        SoundToggles(root);

        // «Статистика» — в левом верхнем углу (справа вверху — звук и музыка).
        var stats = UiKit.CreatePlateButton(root, L.T("Статистика"), new Vector2(240, 64), () => Show(Screen.Stats), 28);
        if (Platform.Portrait)
            Place(stats.transform, new Vector2(0.5f, 0.5f), new Vector2(-135, -520));
        else
            Place(stats.transform, new Vector2(0, 1), new Vector2(40 + 120, -64));

        // «Задача дня» — под «Статистикой».
        mDailyButton = UiKit.CreatePlateButton(root, L.T("Задача дня"), new Vector2(240, 64), StartDaily, 28);
        if (Platform.Portrait)
            Place(mDailyButton.transform, new Vector2(0.5f, 0.5f), new Vector2(135, -520));
        else
            Place(mDailyButton.transform, new Vector2(0, 1), new Vector2(40 + 120, -140));

        // Номер версии — в левом нижнем углу (пригодится, когда игроки пишут об ошибках).
        var version = UiKit.CreateText("Version", root, "v" + Application.version, 18,
            new Color(0.6f, 0.68f, 0.75f, 1f), TextAnchor.LowerLeft, shadow: false);
        var vrt = (RectTransform)version.transform;
        vrt.anchorMin = vrt.anchorMax = vrt.pivot = Vector2.zero;
        vrt.anchoredPosition = new Vector2(16, 10);
        vrt.sizeDelta = new Vector2(300, 26);
        return root.gameObject;
    }

    private GameObject BuildBotSetup()
    {
        var root = ScreenRoot("BotSetup", UiKit.Background);
        Title(root, L.T("Игра с ботом"), 64, new Vector2(0, -50));

        var col = UiKit.CreateColumn("Content", root, 8);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, 20));

        Label(col, L.T("Сложность"));
        var row = UiKit.CreateRow("Difficulty", col, 14);
        string[] diff = { L.T("Новичок"), L.T("Любитель"), L.T("Сильный"), L.T("Мастер") };
        for (int i = 0; i < diff.Length; i++)
        {
            var level = (BotDifficulty)i;
            mDifficultyButtons.Add(UiKit.CreatePlateButton(row, diff[i], new Vector2(230, 66), () =>
            {
                AppSettings.Difficulty = level;
                RefreshSetup();
            }));
        }

        Label(col, L.T("Твой цвет"));
        row = UiKit.CreateRow("Color", col, 14);
        string[] colors = { L.T("Белые"), L.T("Чёрные"), L.T("Случайно") };
        for (int i = 0; i < colors.Length; i++)
        {
            int c = i;
            mColorButtons.Add(UiKit.CreatePlateButton(row, colors[i], new Vector2(230, 66), () =>
            {
                AppSettings.PlayerColor = c;
                RefreshSetup();
            }));
        }

        Label(col, L.T("Время на партию"));
        TimeRow(col, mBotTimeButtons);

        Label(col, L.T("Режим"));
        mBotModeHint = ModeRow(col, mBotModeButtons);

        BottomBar(root, () => Show(Screen.Main), "continue", StartBotGame);
        return root.gameObject;
    }

    /// <summary>«Игра с игроками»: где играем — за одним компьютером или по сети.</summary>
    private GameObject BuildPlayers()
    {
        var root = ScreenRoot("Players", UiKit.Background);
        Title(root, L.T("Игра с игроками"), 64, new Vector2(0, -70));

        var col = UiKit.CreateColumn("Content", root, 22);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, 10));
        var size = new Vector2(560, BigButtonHeight);
        UiKit.CreatePlateButton(col, L.T("За одним компьютером"), size, () => Show(Screen.LocalSetup), 34);
        if (SteamService.Supported)
            UiKit.CreatePlateButton(col, L.T("Играть с другом в Steam"), size, OpenSteamSetup, 34);
        UiKit.CreatePlateButton(col, L.T("Создать игру по сети"), size, () =>
        {
            mHostViaSteam = false;
            mHostTitle.text = L.T("Создать игру по сети");
            Show(Screen.HostSetup);
        }, 34);
        UiKit.CreatePlateButton(col, L.T("Подключиться по IP"), size, () =>
        {
            mAddressField.text = AppSettings.LastAddress;
            mJoinError.text = "";
            Show(Screen.Join);
        }, 34);

        var info = UiKit.CreateText("Info", col,
            SteamService.Supported
                ? L.T("Через Steam — приглашение другу, ничего настраивать не нужно.\nПо IP — в локальной сети, через Radmin VPN или по интернету.")
                : L.T("По IP — в одной сети Wi-Fi, через VPN или по интернету.\nИграть можно и с ПК: версии совместимы."),
            24, new Color(0.75f, 0.8f, 0.75f, 1f));
        UiKit.FitText(info, 1000, 80);

        BottomBar(root, () => Show(Screen.Main), null, null);
        return root.gameObject;
    }

    /// <summary>Хост выбирает цвет, время и режим, потом ждёт соперника.</summary>
    private GameObject BuildHostSetup()
    {
        var root = ScreenRoot("HostSetup", UiKit.Background);
        mHostTitle = Title(root, L.T("Создать игру по сети"), 64, new Vector2(0, -60));

        var col = UiKit.CreateColumn("Content", root, 10);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, 20));

        Label(col, L.T("Твой цвет"));
        var row = UiKit.CreateRow("Color", col, 14);
        string[] colors = { L.T("Белые"), L.T("Чёрные"), L.T("Случайно") };
        for (int i = 0; i < colors.Length; i++)
        {
            int c = i;
            mHostColorButtons.Add(UiKit.CreatePlateButton(row, colors[i], new Vector2(230, 66), () =>
            {
                AppSettings.PlayerColor = c;
                RefreshSetup();
            }));
        }

        Label(col, L.T("Время на партию"));
        TimeRow(col, mHostTimeButtons);

        Label(col, L.T("Режим"));
        mHostModeHint = ModeRow(col, mHostModeButtons);

        BottomBar(root, () => Show(Screen.Players), "continue", StartHosting);
        return root.gameObject;
    }

    /// <summary>Гость вводит адрес хоста.</summary>
    private GameObject BuildJoin()
    {
        var root = ScreenRoot("Join", UiKit.Background);
        Title(root, L.T("Подключиться по IP"), 64, new Vector2(0, -70));

        var col = UiKit.CreateColumn("Content", root, 16);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, 20));

        Label(col, L.T("Адрес соперника"));
        mAddressField = UiKit.CreateInputField(col, new Vector2(620, 72), L.T("например 192.168.1.5"));

        var hint = UiKit.CreateText("Hint", col,
            L.T("Адрес покажет соперник на экране «Создать игру по сети».\nПорт по умолчанию ") + TcpTransport.DefaultPort
            + L.T(", другой можно указать через двоеточие: 26.12.34.56:7777"),
            22, new Color(0.75f, 0.8f, 0.75f, 1f));
        UiKit.FitText(hint, 1000, 70);

        mJoinError = UiKit.CreateText("Error", col, "", 24, new Color32(255, 120, 110, 255));
        UiKit.FitText(mJoinError, 1000, 34);

        BottomBar(root, () => Show(Screen.Players), "continue", StartJoining);
        return root.gameObject;
    }

    /// <summary>Ожидание соперника (хост) или подключения (гость).</summary>
    private GameObject BuildWaiting()
    {
        var root = ScreenRoot("Waiting", UiKit.Background);
        mWaitTitle = Title(root, "", 64, new Vector2(0, -90));

        var col = UiKit.CreateColumn("Content", root, 24);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, 0));

        mWaitText = UiKit.CreateText("Info", col, "", 30, UiKit.TextLight);
        UiKit.FitText(mWaitText, 1100, 330);

        mWaitStatus = UiKit.CreateText("Status", col, "", 26, new Color32(255, 200, 120, 255));
        UiKit.FitText(mWaitStatus, 1100, 40);

        mWaitAction = UiKit.CreatePlateButton(col, L.T("Пригласить"), new Vector2(360, 72),
            () => SteamService.Instance?.OpenInviteDialog(), 34);
        mWaitAction.gameObject.SetActive(false);

        BottomBar(root, CancelNet, null, null);
        return root.gameObject;
    }

    private GameObject BuildLocalSetup()
    {
        var root = ScreenRoot("LocalSetup", UiKit.Background);
        Title(root, L.T("За одним компьютером"), 64, new Vector2(0, -70));

        var col = UiKit.CreateColumn("Content", root, 18);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, 20));

        var info = UiKit.CreateText("Info", col, L.T("Вдвоём за одним компьютером, ходите по очереди."), 28, UiKit.TextLight);
        UiKit.FitText(info, 1000, 50);

        Label(col, L.T("Время на партию"));
        TimeRow(col, mLocalTimeButtons);

        Label(col, L.T("Режим"));
        mLocalModeHint = ModeRow(col, mLocalModeButtons);

        BottomBar(root, () => Show(Screen.Players), "continue", StartLocalGame);
        return root.gameObject;
    }

    /// <summary>«Обучение»: список уроков, у пройденных пометка.</summary>
    private GameObject BuildLessons()
    {
        var root = ScreenRoot("Lessons", UiKit.Background);
        Title(root, L.T("Обучение"), 64, new Vector2(0, -50));

        var col = UiKit.CreateColumn("Lessons", root, 12);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, -10));
        foreach (Lesson lesson in Tutorial.Lessons)
        {
            Lesson l = lesson;
            mLessonButtons.Add(UiKit.CreatePlateButton(col, L.T(l.Title), new Vector2(520, 66), () => StartLesson(l), 30));
        }

        var hint = UiKit.CreateText("Hint", root, L.T("Короткие задания на доске: правила «Пепла и короны» за пять минут."),
            22, new Color(0.8f, 0.85f, 0.88f, 1f));
        Place(hint.transform, new Vector2(0.5f, 1), new Vector2(0, -130));

        BottomBar(root, () => Show(Screen.Main), null, null);
        return root.gameObject;
    }

    private GameObject BuildStats()
    {
        var root = ScreenRoot("Stats", UiKit.Background);
        Title(root, L.T("Статистика"), 64, new Vector2(0, -50));

        var table = UiKit.CreateColumn("Table", root, 6);
        Place(table, new Vector2(0.5f, 1), new Vector2(0, -170));

        string[] header = { "", L.T("Победы"), L.T("Поражения"), L.T("Ничьи") };
        string[] labels = { L.T("Бот: новичок"), L.T("Бот: любитель"), L.T("Бот: сильный"), L.T("Бот: мастер"), L.T("По сети"), L.T("Вдвоём*") };
        var light = new Color(0.82f, 0.86f, 0.9f, 1f);

        RectTransform row = UiKit.CreateRow("Header", table, 0);
        for (int c = 0; c < header.Length; c++)
            StatsCell(row, header[c], c == 0 ? 320 : 200, 24, new Color32(255, 215, 106, 255));
        for (int r = 0; r < labels.Length; r++)
        {
            row = UiKit.CreateRow("Row " + r, table, 0);
            StatsCell(row, labels[r], 320, 28, light);
            for (int c = 0; c < 3; c++)
                mStatsCells.Add(StatsCell(row, "0", 200, 30, UiKit.TextLight));
        }

        // Рейтинг: число и график последних партий. На ПК — справа от таблицы, на телефоне — под сводкой.
        var ratingBox = UiKit.CreateUi("Rating", root);
        ratingBox.sizeDelta = new Vector2(300, 230);
        if (Platform.Portrait)
            Place(ratingBox, new Vector2(0.5f, 1), new Vector2(0, -820));
        else
            Place(ratingBox, new Vector2(1, 1), new Vector2(-190, -290));
        mStatsRating = UiKit.CreateText("Value", ratingBox, "", 24, light);
        mStatsRating.supportRichText = true;
        var vrt = (RectTransform)mStatsRating.transform;
        vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(0.5f, 1);
        vrt.anchoredPosition = Vector2.zero;
        vrt.sizeDelta = new Vector2(300, 130);
        mStatsGraph = UiKit.CreateUi("Graph", ratingBox, typeof(Image));
        mStatsGraph.GetComponent<Image>().color = new Color32(16, 36, 48, 170);
        mStatsGraph.anchorMin = mStatsGraph.anchorMax = mStatsGraph.pivot = new Vector2(0.5f, 0);
        mStatsGraph.anchoredPosition = Vector2.zero;
        mStatsGraph.sizeDelta = new Vector2(280, 90);

        mStatsSummary = UiKit.CreateText("Summary", root, "", 24, light);
        UiKit.FitText(mStatsSummary, 1100, 120);
        Place(mStatsSummary.transform, new Vector2(0.5f, 1), new Vector2(0, -560));

        mStatsReset = UiKit.CreatePlateButton(root, L.T("Сбросить"), new Vector2(300, 64), ResetStats, 28);
        var rrt = (RectTransform)mStatsReset.transform;
        rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(1, 0);
        rrt.anchoredPosition = new Vector2(-60, 60);

        // «Достижения» — внизу по центру.
        var ach = UiKit.CreatePlateButton(root, L.T("Достижения"), new Vector2(320, 72), () => Show(Screen.Achievements), 30);
        var art = (RectTransform)ach.transform;
        art.anchorMin = art.anchorMax = art.pivot = new Vector2(0.5f, 0);
        art.anchoredPosition = Platform.Portrait ? new Vector2(0, 150) : new Vector2(-175, 56); // на узком экране — над «Сбросить»

        var games = UiKit.CreatePlateButton(root, L.T("Прошлые партии"), new Vector2(320, 72), () => Show(Screen.History), 30);
        var grt = (RectTransform)games.transform;
        grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0);
        grt.anchoredPosition = Platform.Portrait ? new Vector2(0, 234) : new Vector2(175, 56);

        BottomBar(root, () => Show(Screen.Main), null, null);
        return root.gameObject;
    }

    #region Прошлые партии

    private RectTransform mHistoryList;
    private Text mHistoryEmpty;

    /// <summary>«Прошлые партии»: список последних партий (новые сверху); нажатие открывает разбор.</summary>
    private GameObject BuildHistory()
    {
        var root = ScreenRoot("History", UiKit.Background);
        Title(root, L.T("Прошлые партии"), 64, new Vector2(0, -50));

        // Прокручиваемый список между заголовком и кнопкой «назад».
        var view = UiKit.CreateUi("View", root, typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        view.anchorMin = Vector2.zero;
        view.anchorMax = Vector2.one;
        view.offsetMin = new Vector2(0, 150);
        view.offsetMax = new Vector2(0, -150);
        view.GetComponent<Image>().color = Color.clear;

        mHistoryList = UiKit.CreateColumn("List", view, 8);
        mHistoryList.anchorMin = mHistoryList.anchorMax = new Vector2(0.5f, 1);
        mHistoryList.pivot = new Vector2(0.5f, 1);
        mHistoryList.anchoredPosition = Vector2.zero;

        var scroll = view.GetComponent<ScrollRect>();
        scroll.content = mHistoryList;
        scroll.viewport = view;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;

        mHistoryEmpty = UiKit.CreateText("Empty", root, L.T("Здесь появятся законченные партии: с ботом, вдвоём и по сети."),
            26, new Color(0.82f, 0.86f, 0.9f, 1f));
        UiKit.FitText(mHistoryEmpty, 1000, 80);
        Place(mHistoryEmpty.transform, new Vector2(0.5f, 0.5f), Vector2.zero);

        BottomBar(root, () => Show(Screen.Stats), null, null);
        return root.gameObject;
    }

    private void RefreshHistory()
    {
        for (int i = mHistoryList.childCount - 1; i >= 0; i--)
            Destroy(mHistoryList.GetChild(i).gameObject);

        List<GameRecord> records = GameHistory.Load();
        mHistoryEmpty.gameObject.SetActive(records.Count == 0);
        float width = UiKit.Wide(1100);
        foreach (GameRecord rec in records)
        {
            var card = UiKit.CreateUi("Game", mHistoryList, typeof(Image), typeof(Button));
            card.sizeDelta = new Vector2(width, Platform.Portrait ? 92 : 64);
            card.GetComponent<Image>().color = new Color32(16, 36, 48, 210);
            GameRecord captured = rec;
            card.GetComponent<Button>().onClick.AddListener(() => OpenRecord(captured));

            string info = rec.Date.ToString("dd.MM.yyyy HH:mm") + "   " + OpponentName(rec) + "   "
                + L.F("ходов: {0}", (rec.Moves.Count + 1) / 2);
            var left = UiKit.CreateText("Info", card, info, 24, UiKit.TextLight, TextAnchor.MiddleLeft, shadow: false);
            var lrt = (RectTransform)left.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(20, 0);
            lrt.offsetMax = new Vector2(-230, 0);
            left.horizontalOverflow = HorizontalWrapMode.Wrap;

            (string result, Color color) = ResultText(rec);
            var right = UiKit.CreateText("Result", card, result, 26, color, TextAnchor.MiddleRight, shadow: false);
            right.fontStyle = FontStyle.Bold;
            var rrt = (RectTransform)right.transform;
            rrt.anchorMin = new Vector2(1, 0);
            rrt.anchorMax = Vector2.one;
            rrt.pivot = new Vector2(1, 0.5f);
            rrt.offsetMin = new Vector2(-230, 0);
            rrt.offsetMax = new Vector2(-20, 0);
        }
    }

    private void OpenRecord(GameRecord record)
    {
        if (mGame.OpenRecord(record))
            Show(Screen.None);
    }

    private static string OpponentName(GameRecord rec)
    {
        switch (rec.Opponent)
        {
            case RecordOpponent.Net: return L.T("По сети");
            case RecordOpponent.Local: return L.T("Вдвоём");
        }
        switch (rec.Difficulty)
        {
            case BotDifficulty.Easy: return L.T("Бот: новичок");
            case BotDifficulty.Medium: return L.T("Бот: любитель");
            case BotDifficulty.Hard: return L.T("Бот: сильный");
            default: return L.T("Бот: мастер");
        }
    }

    private static (string, Color) ResultText(GameRecord rec)
    {
        var gold = new Color32(255, 215, 106, 255);
        var grey = new Color(0.75f, 0.78f, 0.8f, 1f);
        if (rec.Result == GameResult.Draw)
            return (L.T("Ничья"), grey);
        if (rec.Opponent == RecordOpponent.Local)
            return (rec.Result == GameResult.WhiteWins ? L.T("Белые победили") : L.T("Чёрные победили"), gold);
        return rec.PlayerWon == true
            ? (L.T("Победа"), (Color)new Color32(140, 230, 140, 255))
            : (L.T("Поражение"), (Color)new Color32(240, 120, 110, 255));
    }

    #endregion

    /// <summary>«Достижения»: две колонки карточек, открытые — яркие, закрытые — приглушённые.</summary>
    private GameObject BuildAchievements()
    {
        var root = ScreenRoot("Achievements", UiKit.Background);
        Title(root, L.T("Достижения"), 64, new Vector2(0, -40));
        mAchHeader = UiKit.CreateText("Header", root, "", 26, new Color32(255, 215, 106, 255));
        ((RectTransform)mAchHeader.transform).sizeDelta = new Vector2(800, 36);
        Place(mAchHeader.transform, new Vector2(0.5f, 1), new Vector2(0, -118));

        var grid = UiKit.CreateUi("Grid", root, typeof(GridLayoutGroup));
        var layout = grid.GetComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(Platform.Portrait ? 920 : 600, 58);
        layout.spacing = new Vector2(20, 6);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = Platform.Portrait ? 1 : 2;
        layout.childAlignment = TextAnchor.UpperCenter;
        grid.sizeDelta = Platform.Portrait ? new Vector2(920, 19 * 64) : new Vector2(1220, 10 * 64);
        Place(grid, new Vector2(0.5f, 1), new Vector2(0, -165));

        foreach (Achievements.Def def in global::Achievements.All)
        {
            var card = UiKit.CreateUi("Card", grid, typeof(Image));
            card.GetComponent<Image>().color = new Color32(16, 36, 48, 200);
            var title = UiKit.CreateText("Title", card, L.T(def.Title), 22, UiKit.TextLight, TextAnchor.UpperLeft, shadow: false);
            title.fontStyle = FontStyle.Bold;
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0, 1);
            trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1);
            trt.offsetMin = new Vector2(16, -30);
            trt.offsetMax = new Vector2(-16, -4);
            var desc = UiKit.CreateText("Desc", card, L.T(def.Description), 17, UiKit.TextLight, TextAnchor.LowerLeft, shadow: false);
            var drt = (RectTransform)desc.transform;
            drt.anchorMin = new Vector2(0, 0);
            drt.anchorMax = new Vector2(1, 0);
            drt.pivot = new Vector2(0.5f, 0);
            drt.offsetMin = new Vector2(16, 4);
            drt.offsetMax = new Vector2(-16, 26);
            mAchItems.Add((def, title, desc));
        }

        BottomBar(root, () => Show(Screen.Stats), null, null);
        return root.gameObject;
    }

    private void RefreshAchievements()
    {
        int open = global::Achievements.UnlockedCount, total = global::Achievements.All.Count;
        mAchHeader.text = L.F("Открыто: {0} из {1}", open, total);
        foreach (var (def, title, desc) in mAchItems)
        {
            bool unlocked = global::Achievements.IsUnlocked(def.Id);
            title.text = (unlocked ? "+ " : "") + L.T(def.Title);
            title.color = unlocked ? new Color32(255, 215, 106, 255) : new Color32(140, 155, 165, 255);
            desc.color = unlocked ? new Color32(220, 228, 235, 255) : new Color32(120, 132, 140, 255);
        }
    }

    private static Text StatsCell(Transform row, string text, float width, int size, Color color)
    {
        var t = UiKit.CreateText("Cell", row, text, size, color, width >= 300 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
        ((RectTransform)t.transform).sizeDelta = new Vector2(width, 46);
        return t;
    }

    private void RefreshStats()
    {
        GameStats.Outcome[] cols = { GameStats.Outcome.Win, GameStats.Outcome.Loss, GameStats.Outcome.Draw };
        for (int r = 0; r < GameStats.Rows; r++)
            for (int c = 0; c < 3; c++)
                mStatsCells[r * 3 + c].text = GameStats.Get(r, cols[c]).ToString();

        int lessons = 0;
        foreach (Lesson l in Tutorial.Lessons)
            if (AppSettings.IsLessonDone(l.Id))
                lessons++;
        mStatsSummary.text =
            L.F("Сыграно партий: {0}   ·   Серия побед: {1} (лучшая — {2})", GameStats.TotalGames, GameStats.CurrentStreak, GameStats.BestStreak) + "\n"
            + L.F("Королей повержено: {0}   ·   Уроков пройдено: {1} из {2}", GameStats.KingsFelled, lessons, Tutorial.Lessons.Count) + "\n"
            + L.F("Задач дня решено: {0}   ·   Серия: {1} (лучшая — {2})", DailyChallenge.TotalSolved, DailyChallenge.Streak, DailyChallenge.BestStreak) + "\n"
            + L.T("<size=19>* Вдвоём: победы белых / победы чёрных / ничьи. Серия — против бота и по сети.</size>");
        mStatsSummary.supportRichText = true;

        mStatsResetArmed = false;
        mStatsReset.SetText(L.T("Сбросить"));
        RefreshRating();
    }

    private static string RatingDelta(int d) =>
        d > 0 ? "<color=#8CE68C>+" + d + "</color>" : d < 0 ? "<color=#F0786E>" + d + "</color>" : "<color=#BBBBBB>±0</color>";

    /// <summary>Рейтинг в статистике: число, лучший, и столбики — рейтинг после каждой из последних партий.</summary>
    private void RefreshRating()
    {
        int[] history = GameStats.RatingHistory;
        string last = history.Length >= 2 ? "  " + RatingDelta(history[history.Length - 1] - history[history.Length - 2]) : "";
        mStatsRating.text = L.T("Рейтинг") + "\n<size=54><b>" + GameStats.Rating + "</b></size>" + last + "\n"
            + "<size=20>" + L.F("лучший: {0}   ·   партий: {1}", GameStats.PeakRating, GameStats.RatedGames) + "</size>";

        for (int i = mStatsGraph.childCount - 1; i >= 0; i--)
            Destroy(mStatsGraph.GetChild(i).gameObject);
        mStatsGraph.gameObject.SetActive(history.Length >= 2);
        if (history.Length < 2)
            return;

        int min = int.MaxValue, max = int.MinValue;
        foreach (int v in history)
        {
            min = Mathf.Min(min, v);
            max = Mathf.Max(max, v);
        }
        float range = Mathf.Max(40, max - min); // плоский график, если рейтинг почти не менялся
        float w = mStatsGraph.sizeDelta.x - 12, h = mStatsGraph.sizeDelta.y - 12;
        float step = w / history.Length;
        for (int i = 0; i < history.Length; i++)
        {
            var bar = UiKit.CreateUi("Bar", mStatsGraph, typeof(Image));
            bool up = i == 0 || history[i] >= history[i - 1];
            bar.GetComponent<Image>().color = up ? new Color32(255, 215, 106, 255) : new Color32(200, 120, 100, 255);
            bar.GetComponent<Image>().raycastTarget = false;
            bar.anchorMin = bar.anchorMax = bar.pivot = Vector2.zero;
            float bh = 6 + (h - 6) * (history[i] - min) / range;
            bar.sizeDelta = new Vector2(Mathf.Max(2, step - 2), bh);
            bar.anchoredPosition = new Vector2(6 + i * step, 6);
        }
    }

    /// <summary>Сброс — только со второго нажатия.</summary>
    private void ResetStats()
    {
        if (!mStatsResetArmed)
        {
            mStatsResetArmed = true;
            mStatsReset.SetText(L.T("Точно сбросить?"));
            return;
        }
        GameStats.Reset();
        NetMatch.LocalRating = GameStats.Rating;
        RefreshStats();
    }

    private GameObject BuildPause()
    {
        var root = ScreenRoot("Pause", new Color(0.05f, 0.06f, 0.05f, 0.88f));
        Title(root, L.T("Пауза"), 72, new Vector2(0, -110));

        var col = UiKit.CreateColumn("Buttons", root, 22);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, -40));
        UiKit.CreateImageButton(col, "continue", BigButtonHeight, ClosePause);
        mRestartButton = UiKit.CreatePlateButton(col, L.T("Заново"), new Vector2(361, BigButtonHeight), () =>
        {
            mGame.Restart();
            Show(Screen.None);
        }, 38);
        mResignButton = UiKit.CreatePlateButton(col, L.T("Сдаться"), new Vector2(361, BigButtonHeight), () =>
        {
            Show(Screen.None);
            mGame.Resign();
        }, 38);
        UiKit.CreateImageButton(col, "options", BigButtonHeight, () => OpenOptions(Screen.Pause));
        UiKit.CreateImageButton(col, "menu", BigButtonHeight, ToMainMenu);
        return root.gameObject;
    }

    private GameObject BuildGameOver()
    {
        var root = ScreenRoot("GameOver", new Color(0.05f, 0.06f, 0.05f, 0.8f));
        mGameOverText = Title(root, "", 56, new Vector2(0, -200));
        mGameOverInfo = UiKit.CreateText("Info", root, "", 28, new Color32(255, 200, 120, 255));
        Place(mGameOverInfo.transform, new Vector2(0.5f, 1), new Vector2(0, -292));
        mGameOverRating = UiKit.CreateText("Rating", root, "", 26, UiKit.TextLight);
        mGameOverRating.supportRichText = true;
        Place(mGameOverRating.transform, new Vector2(0.5f, 1), new Vector2(0, -256));

        var col = UiKit.CreateColumn("Buttons", root, 22);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, -40));
        mAgainButton = UiKit.CreatePlateButton(col, L.T("Ещё раз"), new Vector2(361, BigButtonHeight), PlayAgain, 38);
        mReviewButton = UiKit.CreatePlateButton(col, L.T("Разбор партии"), new Vector2(361, 72), () =>
        {
            mGame.StartReview();
            if (mGame.Reviewing)
                Show(Screen.None);
        }, 32);
        UiKit.CreateImageButton(col, "menu", BigButtonHeight, ToMainMenu);
        // Можно закрыть окно и посмотреть на доску.
        UiKit.CreatePlateButton(col, L.T("Посмотреть доску"), new Vector2(361, 64), () => Show(Screen.None), 26);
        return root.gameObject;
    }

    #endregion

    #region Обновление состояния кнопок

    private void RefreshSetup()
    {
        for (int i = 0; i < mDifficultyButtons.Count; i++)
            mDifficultyButtons[i].SetSelected(i == (int)AppSettings.Difficulty);
        for (int i = 0; i < mColorButtons.Count; i++)
            mColorButtons[i].SetSelected(i == AppSettings.PlayerColor);

        RefreshModeRow(mBotModeButtons, mBotModeHint);
        RefreshModeRow(mLocalModeButtons, mLocalModeHint);
        RefreshModeRow(mHostModeButtons, mHostModeHint);
        for (int i = 0; i < mHostColorButtons.Count; i++)
            mHostColorButtons[i].SetSelected(i == AppSettings.PlayerColor);

        int time = System.Array.IndexOf(AppSettings.TimeOptions, AppSettings.TimeMinutes);
        if (time < 0)
            time = 0;
        for (int i = 0; i < mBotTimeButtons.Count; i++)
            mBotTimeButtons[i].SetSelected(i == time);
        for (int i = 0; i < mLocalTimeButtons.Count; i++)
            mLocalTimeButtons[i].SetSelected(i == time);
        for (int i = 0; i < mHostTimeButtons.Count; i++)
            mHostTimeButtons[i].SetSelected(i == time);
    }

    private static void RefreshModeRow(List<MenuButton> buttons, Text hint)
    {
        int mode = AppSettings.Mode;
        for (int i = 0; i < buttons.Count; i++)
            buttons[i].SetSelected(i == mode);
        if (hint != null)
            hint.text = mode == 0
                ? L.T("Шахматы с деградацией. После мата король уходит, а 1–3 фигуры его армии загораются и получают силу.\nАрмия без короля побеждает матом второму королю и проигрывает, когда фигур не осталось.")
                : L.T("Все способности у всех фигур сразу. Король слабеет как все, шаха нет.\nПобеда — когда у соперника не осталось ни одной фигуры.");
    }

    private void ToggleSound()
    {
        AppSettings.Sound = !AppSettings.Sound;
        RefreshAudioIcons();
    }

    private void ToggleMusic()
    {
        AppSettings.Music = !AppSettings.Music;
        RefreshAudioIcons();
    }

    private void RefreshAudioIcons()
    {
        foreach (var b in mSoundButtons)
            b.SetSprite(UiKit.LoadSprite(AppSettings.Sound ? "UI/sound" : "UI/sound_off"));
        foreach (var b in mMusicButtons)
            b.SetSprite(UiKit.LoadSprite(AppSettings.Music ? "UI/music" : "UI/music_off"));
    }

    private static string YesNo(bool v) => v ? L.T("да") : L.T("нет");

    #endregion

    #region Мелкие помощники

    private RectTransform ScreenRoot(string name, Color background)
    {
        // Фон перекрывает доску (клики не проходят насквозь), содержимое — в безопасной зоне экрана.
        return UiKit.CreateScreen(name, mCanvas.transform, background,
            background == UiKit.Background ? "bg_menu" : null);
    }

    private static Text Title(Transform parent, string text, int size, Vector2 offsetFromTop)
    {
        var t = UiKit.CreateText("Title", parent, text, size, UiKit.TextLight);
        t.fontStyle = FontStyle.Bold;
        var outline = t.gameObject.AddComponent<Outline>();
        outline.effectColor = UiKit.TextDark;
        outline.effectDistance = new Vector2(3, -3);
        Place(t.transform, new Vector2(0.5f, 1), offsetFromTop);
        return t;
    }

    private static void Label(Transform parent, string text)
    {
        var t = UiKit.CreateText("Label " + text, parent, text, 30, new Color(0.85f, 0.9f, 0.85f, 1f));
        ((RectTransform)t.transform).sizeDelta = new Vector2(600, 44);
    }

    private void TimeRow(Transform parent, List<MenuButton> into)
    {
        var row = UiKit.CreateRow("Time", parent, 14);
        foreach (int minutes in AppSettings.TimeOptions)
        {
            int m = minutes;
            string label = m == 0 ? L.T("Без времени") : m + L.T(" мин");
            into.Add(UiKit.CreatePlateButton(row, label, new Vector2(m == 0 ? 250 : 170, 60), () =>
            {
                AppSettings.TimeMinutes = m;
                RefreshSetup();
            }));
        }
    }

    private Text ModeRow(Transform parent, List<MenuButton> into)
    {
        var row = UiKit.CreateRow("Mode", parent, 14);
        into.Add(UiKit.CreatePlateButton(row, L.T("Корона"), new Vector2(300, 66), () =>
        {
            AppSettings.Mode = 0;
            RefreshSetup();
        }));
        into.Add(UiKit.CreatePlateButton(row, L.T("Хаос"), new Vector2(300, 66), () =>
        {
            AppSettings.Mode = 1;
            RefreshSetup();
        }));

        var hint = UiKit.CreateText("ModeHint", parent, "", 22, new Color(0.75f, 0.8f, 0.75f, 1f));
        UiKit.FitText(hint, 1100, 60);
        return hint;
    }

    /// <summary>Внизу: стрелка «назад» слева и главная кнопка справа.</summary>
    private static void BottomBar(Transform parent, Action back, string mainSprite, Action main)
    {
        var backBtn = UiKit.CreateImageButton(parent, "arrow", 84, back);
        var brt = (RectTransform)backBtn.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0, 0);
        brt.pivot = new Vector2(0.5f, 0); // разворот вокруг центра, иначе стрелка уедет за край
        brt.anchoredPosition = new Vector2(60 + brt.sizeDelta.x / 2, 50);
        brt.localRotation = Quaternion.Euler(0, 180, 0); // стрелка из набора смотрит вправо — разворачиваем

        if (mainSprite == null)
            return; // экран только со стрелкой «назад»

        var mainBtn = UiKit.CreateImageButton(parent, mainSprite, 84, main);
        var mrt = (RectTransform)mainBtn.transform;
        mrt.anchorMin = mrt.anchorMax = mrt.pivot = new Vector2(1, 0);
        mrt.anchoredPosition = new Vector2(-60, 50);
    }

    private void SoundToggles(Transform parent)
    {
        var row = UiKit.CreateRow("AudioToggles", parent, 16);
        Place(row, new Vector2(1, 1), new Vector2(-110, -60));
        mSoundButtons.Add(UiKit.CreateImageButton(row, "sound", 72, ToggleSound));
        mMusicButtons.Add(UiKit.CreateImageButton(row, "music", 72, ToggleMusic));
        RefreshAudioIcons();
    }

    private static void Place(Transform t, Vector2 anchor, Vector2 offset)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y == 1 ? 1 : anchor.y == 0 ? 0 : 0.5f);
        if (anchor.x == 1) rt.pivot = new Vector2(1, rt.pivot.y);
        rt.anchoredPosition = offset;
    }

    private static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    #endregion
}
