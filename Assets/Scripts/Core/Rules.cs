using System.Collections.Generic;

namespace Chess2.Core
{
    /// <summary>
    /// Все правила «Пепла и короны»: генерация ходов, деградация, пробивание, троянский конь,
    /// рокировка, возрождение пешки и определение итога партии.
    /// </summary>
    public static class Rules
    {
        private static readonly int[] KnightDx = { 1, 2, 2, 1, -1, -2, -2, -1 };
        private static readonly int[] KnightDy = { 2, 1, -1, -2, -2, -1, 1, 2 };

        // Первые 4 — прямые (ладья), последние 4 — диагонали (слон).
        private static readonly int[] DirDx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] DirDy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        #region Публичный API

        /// <summary>Все разрешённые ходы стороны, которая сейчас ходит.</summary>
        public static List<Move> GetLegalMoves(GameState s)
        {
            var moves = new List<Move>();
            GenerateLegalMoves(s, moves);
            return moves;
        }

        /// <summary>Разрешённые ходы конкретной фигуры — для подсветки клеток в UI.</summary>
        public static List<Move> GetLegalMovesFrom(GameState s, int from)
        {
            var all = GetLegalMoves(s);
            all.RemoveAll(m => m.From != from);
            return all;
        }

        public static void GenerateLegalMoves(GameState s, List<Move> moves)
        {
            moves.Clear();
            if (s.IsOver)
                return;

            GeneratePseudoMoves(s, s.SideToMove, moves);

            if (s.Rules.WinCondition != WinCondition.Annihilation)
            {
                // Короля не бьют — ему ставят шах и мат. Это важно после падения первого короля:
                // загоревшиеся фигуры могут сразу «увидеть» вражеского короля, пока ход за ними.
                for (int i = moves.Count - 1; i >= 0; i--)
                    if (HitsKing(s, moves[i]) || !KeepsKingSafe(s, moves[i]))
                        moves.RemoveAt(i);
            }
        }

        /// <summary>
        /// Проверить ход и сыграть его. Используй для ходов игрока и ходов из сети:
        /// подделанный или устаревший ход просто вернёт false.
        /// </summary>
        public static bool TryMakeMove(GameState s, Move move, List<MoveEffect> effects = null)
        {
            foreach (var legal in GetLegalMoves(s))
            {
                if (legal.SameAs(move))
                {
                    MakeMove(s, legal, effects);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Удобный вариант для тестов/консоли: TryMakeMove(s, "e2", "e4").
        /// Берётся первый подходящий ход (взрыв коня — "g1", "g1").
        /// </summary>
        public static bool TryMakeMove(GameState s, string from, string to, List<MoveEffect> effects = null)
        {
            int f = Sq.Parse(from), t = Sq.Parse(to);
            foreach (var legal in GetLegalMoves(s))
            {
                if (legal.From == f && legal.To == t)
                {
                    MakeMove(s, legal, effects);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Сыграть ход БЕЗ проверки (ход уже взят из GetLegalMoves).
        /// evaluateResult = false пропускает подсчёт итога партии.
        /// </summary>
        public static void MakeMove(GameState s, Move move, List<MoveEffect> effects = null, bool evaluateResult = true)
        {
            Apply(s, move, effects);
            s.Journal.Clear(); // обычный ход не откатывается
            if (evaluateResult)
                EvaluateResult(s, effects);
        }

        /// <summary>Сдача или флаг по времени — вызывается снаружи (UI, таймер, сеть).</summary>
        public static void EndGame(GameState s, Side loser, EndReason reason)
        {
            s.Result = loser == Side.White ? GameResult.BlackWins : GameResult.WhiteWins;
            s.EndReason = reason;
        }

        public static bool IsInCheck(GameState s, Side side)
        {
            int king = s.FindKing(side);
            return king != Sq.None && IsSquareAttacked(s, king, side.Opponent());
        }

        /// <summary>
        /// Бьёт ли сторона by клетку sq (включая пробивание и двойной прыжок коня).
        /// Призраки ничего не бьют. Работает и для пустых клеток — нужно для рокировки.
        /// Взрыв коня здесь не учитывается: в режиме «Мат» он королей не задевает.
        /// </summary>
        public static bool IsSquareAttacked(GameState s, int sq, Side by)
        {
            int x = Sq.X(sq), y = Sq.Y(sq);

            // Пешки: пешка стороны by бьёт по диагонали вперёд.
            int py = y - by.Forward();
            for (int dx = -1; dx <= 1; dx += 2)
                if (Is(s, x + dx, py, by, PieceType.Pawn))
                    return true;

            // Кони и ферзь-«конь».
            for (int i = 0; i < 8; i++)
            {
                int nx = x + KnightDx[i], ny = y + KnightDy[i];
                if (Is(s, nx, ny, by, PieceType.Knight))
                    return true;
                if (IsWithAbility(s, nx, ny, by, PieceType.Queen))
                    return true;
            }

            // Двойной прыжок коня: через пустую промежуточную клетку.
            if (s.Rules.TrojanKnight)
            {
                for (int i = 0; i < 8; i++)
                {
                    int vx = x + KnightDx[i], vy = y + KnightDy[i];
                    if (!IsEmpty(s, vx, vy))
                        continue;
                    for (int j = 0; j < 8; j++)
                    {
                        int kx = vx + KnightDx[j], ky = vy + KnightDy[j];
                        if (IsWithAbility(s, kx, ky, by, PieceType.Knight) && !s.Cells[Sq.Of(kx, ky)].DoubleUsed)
                            return true;
                    }
                }
            }

            // Король рядом.
            for (int d = 0; d < 8; d++)
                if (Is(s, x + DirDx[d], y + DirDy[d], by, PieceType.King))
                    return true;

            // Лучи: обычные удары и пробивание сквозь одну фигуру.
            for (int d = 0; d < 8; d++)
            {
                bool straight = d < 4;
                int nx = x, ny = y;
                Piece screen = default;
                bool haveScreen = false;

                while (true)
                {
                    nx += DirDx[d];
                    ny += DirDy[d];
                    if (!Sq.InBounds(nx, ny))
                        break;

                    Piece p = s.Cells[Sq.Of(nx, ny)];
                    if (p.IsEmpty)
                        continue;

                    if (!haveScreen)
                    {
                        if (p.Side == by && SlidesAlong(p.Type, straight))
                            return true;
                        screen = p;
                        haveScreen = true;
                        continue;
                    }

                    // Вторая фигура на луче: может ли она пробить сквозь screen?
                    if (p.Side == by && CanPierce(s, p) && PierceMatches(p.Type, straight)
                        && (s.Rules.PierceThroughOwnPieces || screen.Side != by))
                        return true;
                    break;
                }
            }

            return false;
        }

        #endregion

        #region Генерация ходов

        public static void GeneratePseudoMoves(GameState s, Side side, List<Move> moves)
        {
            for (int sq = 0; sq < 64; sq++)
            {
                Piece p = s.Cells[sq];
                if (!p.IsEmpty && p.Side == side)
                    GeneratePieceMoves(s, sq, p, moves);
            }
        }

        private static void GeneratePieceMoves(GameState s, int from, Piece p, List<Move> moves)
        {
            switch (p.Type)
            {
                case PieceType.Ghost:
                    // 1 клетка в любую сторону, бить не может.
                    for (int d = 0; d < 8; d++)
                        AddStep(s, from, p, DirDx[d], DirDy[d], moves, canCapture: false);
                    break;

                case PieceType.Pawn:
                    GeneratePawnMoves(s, from, p, moves);
                    break;

                case PieceType.Knight:
                    for (int i = 0; i < 8; i++)
                        AddStep(s, from, p, KnightDx[i], KnightDy[i], moves, canCapture: true);
                    if (HasAbility(s, p))
                        GenerateTrojanMoves(s, from, p, moves);
                    break;

                case PieceType.Bishop:
                    AddSlides(s, from, p, 4, 8, moves);
                    break;

                case PieceType.Rook:
                    AddSlides(s, from, p, 0, 4, moves);
                    break;

                case PieceType.Queen:
                    AddSlides(s, from, p, 0, 8, moves);
                    if (HasAbility(s, p))
                        for (int i = 0; i < 8; i++)
                            AddStep(s, from, p, KnightDx[i], KnightDy[i], moves, canCapture: true);
                    break;

                case PieceType.King:
                    for (int d = 0; d < 8; d++)
                        AddStep(s, from, p, DirDx[d], DirDy[d], moves, canCapture: true);
                    AddCastling(s, from, p, moves);
                    break;
            }

            // Любая своя фигура, кроме короля и коней, может сесть в соседнего свободного коня.
            if (s.Rules.TrojanKnight && p.Type != PieceType.King && p.Type != PieceType.Knight)
                AddEmbarks(s, from, p, moves);
        }

        private static void GeneratePawnMoves(GameState s, int from, Piece p, List<Move> moves)
        {
            int x = Sq.X(from), y = Sq.Y(from);
            int f = p.Side.Forward();

            // Вперёд на 1, и на 2 если пешка ещё не ходила (пешки из деградации считаются ходившими).
            if (IsEmpty(s, x, y + f))
            {
                moves.Add(new Move(from, Sq.Of(x, y + f), MoveKind.Quiet));
                if (!p.HasMoved && IsEmpty(s, x, y + 2 * f))
                    moves.Add(new Move(from, Sq.Of(x, y + 2 * f), MoveKind.Quiet));
            }

            // Удар по диагонали.
            for (int dx = -1; dx <= 1; dx += 2)
                if (IsEnemy(s, x + dx, y + f, p.Side))
                    moves.Add(new Move(from, Sq.Of(x + dx, y + f), MoveKind.Capture));
        }

        /// <summary>Двойной прыжок, высадка пассажира и взрыв.</summary>
        private static void GenerateTrojanMoves(GameState s, int from, Piece p, List<Move> moves)
        {
            int x = Sq.X(from), y = Sq.Y(from);

            // Клетки, куда уже есть другой ход коня, — двойной прыжок туда не предлагаем,
            // чтобы в интерфейсе у каждой клетки был ровно один ход.
            ulong taken = 1UL << from;
            for (int i = 0; i < 8; i++)
            {
                int nx = x + KnightDx[i], ny = y + KnightDy[i];
                if (Sq.InBounds(nx, ny))
                    taken |= 1UL << Sq.Of(nx, ny);
            }

            // Высадка пассажира на соседнюю пустую клетку.
            if (!s.Cargo[from].IsEmpty)
            {
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + DirDx[d], ny = y + DirDy[d];
                    if (IsEmpty(s, nx, ny))
                    {
                        int to = Sq.Of(nx, ny);
                        moves.Add(new Move(from, to, MoveKind.Disembark));
                        taken |= 1UL << to;
                    }
                }
            }

            // Двойной прыжок (раз за игру): через пустую промежуточную клетку.
            if (!p.DoubleUsed)
            {
                for (int i = 0; i < 8; i++)
                {
                    int vx = x + KnightDx[i], vy = y + KnightDy[i];
                    if (!IsEmpty(s, vx, vy))
                        continue;
                    int via = Sq.Of(vx, vy);

                    for (int j = 0; j < 8; j++)
                    {
                        int tx = vx + KnightDx[j], ty = vy + KnightDy[j];
                        if (!Sq.InBounds(tx, ty))
                            continue;
                        int to = Sq.Of(tx, ty);
                        if ((taken & (1UL << to)) != 0)
                            continue;

                        Piece target = s.Cells[to];
                        if (target.IsEmpty)
                            moves.Add(new Move(from, to, MoveKind.DoubleJump, via));
                        else if (target.Side != p.Side)
                            moves.Add(new Move(from, to, MoveKind.DoubleCapture, via));
                        else
                            continue;
                        taken |= 1UL << to;
                    }
                }
            }

            // Взрыв.
            moves.Add(new Move(from, from, MoveKind.Explode));
        }

        private static void AddEmbarks(GameState s, int from, Piece p, List<Move> moves)
        {
            int x = Sq.X(from), y = Sq.Y(from);
            for (int d = 0; d < 8; d++)
            {
                int nx = x + DirDx[d], ny = y + DirDy[d];
                if (!Sq.InBounds(nx, ny))
                    continue;
                int to = Sq.Of(nx, ny);
                Piece k = s.Cells[to];
                if (k.Type == PieceType.Knight && k.Side == p.Side && s.Cargo[to].IsEmpty && HasAbility(s, k))
                    moves.Add(new Move(from, to, MoveKind.Embark));
            }
        }

        private static void AddStep(GameState s, int from, Piece p, int dx, int dy, List<Move> moves, bool canCapture)
        {
            int nx = Sq.X(from) + dx, ny = Sq.Y(from) + dy;
            if (!Sq.InBounds(nx, ny))
                return;

            int to = Sq.Of(nx, ny);
            Piece target = s.Cells[to];
            if (target.IsEmpty)
                moves.Add(new Move(from, to, MoveKind.Quiet));
            else if (canCapture && target.Side != p.Side)
                moves.Add(new Move(from, to, MoveKind.Capture));
        }

        private static void AddSlides(GameState s, int from, Piece p, int dirStart, int dirEnd, List<Move> moves)
        {
            bool canPierce = CanPierce(s, p);

            for (int d = dirStart; d < dirEnd; d++)
            {
                int nx = Sq.X(from), ny = Sq.Y(from);
                bool haveScreen = false;
                Side screenSide = Side.White;

                while (true)
                {
                    nx += DirDx[d];
                    ny += DirDy[d];
                    if (!Sq.InBounds(nx, ny))
                        break;

                    int to = Sq.Of(nx, ny);
                    Piece target = s.Cells[to];

                    if (!haveScreen)
                    {
                        if (target.IsEmpty)
                        {
                            moves.Add(new Move(from, to, MoveKind.Quiet));
                            continue;
                        }
                        if (target.Side != p.Side)
                            moves.Add(new Move(from, to, MoveKind.Capture));

                        if (!canPierce)
                            break;
                        haveScreen = true;
                        screenSide = target.Side;
                        continue;
                    }

                    // За «экраном»: пробиваем первую фигуру, если она вражеская.
                    if (target.IsEmpty)
                        continue;
                    if (target.Side != p.Side && (s.Rules.PierceThroughOwnPieces || screenSide != p.Side))
                        moves.Add(new Move(from, to, MoveKind.Pierce));
                    break;
                }
            }
        }

        private static void AddCastling(GameState s, int from, Piece king, List<Move> moves)
        {
            int home = king.Side.HomeRank();
            if (king.HasMoved || from != Sq.Of(4, home))
                return;

            // Короткая: ладья h, клетки f и g пустые. Длинная: ладья a, клетки b, c, d пустые.
            if (IsUnmovedRook(s, Sq.Of(7, home), king.Side) && IsEmpty(s, 5, home) && IsEmpty(s, 6, home))
                moves.Add(new Move(from, Sq.Of(6, home), MoveKind.Castle));

            if (IsUnmovedRook(s, Sq.Of(0, home), king.Side) && IsEmpty(s, 1, home) && IsEmpty(s, 2, home) && IsEmpty(s, 3, home))
                moves.Add(new Move(from, Sq.Of(2, home), MoveKind.Castle));
        }

        #endregion

        #region Применение хода

        private static void Apply(GameState s, Move m, List<MoveEffect> effects)
        {
            Piece mover = s.Cells[m.From];
            Side side = mover.Side;
            bool progress = mover.Type == PieceType.Pawn;

            switch (m.Kind)
            {
                case MoveKind.Quiet:
                    Relocate(s, m.From, m.To, effects);
                    TryRevivePawn(s, m.To, side, effects);
                    break;

                case MoveKind.Castle:
                {
                    int home = Sq.Y(m.From);
                    bool kingSide = Sq.X(m.To) == 6;
                    Relocate(s, m.From, m.To, effects);
                    Relocate(s, Sq.Of(kingSide ? 7 : 0, home), Sq.Of(kingSide ? 5 : 3, home), effects);
                    break;
                }

                case MoveKind.Capture:
                case MoveKind.Pierce:
                    progress = true;
                    if (m.Kind == MoveKind.Pierce)
                    {
                        mover.PierceUsed = true;
                        Set(s, m.From, mover);
                    }
                    // Фигура не умирает, а слабеет на месте; атакующий встаёт на клетку,
                    // только если цель (призрак) ушла с доски.
                    if (Hit(s, m.To, effects))
                    {
                        Relocate(s, m.From, m.To, effects);
                        TryRevivePawn(s, m.To, side, effects);
                    }
                    break;

                case MoveKind.DoubleJump:
                    mover.DoubleUsed = true;
                    Set(s, m.From, mover);
                    Relocate(s, m.From, m.To, effects);
                    break;

                case MoveKind.DoubleCapture:
                    progress = true;
                    mover.DoubleUsed = true;
                    Set(s, m.From, mover);
                    Relocate(s, m.From, m.Via, effects);
                    if (Hit(s, m.To, effects))
                        Relocate(s, m.Via, m.To, effects);
                    break;

                case MoveKind.Embark:
                {
                    Piece passenger = s.Cells[m.From];
                    Set(s, m.From, default);
                    SetCargo(s, m.To, passenger);
                    effects?.Add(Effect(EffectKind.Embarked, passenger, m.From, m.To));
                    break;
                }

                case MoveKind.Disembark:
                {
                    Piece passenger = s.Cargo[m.From];
                    progress = passenger.Type == PieceType.Pawn;
                    SetCargo(s, m.From, default);
                    passenger.HasMoved = true;
                    Set(s, m.To, passenger);
                    effects?.Add(Effect(EffectKind.Disembarked, passenger, m.From, m.To));
                    TryRevivePawn(s, m.To, side, effects);
                    break;
                }

                case MoveKind.Explode:
                    progress = true;
                    Explode(s, m.From, effects);
                    break;
            }

            s.SideToMove = side.Opponent();
            s.Ply++;
            s.NoProgressPlies = progress ? 0 : s.NoProgressPlies + 1;
            s.LastMove = m;
        }

        /// <summary>
        /// Удар по фигуре: она слабеет на ступень. Если это был конь с пассажиром — пассажир выпадает.
        /// Возвращает true, если фигура (призрак) ушла с доски и клетка освободилась.
        /// </summary>
        private static bool Hit(GameState s, int sq, List<MoveEffect> effects)
        {
            Piece target = s.Cells[sq];
            PieceType weaker = target.Type.Degrade();

            if (weaker == PieceType.None)
            {
                effects?.Add(new MoveEffect
                {
                    Kind = EffectKind.Removed, PieceId = target.Id, Side = target.Side,
                    From = sq, To = sq, OldType = target.Type, NewType = PieceType.None
                });
                Set(s, sq, default);
                return true;
            }

            effects?.Add(new MoveEffect
            {
                Kind = EffectKind.Degraded, PieceId = target.Id, Side = target.Side,
                From = sq, To = sq, OldType = target.Type, NewType = weaker
            });
            target.Type = weaker;
            target.HasMoved = true;
            Set(s, sq, target);

            if (!s.Cargo[sq].IsEmpty)
                Eject(s, sq, effects);
            return false;
        }

        /// <summary>Пассажир ослабевшего коня выпадает на первую свободную соседнюю клетку, иначе пропадает.</summary>
        private static void Eject(GameState s, int knightSq, List<MoveEffect> effects)
        {
            Piece passenger = s.Cargo[knightSq];
            SetCargo(s, knightSq, default);

            int x = Sq.X(knightSq), y = Sq.Y(knightSq);
            for (int d = 0; d < 8; d++)
            {
                if (!IsEmpty(s, x + DirDx[d], y + DirDy[d]))
                    continue;
                int to = Sq.Of(x + DirDx[d], y + DirDy[d]);
                passenger.HasMoved = true;
                Set(s, to, passenger);
                effects?.Add(Effect(EffectKind.Ejected, passenger, knightSq, to));
                return;
            }

            effects?.Add(Effect(EffectKind.Removed, passenger, knightSq, knightSq));
        }

        /// <summary>
        /// Конь взрывается вместе с пассажиром, все фигуры на соседних клетках слабеют на ступень
        /// (свои тоже). В режиме «Мат» короли от взрыва не страдают.
        /// </summary>
        private static void Explode(GameState s, int sq, List<MoveEffect> effects)
        {
            Piece knight = s.Cells[sq];
            Piece passenger = s.Cargo[sq];
            Set(s, sq, default);
            SetCargo(s, sq, default);
            effects?.Add(Effect(EffectKind.Exploded, knight, sq, sq));
            if (!passenger.IsEmpty)
                effects?.Add(Effect(EffectKind.Removed, passenger, sq, sq));

            // Сначала запоминаем, кого задело, — выпавшие пассажиры под взрыв уже не попадают.
            int x = Sq.X(sq), y = Sq.Y(sq);
            System.Span<int> victims = stackalloc int[8];
            int count = 0;
            for (int d = 0; d < 8; d++)
            {
                int nx = x + DirDx[d], ny = y + DirDy[d];
                if (!Sq.InBounds(nx, ny))
                    continue;
                int n = Sq.Of(nx, ny);
                Piece p = s.Cells[n];
                if (p.IsEmpty)
                    continue;
                if (p.Type == PieceType.King && s.Rules.WinCondition != WinCondition.Annihilation)
                    continue;
                victims[count++] = n;
            }

            for (int i = 0; i < count; i++)
                Hit(s, victims[i], effects);
        }

        private static void Relocate(GameState s, int from, int to, List<MoveEffect> effects)
        {
            Piece p = s.Cells[from];
            Piece cargo = s.Cargo[from];
            effects?.Add(new MoveEffect
            {
                Kind = EffectKind.Moved, PieceId = p.Id, Side = p.Side,
                From = from, To = to, OldType = p.Type, NewType = p.Type
            });
            p.HasMoved = true;
            Set(s, from, default);
            SetCargo(s, from, default);
            Set(s, to, p);
            SetCargo(s, to, cargo); // пассажир едет вместе с конём
        }

        /// <summary>Пешка на последней горизонтали возрождается в ту фигуру, которой была изначально.</summary>
        private static void TryRevivePawn(GameState s, int sq, Side side, List<MoveEffect> effects)
        {
            Piece p = s.Cells[sq];
            if (p.Type != PieceType.Pawn || p.Side != side || Sq.Y(sq) != side.LastRank())
                return;

            PieceType reborn = p.OriginalType > PieceType.Pawn ? p.OriginalType : s.Rules.PawnPromotionFallback;
            effects?.Add(new MoveEffect
            {
                Kind = EffectKind.Promoted, PieceId = p.Id, Side = p.Side,
                From = sq, To = sq, OldType = p.Type, NewType = reborn
            });
            p.Type = reborn;
            Set(s, sq, p);
        }

        private static MoveEffect Effect(EffectKind kind, Piece p, int from, int to) => new MoveEffect
        {
            Kind = kind, PieceId = p.Id, Side = p.Side, From = from, To = to, OldType = p.Type, NewType = p.Type
        };

        // Все изменения клеток идут через журнал — так ход можно быстро откатить.
        private static void Set(GameState s, int sq, Piece p)
        {
            s.Journal.Add(new GameState.JournalEntry { Sq = sq, Cell = s.Cells[sq], Cargo = s.Cargo[sq] });
            s.Cells[sq] = p;
        }

        private static void SetCargo(GameState s, int sq, Piece p)
        {
            s.Journal.Add(new GameState.JournalEntry { Sq = sq, Cell = s.Cells[sq], Cargo = s.Cargo[sq] });
            s.Cargo[sq] = p;
        }

        #endregion

        #region Быстрый ход/отмена (для ИИ)

        /// <summary>Всё, что нужно, чтобы откатить ход без копирования доски.</summary>
        public struct UndoInfo
        {
            internal int JournalMark;
            internal Side SideToMove;
            internal int Ply, NoProgressPlies;
            internal Move? LastMove;
            internal GameResult Result;
            internal EndReason EndReason;
        }

        /// <summary>
        /// Сыграть ход без проверок и без подсчёта итога — только для перебора в ИИ.
        /// Откатывается через UndoMove (строго в обратном порядке).
        /// </summary>
        public static UndoInfo MakeMoveForSearch(GameState s, Move m)
        {
            var u = new UndoInfo
            {
                JournalMark = s.Journal.Count,
                SideToMove = s.SideToMove,
                Ply = s.Ply,
                NoProgressPlies = s.NoProgressPlies,
                LastMove = s.LastMove,
                Result = s.Result,
                EndReason = s.EndReason
            };
            Apply(s, m, null);
            return u;
        }

        public static void UndoMove(GameState s, in UndoInfo u)
        {
            var journal = s.Journal;
            for (int i = journal.Count - 1; i >= u.JournalMark; i--)
            {
                var e = journal[i];
                s.Cells[e.Sq] = e.Cell;
                s.Cargo[e.Sq] = e.Cargo;
            }
            journal.RemoveRange(u.JournalMark, journal.Count - u.JournalMark);

            s.SideToMove = u.SideToMove;
            s.Ply = u.Ply;
            s.NoProgressPlies = u.NoProgressPlies;
            s.LastMove = u.LastMove;
            s.Result = u.Result;
            s.EndReason = u.EndReason;
        }

        #endregion

        #region Итог партии

        public static void EvaluateResult(GameState s) => EvaluateResult(s, null);

        public static void EvaluateResult(GameState s, List<MoveEffect> effects)
        {
            if (s.IsOver)
                return;

            Side side = s.SideToMove;

            if (s.CountPieces(side) == 0)
            {
                EndGame(s, side, EndReason.Annihilation);
                return;
            }

            if (OnlyGhostsLeft(s))
            {
                SetDraw(s, EndReason.OnlyGhosts);
                return;
            }

            if (GetLegalMoves(s).Count == 0)
            {
                bool mated = s.Rules.WinCondition != WinCondition.Annihilation && IsInCheck(s, side);
                if (mated && s.Rules.WinCondition == WinCondition.KingFall && s.FindKing(side.Opponent()) != Sq.None)
                {
                    // Первый мат в режиме «Корона»: король уходит, армия загорается, игра продолжается.
                    KingFalls(s, side, effects);
                    EvaluateResult(s, effects);
                }
                else if (mated)
                    EndGame(s, side, EndReason.Checkmate);
                else
                    SetDraw(s, EndReason.Stalemate);
                return;
            }

            if (s.NoProgressPlies >= s.Rules.NoProgressDrawPlies)
                SetDraw(s, EndReason.NoProgress);
        }

        private static void SetDraw(GameState s, EndReason reason)
        {
            s.Result = GameResult.Draw;
            s.EndReason = reason;
        }

        public static bool OnlyGhostsLeft(GameState s)
        {
            for (int sq = 0; sq < 64; sq++)
            {
                PieceType t = s.Cells[sq].Type;
                if (t != PieceType.None && t != PieceType.Ghost)
                    return false;
                PieceType c = s.Cargo[sq].Type;
                if (c != PieceType.None && c != PieceType.Ghost)
                    return false;
            }
            return true;
        }

        #endregion

        #region Помощники

        private static bool KeepsKingSafe(GameState s, Move m)
        {
            Side side = s.SideToMove;

            if (m.Kind == MoveKind.Castle && !CastleAllowedInCheckMode(s, m))
                return false;

            UndoInfo undo = MakeMoveForSearch(s, m);
            bool safe = !IsInCheck(s, side);
            UndoMove(s, undo);
            return safe;
        }

        /// <summary>В режиме «Мат»: нельзя рокироваться из-под шаха и через битую клетку.</summary>
        public static bool CastleAllowedInCheckMode(GameState s, Move m)
        {
            Side side = s.SideToMove;
            if (IsInCheck(s, side))
                return false;
            int passX = Sq.X(m.To) == 6 ? 5 : 3;
            return !IsSquareAttacked(s, Sq.Of(passX, Sq.Y(m.From)), side.Opponent());
        }

        /// <summary>Ход бьёт короля (в режимах с шахом такие ходы запрещены).</summary>
        public static bool HitsKing(GameState s, Move m) =>
            m.IsCapture && s.Cells[m.To].Type == PieceType.King;

        private static bool CanPierce(GameState s, Piece p) =>
            !p.PierceUsed && (p.Type == PieceType.Bishop || p.Type == PieceType.Rook) && HasAbility(s, p);

        /// <summary>
        /// Есть ли у фигуры способность её типа: конь — троянский, слон и ладья — пробивание,
        /// ферзь — ходы коня. В режиме «Корона» — только у загоревшихся фигур.
        /// </summary>
        public static bool HasAbility(GameState s, Piece p)
        {
            if (!s.Rules.AbilitiesForAll && !p.Empowered)
                return false;
            switch (p.Type)
            {
                case PieceType.Knight: return s.Rules.TrojanKnight;
                case PieceType.Bishop:
                case PieceType.Rook: return s.Rules.EnablePierce;
                case PieceType.Queen: return s.Rules.QueenMovesAsKnight;
                default: return false;
            }
        }

        /// <summary>Может ли фигура такого типа вообще получить способность при текущих правилах.</summary>
        public static bool CanBeEmpowered(GameState s, PieceType type)
        {
            switch (type)
            {
                case PieceType.Knight: return s.Rules.TrojanKnight;
                case PieceType.Bishop:
                case PieceType.Rook: return s.Rules.EnablePierce;
                case PieceType.Queen: return s.Rules.QueenMovesAsKnight;
                default: return false;
            }
        }

        private static bool IsWithAbility(GameState s, int x, int y, Side side, PieceType type)
        {
            if (!Sq.InBounds(x, y))
                return false;
            Piece p = s.Cells[Sq.Of(x, y)];
            return p.Type == type && p.Side == side && HasAbility(s, p);
        }

        /// <summary>
        /// Король стороны side получил мат: он уходит с доски, а 1–3 случайные фигуры его армии,
        /// которые могут получить способность, загораются. Выбор детерминирован зерном партии.
        /// </summary>
        private static void KingFalls(GameState s, Side side, List<MoveEffect> effects)
        {
            int kingSq = s.FindKing(side);
            Piece king = s.Cells[kingSq];
            s.Cells[kingSq] = default;
            effects?.Add(Effect(EffectKind.KingFell, king, kingSq, kingSq));

            var candidates = new List<int>();
            for (int sq = 0; sq < 64; sq++)
            {
                Piece p = s.Cells[sq];
                if (!p.IsEmpty && p.Side == side && !p.Empowered && CanBeEmpowered(s, p.Type))
                    candidates.Add(sq);
            }

            // Простой детерминированный генератор — одинаковый в Unity и .NET.
            uint rng = (uint)(s.Seed * 2654435761u) ^ (uint)(s.Ply * 40503 + 1) ^ (side == Side.White ? 0x9E37u : 0x79B9u);
            uint Next()
            {
                rng ^= rng << 13;
                rng ^= rng >> 17;
                rng ^= rng << 5;
                return rng;
            }

            int min = System.Math.Max(0, s.Rules.EmpowerMin);
            int max = System.Math.Max(min, s.Rules.EmpowerMax);
            int count = min + (int)(Next() % (uint)(max - min + 1));
            if (count > candidates.Count)
                count = candidates.Count;

            for (int i = 0; i < count; i++)
            {
                int pick = i + (int)(Next() % (uint)(candidates.Count - i));
                int sq = candidates[pick];
                candidates[pick] = candidates[i];
                candidates[i] = sq;

                Piece p = s.Cells[sq];
                p.Empowered = true;
                s.Cells[sq] = p;
                effects?.Add(Effect(EffectKind.Empowered, p, sq, sq));
            }
        }

        private static bool SlidesAlong(PieceType t, bool straight) =>
            t == PieceType.Queen || (straight ? t == PieceType.Rook : t == PieceType.Bishop);

        private static bool PierceMatches(PieceType t, bool straight) =>
            straight ? t == PieceType.Rook : t == PieceType.Bishop;

        private static bool Is(GameState s, int x, int y, Side side, PieceType type)
        {
            if (!Sq.InBounds(x, y))
                return false;
            Piece p = s.Cells[Sq.Of(x, y)];
            return p.Type == type && p.Side == side;
        }

        private static bool IsEmpty(GameState s, int x, int y) =>
            Sq.InBounds(x, y) && s.Cells[Sq.Of(x, y)].IsEmpty;

        private static bool IsEnemy(GameState s, int x, int y, Side mySide)
        {
            if (!Sq.InBounds(x, y))
                return false;
            Piece p = s.Cells[Sq.Of(x, y)];
            return !p.IsEmpty && p.Side != mySide;
        }

        private static bool IsUnmovedRook(GameState s, int sq, Side side)
        {
            Piece p = s.Cells[sq];
            return p.Type == PieceType.Rook && p.Side == side && !p.HasMoved;
        }

        #endregion
    }
}
