using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chess2.Core;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Связывает ядро правил (Chess2.Core) с картинкой и ботом.
/// Ходы игрока, бота (и в будущем сети) проходят через ApplyMove.
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

    private GameObject mRoot;
    private MenuButton mExplodeButton;
    private Text mBannerText;
    private Coroutine mBannerRoutine;
    private Text mHintText;
    private int mSelected = Sq.None;
    private List<Move> mSelectedMoves = new List<Move>();
    private readonly List<MoveEffect> mEffects = new List<MoveEffect>();

    private Task<Move?> mBotTask;
    private CancellationTokenSource mBotCancel;
    private float mBotStartedAt;
    private bool mBuilt;
    private ClockPanel mTopClock;
    private ClockPanel mBottomClock;

    void Start()
    {
        EnsureBuilt();

        // Без меню (например, GameManager поставлен в сцену вручную) — сразу партия против бота.
        if (GetComponent<MenuManager>() == null && State == null)
            StartGame(true, Side.Black, BotDifficulty.Medium, RuleSet.Default);
    }

    void Update()
    {
        TickClock();
        PollBot();
        UpdateClockUi();
    }

    void OnDestroy()
    {
        CancelBot();
    }

    #region Партия

    /// <summary>Начать партию. vsBot = false — вдвоём за одним компьютером.</summary>
    public void StartGame(bool vsBot, Side botSide, BotDifficulty difficulty, RuleSet rules, int timeMinutes = 0)
    {
        EnsureBuilt();
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
    public void Restart() => StartGame(PlayVsBot, BotSide, BotDifficulty, Rules, TimeMinutes);

    /// <summary>Убрать доску (выход в главное меню).</summary>
    public void StopGame()
    {
        CancelBot();
        Deselect();
        State = null;
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        EnsureBuilt();
        mRoot.SetActive(visible);
    }

    private void NewGame()
    {
        CancelBot();
        State = GameState.CreateStandard(Rules);
        Clock = new ChessClock(TimeMinutes);

        // Человек всегда снизу.
        mBoard.SetFlipped(PlayVsBot && BotSide == Side.White);

        Deselect();
        mBoard.SetLastMove(null);
        mPieceManager.Clear();
        mPieceManager.Sync(State, null);
        UpdateStatus();
        MaybeStartBot();
    }

    /// <summary>
    /// Единая точка, через которую проходит любой ход: игрока, бота или из сети.
    /// Возвращает false, если ход не по правилам.
    /// </summary>
    public bool ApplyMove(Move move)
    {
        if (State == null)
            return false;

        mEffects.Clear();
        if (!Chess2.Core.Rules.TryMakeMove(State, move, mEffects))
            return false;

        Clock.OnMoveMade();
        if (State.IsOver)
            Clock.Stop();

        Deselect();
        mBoard.SetLastMove(State.LastMove);
        mPieceManager.Sync(State, mEffects);
        UpdateStatus();

        foreach (MoveEffect e in mEffects)
        {
            if (e.Kind == EffectKind.KingFell)
            {
                ShowBanner(KingFallText(mEffects));
                break;
            }
        }

        if (State.IsOver)
            GameEnded?.Invoke(State);
        else
            MaybeStartBot();
        return true;
    }

    #endregion

    #region Часы

    private void TickClock()
    {
        if (State == null || State.IsOver || Paused || !Clock.Enabled)
            return;

        Side toMove = State.SideToMove;
        if (!Clock.Tick(toMove, Time.unscaledDeltaTime))
            return;

        // Флаг упал: у того, чей ход, кончилось время.
        Chess2.Core.Rules.EndGame(State, toMove, EndReason.Timeout);
        CancelBot();
        Deselect();
        UpdateStatus();
        GameEnded?.Invoke(State);
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
        string color = side == Side.White ? "Белые" : "Чёрные";
        if (!PlayVsBot)
            return color;
        return side == BotSide ? $"Бот · {color.ToLowerInvariant()}" : $"Ты · {color.ToLowerInvariant()}";
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
        if (State == null || State.IsOver || IsBotTurn || Paused)
            return false;
        Piece p = State.Cells[sq];
        return !p.IsEmpty && p.Side == State.SideToMove;
    }

    public void OnSquareClicked(int sq)
    {
        if (State == null || IsBotTurn || Paused)
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
        mBoard.ShowMoves(sq, mSelectedMoves);
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
        var parts = new List<string>();
        if (hasDouble) parts.Add("<color=#3cd0d8>бирюзовые</color> — двойной прыжок (раз за игру)");
        if (hasDisembark) parts.Add("<color=#6fd67c>зелёные точки</color> — высадить пассажира");
        if (hasEmbark) parts.Add("<color=#6fd67c>зелёное кольцо</color> — сесть в коня");
        if (hasPierce) parts.Add("<color=#b27cf0>фиолетовые</color> — удар сквозь фигуру (раз за игру)");
        if (canExplode) parts.Add("кнопка справа — взорвать коня");
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
        if (State.IsOver)
            return ResultText(State);

        string text;
        if (IsBotTurn)
            text = $"Бот ({DifficultyText(BotDifficulty)}) думает…";
        else if (PlayVsBot)
            text = "Твой ход";
        else
            text = "Ход " + (State.SideToMove == Side.White ? "белых" : "чёрных");

        bool checkRules = State.Rules.WinCondition != WinCondition.Annihilation;
        if (checkRules && State.FindKing(State.SideToMove) == Sq.None)
            text += " (без короля)";
        if (checkRules && Chess2.Core.Rules.IsInCheck(State, State.SideToMove))
            text += " — ШАХ!";
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

        string who = PlayVsBot
            ? (side == BotSide ? "Король бота пал!" : "Твой король пал!")
            : (side == Side.White ? "Белый король пал!" : "Чёрный король пал!");
        string fire = lit.Count == 0
            ? "Но загореться некому…"
            : "Загорелись: " + string.Join(", ", lit);
        return who + "\n" + fire;
    }

    public static string AbilityText(PieceType type)
    {
        switch (type)
        {
            case PieceType.Knight: return "конь (троянский)";
            case PieceType.Bishop: return "слон (пробивание)";
            case PieceType.Rook: return "ладья (пробивание)";
            case PieceType.Queen: return "ферзь (ходы коня)";
            default: return type.ToString();
        }
    }

    /// <summary>Итог партии одной строкой (для надписи и окна конца игры).</summary>
    public string ResultText(GameState s)
    {
        string reason = ReasonText(s.EndReason);
        if (s.Result == GameResult.Draw)
            return $"Ничья: {reason}";

        Side winner = s.Result == GameResult.WhiteWins ? Side.White : Side.Black;
        if (PlayVsBot)
            return (winner == BotSide ? "Бот победил" : "Ты победил") + $": {reason}";
        return (winner == Side.White ? "Белые победили" : "Чёрные победили") + $": {reason}";
    }

    public static string DifficultyText(BotDifficulty d)
    {
        switch (d)
        {
            case BotDifficulty.Easy: return "новичок";
            case BotDifficulty.Medium: return "любитель";
            case BotDifficulty.Hard: return "сильный";
            default: return "мастер";
        }
    }

    private static string ReasonText(EndReason reason)
    {
        switch (reason)
        {
            case EndReason.Checkmate: return "мат";
            case EndReason.Annihilation: return "фигур не осталось";
            case EndReason.Stalemate: return "пат, ходить нечем";
            case EndReason.NoProgress: return "50 ходов без ударов";
            case EndReason.OnlyGhosts: return "остались одни призраки";
            case EndReason.Resignation: return "сдача";
            case EndReason.Timeout: return "время вышло";
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

        UiKit.CreatePanel("Background", canvas.transform, UiKit.Background).raycastTarget = false;

        // Доска 800x800 по центру, сверху и снизу по 50 под надписи.
        var boardRt = UiKit.CreateUi("Board", canvas.transform, typeof(Board));
        boardRt.sizeDelta = new Vector2(800, 800);
        mBoard = boardRt.GetComponent<Board>();

        var piecesRt = UiKit.CreateUi("Pieces", canvas.transform, typeof(PieceManager));
        piecesRt.sizeDelta = new Vector2(800, 800);
        mPieceManager = piecesRt.GetComponent<PieceManager>();

        if (mStatusText == null)
        {
            mStatusText = UiKit.CreateText("Status", canvas.transform, "", 30, UiKit.TextLight);
            var rt = (RectTransform)mStatusText.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -6);
            rt.sizeDelta = new Vector2(0, 44);
        }

        // Подсказка под доской: что значат цвета меток у выбранной фигуры.
        mHintText = UiKit.CreateText("Hint", canvas.transform, "", 20, new Color(0.85f, 0.88f, 0.85f, 1f));
        mHintText.supportRichText = true;
        var hrt = (RectTransform)mHintText.transform;
        hrt.anchorMin = new Vector2(0, 0);
        hrt.anchorMax = new Vector2(1, 0);
        hrt.pivot = new Vector2(0.5f, 0);
        hrt.anchoredPosition = new Vector2(0, 8);
        hrt.sizeDelta = new Vector2(0, 36);

        // Кнопка взрыва троянского коня — справа от доски, видна только когда выбран конь.
        mExplodeButton = UiKit.CreatePlateButton(canvas.transform, "Взорвать коня", new Vector2(270, 72), ExplodeSelected, 30);
        var ert = (RectTransform)mExplodeButton.transform;
        ert.anchoredPosition = new Vector2(400 + 40 + 135, 0);
        mExplodeButton.gameObject.SetActive(false);

        // Часы справа от доски: сверху — соперник, снизу — тот, кто внизу доски.
        mTopClock = ClockPanel.Create(canvas.transform, new Vector2(400 + 40 + 115, 400 - 50));
        mBottomClock = ClockPanel.Create(canvas.transform, new Vector2(400 + 40 + 115, -400 + 50));

        // Баннер поверх доски («Король пал!»). Не ловит клики.
        mBannerText = UiKit.CreateText("Banner", canvas.transform, "", 48, new Color32(255, 225, 255, 255));
        mBannerText.fontStyle = FontStyle.Bold;
        mBannerText.lineSpacing = 1.1f;
        var outline = mBannerText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(90, 0, 90, 255);
        outline.effectDistance = new Vector2(3, -3);
        ((RectTransform)mBannerText.transform).sizeDelta = new Vector2(1200, 160);
        mBannerText.gameObject.SetActive(false);
    }

    #endregion
}
