using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chess2.Core;
using Chess2.Core.Net;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Связывает ядро правил (Chess2.Core) с картинкой и ботом.
/// Ходы игрока, бота и соперника по сети проходят через ApplyMove.
/// Партии запускает меню (MenuManager) через StartGame.
/// Доска, фигуры и надпись строятся из кода — сцену настраивать не нужно.
/// </summary>
public class GameManager : MonoBehaviour
{
    public Board mBoard;
    public PieceManager mPieceManager;

    [Tooltip("Минимальная пауза перед ходом бота, чтобы он не ходил мгновенно")]
    public float mBotMinThinkSeconds = 0.6f;

    [Header("Интерфейс (если пусто — создаётся сам)")]
    public Text mStatusText;

    public GameState State { get; private set; }
    public bool PlayVsBot { get; private set; }
    public Side BotSide { get; private set; } = Side.Black;
    public BotDifficulty BotDifficulty { get; private set; } = BotDifficulty.Medium;
    public RuleSet Rules { get; private set; } = RuleSet.Default;

    /// <summary>Шахматные часы партии (минуты на каждого; 0 — без контроля времени).</summary>
    public ChessClock Clock { get; private set; } = new ChessClock(0);
    public int TimeMinutes { get; private set; }

    /// <summary>Пока true — ходить нельзя, ход бота откладывается (открыто меню паузы).</summary>
    public bool Paused { get; set; }

    /// <summary>Партия закончилась (мат, уничтожение, ничья).</summary>
    public event Action<GameState> GameEnded;

    public bool IsBotTurn => PlayVsBot && State != null && !State.IsOver && State.SideToMove == BotSide;

    /// <summary>Сетевая партия (null — игра на этом компьютере).</summary>
    public NetMatch Net { get; private set; }

    /// <summary>За кого играет этот компьютер в сетевой партии.</summary>
    public Side LocalSide => Net != null && Net.InGame ? Net.LocalSide : Side.White;

    public bool IsRemoteTurn => Net != null && State != null && !State.IsOver && State.SideToMove != LocalSide;

    /// <summary>Сетевые события для меню: соперник хочет реванш / началась новая партия / соединение закрыто.</summary>
    public event Action RematchOffered;
    public event Action NetGameStarted;
    public event Action<string> NetClosed;

    /// <summary>Идёт урок обучения (null — обычная партия).</summary>
    public Lesson Lesson { get; private set; }

    /// <summary>Текущий шаг урока (равен числу шагов, когда урок пройден).</summary>
    public int LessonStepIndex { get; private set; }

    /// <summary>Игрок нажал «К урокам» в конце урока.</summary>
    public event Action LessonMenuRequested;

    private GameObject mRoot;
    private MenuButton mExplodeButton;
    private Text mBannerText;
    private Coroutine mBannerRoutine;
    private Text mHintText;
    private int mSelected = Sq.None;
    private readonly List<GameState> mHistory = new List<GameState>(); // позиции перед каждым ходом — для «Ход назад»
    private MenuButton mUndoButton;
    private MenuButton mHintButton;

    // Разбор партии.
    public bool Reviewing { get; private set; }
    public event Action ReviewClosed;
    private readonly List<GameState> mReviewPositions = new List<GameState>();
    private MoveReview[] mReviews;
    private int mReviewIndex;
    private volatile int mAnalyzed;
    private int mShownAnalyzed = -1;
    private CancellationTokenSource mAnalysisCancel;
    private ReviewPanel mReviewPanel;
    private GameObject mReviewControls;
    private Task<Move?> mHintTask;
    private CancellationTokenSource mHintCancel;
    private List<Move> mSelectedMoves = new List<Move>();
    private readonly List<MoveEffect> mEffects = new List<MoveEffect>();

    private Task<Move?> mBotTask;
    private CancellationTokenSource mBotCancel;
    private float mBotStartedAt;
    private bool mBuilt;
    private ClockPanel mTopClock;
    private ClockPanel mBottomClock;
    private ChatPanel mChat;
    private NetMatch mChatOpponent; // с кем сейчас переписка (новый соперник — чистая история)
    private float mLastChatSent = -10f;
    private bool mWasShaky;
    private const float ChatCooldown = 0.5f;
    private LessonPanel mLessonPanel;
    private GameState mStepStart;   // позиция в начале шага: к ней откатываемся после ошибки
    private bool mLessonBusy;       // показываем «не то» и откатываем ход, ввод закрыт

    void Start()
    {
        EnsureBuilt();

        // Без меню (например, GameManager поставлен в сцену вручную) — сразу партия против бота.
        if (GetComponent<MenuManager>() == null && State == null)
            StartGame(true, Side.Black, BotDifficulty.Medium, RuleSet.Default);
    }

    void Update()
    {
        PollNet();
        TickClock();
        PollBot();
        UpdateClockUi();
        UpdateConnectionUi();
        UpdateUndo();
        UpdateReview();
    }

    /// <summary>Пинг в заголовке чата и предупреждение в статусе, если соперник давно молчит.</summary>
    private void UpdateConnectionUi()
    {
        if (Net == null)
            return;
        bool connected = Net.Transport.Status == Chess2.Core.Net.TransportStatus.Connected;
        bool shaky = Net.Shaky;
        if (mChat != null && mChat.gameObject.activeSelf)
            mChat.SetConnection(connected, Net.PingMs, shaky);
        if (shaky != mWasShaky)
        {
            mWasShaky = shaky;
            UpdateStatus();
        }
    }

    void OnDestroy()
    {
        CancelBot();
        Net?.Close();
    }

    // Телефон свернул игру (и может выгрузить её) или игру закрыли — сохраняем партию с временем на часах.
    void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveNow();
    }

    void OnApplicationQuit() => SaveNow();

    #region Партия

    /// <summary>Начать партию. vsBot = false — вдвоём за одним компьютером.</summary>
    public void StartGame(bool vsBot, Side botSide, BotDifficulty difficulty, RuleSet rules, int timeMinutes = 0)
    {
        EnsureBuilt();
        if (Net != null)
        {
            Net.Close();
            Net = null;
        }
        TimeMinutes = timeMinutes;
        PlayVsBot = vsBot;
        BotSide = botSide;
        BotDifficulty = difficulty;
        Rules = rules ?? RuleSet.Default;
        Paused = false;
        SetVisible(true);
        NewGame();
    }

    /// <summary>Та же партия заново (с теми же настройками).</summary>
    public void Restart()
    {
        if (Lesson != null)
            StartLesson(Lesson);
        else
            StartGame(PlayVsBot, BotSide, BotDifficulty, Rules, TimeMinutes);
    }

    /// <summary>
    /// Начать сетевую партию по настройкам, которые прислал хост (или выбрал сам хост).
    /// GameManager забирает соединение себе и дальше сам обрабатывает его события.
    /// </summary>
    public void StartNetGame(NetMatch net)
    {
        EnsureBuilt();
        bool rematch = mChatOpponent == net;
        Net = net;
        NetSetup setup = net.Setup;
        TimeMinutes = setup.TimeMinutes;
        PlayVsBot = false;
        Rules = setup.BuildRules();
        Paused = false;
        SetVisible(true);
        NewGame();
        if (mChat != null)
        {
            if (!rematch)
            {
                mChat.Clear();
                mChat.AddSystem(L.T("Соперник подключился. Удачной игры!"));
            }
            else
                mChat.AddSystem(L.T("Реванш! Цвета поменялись."));
        }
        mChatOpponent = net;
        NetGameStarted?.Invoke();
    }

    /// <summary>Сдаться. В сетевой игре соперник узнает об этом сразу.</summary>
    public void Resign()
    {
        if (State == null || State.IsOver)
            return;
        Side loser = Net != null ? LocalSide
            : PlayVsBot ? BotSide.Opponent()
            : State.SideToMove;
        Net?.SendResign();
        FinishGame(loser, EndReason.Resignation);
    }

    /// <summary>Реванш в сетевой игре (новую партию начнёт хост, когда согласятся оба).</summary>
    public void RequestRematch() => Net?.RequestRematch();

    /// <summary>Убрать доску (выход в главное меню). Сетевое соединение закрывается.</summary>
    public void StopGame()
    {
        SaveNow(); // вышли в меню посреди партии — её можно будет продолжить
        if (Net != null)
        {
            Net.Close();
            Net = null;
        }
        CancelBot();
        StopReview();
        Deselect();
        EndLesson();
        State = null;
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        EnsureBuilt();
        mRoot.SetActive(visible);
    }

    private void NewGame(SavedGame saved = null)
    {
        StopReview();
        CancelBot();
        EndLesson();
        mHistory.Clear();
        if (saved != null)
        {
            State = saved.State;
            Clock = new ChessClock(TimeMinutes);
            Clock.SetRemaining(Side.White, saved.WhiteSeconds);
            Clock.SetRemaining(Side.Black, saved.BlackSeconds);
            if (State.Ply > 0)
                Clock.OnMoveMade(); // часы шли — пусть идут дальше
        }
        else
        {
            // В сетевой игре зерно общее — загорятся одни и те же фигуры у обоих.
            State = GameState.CreateStandard(Rules, Net != null ? Net.Setup.Seed : (int?)null);
            Clock = new ChessClock(TimeMinutes);
        }
        if (mChat != null)
            mChat.gameObject.SetActive(Net != null); // чат — только в сетевой игре

        // Человек всегда снизу.
        mBoard.SetFlipped(Net != null ? LocalSide == Side.Black : PlayVsBot && BotSide == Side.White);

        Deselect();
        mBoard.SetLastMove(AppSettings.ShowLastMove ? State.LastMove : null);
        mPieceManager.Clear();
        mPieceManager.Sync(State, null);
        UpdateStatus();
        SoundManager.Play("start", 0.7f, 0f);
        SaveNow();
        MaybeStartBot();
    }

    #region Ход назад

    /// <summary>
    /// Отменить ход можно в партии с ботом и вдвоём (не по сети и не в уроке), пока партия идёт.
    /// </summary>
    public bool CanUndo => State != null && !State.IsOver && Net == null && Lesson == null
                           && mHistory.Count > 0 && !Paused;

    /// <summary>
    /// «Ход назад». Против бота — до своего последнего хода (вместе с ответом бота,
    /// а если бот ещё думает — его раздумья отменяются). Вдвоём — на один ход.
    /// Время на часах не возвращается.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo)
            return;
        CancelBot();

        GameState target;
        do
        {
            target = mHistory[mHistory.Count - 1];
            mHistory.RemoveAt(mHistory.Count - 1);
        }
        while (PlayVsBot && target.SideToMove == BotSide && mHistory.Count > 0);

        State = target;
        Deselect();
        mBoard.SetLastMove(AppSettings.ShowLastMove ? State.LastMove : null);
        mPieceManager.Clear();
        mPieceManager.Sync(State, null);
        UpdateStatus();
        SoundManager.Play("disembark", 0.7f);
        SaveNow();
        MaybeStartBot(); // бот ходил первым и мы откатились к началу — пусть снова ходит
    }

    private void UpdateUndo()
    {
        if (mUndoButton == null)
            return;
        PollHint();
        bool hint = CanHint;
        if (mHintButton.gameObject.activeSelf != hint)
            mHintButton.gameObject.SetActive(hint);

        bool show = CanUndo;
        if (mUndoButton.gameObject.activeSelf != show)
            mUndoButton.gameObject.SetActive(show);
        if (show && Platform.IsDesktop && UndoKeyPressed())
            Undo();
    }

    /// <summary>Ctrl+Z (на ПК).</summary>
    private static bool UndoKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.zKey.wasPressedThisFrame && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
#else
        return Input.GetKeyDown(KeyCode.Z) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
#endif
    }

    #endregion

    #region Разбор партии

    /// <summary>Разобрать можно законченную партию (с ботом, вдвоём или по сети), в которой были ходы.</summary>
    public bool CanReview => State != null && State.IsOver && Lesson == null && mHistory.Count > 0 && !Reviewing;

    /// <summary>
    /// Начать разбор: ходы можно листать, а бот в фоне оценивает каждый ход
    /// (лучший / неточность / ошибка / зевок) и советует, что было лучше.
    /// </summary>
    public void StartReview()
    {
        if (!CanReview)
            return;
        CancelBot();
        mReviewPositions.Clear();
        mReviewPositions.AddRange(mHistory);
        mReviewPositions.Add(State.Clone());
        int moves = mReviewPositions.Count - 1;
        mReviews = new MoveReview[moves];
        mAnalyzed = 0;
        mShownAnalyzed = -1;
        Reviewing = true;
        mReviewIndex = moves;

        // Анализ — в фоне, по ходу от начала партии. Позиции не меняются, их можно читать из потока.
        var positions = mReviewPositions.ToArray();
        MoveReview[] reviews = mReviews;
        mAnalysisCancel = new CancellationTokenSource();
        CancellationToken token = mAnalysisCancel.Token;
        System.Action work = () =>
        {
            for (int i = 0; i < reviews.Length && !token.IsCancellationRequested; i++)
            {
                Move? played = positions[i + 1].LastMove;
                if (played.HasValue)
                    reviews[i] = GameAnalysis.Review(positions[i].Clone(), played.Value, null, token); // копия: поиск временно меняет позицию
                mAnalyzed = i + 1;
            }
        };
#if UNITY_WEBGL && !UNITY_EDITOR
        work();
#else
        Task.Run(work);
#endif

        if (mChat != null)
            mChat.gameObject.SetActive(false);
        mReviewPanel.gameObject.SetActive(true);
        mReviewControls.SetActive(true);
        ShowReviewPosition();
    }

    /// <summary>Закончить разбор и вернуться к экрану конца партии.</summary>
    public void EndReview()
    {
        if (!Reviewing)
            return;
        StopReview();
        ReviewClosed?.Invoke();
    }

    private void StopReview()
    {
        if (!Reviewing)
            return;
        Reviewing = false;
        mAnalysisCancel?.Cancel();
        mAnalysisCancel = null;
        mReviewPanel.gameObject.SetActive(false);
        mReviewControls.SetActive(false);
        if (mChat != null)
            mChat.gameObject.SetActive(Net != null);
        if (State != null)
        {
            mPieceManager.Clear();
            mPieceManager.Sync(State, null);
            mBoard.SetLastMove(AppSettings.ShowLastMove ? State.LastMove : null);
        }
        UpdateStatus();
    }

    private void ReviewGo(int index)
    {
        if (!Reviewing)
            return;
        index = Mathf.Clamp(index, 0, mReviewPositions.Count - 1);
        if (index == mReviewIndex)
            return;
        mReviewIndex = index;
        SoundManager.Play("select", 0.5f);
        ShowReviewPosition();
    }

    /// <summary>К следующей ошибке или зевку (по кругу).</summary>
    private void ReviewNextMistake()
    {
        int n = mReviews.Length;
        for (int step = 1; step <= n; step++)
        {
            int i = (mReviewIndex - 1 + step) % n; // индекс хода; позиция после него — i + 1
            MoveReview r = mReviews[i];
            if (r != null && (r.Quality == MoveQuality.Mistake || r.Quality == MoveQuality.Blunder))
            {
                ReviewGo(i + 1);
                return;
            }
        }
        ShowBanner(mAnalyzed < n ? L.T("Ошибок пока не найдено — анализ ещё идёт") : L.T("Ошибок и зевков нет!"), 2f);
    }

    private void ShowReviewPosition()
    {
        GameState pos = mReviewPositions[mReviewIndex];
        Deselect();
        mPieceManager.Clear();
        mPieceManager.Sync(pos, null);
        mBoard.SetLastMove(pos.LastMove);
        RefreshReviewPanel();
        UpdateStatus();
    }

    private void UpdateReview()
    {
        if (!Reviewing)
            return;
        if (mAnalyzed != mShownAnalyzed)
            RefreshReviewPanel();
        if (Platform.IsDesktop && !Paused)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null)
                return;
            if (kb.leftArrowKey.wasPressedThisFrame) ReviewGo(mReviewIndex - 1);
            if (kb.rightArrowKey.wasPressedThisFrame) ReviewGo(mReviewIndex + 1);
            if (kb.homeKey.wasPressedThisFrame) ReviewGo(0);
            if (kb.endKey.wasPressedThisFrame) ReviewGo(mReviewPositions.Count - 1);
#else
            if (Input.GetKeyDown(KeyCode.LeftArrow)) ReviewGo(mReviewIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) ReviewGo(mReviewIndex + 1);
            if (Input.GetKeyDown(KeyCode.Home)) ReviewGo(0);
            if (Input.GetKeyDown(KeyCode.End)) ReviewGo(mReviewPositions.Count - 1);
#endif
        }
    }

    private static string QualityText(MoveQuality q)
    {
        switch (q)
        {
            case MoveQuality.Best: return L.T("<color=#6fd67c>лучший ход</color>");
            case MoveQuality.Good: return L.T("<color=#9fd6a8>хороший ход</color>");
            case MoveQuality.Forced: return L.T("<color=#9fb7c4>единственный ход</color>");
            case MoveQuality.Inaccuracy: return L.T("<color=#ffd76a>неточность</color>");
            case MoveQuality.Mistake: return L.T("<color=#ffa25a>ошибка</color>");
            default: return L.T("<color=#ff6b6b>зевок</color>");
        }
    }

    private static string SideName(Side side, bool capital) =>
        side == Side.White ? (capital ? L.T("Белые") : L.T("белые")) : (capital ? L.T("Чёрные") : L.T("чёрные"));

    private void RefreshReviewPanel()
    {
        int total = mReviews.Length;
        int analyzed = Mathf.Min(mAnalyzed, total);
        mShownAnalyzed = mAnalyzed;

        string move;
        if (mReviewIndex == 0)
            move = L.T("Начальная позиция.\nЛистай ходы кнопками справа") + (Platform.IsDesktop ? L.T(" или стрелками ← →.") : ".");
        else
        {
            GameState before = mReviewPositions[mReviewIndex - 1];
            Move played = mReviewPositions[mReviewIndex].LastMove ?? default;
            MoveReview r = mReviews[mReviewIndex - 1];
            move = L.F("<b>Ход {0}</b> ({1}):", mReviewIndex, SideName(before.SideToMove, false)) + "\n" + DescribeMove(before, played) + "\n\n";
            if (r == null)
                move += L.T("<color=#9fb7c4>бот ещё оценивает этот ход…</color>");
            else
            {
                move += L.T("Оценка: ") + QualityText(r.Quality);
                bool worse = r.Quality == MoveQuality.Inaccuracy || r.Quality == MoveQuality.Mistake
                             || r.Quality == MoveQuality.Blunder || r.Quality == MoveQuality.Good;
                if (worse && r.Best.HasValue)
                    move += L.T("\nЛучше было: ") + DescribeMove(before, r.Best.Value);
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.Append(analyzed < total ? L.F("Анализ: {0} из {1} ходов…", analyzed, total) + "\n\n" : L.T("Анализ готов.") + "\n\n");
        foreach (Side side in new[] { Side.White, Side.Black })
        {
            int inacc = 0, mist = 0, blund = 0;
            for (int i = 0; i < total; i++)
            {
                MoveReview r = mReviews[i];
                if (r == null || mReviewPositions[i].SideToMove != side)
                    continue;
                if (r.Quality == MoveQuality.Inaccuracy) inacc++;
                else if (r.Quality == MoveQuality.Mistake) mist++;
                else if (r.Quality == MoveQuality.Blunder) blund++;
            }
            sb.Append(L.F("<b>{0}</b>: зевков {1}, ошибок {2}, неточностей {3}", SideName(side, true), blund, mist, inacc) + "\n");
        }
        mReviewPanel.Show(move, sb.ToString());
    }

    #endregion

    #region Подсказка

    /// <summary>
    /// Подсказка — в партии с ботом (в свой ход) и вдвоём; не по сети и не в уроке.
    /// Пока ищем ход, кнопка скрыта.
    /// </summary>
    public bool CanHint => State != null && !State.IsOver && Net == null && Lesson == null
                           && !IsBotTurn && !Paused && mHintTask == null;

    /// <summary>«Подсказка»: бот уровня «сильный» ищет хороший ход за текущую сторону.</summary>
    public void RequestHint()
    {
        if (!CanHint)
            return;
        GameState snapshot = State.Clone();
        var settings = BotSettings.For(BotDifficulty.Hard);
        settings.TimeLimitMs = 1200;
        var bot = new Bot(settings);
        mHintCancel = new CancellationTokenSource();
        CancellationToken token = mHintCancel.Token;
#if UNITY_WEBGL && !UNITY_EDITOR
        mHintTask = Task.FromResult(bot.FindBestMove(snapshot, token));
#else
        mHintTask = Task.Run(() => bot.FindBestMove(snapshot, token));
#endif
        if (mHintText != null)
            mHintText.text = L.T("Ищу хороший ход…");
    }

    private void CancelHint()
    {
        if (mHintCancel != null)
        {
            mHintCancel.Cancel();
            mHintCancel = null;
        }
        mHintTask = null;
    }

    /// <summary>Подсказка готова: выбираем фигуру и показываем только советуемый ход.</summary>
    private void PollHint()
    {
        if (mHintTask == null || !mHintTask.IsCompleted)
            return;
        Task<Move?> task = mHintTask;
        mHintTask = null;
        mHintCancel?.Dispose();
        mHintCancel = null;
        if (task.IsFaulted || task.IsCanceled || !task.Result.HasValue || State == null || State.IsOver)
            return;

        Move m = task.Result.Value;
        Select(m.From);
        if (m.Kind != MoveKind.Explode)
            mBoard.ShowMoves(m.From, new List<Move> { m });
        if (mHintText != null)
            mHintText.text = L.T("<color=#ffd76a>Подсказка:</color> ") + DescribeMove(State, m);
    }

    public static string PieceNameOf(PieceType t) => PieceName(t);

    private static string PieceName(PieceType t)
    {
        switch (t)
        {
            case PieceType.King: return L.T("король");
            case PieceType.Queen: return L.T("ферзь");
            case PieceType.Rook: return L.T("ладья");
            case PieceType.Bishop: return L.T("слон");
            case PieceType.Knight: return L.T("конь");
            case PieceType.Pawn: return L.T("пешка");
            case PieceType.Ghost: return L.T("призрак");
            default: return L.T("фигура");
        }
    }

    /// <summary>Ход словами: «конь g1 → f3», «ладья a1 пробивает a7»…</summary>
    public static string DescribeMove(GameState s, Move m)
    {
        string who = PieceName(s.Cells[m.From].Type);
        string from = Sq.Name(m.From), to = Sq.Name(m.To);
        switch (m.Kind)
        {
            case MoveKind.Capture: return L.F("{0} {1} бьёт {2}", who, from, to);
            case MoveKind.Pierce: return L.F("{0} {1} бьёт {2} сквозь фигуру", who, from, to);
            case MoveKind.Castle: return L.F("рокировка: король {0} → {1}", from, to);
            case MoveKind.DoubleJump: return L.F("двойной прыжок: конь {0} → {1} → {2}", from, Sq.Name(m.Via), to);
            case MoveKind.DoubleCapture: return L.F("двойной прыжок: конь {0} через {1} бьёт {2}", from, Sq.Name(m.Via), to);
            case MoveKind.Embark: return L.F("{0} {1} садится в коня на {2}", who, from, to);
            case MoveKind.Disembark: return L.F("высадить пассажира коня {0} на {1}", from, to);
            case MoveKind.Explode: return L.F("взорвать коня на {0} (кнопка справа)", from);
            default: return L.F("{0} {1} → {2}", who, from, to);
        }
    }

    #endregion

    #region Сохранение

    /// <summary>Сохраняются только партии против бота и вдвоём — не сетевые и не уроки.</summary>
    private bool CanSave => State != null && Net == null && Lesson == null;

    /// <summary>Записать партию на диск (законченную — стереть).</summary>
    public void SaveNow()
    {
        if (!CanSave)
            return;
        if (State.IsOver)
        {
            SaveStore.Delete();
            return;
        }
        SaveStore.Save(new SavedGame
        {
            State = State,
            VsBot = PlayVsBot,
            BotSide = BotSide,
            Difficulty = BotDifficulty,
            TimeMinutes = TimeMinutes,
            WhiteSeconds = Clock.Remaining(Side.White),
            BlackSeconds = Clock.Remaining(Side.Black)
        });
    }

    /// <summary>Продолжить сохранённую партию. false — сохранения нет или оно повреждено.</summary>
    public bool ResumeSaved()
    {
        if (!SaveStore.TryLoad(out SavedGame saved))
            return false;
        EnsureBuilt();
        if (Net != null)
        {
            Net.Close();
            Net = null;
        }
        PlayVsBot = saved.VsBot;
        BotSide = saved.BotSide;
        BotDifficulty = saved.Difficulty;
        TimeMinutes = saved.TimeMinutes;
        Rules = saved.State.Rules;
        Paused = false;
        SetVisible(true);
        NewGame(saved);
        return true;
    }

    #endregion

    /// <summary>
    /// Единая точка, через которую проходит любой ход: игрока, бота или из сети.
    /// Возвращает false, если ход не по правилам.
    /// </summary>
    public bool ApplyMove(Move move)
    {
        if (State == null)
            return false;

        CancelHint();
        Side mover = State.SideToMove;
        mEffects.Clear();
        GameState before = Lesson == null ? State.Clone() : null;
        if (!Chess2.Core.Rules.TryMakeMove(State, move, mEffects))
            return false;
        if (before != null)
            mHistory.Add(before);

        // Свой ход в сетевой игре — сразу сопернику, вместе с нашим временем на часах.
        if (Net != null && mover == LocalSide)
            Net.SendMove(State.LastMove ?? move, Clock.Remaining(mover));

        Clock.OnMoveMade();
        if (State.IsOver)
            Clock.Stop();

        Deselect();
        mBoard.SetLastMove(AppSettings.ShowLastMove ? State.LastMove : null);
        mPieceManager.Sync(State, mEffects);
        UpdateStatus();

        foreach (MoveEffect e in mEffects)
        {
            if (e.Kind == EffectKind.KingFell)
            {
                // Пал король соперника — в статистику (против бота и по сети).
                if (Lesson == null && (Net != null || PlayVsBot)
                    && e.Side != (Net != null ? LocalSide : BotSide.Opponent()))
                    GameStats.AddKingFelled();
                ShowBanner(KingFallText(mEffects));
                break;
            }
        }

        PlayMoveSound(State.LastMove ?? move, mEffects);

        if (Lesson != null)
        {
            OnLessonMove(State.LastMove ?? move);
            return true;
        }

        TrackAchievements(mover, State.LastMove ?? move);
        SaveNow(); // после каждого хода: партия не потеряется, даже если телефон выгрузит игру

        if (State.IsOver)
            NotifyGameEnded();
        else
            MaybeStartBot();
        return true;
    }

    /// <summary>Один главный звук хода (самое яркое событие) и «шах» следом.</summary>
    private void PlayMoveSound(Move move, List<MoveEffect> effects)
    {
        bool kingFell = false, exploded = false, empowered = false, removed = false, degraded = false,
            promoted = false, embarked = false, disembarked = false;
        foreach (MoveEffect e in effects)
        {
            switch (e.Kind)
            {
                case EffectKind.KingFell: kingFell = true; break;
                case EffectKind.Exploded: exploded = true; break;
                case EffectKind.Empowered: empowered = true; break;
                case EffectKind.Removed: removed = true; break;
                case EffectKind.Degraded: degraded = true; break;
                case EffectKind.Promoted: promoted = true; break;
                case EffectKind.Embarked: embarked = true; break;
                case EffectKind.Disembarked:
                case EffectKind.Ejected: disembarked = true; break;
            }
        }

        if (kingFell)
        {
            SoundManager.Play("king_fall", 1f, 0f);
            if (empowered)
                SoundManager.PlayDelayed("empower", 0.6f);
        }
        else if (exploded)
            SoundManager.Play("explode");
        else if (move.Kind == MoveKind.Pierce)
            SoundManager.Play("pierce");
        else if (degraded)
            SoundManager.Play("capture");
        else if (removed)
            SoundManager.Play("vanish");
        else if (promoted)
            SoundManager.Play("promote");
        else if (embarked)
            SoundManager.Play("embark");
        else if (disembarked)
            SoundManager.Play("disembark");
        else if (move.IsDouble)
            SoundManager.Play("double");
        else if (move.Kind == MoveKind.Castle)
            SoundManager.Play("castle");
        else
            SoundManager.Play("move");

        // Пешка возродилась вместе с ударом/ходом — отдельный звук следом.
        if (promoted && (degraded || removed || exploded))
            SoundManager.PlayDelayed("promote", 0.25f);

        if (!State.IsOver && State.Rules.WinCondition != WinCondition.Annihilation
            && Chess2.Core.Rules.IsInCheck(State, State.SideToMove))
            SoundManager.PlayDelayed("check", 0.2f);
    }

    /// <summary>Партия закончилась: звук победы/поражения/ничьей и событие для меню.</summary>
    private void NotifyGameEnded()
    {
        SaveNow(); // законченная партия стирается из сохранения
        if (State.Result == GameResult.Draw)
            SoundManager.Play("draw", 1f, 0f);
        else
        {
            Side winner = State.Result == GameResult.WhiteWins ? Side.White : Side.Black;
            // Кто «мы»: в сети — своя сторона, против бота — человек; вдвоём — всегда радуемся.
            bool weWon = Net != null ? winner == LocalSide
                : PlayVsBot ? winner != BotSide
                : true;
            SoundManager.Play(weWon ? "win" : "lose", 1f, 0f);
        }
        RecordStats();
        GameEnded?.Invoke(State);
    }

    #endregion

    /// <summary>Итог партии — в статистику (уроки не считаются).</summary>
    private void RecordStats()
    {
        if (Lesson != null)
            return;
        int row = Net != null ? GameStats.NetRow
            : PlayVsBot ? GameStats.BotRow(BotDifficulty)
            : GameStats.LocalRow;
        GameStats.Outcome outcome;
        if (State.Result == GameResult.Draw)
            outcome = GameStats.Outcome.Draw;
        else
        {
            Side winner = State.Result == GameResult.WhiteWins ? Side.White : Side.Black;
            // Вдвоём «победа» — это победа белых.
            Side us = Net != null ? LocalSide : PlayVsBot ? BotSide.Opponent() : Side.White;
            outcome = winner == us ? GameStats.Outcome.Win : GameStats.Outcome.Loss;
        }
        GameStats.Record(row, outcome);

        // Достижения за победу (против бота и по сети).
        if (outcome != GameStats.Outcome.Win || row == GameStats.LocalRow)
            return;
        Achievements.Unlock("first_win");
        if (Net != null)
            Achievements.Unlock("net_win");
        else
            Achievements.Unlock(new[] { "beat_easy", "beat_medium", "beat_hard", "beat_expert" }[GameStats.BotRow(BotDifficulty)]);
        Side me = Net != null ? LocalSide : BotSide.Opponent();
        if (State.Rules.WinCondition == WinCondition.Annihilation)
            Achievements.Unlock("chaos_win");
        else if (State.FindKing(me) == Sq.None)
            Achievements.Unlock("kingless_win");
        if (GameStats.CurrentStreak >= 3)
            Achievements.Unlock("streak3");
    }

    /// <summary>Чей ход — игрока за этим устройством (вдвоём — обоих).</summary>
    private bool IsHumanSide(Side side) => Net != null ? side == LocalSide : !PlayVsBot || side != BotSide;

    /// <summary>Достижения за ходы игрока: способности, мат, возрождение, призраки.</summary>
    private void TrackAchievements(Side mover, Move move)
    {
        if (Lesson != null || !IsHumanSide(mover))
            return;

        switch (move.Kind)
        {
            case MoveKind.Pierce: Achievements.Unlock("pierce"); break;
            case MoveKind.DoubleJump:
            case MoveKind.DoubleCapture: Achievements.Unlock("double_jump"); break;
            case MoveKind.Explode: Achievements.Unlock("trojan_boom"); break;
        }

        int enemiesHit = 0;
        foreach (MoveEffect e in mEffects)
        {
            bool enemy = e.Side != mover;
            if (enemy && (e.Kind == EffectKind.Degraded || e.Kind == EffectKind.Removed))
                enemiesHit++;
            if (enemy && e.Kind == EffectKind.KingFell)
                Achievements.Unlock("king_fell");
            if (enemy && e.Kind == EffectKind.Removed && e.OldType == PieceType.Ghost
                && Achievements.AddProgress("ghosts") >= 10)
                Achievements.Unlock("ghost_hunter");
            if (!enemy && e.Kind == EffectKind.Promoted && State.Cells[e.To].OriginalType > PieceType.Pawn)
                Achievements.Unlock("revive");
        }
        if (move.Kind == MoveKind.Explode && enemiesHit >= 3)
            Achievements.Unlock("big_boom");
    }

    #region Обучение

    /// <summary>Начать урок: своя расстановка, без часов и бота, чёрные не ходят.</summary>
    public void StartLesson(Lesson lesson)
    {
        EnsureBuilt();
        if (Net != null)
        {
            Net.Close();
            Net = null;
        }
        CancelBot();
        if (mRetryRoutine != null)
            StopCoroutine(mRetryRoutine);
        mRetryRoutine = null;
        mLessonBusy = false;
        PlayVsBot = false;
        TimeMinutes = 0;
        Paused = false;
        SetVisible(true);

        StopReview();
        Lesson = lesson;
        LessonStepIndex = 0;
        mHistory.Clear();
        State = lesson.Setup();
        Rules = State.Rules;
        mStepStart = State.Clone();
        Clock = new ChessClock(0);
        if (mChat != null)
            mChat.gameObject.SetActive(false);
        mBoard.SetFlipped(lesson.PlayerSide == Side.Black); // играешь за чёрных — они снизу
        if (mBannerText != null)
            mBannerText.gameObject.SetActive(false);

        ShowPosition();
        RefreshLessonPanel(null);
        SoundManager.Play("start", 0.7f, 0f);
    }

    private Coroutine mRetryRoutine;

    private void EndLesson()
    {
        if (Lesson == null)
            return;
        Lesson = null;
        if (mRetryRoutine != null)
            StopCoroutine(mRetryRoutine);
        mRetryRoutine = null;
        mLessonBusy = false;
        if (mLessonPanel != null)
            mLessonPanel.gameObject.SetActive(false);
    }

    private void ShowPosition()
    {
        Deselect();
        mBoard.SetLastMove(null);
        mPieceManager.Clear();
        mPieceManager.Sync(State, null);
        UpdateStatus();
    }

    private static Lesson NextLessonOf(Lesson lesson)
    {
        var all = Tutorial.Lessons;
        for (int i = 0; i < all.Count - 1; i++)
            if (all[i] == lesson)
                return all[i + 1];
        return null;
    }

    private void NextLesson()
    {
        Lesson next = Lesson != null ? NextLessonOf(Lesson) : null;
        if (next != null)
            StartLesson(next);
        else
            LessonMenuRequested?.Invoke();
    }

    private void RefreshLessonPanel(string note)
    {
        if (mLessonPanel == null || Lesson == null)
            return;
        mLessonPanel.gameObject.SetActive(true);
        mLessonPanel.Show(Lesson, LessonStepIndex, note, NextLessonOf(Lesson) != null);
    }

    /// <summary>Игрок походил в уроке: засчитать шаг, принять как часть шага или откатить.</summary>
    private void OnLessonMove(Move move)
    {
        if (LessonStepIndex >= Lesson.Steps.Length)
            return;
        LessonStep step = Lesson.Steps[LessonStepIndex];
        switch (step.Check(State, move, mEffects))
        {
            case LessonVerdict.Progress:
                Tutorial.PassTurn(State, Lesson.PlayerSide);
                UpdateStatus();
                RefreshLessonPanel(null);
                break;

            case LessonVerdict.StepDone:
                Tutorial.PassTurn(State, Lesson.PlayerSide);
                LessonStepIndex++;
                mStepStart = State.Clone();
                if (LessonStepIndex >= Lesson.Steps.Length && Lesson.IsDaily)
                {
                    DailyChallenge.MarkSolved();
                    Lesson.Outro = DailyChallenge.OutroText();
                    SoundManager.PlayDelayed("win", 0.4f);
                }
                else if (LessonStepIndex >= Lesson.Steps.Length)
                {
                    AppSettings.MarkLessonDone(Lesson.Id);
                    bool all = true;
                    foreach (Lesson l in Tutorial.Lessons)
                        all &= AppSettings.IsLessonDone(l.Id);
                    if (all)
                        Achievements.Unlock("all_lessons");
                    SoundManager.PlayDelayed("win", 0.4f);
                }
                UpdateStatus();
                RefreshLessonPanel(null);
                break;

            default:
                mRetryRoutine = StartCoroutine(LessonRetry(L.T(step.Hint)));
                break;
        }
    }

    private System.Collections.IEnumerator LessonRetry(string hint)
    {
        mLessonBusy = true;
        RefreshLessonPanel(L.T("Не то. ") + hint + ".");
        yield return new WaitForSeconds(1.1f);
        State = mStepStart.Clone();
        mLessonBusy = false;
        mRetryRoutine = null;
        ShowPosition();
    }

    #endregion

    #region Часы

    private void TickClock()
    {
        // В сетевой игре пауза ничего не останавливает — соперник-то играет.
        if (State == null || State.IsOver || (Paused && Net == null) || !Clock.Enabled)
            return;

        Side toMove = State.SideToMove;
        double before = Clock.Remaining(toMove);
        bool flag = Clock.Tick(toMove, Time.unscaledDeltaTime);
        double after = Clock.Remaining(toMove);
        bool ourClock = Net != null ? toMove == LocalSide : !(PlayVsBot && toMove == BotSide);
        if (ourClock && after < 10 && after > 0 && (int)before != (int)after)
            SoundManager.Play("clock_tick", 0.8f, 0f);
        if (!flag)
            return;

        // В сети флаг объявляет только тот, у кого кончилось время, — иначе из-за задержки
        // сети часы у двух игроков могли бы разойтись. Чужой флаг ждём сообщением TIMEOUT.
        if (Net != null && toMove != LocalSide)
            return;

        Net?.SendTimeout();
        FinishGame(toMove, EndReason.Timeout);
    }

    /// <summary>Закончить партию не ходом (время, сдача, отключение).</summary>
    private void FinishGame(Side loser, EndReason reason)
    {
        if (State == null || State.IsOver)
            return;
        Chess2.Core.Rules.EndGame(State, loser, reason);
        Clock.Stop();
        CancelBot();
        Deselect();
        UpdateStatus();
        NotifyGameEnded();
    }

    private void UpdateClockUi()
    {
        if (mTopClock == null)
            return;

        bool show = State != null && Clock.Enabled;
        mTopClock.gameObject.SetActive(show);
        mBottomClock.gameObject.SetActive(show);
        if (!show)
            return;

        // Снизу — сторона, которая стоит внизу доски.
        Side bottom = mBoard.Flipped ? Side.Black : Side.White;
        Side top = bottom.Opponent();
        bool ticking = Clock.Running && !State.IsOver;
        mTopClock.Show(SideName(top), Clock.Remaining(top), ticking && State.SideToMove == top);
        mBottomClock.Show(SideName(bottom), Clock.Remaining(bottom), ticking && State.SideToMove == bottom);
    }

    private string SideName(Side side)
    {
        string color = side == Side.White ? L.T("Белые") : L.T("Чёрные");
        if (Net != null)
            return side == LocalSide ? L.F("Ты · {0}", color.ToLowerInvariant()) : L.F("Соперник · {0}", color.ToLowerInvariant());
        if (!PlayVsBot)
            return color;
        return side == BotSide ? L.F("Бот · {0}", color.ToLowerInvariant()) : L.F("Ты · {0}", color.ToLowerInvariant());
    }

    #endregion

    #region Сеть

    private void PollNet()
    {
        if (Net == null)
            return;

        foreach (NetEvent e in Net.Poll())
        {
            switch (e.Kind)
            {
                case NetEventKind.RemoteMove:
                {
                    if (State == null || State.IsOver || !IsRemoteTurn)
                        break;
                    Side mover = State.SideToMove;
                    if (ApplyMove(e.Move))
                        Clock.SetRemaining(mover, e.ClockSeconds);
                    else
                    {
                        // Такого быть не должно: у соперника другая позиция.
                        Debug.LogError("Соперник прислал невозможный ход: " + e.Move);
                        CloseNet(L.T("Позиции разошлись — партия остановлена"));
                    }
                    break;
                }

                case NetEventKind.RemoteResigned:
                    if (State != null)
                        FinishGame(LocalSide.Opponent(), EndReason.Resignation);
                    break;

                case NetEventKind.RemoteTimeout:
                    if (State != null)
                    {
                        Clock.SetRemaining(LocalSide.Opponent(), 0);
                        FinishGame(LocalSide.Opponent(), EndReason.Timeout);
                    }
                    break;

                case NetEventKind.RematchRequested:
                    RematchOffered?.Invoke();
                    break;

                case NetEventKind.GameStarted:
                    StartNetGame(Net); // реванш
                    break;

                case NetEventKind.Chat:
                    mChat?.AddText(L.T("Соперник"), false, e.Text);
                    SoundManager.Play("chat", 0.8f, 0f);
                    break;

                case NetEventKind.Emote:
                    mChat?.AddEmote(L.T("Соперник"), false, e.Value, EmotePosition(LocalSide.Opponent()));
                    SoundManager.Play("emote", 0.8f);
                    break;

                case NetEventKind.Disconnected:
                    CloseNet(string.IsNullOrEmpty(e.Text) ? L.T("Соперник отключился") : e.Text);
                    return;
            }
        }
    }

    private void SendChatText(string text)
    {
        if (Net == null || Net.Transport.Status != Chess2.Core.Net.TransportStatus.Connected)
            return;
        if (Time.unscaledTime - mLastChatSent < ChatCooldown)
            return; // защита от спама
        string sent = Net.SendChat(text);
        if (sent.Length == 0)
            return;
        mLastChatSent = Time.unscaledTime;
        mChat.AddText(L.T("Ты"), true, sent);
    }

    private void SendEmote(int id)
    {
        if (Net == null || Net.Transport.Status != Chess2.Core.Net.TransportStatus.Connected)
            return;
        if (Time.unscaledTime - mLastChatSent < ChatCooldown)
            return;
        mLastChatSent = Time.unscaledTime;
        Net.SendEmote(id);
        mChat.AddEmote(L.T("Ты"), true, id, EmotePosition(LocalSide));
    }

    /// <summary>Где всплывает крупный эмодзи: справа, у часов того, кто его отправил.</summary>
    private Vector2 EmotePosition(Side side)
    {
        Side bottom = mBoard.Flipped ? Side.Black : Side.White;
        if (Platform.Portrait)
        {
            // Справа от часов того, кто отправил.
            Transform clock = side == bottom ? mBottomClock.transform : mTopClock.transform;
            return (Vector2)clock.localPosition + new Vector2(300, -50);
        }
        return new Vector2(400 + 40 + 115, side == bottom ? -200 : 200);
    }

    private void CloseNet(string reason)
    {
        mChat?.AddSystem(reason);
        if (State != null && !State.IsOver)
        {
            ShowBanner(reason);
            FinishGame(LocalSide.Opponent(), EndReason.Disconnected);
        }
        Net?.Close();
        NetClosed?.Invoke(reason);
    }

    #endregion

    #region Бот

    private void MaybeStartBot()
    {
        if (!IsBotTurn || mBotTask != null)
            return;

        // Бот думает над копией и в отдельном потоке — игра не подвисает.
        GameState snapshot = State.Clone();
        var settings = BotSettings.For(BotDifficulty);
        if (Clock.Enabled)
        {
            // Тратим не больше ~1/30 оставшегося времени на ход, чтобы не проиграть по флагу.
            double left = Clock.Remaining(BotSide);
            settings.TimeLimitMs = (int)Math.Min(settings.TimeLimitMs, Math.Max(100, left * 1000 / 30));
        }
        var bot = new Bot(settings);
        mBotCancel = new CancellationTokenSource();
        CancellationToken token = mBotCancel.Token;
        mBotStartedAt = Time.time;

#if UNITY_WEBGL && !UNITY_EDITOR
        // В браузере потоков нет — считаем сразу.
        mBotTask = Task.FromResult(bot.FindBestMove(snapshot, token));
#else
        mBotTask = Task.Run(() => bot.FindBestMove(snapshot, token));
#endif
        UpdateStatus();
    }

    private void PollBot()
    {
        if (mBotTask == null || !mBotTask.IsCompleted || Paused)
            return;
        // Пауза «для вида» — только если времени хватает.
        float minThink = Clock.Enabled && Clock.Remaining(BotSide) < 30 ? 0f : mBotMinThinkSeconds;
        if (Time.time - mBotStartedAt < minThink)
            return;

        Task<Move?> task = mBotTask;
        mBotTask = null;
        mBotCancel?.Dispose();
        mBotCancel = null;

        if (task.IsFaulted)
        {
            Debug.LogException(task.Exception);
            return;
        }
        if (task.IsCanceled || !IsBotTurn)
            return;

        Move? move = task.Result;
        if (move.HasValue && !ApplyMove(move.Value))
            Debug.LogError("Бот выбрал запрещённый ход: " + move.Value);
    }

    private void CancelBot()
    {
        CancelHint(); // позиция меняется — подсказка устарела
        if (mBotCancel != null)
        {
            mBotCancel.Cancel();
            mBotCancel = null; // поток сам завершится, его результат будет отброшен
        }
        mBotTask = null;
    }

    #endregion

    #region Ввод

    public bool CanPickUp(int sq)
    {
        if (State == null || State.IsOver || IsBotTurn || IsRemoteTurn || Paused || mLessonBusy)
            return false;
        Piece p = State.Cells[sq];
        return !p.IsEmpty && p.Side == State.SideToMove;
    }

    public void OnSquareClicked(int sq)
    {
        if (State == null || IsBotTurn || IsRemoteTurn || Paused || mLessonBusy || Reviewing)
            return;

        // Сначала пробуем походить выбранной фигурой.
        if (mSelected != Sq.None && TryMoveSelectedTo(sq))
            return;

        // Иначе — выбрать свою фигуру (или снять выбор).
        if (CanPickUp(sq) && sq != mSelected)
            Select(sq);
        else
            Deselect();
    }

    public void OnPieceDropped(int from, Vector2 screenPoint, Camera eventCamera)
    {
        int to = mBoard.ScreenToSquare(screenPoint, eventCamera);
        if (mSelected == from && to != Sq.None && to != from && TryMoveSelectedTo(to))
            return;

        // Не получилось — вернуть фигуру, выбор оставить (можно доходить кликом).
        mPieceManager.ReturnToSquare(from);
    }

    public void Select(int sq)
    {
        mSelected = sq;
        mSelectedMoves = Chess2.Core.Rules.GetLegalMovesFrom(State, sq);
        // Без подсказок подсвечиваем только выбранную клетку — ходы игрок ищет сам.
        mBoard.ShowMoves(sq, AppSettings.ShowMoveHints ? mSelectedMoves : new List<Move>());
        SoundManager.Play("select", 0.6f);
        UpdateSelectionUi();
    }

    public void Deselect()
    {
        mSelected = Sq.None;
        mSelectedMoves.Clear();
        if (mBoard != null)
            mBoard.ClearMoves();
        UpdateSelectionUi();
    }

    private bool TryMoveSelectedTo(int to)
    {
        foreach (Move m in mSelectedMoves)
        {
            if (m.To == to && m.Kind != MoveKind.Explode)
            {
                ApplyMove(m);
                return true;
            }
        }
        return false;
    }

    /// <summary>Взорвать выбранного троянского коня (кнопка рядом с доской).</summary>
    public void ExplodeSelected()
    {
        foreach (Move m in mSelectedMoves)
        {
            if (m.Kind == MoveKind.Explode)
            {
                ApplyMove(m);
                return;
            }
        }
    }

    /// <summary>Кнопка взрыва и подсказка: что значат цвета меток у выбранной фигуры.</summary>
    private void UpdateSelectionUi()
    {
        bool canExplode = false, hasDouble = false, hasEmbark = false, hasDisembark = false, hasPierce = false;
        foreach (Move m in mSelectedMoves)
        {
            switch (m.Kind)
            {
                case MoveKind.Explode: canExplode = true; break;
                case MoveKind.DoubleJump:
                case MoveKind.DoubleCapture: hasDouble = true; break;
                case MoveKind.Embark: hasEmbark = true; break;
                case MoveKind.Disembark: hasDisembark = true; break;
                case MoveKind.Pierce: hasPierce = true; break;
            }
        }

        if (mExplodeButton != null)
            mExplodeButton.gameObject.SetActive(canExplode);

        if (mHintText == null)
            return;
        if (!AppSettings.ShowHintText)
        {
            mHintText.text = "";
            return;
        }
        var parts = new List<string>();
        if (hasDouble) parts.Add(L.T("<color=#3cd0d8>бирюзовые</color> — двойной прыжок (раз за игру)"));
        if (hasDisembark) parts.Add(L.T("<color=#6fd67c>зелёные точки</color> — высадить пассажира"));
        if (hasEmbark) parts.Add(L.T("<color=#6fd67c>зелёное кольцо</color> — сесть в коня"));
        if (hasPierce) parts.Add(L.T("<color=#b27cf0>фиолетовые</color> — удар сквозь фигуру (раз за игру)"));
        if (canExplode) parts.Add(L.T("кнопка справа — взорвать коня"));
        mHintText.text = string.Join("   ·   ", parts);
    }

    #endregion

    #region Надписи

    private void UpdateStatus()
    {
        if (mStatusText != null && State != null)
            mStatusText.text = StatusLine();
    }

    private string StatusLine()
    {
        if (Reviewing)
            return L.F("Разбор партии · ход {0} из {1}", mReviewIndex, mReviewPositions.Count - 1);
        if (State.IsOver)
            return ResultText(State);

        if (Lesson != null)
            return LessonStepIndex < Lesson.Steps.Length
                ? L.T("Задание: ") + L.T(Lesson.Steps[LessonStepIndex].Task)
                : L.F("Урок «{0}» пройден!", L.T(Lesson.Title));

        string text;
        if (IsBotTurn)
            text = L.F("Бот ({0}) думает…", DifficultyText(BotDifficulty));
        else if (IsRemoteTurn)
            text = L.T("Ход соперника…");
        else if (PlayVsBot || Net != null)
            text = L.T("Твой ход");
        else
            text = L.T("Ход ") + (State.SideToMove == Side.White ? L.T("белых") : L.T("чёрных"));

        bool checkRules = State.Rules.WinCondition != WinCondition.Annihilation;
        if (checkRules && State.FindKing(State.SideToMove) == Sq.None)
            text += L.T(" (без короля)");
        if (checkRules && Chess2.Core.Rules.IsInCheck(State, State.SideToMove))
            text += L.T(" — ШАХ!");
        if (Net != null && Net.Shaky)
            text += L.T("   · связь с соперником нестабильна…");
        return text;
    }

    /// <summary>Крупная надпись над доской на несколько секунд (например, «Король пал!»).</summary>
    public void ShowBanner(string text, float seconds = 3.5f)
    {
        if (mBannerText == null)
            return;
        if (mBannerRoutine != null)
            StopCoroutine(mBannerRoutine);
        mBannerRoutine = StartCoroutine(BannerRoutine(text, seconds));
    }

    private System.Collections.IEnumerator BannerRoutine(string text, float seconds)
    {
        mBannerText.text = text;
        mBannerText.gameObject.SetActive(true);
        Color c = mBannerText.color;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            // Быстро проявляется, держится и плавно гаснет.
            float a = Mathf.Min(1f, t / 0.25f) * Mathf.Min(1f, (seconds - t) / 0.6f);
            mBannerText.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }
        mBannerText.gameObject.SetActive(false);
        mBannerText.color = new Color(c.r, c.g, c.b, 1f);
        mBannerRoutine = null;
    }

    /// <summary>Текст баннера, когда пал король: чья армия и что загорелось.</summary>
    private string KingFallText(List<MoveEffect> effects)
    {
        Side side = Side.White;
        var lit = new List<string>();
        foreach (MoveEffect e in effects)
        {
            if (e.Kind == EffectKind.KingFell)
                side = e.Side;
            else if (e.Kind == EffectKind.Empowered)
                lit.Add(AbilityText(e.OldType));
        }

        string who = Net != null
            ? (side == LocalSide ? L.T("Твой король пал!") : L.T("Король соперника пал!"))
            : PlayVsBot
            ? (side == BotSide ? L.T("Король бота пал!") : L.T("Твой король пал!"))
            : (side == Side.White ? L.T("Белый король пал!") : L.T("Чёрный король пал!"));
        string fire = lit.Count == 0
            ? L.T("Но загореться некому…")
            : L.T("Загорелись: ") + string.Join(", ", lit);
        return who + "\n" + fire;
    }

    public static string AbilityText(PieceType type)
    {
        switch (type)
        {
            case PieceType.Knight: return L.T("конь (троянский)");
            case PieceType.Bishop: return L.T("слон (пробивание)");
            case PieceType.Rook: return L.T("ладья (пробивание)");
            case PieceType.Queen: return L.T("ферзь (ходы коня)");
            default: return type.ToString();
        }
    }

    /// <summary>Итог партии одной строкой (для надписи и окна конца игры).</summary>
    public string ResultText(GameState s)
    {
        string reason = ReasonText(s.EndReason);
        if (s.Result == GameResult.Draw)
            return L.F("Ничья: {0}", reason);

        Side winner = s.Result == GameResult.WhiteWins ? Side.White : Side.Black;
        if (Net != null)
            return (winner == LocalSide ? L.T("Ты победил") : L.T("Соперник победил")) + $": {reason}";
        if (PlayVsBot)
            return (winner == BotSide ? L.T("Бот победил") : L.T("Ты победил")) + $": {reason}";
        return (winner == Side.White ? L.T("Белые победили") : L.T("Чёрные победили")) + $": {reason}";
    }

    public static string DifficultyText(BotDifficulty d)
    {
        switch (d)
        {
            case BotDifficulty.Easy: return L.T("новичок");
            case BotDifficulty.Medium: return L.T("любитель");
            case BotDifficulty.Hard: return L.T("сильный");
            default: return L.T("мастер");
        }
    }

    private static string ReasonText(EndReason reason)
    {
        switch (reason)
        {
            case EndReason.Checkmate: return L.T("мат");
            case EndReason.Annihilation: return L.T("фигур не осталось");
            case EndReason.Stalemate: return L.T("пат, ходить нечем");
            case EndReason.NoProgress: return L.T("50 ходов без ударов");
            case EndReason.OnlyGhosts: return L.T("остались одни призраки");
            case EndReason.Resignation: return L.T("сдача");
            case EndReason.Timeout: return L.T("время вышло");
            case EndReason.Disconnected: return L.T("соперник отключился");
            default: return reason.ToString();
        }
    }

    #endregion

    #region Сборка сцены из кода

    private void EnsureBuilt()
    {
        if (mBuilt)
            return;
        mBuilt = true;

        if (mBoard == null || mPieceManager == null)
            BuildScene();
        else
            mRoot = mBoard.GetComponentInParent<Canvas>().rootCanvas.gameObject;

        mBoard.Create(this);
        mPieceManager.Setup(mBoard, this);
        mRoot.SetActive(false);
    }

    private void BuildScene()
    {
        UiKit.EnsureEventSystem();

        Canvas canvas = UiKit.CreateCanvas("Chess2 Board Canvas", transform, 0);
        mRoot = canvas.gameObject;

        Image background = UiKit.CreatePanel("Background", canvas.transform, UiKit.Background);
        background.raycastTarget = false;
        UiKit.CreateBackdrop(background.transform, "bg_game");

        // Всё остальное — в безопасной зоне экрана (на телефоне не под вырезом камеры).
        RectTransform safe = UiKit.CreateSafeRoot(canvas.transform);

        // Доска 800x800 по центру, сверху и снизу по 50 под надписи.
        var boardRt = UiKit.CreateUi("Board", safe, typeof(Board));
        boardRt.sizeDelta = new Vector2(800, 800);
        mBoard = boardRt.GetComponent<Board>();

        var piecesRt = UiKit.CreateUi("Pieces", safe, typeof(PieceManager));
        piecesRt.sizeDelta = new Vector2(800, 800);
        mPieceManager = piecesRt.GetComponent<PieceManager>();

        if (mStatusText == null)
        {
            mStatusText = UiKit.CreateText("Status", safe, "", 30, UiKit.TextLight);
            var rt = (RectTransform)mStatusText.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -6);
            rt.sizeDelta = new Vector2(0, 44);
        }

        // Подсказка под доской: что значат цвета меток у выбранной фигуры.
        mHintText = UiKit.CreateText("Hint", safe, "", 20, new Color(0.85f, 0.88f, 0.85f, 1f));
        mHintText.supportRichText = true;
        var hrt = (RectTransform)mHintText.transform;
        hrt.anchorMin = new Vector2(0, 0);
        hrt.anchorMax = new Vector2(1, 0);
        hrt.pivot = new Vector2(0.5f, 0);
        hrt.anchoredPosition = new Vector2(0, 8);
        hrt.sizeDelta = new Vector2(0, 36);

        // Кнопка взрыва троянского коня — справа от доски, видна только когда выбран конь.
        mExplodeButton = UiKit.CreatePlateButton(safe, L.T("Взорвать коня"), new Vector2(270, 72), ExplodeSelected, 30);
        var ert = (RectTransform)mExplodeButton.transform;
        ert.anchoredPosition = new Vector2(400 + 40 + 135, 0);
        mExplodeButton.gameObject.SetActive(false);

        // «Ход назад» — справа от доски, под кнопкой взрыва.
        mUndoButton = UiKit.CreatePlateButton(safe, L.T("Ход назад"), new Vector2(270, 64), Undo, 28);
        ((RectTransform)mUndoButton.transform).anchoredPosition = new Vector2(400 + 40 + 135, -100);
        mUndoButton.gameObject.SetActive(false);

        // Разбор партии: панель слева (на месте чата) и кнопки справа.
        mReviewPanel = ReviewPanel.Create(safe, new Vector2(-(400 + 35 + ReviewPanel.Width / 2), -10));
        mReviewPanel.gameObject.SetActive(false);
        var controls = UiKit.CreateUi("Review Controls", safe);
        controls.sizeDelta = Vector2.zero;
        mReviewControls = controls.gameObject;
        void ReviewButton(string label, float y, Action onClick)
        {
            var b = UiKit.CreatePlateButton(controls, label, new Vector2(270, 64), onClick, 26);
            ((RectTransform)b.transform).anchoredPosition = new Vector2(400 + 40 + 135, y);
        }
        ReviewButton(L.T("« В начало"), 230, () => ReviewGo(0));
        ReviewButton(L.T("‹ Назад"), 150, () => ReviewGo(mReviewIndex - 1));
        ReviewButton(L.T("Вперёд ›"), 70, () => ReviewGo(mReviewIndex + 1));
        ReviewButton(L.T("В конец »"), -10, () => ReviewGo(mReviewPositions.Count - 1));
        ReviewButton(L.T("К ошибке ›"), -90, ReviewNextMistake);
        ReviewButton(L.T("Закончить"), -170, EndReview);
        mReviewControls.SetActive(false);

        // «Подсказка» — под «Ход назад».
        mHintButton = UiKit.CreatePlateButton(safe, L.T("Подсказка"), new Vector2(270, 64), RequestHint, 28);
        ((RectTransform)mHintButton.transform).anchoredPosition = new Vector2(400 + 40 + 135, -180);
        mHintButton.gameObject.SetActive(false);

        // Часы справа от доски: сверху — соперник, снизу — тот, кто внизу доски.
        mTopClock = ClockPanel.Create(safe, new Vector2(400 + 40 + 115, 400 - 50));
        mBottomClock = ClockPanel.Create(safe, new Vector2(400 + 40 + 115, -400 + 50));

        // Чат слева от доски (только в сетевой игре) и слой для всплывающих эмодзи поверх всего.
        var floatLayer = UiKit.CreateUi("Emote Layer", safe);
        floatLayer.sizeDelta = Vector2.zero;
        mChat = ChatPanel.Create(safe, new Vector2(-(400 + 35 + ChatPanel.Width / 2), -10), floatLayer,
            SendChatText, SendEmote);
        mChat.gameObject.SetActive(false);
        floatLayer.SetAsLastSibling();

        // Панель урока на месте чата.
        mLessonPanel = LessonPanel.Create(safe, new Vector2(-(400 + 35 + LessonPanel.Width / 2), -10),
            NextLesson, () => LessonMenuRequested?.Invoke());
        mLessonPanel.gameObject.SetActive(false);

        // Баннер поверх доски («Король пал!»). Не ловит клики.
        mBannerText = UiKit.CreateText("Banner", safe, "", 48, new Color32(255, 225, 255, 255));
        mBannerText.fontStyle = FontStyle.Bold;
        mBannerText.lineSpacing = 1.1f;
        var outline = mBannerText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(90, 0, 90, 255);
        outline.effectDistance = new Vector2(3, -3);
        ((RectTransform)mBannerText.transform).sizeDelta = new Vector2(UiKit.Wide(1200), 160);
        mBannerText.horizontalOverflow = Platform.Portrait ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        mBannerText.gameObject.SetActive(false);

        if (Platform.Portrait)
            ApplyPortraitLayout(boardRt, piecesRt, hrt, controls);
    }

    /// <summary>
    /// Телефон, вертикальный экран (холст 1000 в ширину), сверху вниз:
    /// строка хода · часы соперника · доска · твои часы · подсказка · кнопки в ряд · панель (урок, чат, разбор).
    /// </summary>
    private void ApplyPortraitLayout(RectTransform board, RectTransform pieces, RectTransform hint, RectTransform controls)
    {
        void Top(RectTransform rt, float x, float fromTop)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(x, -fromTop);
        }

        Top((RectTransform)mTopClock.transform, 0, 60);
        Top(board, 0, 170);
        Top(pieces, 0, 170);
        Top((RectTransform)mBottomClock.transform, 0, 980);

        hint.anchorMin = new Vector2(0, 1);
        hint.anchorMax = new Vector2(1, 1);
        hint.pivot = new Vector2(0.5f, 1);
        hint.anchoredPosition = new Vector2(0, -1085);
        hint.sizeDelta = new Vector2(-20, 40);
        mHintText.horizontalOverflow = HorizontalWrapMode.Wrap;

        // Кнопки рядом с партией — в один ряд под доской.
        Top((RectTransform)mExplodeButton.transform, -310, 1130);
        Top((RectTransform)mUndoButton.transform, 0, 1130);
        Top((RectTransform)mHintButton.transform, 310, 1130);
        foreach (RectTransform b in new[] { (RectTransform)mExplodeButton.transform, (RectTransform)mUndoButton.transform,
                     (RectTransform)mHintButton.transform })
            b.sizeDelta = new Vector2(290, 64);

        // Кнопки разбора — в два ряда по три.
        controls.anchorMin = controls.anchorMax = new Vector2(0.5f, 1);
        controls.pivot = new Vector2(0.5f, 1);
        controls.anchoredPosition = Vector2.zero;
        for (int i = 0; i < controls.childCount; i++)
        {
            var b = (RectTransform)controls.GetChild(i);
            b.sizeDelta = new Vector2(290, 64);
            Top(b, (i % 3 - 1) * 310, 1130 + (i / 3) * 74);
        }

        // Панели урока, чата и разбора занимают всё, что осталось внизу.
        void Fill(RectTransform rt, float fromTop)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(20, 10);
            rt.offsetMax = new Vector2(-20, -fromTop);
        }
        Fill((RectTransform)mLessonPanel.transform, 1205);
        Fill((RectTransform)mChat.transform, 1205);
        Fill((RectTransform)mReviewPanel.transform, 1280);
    }

    #endregion
}
