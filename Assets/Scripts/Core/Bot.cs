using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Chess2.Core
{
    public enum BotDifficulty : byte
    {
        Easy,    // Новичок: смотрит на 1 ход, часто ошибается
        Medium,  // Любитель: 2 хода, немного ошибается
        Hard,    // Сильный: до 4 ходов, без ошибок
        Expert   // Мастер: сколько успеет за время
    }

    public sealed class BotSettings
    {
        /// <summary>Глубина перебора в полуходах.</summary>
        public int MaxDepth = 4;

        /// <summary>Сколько максимум думать над ходом.</summary>
        public int TimeLimitMs = 1500;

        /// <summary>Глубина «досчёта» разменов в конце перебора.</summary>
        public int QuiescenceDepth = 6;

        /// <summary>Случайный разброс оценки ходов (в сотых пешки). Делает бота слабее и живее.</summary>
        public int EvalNoise;

        /// <summary>Шанс сделать вообще случайный ход.</summary>
        public double RandomMoveChance;

        public static BotSettings For(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    return new BotSettings { MaxDepth = 1, TimeLimitMs = 300, QuiescenceDepth = 1, EvalNoise = 150, RandomMoveChance = 0.15 };
                case BotDifficulty.Medium:
                    return new BotSettings { MaxDepth = 2, TimeLimitMs = 700, QuiescenceDepth = 4, EvalNoise = 40 };
                case BotDifficulty.Hard:
                    return new BotSettings { MaxDepth = 4, TimeLimitMs = 1500, QuiescenceDepth = 6 };
                default:
                    return new BotSettings { MaxDepth = 12, TimeLimitMs = 3500, QuiescenceDepth = 8 };
            }
        }
    }

    /// <summary>
    /// ИИ для «Пепла и короны»: перебор negamax с альфа-бета отсечением, итеративным углублением
    /// и досчётом разменов. Не зависит от Unity — в игре запускается в фоновом потоке.
    /// Один экземпляр Bot нельзя использовать из двух потоков одновременно.
    /// </summary>
    public sealed class Bot
    {
        private const int Inf = 1_000_000;
        private const int Mate = 100_000;
        private const int MaxPly = 96;
        private const int KingFallLoss = 2500;

        public BotSettings Settings { get; }

        // Статистика последнего поиска (для отладки и UI).
        public int LastDepth { get; private set; }
        public int LastScore { get; private set; }
        public long LastNodes { get; private set; }

        private readonly Random _random;
        private readonly List<Move>[] _moveLists = new List<Move>[MaxPly];
        private readonly int[][] _orderKeys = new int[MaxPly][];

        private GameState _s;
        private RuleSet _rules;
        private bool _mateMode;
        private Stopwatch _clock;
        private CancellationToken _cancel;
        private bool _aborted;
        private long _nodes;

        public Bot(BotSettings settings, int? seed = null)
        {
            Settings = settings;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            for (int i = 0; i < MaxPly; i++)
            {
                _moveLists[i] = new List<Move>(64);
                _orderKeys[i] = new int[256];
            }
        }

        public Bot(BotDifficulty difficulty, int? seed = null) : this(BotSettings.For(difficulty), seed) { }

        /// <summary>
        /// Выбрать ход для стороны, которая сейчас ходит. Исходное состояние не меняется.
        /// null — если партия окончена или ходов нет.
        /// </summary>
        public Move? FindBestMove(GameState state, CancellationToken cancel = default)
        {
            if (state.IsOver)
                return null;

            _s = state.Clone();
            _rules = _s.Rules;
            // Шах важен во всех режимах, кроме «Хаоса». У армии без короля IsInCheck сам вернёт false.
            _mateMode = _rules.WinCondition != WinCondition.Annihilation;
            _cancel = cancel;
            _clock = Stopwatch.StartNew();
            _aborted = false;
            _nodes = 0;

            // Корневые ходы — только разрешённые.
            var root = Rules.GetLegalMoves(state);
            if (root.Count == 0)
                return null;
            if (root.Count == 1)
                return root[0];

            if (Settings.RandomMoveChance > 0 && _random.NextDouble() < Settings.RandomMoveChance)
                return root[_random.Next(root.Count)];

            var scores = new int[root.Count];
            OrderMoves(root, 0, null);

            // Шум фиксируем на весь поиск, чтобы он не «прыгал» между итерациями.
            var noise = new int[root.Count];
            for (int i = 0; i < noise.Length; i++)
                noise[i] = Settings.EvalNoise > 0 ? _random.Next(-Settings.EvalNoise, Settings.EvalNoise + 1) : 0;

            Move best = root[0];
            int bestScore = -Inf;
            bool exactRoot = Settings.EvalNoise > 0; // для шума нужны точные оценки всех ходов

            for (int depth = 1; depth <= Settings.MaxDepth; depth++)
            {
                int alpha = -Inf;
                Move iterBest = root[0];
                int iterBestScore = -Inf;

                for (int i = 0; i < root.Count; i++)
                {
                    Move m = root[i];
                    var undo = Rules.MakeMoveForSearch(_s, m);
                    int score = exactRoot
                        ? -Negamax(depth - 1, -Inf, Inf, 1)
                        : -Negamax(depth - 1, -Inf, -alpha, 1);
                    Rules.UndoMove(_s, undo);

                    if (_aborted)
                        break;

                    score += noise[i];
                    scores[i] = score;
                    if (score > iterBestScore)
                    {
                        iterBestScore = score;
                        iterBest = m;
                    }
                    if (score > alpha)
                        alpha = score;
                }

                if (_aborted)
                    break;

                best = iterBest;
                bestScore = iterBestScore;
                LastDepth = depth;

                // Лучшие ходы — вперёд, следующая итерация быстрее отсекает.
                SortByScores(root, scores);

                // Нашли мат — глубже смотреть незачем.
                if (Math.Abs(bestScore) > Mate - 1000)
                    break;
                if (TimeUp())
                    break;
            }

            LastScore = bestScore;
            LastNodes = _nodes;
            return best;
        }

        #region Поиск

        private int Negamax(int depth, int alpha, int beta, int ply)
        {
            _nodes++;
            if ((_nodes & 1023) == 0 && TimeUp())
                _aborted = true;
            if (_aborted)
                return 0;

            Side side = _s.SideToMove;
            if (_s.CountPieces(side) == 0)
                return -Mate + ply;
            if (_s.NoProgressPlies >= _rules.NoProgressDrawPlies || Rules.OnlyGhostsLeft(_s))
                return 0;
            if (depth <= 0 || ply >= MaxPly - 1)
                return Quiesce(alpha, beta, ply, Settings.QuiescenceDepth);

            var moves = _moveLists[ply];
            moves.Clear();
            Rules.GeneratePseudoMoves(_s, side, moves);
            OrderMoves(moves, ply, null);

            int best = -Inf;
            int legal = 0;
            for (int i = 0; i < moves.Count; i++)
            {
                Move m = moves[i];
                if (_mateMode && m.Kind == MoveKind.Castle && !Rules.CastleAllowedInCheckMode(_s, m))
                    continue;
                if (_mateMode && Rules.HitsKing(_s, m))
                    continue;

                var undo = Rules.MakeMoveForSearch(_s, m);
                if (_mateMode && Rules.IsInCheck(_s, side))
                {
                    Rules.UndoMove(_s, undo);
                    continue;
                }
                legal++;
                int score = -Negamax(depth - 1, -beta, -alpha, ply + 1);
                Rules.UndoMove(_s, undo);

                if (_aborted)
                    return 0;
                if (score > best)
                    best = score;
                if (score > alpha)
                    alpha = score;
                if (alpha >= beta)
                    break;
            }

            if (legal == 0)
            {
                if (!_mateMode || !Rules.IsInCheck(_s, side))
                    return 0;
                // Режим «Корона»: первый мат не конец — король уходит, но армия загорается.
                // Для перебора это большая потеря, но не проигрыш.
                if (_rules.WinCondition == WinCondition.KingFall && _s.FindKing(side.Opponent()) != Sq.None)
                    return -KingFallLoss + ply;
                return -Mate + ply;
            }
            return best;
        }

        /// <summary>Досчитываем только удары, чтобы не обрывать перебор посреди размена.</summary>
        private int Quiesce(int alpha, int beta, int ply, int qDepth)
        {
            _nodes++;
            if ((_nodes & 1023) == 0 && TimeUp())
                _aborted = true;
            if (_aborted)
                return 0;

            Side side = _s.SideToMove;
            if (_s.CountPieces(side) == 0)
                return -Mate + ply;

            int standPat = Evaluate();
            if (standPat >= beta || qDepth <= 0 || ply >= MaxPly - 1)
                return standPat;
            if (standPat > alpha)
                alpha = standPat;

            var moves = _moveLists[ply];
            moves.Clear();
            Rules.GeneratePseudoMoves(_s, side, moves);
            for (int i = moves.Count - 1; i >= 0; i--)
                if (!moves[i].IsCapture)
                    moves.RemoveAt(i);
            OrderMoves(moves, ply, null);

            for (int i = 0; i < moves.Count; i++)
            {
                if (_mateMode && Rules.HitsKing(_s, moves[i]))
                    continue;
                var undo = Rules.MakeMoveForSearch(_s, moves[i]);
                if (_mateMode && Rules.IsInCheck(_s, side))
                {
                    Rules.UndoMove(_s, undo);
                    continue;
                }
                int score = -Quiesce(-beta, -alpha, ply + 1, qDepth - 1);
                Rules.UndoMove(_s, undo);

                if (_aborted)
                    return 0;
                if (score >= beta)
                    return score;
                if (score > alpha)
                    alpha = score;
            }
            return alpha;
        }

        private bool TimeUp() =>
            _cancel.IsCancellationRequested || _clock.ElapsedMilliseconds >= Settings.TimeLimitMs;

        #endregion

        #region Порядок ходов

        private void OrderMoves(List<Move> moves, int ply, Move? first)
        {
            int n = moves.Count;
            if (n < 2)
                return;
            int[] keys = n <= _orderKeys[ply].Length ? _orderKeys[ply] : new int[n];

            for (int i = 0; i < n; i++)
            {
                Move m = moves[i];
                int key = 0;
                if (first.HasValue && first.Value.SameAs(m))
                    key = int.MaxValue;
                else if (m.IsCapture)
                    // Сначала бьём самое ценное самым дешёвым.
                    key = 100_000 + TypeValue(_s.Cells[m.To].Type) * 10 - TypeValue(_s.Cells[m.From].Type) / 10;
                else if (_s.Cells[m.From].Type == PieceType.Pawn && Sq.Y(m.To) == _s.Cells[m.From].Side.LastRank())
                    key = 90_000;
                else if (m.Kind == MoveKind.Castle)
                    key = 1_000;
                else if (m.Kind == MoveKind.Explode)
                    key = 500;
                keys[i] = key;
            }

            // Сортировка вставками — списки короткие.
            for (int i = 1; i < n; i++)
            {
                Move m = moves[i];
                int k = keys[i];
                int j = i - 1;
                while (j >= 0 && keys[j] < k)
                {
                    moves[j + 1] = moves[j];
                    keys[j + 1] = keys[j];
                    j--;
                }
                moves[j + 1] = m;
                keys[j + 1] = k;
            }
        }

        private static void SortByScores(List<Move> moves, int[] scores)
        {
            for (int i = 1; i < moves.Count; i++)
            {
                Move m = moves[i];
                int sc = scores[i];
                int j = i - 1;
                while (j >= 0 && scores[j] < sc)
                {
                    moves[j + 1] = moves[j];
                    scores[j + 1] = scores[j];
                    j--;
                }
                moves[j + 1] = m;
                scores[j + 1] = sc;
            }
        }

        #endregion

        #region Оценка позиции

        /// <summary>Оценка с точки зрения стороны, которая ходит (в сотых пешки).</summary>
        private int Evaluate()
        {
            int score = 0;
            for (int sq = 0; sq < 64; sq++)
            {
                Piece p = _s.Cells[sq];
                if (p.IsEmpty)
                    continue;
                int v = PieceScore(p, sq);
                score += p.Side == Side.White ? v : -v;

                // Пассажир коня: его ценность плюс небольшой бонус за «десант».
                Piece c = _s.Cargo[sq];
                if (!c.IsEmpty)
                {
                    int cv = TypeValue(c.Type) + 8 * (int)c.Type + 15;
                    score += c.Side == Side.White ? cv : -cv;
                }
            }
            return _s.SideToMove == Side.White ? score : -score;
        }

        private int TypeValue(PieceType t)
        {
            switch (t)
            {
                case PieceType.Ghost: return 40;
                case PieceType.Pawn: return 100;
                case PieceType.Knight: return 310;
                case PieceType.Bishop: return 330;
                case PieceType.Rook: return 500;
                case PieceType.Queen: return 950;
                // В режиме мата король «бесценен» — это решает поиск.
                // В «Уничтожении» король после удара станет ферзём, значит он должен стоить больше ферзя,
                // иначе бот считает удар по королю подарком сопернику и не добивает его.
                case PieceType.King: return _mateMode ? 0 : TypeValue(PieceType.Queen) + 100;
                default: return 0;
            }
        }

        private int PieceScore(Piece p, int sq)
        {
            int x = Sq.X(sq), y = Sq.Y(sq);
            int v = TypeValue(p.Type);

            // Запас прочности: каждая ступень — ещё один удар, который фигура выдержит.
            v += 8 * (int)p.Type;

            // Способности (у загоревшихся фигур или у всех в «Хаосе»).
            if (Rules.HasAbility(_s, p))
            {
                switch (p.Type)
                {
                    case PieceType.Queen:
                        v += 200; // ходит и как конь
                        break;
                    case PieceType.Bishop:
                    case PieceType.Rook:
                        if (!p.PierceUsed)
                            v += 25; // неиспользованное пробивание
                        break;
                    case PieceType.Knight:
                        v += 40; // возит фигуры и может взорваться
                        if (!p.DoubleUsed)
                            v += 20;
                        break;
                }
            }

            // Центр (0 в углу .. 6 в центре).
            int center = 6 - (int)(Math.Abs(3.5f - x) + Math.Abs(3.5f - y));

            switch (p.Type)
            {
                case PieceType.Knight:
                    v += center * 6;
                    break;
                case PieceType.Bishop:
                case PieceType.Queen:
                case PieceType.Ghost:
                    v += center * 3;
                    break;
                case PieceType.King:
                    if (_mateMode)
                        v += y == p.Side.HomeRank() ? 20 : -10 * Math.Abs(y - p.Side.HomeRank());
                    else
                        v += center * 2;
                    break;
                case PieceType.Pawn:
                {
                    // Чем ближе к концу — тем ценнее: там пешка возродится.
                    int advance = p.Side == Side.White ? y - 1 : 6 - y;
                    if (advance < 0) advance = 0;
                    PieceType reborn = p.OriginalType > PieceType.Pawn ? p.OriginalType : _rules.PawnPromotionFallback;
                    int gain = Math.Max(0, TypeValue(reborn) - 100);
                    v += advance * 8 + gain * advance * advance / 108;
                    if (x >= 2 && x <= 5)
                        v += 5;
                    break;
                }
            }
            return v;
        }

        #endregion
    }
}
