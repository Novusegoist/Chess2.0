using System;
using System.Collections.Generic;
using System.Linq;
using Chess2.Core;

// Простой набор проверок без внешних библиотек: dotnet run в этой папке.
internal static class Program
{
    private static int _failed;
    private static int _passed;

    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "arena")
            return Arena(args.Length > 1 ? int.Parse(args[1]) : 10);

        Run("Стартовая позиция: 20 ходов в «Короне», 24/46 в «Хаосе»", StartPosition);
        Run("Взятие: цель слабеет на месте, атакующий не двигается", CaptureDegrades);
        Run("Полная цепочка деградации короля до исчезновения", FullChain);
        Run("Срубленный призрак уходит, атакующий встаёт на его клетку", GhostRemoved);
        Run("Призрак ходит на 1 клетку и не бьёт", GhostMoves);
        Run("Ферзь ходит как конь", QueenKnight);
        Run("Пробивание: сквозь фигуру и только раз за игру", PierceOnce);
        Run("Пробивание сквозь свои выключается настройкой", PierceOwnOption);
        Run("Пешка из коня возрождается конём", PawnRevivesOriginal);
        Run("Обычная пешка возрождается в запасной вариант (по умолчанию ферзь)", PawnFallback);
        Run("Пешка из деградации не ходит на 2 клетки", DegradedPawnNoDoubleStep);
        Run("Рокировка в обе стороны", Castling);
        Run("Режим «Мат»: нельзя оставить короля под ударом, мат = победа", CheckmateMode);
        Run("Режим «Мат»: снятие шаха ослаблением атакующей фигуры", EscapeByDegrading);
        Run("Режим «Уничтожение»: без фигур — поражение", Annihilation);
        Run("Одни призраки на доске — ничья", OnlyGhosts);
        Run("Подделанный ход отклоняется", RejectsIllegal);
        Run("Эффекты хода для UI", Effects);
        Run("IsSquareAttacked согласован с генерацией ударов (случайные партии)", AttackConsistency);
        Run("1000 случайных партий в обоих режимах доигрываются без ошибок", RandomGames);
        Run("Быстрый ход + отмена возвращают позицию точь-в-точь", UndoRestores);
        Run("Троянский конь: посадка, перевозка, высадка", TrojanRide);
        Run("Троянский конь: король и кони не садятся, в коня — только один", TrojanBoardingLimits);
        Run("Троянский конь: пассажир выпадает, когда коня ослабили", TrojanEject);
        Run("Троянский конь: пассажиру некуда выпасть — он пропадает", TrojanEjectNoRoom);
        Run("Троянский конь: пешка-пассажир возрождается на последней линии", TrojanPawnRevive);
        Run("Троянский конь: двойной прыжок раз за игру", TrojanDouble);
        Run("Троянский конь: двойной прыжок с ударом — конь остаётся на промежуточной клетке", TrojanDoubleCapture);
        Run("Троянский конь: взрыв ослабляет всех соседей, конь и пассажир исчезают", TrojanExplode);
        Run("Троянский конь: в режиме «Мат» взрыв не задевает королей, двойной прыжок даёт шах", TrojanMateMode);
        Run("«Корона»: мат — король уходит, 1–3 фигуры загораются, игра идёт", KingFallFirstMate);
        Run("«Корона»: выбор загоревшихся фигур зависит только от зерна партии", KingFallDeterministic);
        Run("«Корона»: армия без короля побеждает матом второму королю", KingFallSecondMate);
        Run("«Корона»: армия без короля проигрывает, когда фигур не осталось", KingFallAnnihilation);
        Run("«Корона»: способности только у загоревшихся фигур", KingFallAbilitiesOnlyEmpowered);
        Run("Часы: стартуют после первого хода, тикают у того, чей ход", ClockBasics);
        Run("Часы: флаг падает ровно на нуле, без контроля времени не тикают", ClockFlag);
        Run("Часы: формат времени", ClockFormat);
        Run("Сеть: хост и гость через localhost играют одну и ту же партию", NetFullGame);
        Run("Сеть: сдача, реванш со сменой цветов, отключение", NetResignRematchDisconnect);
        Run("Сеть: подключение к закрытому порту сообщает об ошибке", NetConnectFails);
        Run("Сеть: чат (кириллица, переносы, длина) и эмодзи", NetChat);
        Run("Сеть: пульс — пинг измеряется, молчание соперника = потеря связи", NetHeartbeat);
        Run("Сеть: партия поверх любого соединения (как будет со Steam)", NetCustomTransport);
        Run("Бот: мат в 1 ход", BotMateInOne);
        Run("Бот: добивает последнюю фигуру соперника", BotFinishes);
        Run("Бот: бьёт незащищённого ферзя", BotHitsQueen);
        Run("Бот: в режиме «Мат» не подставляет короля", BotLegalInMateMode);
        Run("Бот: скорость перебора", BotSpeed);
        foreach (Lesson lesson in Tutorial.Lessons)
            Run("Урок «" + lesson.Title + "» проходится", () => LessonSolvable(lesson));

        Console.WriteLine();
        Console.WriteLine($"Итого: {_passed} прошло, {_failed} упало");
        return _failed == 0 ? 0 : 1;
    }

    // ---------- тесты ----------

    private static void StartPosition()
    {
        Eq(20, Rules.GetLegalMoves(GameState.CreateStandard()).Count, "«Корона»: как в классике, способностей нет");

        var s = GameState.CreateStandard(new RuleSet { WinCondition = WinCondition.Annihilation, AbilitiesForAll = true, TrojanKnight = false });
        var moves = Rules.GetLegalMoves(s);
        Eq(24, moves.Count, "число ходов");
        True(moves.Any(m => m.Kind == MoveKind.Pierce && m.ToString() == "a1*a7"), "a1*a7");
        True(moves.Any(m => m.ToString() == "d1-c3"), "ферзь d1-c3");
        Eq(24, Rules.GetLegalMoves(GameState.CreateStandard(
            new RuleSet { WinCondition = WinCondition.Checkmate, AbilitiesForAll = true, TrojanKnight = false })).Count, "в режиме мата тоже");

        // С троянским конём у каждого коня: 5 двойных прыжков, взрыв, и 5 соседних фигур могут в него сесть.
        var trojan = Rules.GetLegalMoves(GameState.CreateStandard(RuleSet.Chaos));
        Eq(46, trojan.Count, "с троянским конём");
        True(trojan.Any(m => m.ToString() == "b1:c3:e4"), "двойной прыжок b1-c3-e4");
        True(trojan.Any(m => m.ToString() == "a1@b1"), "ладья садится в коня");
        True(trojan.Any(m => m.ToString() == "g1!"), "взрыв");
    }

    private static void CaptureDegrades()
    {
        var s = Empty();
        s.Put("a1", Side.White, PieceType.Rook);
        s.Put("a5", Side.Black, PieceType.Knight);
        Kings(s);
        True(Rules.TryMakeMove(s, "a1", "a5"), "ход");
        Eq(PieceType.Rook, s["a1"].Type, "ладья осталась на a1");
        Eq(PieceType.Pawn, s["a5"].Type, "конь стал пешкой");
        Eq(Side.Black, s["a5"].Side, "цвет не поменялся");
        Eq(Side.Black, s.SideToMove, "ход перешёл");
    }

    private static void FullChain()
    {
        var s = Empty();
        s.Put("a1", Side.White, PieceType.Rook);
        s.Put("a8", Side.Black, PieceType.King);
        s.Put("h1", Side.White, PieceType.King);
        s.Put("h8", Side.Black, PieceType.Ghost); // чтобы у чёрных было чем ходить
        var expected = new[] { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight, PieceType.Pawn, PieceType.Ghost };
        foreach (var t in expected)
        {
            True(Rules.TryMakeMove(s, "a1", "a8"), "удар по a8");
            Eq(t, s["a8"].Type, "a8");
            True(MoveGhostH(s), "чёрные ходят");
        }
        True(Rules.TryMakeMove(s, "a1", "a8"), "последний удар");
        Eq(PieceType.Rook, s["a8"].Type, "ладья встала на a8");
        Eq(Side.White, s["a8"].Side, "белая");
        True(s["a1"].IsEmpty, "a1 пустая");
    }

    private static void GhostRemoved()
    {
        var s = Empty();
        s.Put("d4", Side.White, PieceType.Knight);
        s.Put("e6", Side.Black, PieceType.Ghost);
        Kings(s);
        True(Rules.TryMakeMove(s, "d4", "e6"), "ход");
        Eq(PieceType.Knight, s["e6"].Type, "конь на e6");
        True(s["d4"].IsEmpty, "d4 пустая");
    }

    private static void GhostMoves()
    {
        var s = Empty();
        s.Put("d4", Side.White, PieceType.Ghost);
        s.Put("d5", Side.Black, PieceType.Pawn);
        Kings(s);
        var moves = Rules.GetLegalMovesFrom(s, Sq.Parse("d4"));
        Eq(7, moves.Count, "7 пустых соседних клеток");
        True(moves.All(m => m.Kind == MoveKind.Quiet), "без ударов");
    }

    private static void QueenKnight()
    {
        var s = Empty();
        s.Put("d4", Side.White, PieceType.Queen);
        Kings(s);
        var moves = Rules.GetLegalMovesFrom(s, Sq.Parse("d4"));
        Eq(27 + 8, moves.Count, "27 ферзевых + 8 конских");

        var off = Empty(new RuleSet { WinCondition = WinCondition.Annihilation, AbilitiesForAll = true, QueenMovesAsKnight = false });
        off.Put("d4", Side.White, PieceType.Queen);
        Kings(off);
        Eq(27, Rules.GetLegalMovesFrom(off, Sq.Parse("d4")).Count, "без коня");
    }

    private static void PierceOnce()
    {
        var s = Empty();
        s.Put("a1", Side.White, PieceType.Bishop);
        s.Put("c3", Side.Black, PieceType.Pawn);
        s.Put("e5", Side.Black, PieceType.Rook);
        s.Put("h1", Side.White, PieceType.King);
        s.Put("h8", Side.Black, PieceType.King);
        var moves = Rules.GetLegalMovesFrom(s, Sq.Parse("a1"));
        True(moves.Any(m => m.ToString() == "a1xc3"), "обычный удар");
        True(moves.Any(m => m.ToString() == "a1*e5"), "пробивание");
        True(Rules.TryMakeMove(s, new Move(Sq.Parse("a1"), Sq.Parse("e5"), MoveKind.Pierce)), "пробили");
        Eq(PieceType.Bishop, s["e5"].Type, "ладья → слон");
        Eq(PieceType.Pawn, s["c3"].Type, "экран не тронут");
        True(s["a1"].PierceUsed, "заряд потрачен");
        True(Rules.TryMakeMove(s, "h8", "g8"), "чёрные");
        True(!Rules.GetLegalMovesFrom(s, Sq.Parse("a1")).Any(m => m.Kind == MoveKind.Pierce), "второго нет");
    }

    private static void PierceOwnOption()
    {
        foreach (bool allow in new[] { true, false })
        {
            var s = Empty(new RuleSet { WinCondition = WinCondition.Annihilation, AbilitiesForAll = true, PierceThroughOwnPieces = allow });
            s.Put("a1", Side.White, PieceType.Rook);
            s.Put("a2", Side.White, PieceType.Pawn);
            s.Put("a7", Side.Black, PieceType.Pawn);
            Kings(s);
            bool has = Rules.GetLegalMoves(s).Any(m => m.ToString() == "a1*a7");
            Eq(allow, has, "сквозь свою пешку, allow=" + allow);
            Eq(allow, Rules.IsSquareAttacked(s, Sq.Parse("a7"), Side.White), "IsSquareAttacked, allow=" + allow);
        }
    }

    private static void PawnRevivesOriginal()
    {
        var s = Empty();
        s.Put("c7", Side.White, PieceType.Pawn, original: PieceType.Knight);
        Kings(s);
        True(Rules.TryMakeMove(s, "c7", "c8"), "ход");
        Eq(PieceType.Knight, s["c8"].Type, "конь вернулся");
    }

    private static void PawnFallback()
    {
        var s = Empty(new RuleSet { WinCondition = WinCondition.Annihilation, AbilitiesForAll = true, PawnPromotionFallback = PieceType.Rook });
        s.Put("c2", Side.Black, PieceType.Pawn);
        Kings(s);
        s.SideToMove = Side.Black;
        True(Rules.TryMakeMove(s, "c2", "c1"), "ход");
        Eq(PieceType.Rook, s["c1"].Type, "запасной вариант");
    }

    private static void DegradedPawnNoDoubleStep()
    {
        var s = Empty();
        s.Put("d1", Side.White, PieceType.Rook);
        s.Put("d7", Side.Black, PieceType.Knight, hasMoved: false);
        Kings(s);
        True(Rules.TryMakeMove(s, "d1", "d7"), "удар");
        var moves = Rules.GetLegalMovesFrom(s, Sq.Parse("d7"));
        Eq(1, moves.Count, "только d7-d6");
    }

    private static void Castling()
    {
        var s = Empty();
        s.Put("e1", Side.White, PieceType.King, hasMoved: false);
        s.Put("a1", Side.White, PieceType.Rook, hasMoved: false);
        s.Put("h1", Side.White, PieceType.Rook, hasMoved: false);
        s.Put("e8", Side.Black, PieceType.King);
        var ks = s.Clone();
        True(Rules.TryMakeMove(ks, "e1", "g1"), "короткая");
        Eq(PieceType.King, ks["g1"].Type, "король g1");
        Eq(PieceType.Rook, ks["f1"].Type, "ладья f1");
        True(Rules.TryMakeMove(s, "e1", "c1"), "длинная");
        Eq(PieceType.Rook, s["d1"].Type, "ладья d1");

        // В режиме мата нельзя рокироваться через битую клетку.
        var m = Empty(RuleSet.ClassicMate);
        m.Put("e1", Side.White, PieceType.King, hasMoved: false);
        m.Put("h1", Side.White, PieceType.Rook, hasMoved: false);
        m.Put("f8", Side.Black, PieceType.Rook);
        m.Put("a8", Side.Black, PieceType.King);
        True(!Rules.TryMakeMove(m, "e1", "g1"), "через f1 под ударом нельзя");
    }

    private static void CheckmateMode()
    {
        var s = Empty(RuleSet.ClassicMate);
        s.Put("h1", Side.White, PieceType.King);
        s.Put("g2", Side.White, PieceType.Pawn);
        s.Put("h2", Side.White, PieceType.Pawn);
        s.Put("a8", Side.Black, PieceType.Rook);
        s.Put("e8", Side.Black, PieceType.King);
        s.SideToMove = Side.Black;
        True(Rules.TryMakeMove(s, "a8", "a1"), "Ra1");
        Eq(GameResult.BlackWins, s.Result, "мат");
        Eq(EndReason.Checkmate, s.EndReason, "причина");

        var t = Empty(RuleSet.ClassicMate);
        t.Put("e1", Side.White, PieceType.King);
        t.Put("e2", Side.White, PieceType.Bishop);
        t.Put("e8", Side.Black, PieceType.Rook);
        t.Put("a8", Side.Black, PieceType.King);
        True(!Rules.TryMakeMove(t, "e2", "d3"), "связанный слон не ходит");
    }

    private static void EscapeByDegrading()
    {
        // Ферзь даёт шах, белый конь бьёт его — ферзь становится ладьёй на h4 и шах снят.
        var s = Empty(RuleSet.ClassicMate);
        s.Put("e1", Side.White, PieceType.King);
        s.Put("d1", Side.White, PieceType.Pawn); // закрываем d1
        s.Put("d2", Side.White, PieceType.Pawn);
        s.Put("e2", Side.White, PieceType.Pawn);
        s.Put("f1", Side.White, PieceType.Pawn);
        s.Put("f2", Side.White, PieceType.Ghost); // призрак не спасает — не бьёт
        s.Put("g2", Side.White, PieceType.Knight);
        s.Put("h4", Side.Black, PieceType.Queen);
        s.Put("a8", Side.Black, PieceType.King);
        // Ферзь h4 бьёт e1? Диагональ h4-g3-f2: f2 закрыт призраком — значит шаха нет.
        True(!Rules.IsInCheck(s, Side.White), "призрак закрывает");
        s.Cells[Sq.Parse("f2")] = default;
        True(Rules.IsInCheck(s, Side.White), "теперь шах");
        var moves = Rules.GetLegalMoves(s);
        True(moves.Any(m => m.ToString() == "g2xh4"), "можно ослабить ферзя конём");
        True(Rules.TryMakeMove(s, "g2", "h4"), "бьём");
        Eq(PieceType.Rook, s["h4"].Type, "ферзь → ладья");
        True(!Rules.IsInCheck(s, Side.White), "шах снят");
    }

    private static void Annihilation()
    {
        var s = Empty();
        s.Put("a1", Side.White, PieceType.Ghost);
        s.Put("a3", Side.Black, PieceType.Rook);
        s.SideToMove = Side.Black;
        True(Rules.TryMakeMove(s, "a3", "a1"), "рубим последнего");
        Eq(GameResult.BlackWins, s.Result, "чёрные выиграли");
        Eq(EndReason.Annihilation, s.EndReason, "причина");
    }

    private static void OnlyGhosts()
    {
        // Пешка ослабляет вражескую пешку до призрака, но у белых ещё есть пешка — игра идёт.
        var s = Empty();
        s.Put("a1", Side.White, PieceType.Ghost);
        s.Put("h8", Side.Black, PieceType.Ghost);
        s.Put("d4", Side.Black, PieceType.Pawn);
        s.Put("c3", Side.White, PieceType.Pawn);
        True(Rules.TryMakeMove(s, "c3", "d4"), "пешка бьёт пешку");
        Eq(PieceType.Ghost, s["d4"].Type, "стала призраком");
        Eq(GameResult.Ongoing, s.Result, "ещё есть пешка");

        // Одни призраки — никто никого не может срубить.
        var ghosts = Empty();
        ghosts.Put("a1", Side.White, PieceType.Ghost);
        ghosts.Put("h8", Side.Black, PieceType.Ghost);
        True(Rules.TryMakeMove(ghosts, "a1", "a2"), "ход призрака");
        Eq(GameResult.Draw, ghosts.Result, "ничья");
        Eq(EndReason.OnlyGhosts, ghosts.EndReason, "причина");
    }

    private static void RejectsIllegal()
    {
        var s = GameState.CreateStandard();
        True(!Rules.TryMakeMove(s, new Move(Sq.Parse("e2"), Sq.Parse("e5"), MoveKind.Quiet)), "e2-e5");
        True(!Rules.TryMakeMove(s, new Move(Sq.Parse("e7"), Sq.Parse("e5"), MoveKind.Quiet)), "чужой ход");
        True(!Rules.TryMakeMove(s, new Move(Sq.Parse("a1"), Sq.Parse("a7"), MoveKind.Capture)), "удар вместо пробивания");
        Eq(0, s.Ply, "ничего не сыграно");
    }

    private static void Effects()
    {
        var s = Empty();
        s.Put("a1", Side.White, PieceType.Rook);
        s.Put("a5", Side.Black, PieceType.Bishop);
        s.Put("b7", Side.Black, PieceType.Ghost);
        s.Put("b1", Side.White, PieceType.Knight);
        Kings(s);
        var fx = new List<MoveEffect>();
        Rules.TryMakeMove(s, "a1", "a5", fx);
        Eq(1, fx.Count, "один эффект");
        Eq(EffectKind.Degraded, fx[0].Kind, "Degraded");
        Eq(PieceType.Knight, fx[0].NewType, "слон → конь");
    }

    private static void AttackConsistency()
    {
        var rnd = new Random(1);
        var moves = new List<Move>();
        foreach (var rules in new[] { RuleSet.Default, RuleSet.Chaos, RuleSet.ClassicMate, new RuleSet { WinCondition = WinCondition.Annihilation, AbilitiesForAll = true, PierceThroughOwnPieces = false } })
        {
            for (int game = 0; game < 100; game++)
            {
                var s = GameState.CreateStandard(rules);
                while (!s.IsOver && s.Ply < 200)
                {
                    foreach (Side by in new[] { Side.White, Side.Black })
                    {
                        moves.Clear();
                        Rules.GeneratePseudoMoves(s, by, moves);
                        for (int sq = 0; sq < 64; sq++)
                        {
                            var p = s.Cells[sq];
                            if (p.IsEmpty || p.Side == by) continue;
                            bool gen = moves.Any(m => m.IsCapture && m.To == sq);
                            bool att = Rules.IsSquareAttacked(s, sq, by);
                            if (gen != att)
                            {
                                Fail($"рассинхрон на {Sq.Name(sq)} (by {by}): gen={gen} att={att}\n{s.ToAscii()}");
                                return;
                            }
                        }
                    }
                    var legal = Rules.GetLegalMoves(s);
                    if (legal.Count == 0) break;
                    Rules.MakeMove(s, legal[rnd.Next(legal.Count)]);
                }
            }
        }
    }

    private static void RandomGames()
    {
        var rnd = new Random(42);
        var results = new Dictionary<string, int>();
        foreach (var rules in new[] { RuleSet.Default, RuleSet.Chaos, RuleSet.ClassicMate })
        {
            for (int game = 0; game < 500; game++)
            {
                var s = GameState.CreateStandard(rules);
                int guard = 0;
                while (!s.IsOver)
                {
                    var legal = Rules.GetLegalMoves(s);
                    True(legal.Count > 0, "ходы есть, пока игра не закончилась");
                    Rules.MakeMove(s, legal[rnd.Next(legal.Count)]);
                    if (++guard > 5000) { Fail("партия не закончилась за 5000 полуходов"); return; }

                    if (rules.WinCondition != WinCondition.Annihilation)
                    {
                        // В режимах с шахом короля никогда не бьют: он либо стоит, либо ушёл после мата.
                        for (int sq = 0; sq < 64; sq++)
                            if (s.Cells[sq].Type == PieceType.Queen && s.Cells[sq].OriginalType == PieceType.King)
                            { Fail("короля ослабили в режиме с шахом\n" + s.ToAscii()); return; }
                    }
                    if (rules.WinCondition == WinCondition.Checkmate)
                    {
                        // Короля в этом режиме никогда не бьют.
                        if (s.FindKing(Side.White) < 0 || s.FindKing(Side.Black) < 0)
                        { Fail("пропал король в режиме мата\n" + s.ToAscii()); return; }
                    }
                }
                string key = $"{rules.WinCondition}/{s.Result}/{s.EndReason}";
                results[key] = results.TryGetValue(key, out int n) ? n + 1 : 1;
            }
        }
        foreach (var kv in results.OrderBy(k => k.Key))
            Console.WriteLine($"      {kv.Key}: {kv.Value}");
    }

    private static void UndoRestores()
    {
        var rnd = new Random(7);
        var moves = new List<Move>();
        foreach (var rules in new[] { RuleSet.Default, RuleSet.Chaos, RuleSet.ClassicMate })
        {
            for (int game = 0; game < 50; game++)
            {
                var s = GameState.CreateStandard(rules);
                while (!s.IsOver && s.Ply < 150)
                {
                    moves.Clear();
                    Rules.GeneratePseudoMoves(s, s.SideToMove, moves);
                    string before = Snapshot(s);
                    foreach (var m in moves)
                    {
                        var u = Rules.MakeMoveForSearch(s, m);
                        Rules.UndoMove(s, u);
                        if (Snapshot(s) != before) { Fail($"после {m} позиция изменилась"); return; }
                    }
                    var legal = Rules.GetLegalMoves(s);
                    if (Snapshot(s) != before) { Fail("GetLegalMoves изменил позицию"); return; }
                    if (legal.Count == 0) break;
                    Rules.MakeMove(s, legal[rnd.Next(legal.Count)]);
                }
            }
        }
    }

    private static void TrojanRide()
    {
        var s = Empty();
        s.Put("d4", Side.White, PieceType.Knight);
        s.Put("d3", Side.White, PieceType.Rook);
        Kings(s);
        True(Rules.TryMakeMove(s, new Move(Sq.Parse("d3"), Sq.Parse("d4"), MoveKind.Embark)), "ладья садится");
        True(s["d3"].IsEmpty, "d3 пустая");
        Eq(PieceType.Rook, s.Cargo[Sq.Parse("d4")].Type, "ладья в коне");
        Eq(3, s.CountPieces(Side.White), "пассажир считается фигурой (конь + ладья + король)");
        True(Rules.TryMakeMove(s, "h8", "g8"), "чёрные");
        True(Rules.TryMakeMove(s, new Move(Sq.Parse("d4"), Sq.Parse("f5"), MoveKind.Quiet)), "конь прыгает");
        Eq(PieceType.Rook, s.Cargo[Sq.Parse("f5")].Type, "пассажир переехал");
        True(s.Cargo[Sq.Parse("d4")].IsEmpty, "старая клетка пустая");
        True(Rules.TryMakeMove(s, "g8", "h8"), "чёрные");
        var fx = new List<MoveEffect>();
        True(Rules.TryMakeMove(s, new Move(Sq.Parse("f5"), Sq.Parse("g6"), MoveKind.Disembark), fx), "высадка");
        Eq(PieceType.Rook, s["g6"].Type, "ладья на g6");
        True(s.Cargo[Sq.Parse("f5")].IsEmpty, "конь пустой");
        Eq(EffectKind.Disembarked, fx[0].Kind, "эффект высадки");
    }

    private static void TrojanBoardingLimits()
    {
        var s = Empty();
        s.Put("d4", Side.White, PieceType.Knight);
        s.Put("e4", Side.White, PieceType.Knight);
        s.Put("d5", Side.White, PieceType.Pawn);
        s.Put("c5", Side.White, PieceType.Ghost);
        s.Put("d3", Side.White, PieceType.King);
        s.Put("h8", Side.Black, PieceType.King);
        var moves = Rules.GetLegalMoves(s);
        True(!moves.Any(m => m.Kind == MoveKind.Embark && m.From == Sq.Parse("d3")), "король не садится");
        True(!moves.Any(m => m.Kind == MoveKind.Embark && m.From == Sq.Parse("e4")), "конь в коня не садится");
        True(moves.Any(m => m.ToString() == "c5@d4"), "призрак может сесть");
        True(Rules.TryMakeMove(s, new Move(Sq.Parse("d5"), Sq.Parse("d4"), MoveKind.Embark)), "пешка села");
        True(Rules.TryMakeMove(s, "h8", "g8"), "чёрные");
        True(!Rules.GetLegalMoves(s).Any(m => m.Kind == MoveKind.Embark && m.To == Sq.Parse("d4")), "второй не влезет");
    }

    private static void TrojanEject()
    {
        var s = Empty();
        s.Put("d4", Side.White, PieceType.Knight);
        s.PutCargo("d4", Side.White, PieceType.Queen);
        s.Put("d8", Side.Black, PieceType.Rook);
        Kings(s);
        s.SideToMove = Side.Black;
        var fx = new List<MoveEffect>();
        True(Rules.TryMakeMove(s, "d8", "d4", fx), "удар по коню");
        Eq(PieceType.Pawn, s["d4"].Type, "конь → пешка");
        True(s.Cargo[Sq.Parse("d4")].IsEmpty, "трюм пуст");
        True(fx.Any(e => e.Kind == EffectKind.Ejected), "эффект выпадения");
        int landed = fx.First(e => e.Kind == EffectKind.Ejected).To;
        Eq(PieceType.Queen, s.Cells[landed].Type, "ферзь выпал рядом");
        Eq(Side.White, s.Cells[landed].Side, "свой");
    }

    private static void TrojanEjectNoRoom()
    {
        var s = Empty();
        s.Put("a1", Side.White, PieceType.Knight);
        s.PutCargo("a1", Side.White, PieceType.Rook);
        s.Put("a2", Side.White, PieceType.Pawn);
        s.Put("b2", Side.White, PieceType.Pawn);
        s.Put("b1", Side.White, PieceType.Bishop);
        s.Put("c2", Side.Black, PieceType.Knight); // бьёт a1 прыжком
        s.Put("h1", Side.White, PieceType.King);
        s.Put("h8", Side.Black, PieceType.King);
        s.SideToMove = Side.Black;
        int before = s.CountPieces(Side.White);
        True(Rules.TryMakeMove(s, "c2", "a1"), "удар");
        Eq(PieceType.Pawn, s["a1"].Type, "конь → пешка");
        Eq(before - 1, s.CountPieces(Side.White), "пассажир пропал");
    }

    private static void TrojanPawnRevive()
    {
        var s = Empty();
        s.Put("d6", Side.White, PieceType.Knight);
        s.PutCargo("d6", Side.White, PieceType.Pawn, original: PieceType.Bishop);
        Kings(s);
        True(Rules.TryMakeMove(s, new Move(Sq.Parse("d6"), Sq.Parse("d7"), MoveKind.Disembark)), "высадка на d7");
        Eq(PieceType.Pawn, s["d7"].Type, "d7 ещё не конец");
        True(Rules.TryMakeMove(s, "h8", "g8"), "чёрные");
        True(Rules.TryMakeMove(s, "d7", "d8"), "пешка идёт");
        Eq(PieceType.Bishop, s["d8"].Type, "возродилась слоном");

        var t = Empty();
        t.Put("d7", Side.White, PieceType.Knight);
        t.PutCargo("d7", Side.White, PieceType.Pawn, original: PieceType.Rook);
        Kings(t);
        True(Rules.TryMakeMove(t, new Move(Sq.Parse("d7"), Sq.Parse("d8"), MoveKind.Disembark)), "высадка сразу на d8");
        Eq(PieceType.Rook, t["d8"].Type, "возродилась ладьёй");
    }

    private static void TrojanDouble()
    {
        var s = Empty();
        s.Put("b1", Side.White, PieceType.Knight);
        Kings(s);
        var m = Rules.GetLegalMoves(s).First(x => x.ToString() == "b1:c3:e4");
        True(Rules.TryMakeMove(s, m), "двойной прыжок");
        Eq(PieceType.Knight, s["e4"].Type, "конь на e4");
        True(s["e4"].DoubleUsed, "заряд потрачен");
        True(Rules.TryMakeMove(s, "h8", "g8"), "чёрные");
        True(!Rules.GetLegalMoves(s).Any(x => x.IsDouble), "второго двойного нет");

        // Через занятые клетки прыгать нельзя.
        var t = Empty();
        t.Put("b1", Side.White, PieceType.Knight);
        t.Put("a3", Side.White, PieceType.Pawn);
        t.Put("c3", Side.White, PieceType.Pawn);
        t.Put("d2", Side.White, PieceType.Pawn);
        Kings(t);
        True(!Rules.GetLegalMoves(t).Any(x => x.IsDouble), "все промежуточные заняты");
    }

    private static void TrojanDoubleCapture()
    {
        var s = Empty();
        s.Put("b1", Side.White, PieceType.Knight);
        s.Put("e4", Side.Black, PieceType.Queen);
        Kings(s);
        var m = Rules.GetLegalMoves(s).First(x => x.Kind == MoveKind.DoubleCapture && x.To == Sq.Parse("e4"));
        True(Rules.TryMakeMove(s, m), "двойной удар");
        Eq(PieceType.Rook, s["e4"].Type, "ферзь → ладья");
        Eq(PieceType.Knight, s.Cells[m.Via].Type, "конь на промежуточной клетке " + Sq.Name(m.Via));
        True(s["b1"].IsEmpty, "b1 пустая");
    }

    private static void TrojanExplode()
    {
        var s = Empty();
        s.Put("d4", Side.White, PieceType.Knight);
        s.PutCargo("d4", Side.White, PieceType.Rook);
        s.Put("d5", Side.Black, PieceType.Queen);
        s.Put("e5", Side.Black, PieceType.Ghost);
        s.Put("c3", Side.White, PieceType.Bishop);
        s.Put("e3", Side.Black, PieceType.Knight);
        s.PutCargo("e3", Side.Black, PieceType.Pawn);
        s.Put("d6", Side.Black, PieceType.Rook); // не сосед — не задета
        s.Put("a1", Side.White, PieceType.King);
        s.Put("h8", Side.Black, PieceType.King);
        var fx = new List<MoveEffect>();
        True(Rules.TryMakeMove(s, new Move(Sq.Parse("d4"), Sq.Parse("d4"), MoveKind.Explode), fx), "взрыв");
        True(s["d4"].IsEmpty && s.Cargo[Sq.Parse("d4")].IsEmpty, "конь и пассажир исчезли");
        Eq(PieceType.Rook, s["d5"].Type, "ферзь → ладья");
        True(s["e5"].IsEmpty, "призрак ушёл");
        Eq(PieceType.Knight, s["c3"].Type, "свой слон тоже ослаб");
        Eq(PieceType.Pawn, s["e3"].Type, "вражеский конь → пешка");
        True(fx.Any(e => e.Kind == EffectKind.Ejected && e.Side == Side.Black), "из него выпала пешка");
        Eq(PieceType.Rook, s["d6"].Type, "d6 не задета");
        True(fx.Any(e => e.Kind == EffectKind.Exploded), "эффект взрыва");
    }

    private static void TrojanMateMode()
    {
        var s = Empty(RuleSet.ClassicMate);
        s.Put("d4", Side.White, PieceType.Knight);
        s.Put("e5", Side.Black, PieceType.King);
        s.Put("d5", Side.Black, PieceType.Pawn);
        s.Put("a1", Side.White, PieceType.King);
        True(Rules.TryMakeMove(s, new Move(Sq.Parse("d4"), Sq.Parse("d4"), MoveKind.Explode)), "взрыв");
        Eq(PieceType.King, s["e5"].Type, "король не задет");
        Eq(PieceType.Ghost, s["d5"].Type, "пешка задета");

        // Двойной прыжок даёт шах: королю нельзя встать туда, куда конь достаёт за два прыжка.
        var t = Empty(RuleSet.ClassicMate);
        t.Put("b1", Side.Black, PieceType.Knight);
        t.Put("h8", Side.Black, PieceType.King);
        t.Put("e5", Side.White, PieceType.King);
        True(Rules.IsSquareAttacked(t, Sq.Parse("e4"), Side.Black), "e4 под двойным прыжком");
        True(!Rules.GetLegalMoves(t).Any(m => m.ToString() == "e5-e4"), "король туда не идёт");
    }

    /// <summary>Позиция, где чёрные ставят мат ходом a8-a1 (белые: король h1, пешки g2/h2, конь h4, слон b7).</summary>
    private static GameState KingFallPosition(int seed)
    {
        var s = new GameState(RuleSet.Default) { Seed = seed };
        s.Put("h1", Side.White, PieceType.King);
        s.Put("g2", Side.White, PieceType.Pawn);
        s.Put("h2", Side.White, PieceType.Pawn);
        s.Put("h4", Side.White, PieceType.Knight);
        s.Put("b7", Side.White, PieceType.Bishop);
        s.Put("a8", Side.Black, PieceType.Rook);
        s.Put("e8", Side.Black, PieceType.King);
        s.SideToMove = Side.Black;
        return s;
    }

    private static void KingFallFirstMate()
    {
        var s = KingFallPosition(1);
        var fx = new List<MoveEffect>();
        True(Rules.TryMakeMove(s, "a8", "a1", fx), "мат ладьёй");
        Eq(GameResult.Ongoing, s.Result, "партия продолжается");
        Eq(Sq.None, s.FindKing(Side.White), "белого короля нет");
        Eq(Side.White, s.SideToMove, "ходят белые");
        True(fx.Any(e => e.Kind == EffectKind.KingFell), "эффект падения короля");
        int lit = fx.Count(e => e.Kind == EffectKind.Empowered);
        True(lit >= 1 && lit <= 2, "загорелось 1–2 из двух подходящих фигур, а не " + lit);
        True(s["h4"].Empowered || s["b7"].Empowered, "загорелся конь или слон");
        True(!s["g2"].Empowered && !s["h2"].Empowered, "пешки не загораются");
        // Армия без короля ходит без оглядки на шах.
        True(Rules.GetLegalMoves(s).Count > 0, "белым есть чем ходить");
    }

    private static void KingFallDeterministic()
    {
        string Lit(int seed)
        {
            var s = KingFallPosition(seed);
            Rules.TryMakeMove(s, "a8", "a1");
            return (s["h4"].Empowered ? "N" : "") + (s["b7"].Empowered ? "B" : "");
        }
        for (int seed = 0; seed < 20; seed++)
            Eq(Lit(seed), Lit(seed), "одно зерно — один результат, seed=" + seed);
        var variants = new HashSet<string>();
        for (int seed = 0; seed < 50; seed++)
            variants.Add(Lit(seed));
        True(variants.Count >= 2, "при разных зёрнах выбор разный");
    }

    private static void KingFallSecondMate()
    {
        var s = new GameState(RuleSet.Default);
        s.Put("a1", Side.White, PieceType.Rook);
        s.Put("c3", Side.White, PieceType.Pawn);
        s.Put("h8", Side.Black, PieceType.King);
        s.Put("g7", Side.Black, PieceType.Pawn);
        s.Put("h7", Side.Black, PieceType.Pawn);
        True(Rules.TryMakeMove(s, "a1", "a8"), "мат второму королю");
        Eq(GameResult.WhiteWins, s.Result, "армия без короля победила");
        Eq(EndReason.Checkmate, s.EndReason, "причина");
    }

    private static void KingFallAnnihilation()
    {
        var s = new GameState(RuleSet.Default);
        s.Put("a1", Side.White, PieceType.Ghost);
        s.Put("a3", Side.Black, PieceType.Rook);
        s.Put("h8", Side.Black, PieceType.King);
        s.SideToMove = Side.Black;
        True(Rules.TryMakeMove(s, "a3", "a1"), "срубили последнего");
        Eq(GameResult.BlackWins, s.Result, "чёрные победили");
        Eq(EndReason.Annihilation, s.EndReason, "причина");
    }

    private static void KingFallAbilitiesOnlyEmpowered()
    {
        var s = new GameState(RuleSet.Default);
        s.Put("d4", Side.White, PieceType.Queen);
        s.Put("a1", Side.White, PieceType.Knight);
        s.Put("h1", Side.White, PieceType.Rook);
        s.Put("h2", Side.White, PieceType.Pawn);
        s.Put("h7", Side.Black, PieceType.Pawn);
        s.Put("a8", Side.Black, PieceType.King);
        int plain = Rules.GetLegalMovesFrom(s, Sq.Parse("d4")).Count;
        True(!Rules.GetLegalMoves(s).Any(m => m.Kind == MoveKind.Pierce || m.IsDouble || m.Kind == MoveKind.Explode),
            "без огня способностей нет");

        foreach (var sq in new[] { "d4", "a1", "h1" })
        {
            var p = s.Cells[Sq.Parse(sq)];
            p.Empowered = true;
            s.Cells[Sq.Parse(sq)] = p;
        }
        Eq(plain + 8, Rules.GetLegalMovesFrom(s, Sq.Parse("d4")).Count, "загоревшийся ферзь ходит и как конь");
        True(Rules.GetLegalMoves(s).Any(m => m.ToString() == "h1*h7"), "загоревшаяся ладья пробивает");
        True(Rules.GetLegalMoves(s).Any(m => m.ToString() == "a1!"), "загоревшийся конь может взорваться");
    }

    private static void ClockBasics()
    {
        var c = new ChessClock(10);
        True(c.Enabled, "включены");
        True(!c.Tick(Side.White, 30), "до первого хода не тикают");
        Eq(600.0, c.WhiteSeconds, "белые 10:00");
        c.OnMoveMade();
        True(!c.Tick(Side.Black, 12.5), "тикают чёрные");
        Eq(587.5, c.BlackSeconds, "чёрные потратили 12.5 с");
        Eq(600.0, c.WhiteSeconds, "белые не тратили");
        True(!c.Tick(Side.White, 100), "тикают белые");
        Eq(500.0, c.WhiteSeconds, "белые потратили 100 с");
    }

    private static void ClockFlag()
    {
        var c = new ChessClock(10);
        c.OnMoveMade();
        True(!c.Tick(Side.Black, 599.9), "ещё есть время");
        True(c.Tick(Side.Black, 0.2), "флаг упал");
        Eq(0.0, c.BlackSeconds, "не уходит в минус");
        True(!c.Running, "часы остановились");
        True(!c.Tick(Side.Black, 1), "после флага не тикают");

        var off = new ChessClock(0);
        off.OnMoveMade();
        True(!off.Enabled && !off.Tick(Side.White, 1e6), "без контроля времени");
    }

    private static void ClockFormat()
    {
        Eq("10:00", ChessClock.Format(600), "10 минут");
        Eq("1:05", ChessClock.Format(64.2), "округление вверх");
        Eq("0:10", ChessClock.Format(10), "ровно 10 секунд");
        Eq("0:09.9", ChessClock.Format(9.95), "десятые");
        Eq("0:00.0", ChessClock.Format(-1), "ноль");
    }

    // ---------- сеть ----------

    /// <summary>Крутит Poll у обоих, пока не выполнится условие (или 5 секунд).</summary>
    private static bool Pump(Chess2.Core.Net.NetMatch a, List<Chess2.Core.Net.NetEvent> aEvents,
                             Chess2.Core.Net.NetMatch b, List<Chess2.Core.Net.NetEvent> bEvents, Func<bool> done)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 5000)
        {
            aEvents.AddRange(a.Poll());
            bEvents.AddRange(b.Poll());
            if (done())
                return true;
            System.Threading.Thread.Sleep(5);
        }
        return false;
    }

    private static (Chess2.Core.Net.NetMatch host, Chess2.Core.Net.NetMatch guest,
        List<Chess2.Core.Net.NetEvent> he, List<Chess2.Core.Net.NetEvent> ge) NetPair(int hostColor = 0)
    {
        var template = new Chess2.Core.Net.NetSetup { HostColorChoice = hostColor, Mode = 0, TimeMinutes = 10 };
        var host = Chess2.Core.Net.NetMatch.Host(0, template);
        var guest = Chess2.Core.Net.NetMatch.Join("127.0.0.1", ((Chess2.Core.Net.TcpTransport)host.Transport).ListenPort);
        var he = new List<Chess2.Core.Net.NetEvent>();
        var ge = new List<Chess2.Core.Net.NetEvent>();
        bool ok = Pump(host, he, guest, ge, () =>
            he.Any(e => e.Kind == Chess2.Core.Net.NetEventKind.GameStarted) &&
            ge.Any(e => e.Kind == Chess2.Core.Net.NetEventKind.GameStarted));
        True(ok, "обе стороны получили начало партии");
        return (host, guest, he, ge);
    }

    private static void NetFullGame()
    {
        var (host, guest, he, ge) = NetPair(hostColor: 1);
        try
        {
            Eq(host.Setup.Seed, guest.Setup.Seed, "одинаковое зерно");
            Eq(Side.Black, host.LocalSide, "хост за чёрных, как выбрал");
            Eq(Side.White, guest.LocalSide, "гость за белых");
            Eq(10, guest.Setup.TimeMinutes, "время пришло гостю");

            var hs = GameState.CreateStandard(host.Setup.BuildRules(), host.Setup.Seed);
            var gs = GameState.CreateStandard(guest.Setup.BuildRules(), guest.Setup.Seed);
            var rnd = new Random(11);
            he.Clear();
            ge.Clear();

            for (int ply = 0; ply < 80 && !hs.IsOver; ply++)
            {
                bool hostMoves = hs.SideToMove == host.LocalSide;
                var (me, meState, other, otherState, otherEvents, meEvents) = hostMoves
                    ? (host, hs, guest, gs, ge, he)
                    : (guest, gs, host, hs, he, ge);

                var legal = Rules.GetLegalMoves(meState);
                var move = legal[rnd.Next(legal.Count)];
                True(Rules.TryMakeMove(meState, move), "свой ход");
                me.SendMove(move, 500 - ply);

                int before = otherEvents.Count;
                True(Pump(me, meEvents, other, otherEvents, () => otherEvents.Count > before), "ход дошёл, ply " + ply);
                var ev = otherEvents[otherEvents.Count - 1];
                Eq(Chess2.Core.Net.NetEventKind.RemoteMove, ev.Kind, "это ход");
                Eq(500.0 - ply, ev.ClockSeconds, "время соперника");
                True(Rules.TryMakeMove(otherState, ev.Move), $"ход {move} принят у соперника");
            }
            Eq(Snapshot(hs), Snapshot(gs), "позиции у обоих совпадают");
        }
        finally
        {
            host.Close();
            guest.Close();
        }
    }

    private static void NetResignRematchDisconnect()
    {
        var (host, guest, he, ge) = NetPair(hostColor: 0);
        try
        {
            Side hostSide = host.LocalSide;
            he.Clear();
            ge.Clear();

            host.SendResign();
            True(Pump(host, he, guest, ge, () => ge.Any(e => e.Kind == Chess2.Core.Net.NetEventKind.RemoteResigned)), "гость узнал о сдаче");

            guest.RequestRematch();
            True(Pump(host, he, guest, ge, () => he.Any(e => e.Kind == Chess2.Core.Net.NetEventKind.RematchRequested)), "хост видит просьбу о реванше");
            True(host.RemoteWantsRematch, "флаг реванша");

            host.RequestRematch();
            True(Pump(host, he, guest, ge, () =>
                he.Any(e => e.Kind == Chess2.Core.Net.NetEventKind.GameStarted) &&
                ge.Any(e => e.Kind == Chess2.Core.Net.NetEventKind.GameStarted)), "реванш начался у обоих");
            Eq(hostSide.Opponent(), host.LocalSide, "хост сменил цвет");
            Eq(hostSide, guest.LocalSide, "гость сменил цвет");
            Eq(host.Setup.Seed, guest.Setup.Seed, "новое общее зерно");

            guest.Close();
            True(Pump(host, he, guest, ge, () => he.Any(e => e.Kind == Chess2.Core.Net.NetEventKind.Disconnected)), "хост узнал об отключении");
        }
        finally
        {
            host.Close();
            guest.Close();
        }
    }

    private static void NetChat()
    {
        var (host, guest, he, ge) = NetPair();
        try
        {
            he.Clear();
            ge.Clear();
            string sent = host.SendChat("  Привет!\nХороший ход 👍  ");
            Eq("Привет! Хороший ход 👍", sent, "перенос заменён пробелом, края обрезаны");
            guest.SendEmote(7);
            True(Pump(host, he, guest, ge, () =>
                ge.Any(e => e.Kind == Chess2.Core.Net.NetEventKind.Chat) &&
                he.Any(e => e.Kind == Chess2.Core.Net.NetEventKind.Emote)), "сообщения дошли");
            Eq(sent, ge.First(e => e.Kind == Chess2.Core.Net.NetEventKind.Chat).Text, "текст как отправлен");
            Eq(7, he.First(e => e.Kind == Chess2.Core.Net.NetEventKind.Emote).Value, "номер эмодзи");

            Eq(Chess2.Core.Net.NetMatch.MaxChatLength, Chess2.Core.Net.NetMatch.CleanChat(new string('а', 500)).Length, "длинное обрезано");
            Eq("", host.SendChat("   \n  "), "пустое не отправляется");
        }
        finally
        {
            host.Close();
            guest.Close();
        }
    }

    private static void NetHeartbeat()
    {
        var (host, guest, he, ge) = NetPair();
        try
        {
            host.PingInterval = guest.PingInterval = 0.05;
            True(Pump(host, he, guest, ge, () => host.PingMs.HasValue && guest.PingMs.HasValue), "пинг измерен у обоих");
            True(host.PingMs.Value < 1000, "на localhost пинг маленький: " + host.PingMs + " мс");
            True(!host.Shaky && !guest.Shaky, "связь стабильна");

            // Гость «завис»: перестал отвечать (Poll не вызывается). Хост должен это заметить.
            host.ShakyAfter = 0.3;
            host.LostAfter = 0.8;
            he.Clear();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool sawShaky = false;
            while (sw.ElapsedMilliseconds < 5000 && !he.Any(e => e.Kind == Chess2.Core.Net.NetEventKind.Disconnected))
            {
                he.AddRange(host.Poll());
                sawShaky |= host.Shaky;
                System.Threading.Thread.Sleep(20);
            }
            True(sawShaky, "сначала «связь нестабильна»");
            var lost = he.FirstOrDefault(e => e.Kind == Chess2.Core.Net.NetEventKind.Disconnected);
            Eq("Связь с соперником потеряна", lost.Text, "потом — потеря связи");
            True(sw.ElapsedMilliseconds < 3000, "заметил быстро: " + sw.ElapsedMilliseconds + " мс");
        }
        finally
        {
            host.Close();
            guest.Close();
        }
    }

    /// <summary>Соединение «в памяти»: две очереди. Так же устроен Steam-транспорт — сообщения приходят снаружи.</summary>
    private sealed class MemoryTransport : Chess2.Core.Net.INetTransport
    {
        public MemoryTransport Other;
        public readonly Queue<string> Inbox = new Queue<string>();
        public Chess2.Core.Net.TransportStatus Status { get; set; } = Chess2.Core.Net.TransportStatus.Connected;
        public string Error => null;
        public bool Send(string line)
        {
            if (Status != Chess2.Core.Net.TransportStatus.Connected) return false;
            Other.Inbox.Enqueue(line);
            return true;
        }
        public bool TryReceive(out string line)
        {
            if (Inbox.Count > 0) { line = Inbox.Dequeue(); return true; }
            line = null;
            return false;
        }
        public void Close()
        {
            Status = Chess2.Core.Net.TransportStatus.Closed;
            Other.Status = Chess2.Core.Net.TransportStatus.Closed;
        }
        public void Dispose() => Close();
    }

    private static void NetCustomTransport()
    {
        var a = new MemoryTransport();
        var b = new MemoryTransport { Other = a };
        a.Other = b;
        var host = Chess2.Core.Net.NetMatch.Create(Chess2.Core.Net.NetRole.Host, a,
            new Chess2.Core.Net.NetSetup { HostColorChoice = 0, TimeMinutes = 15 });
        var guest = Chess2.Core.Net.NetMatch.Create(Chess2.Core.Net.NetRole.Guest, b);
        var he = new List<Chess2.Core.Net.NetEvent>();
        var ge = new List<Chess2.Core.Net.NetEvent>();
        True(Pump(host, he, guest, ge, () =>
            he.Any(e => e.Kind == Chess2.Core.Net.NetEventKind.GameStarted) &&
            ge.Any(e => e.Kind == Chess2.Core.Net.NetEventKind.GameStarted)), "партия началась");
        Eq(15, guest.Setup.TimeMinutes, "настройки дошли");

        var hs = GameState.CreateStandard(host.Setup.BuildRules(), host.Setup.Seed);
        var gs = GameState.CreateStandard(guest.Setup.BuildRules(), guest.Setup.Seed);
        True(Rules.TryMakeMove(hs, "e2", "e4"), "ход хоста");
        host.SendMove(hs.LastMove.Value, 899);
        ge.Clear();
        True(Pump(host, he, guest, ge, () => ge.Any(e => e.Kind == Chess2.Core.Net.NetEventKind.RemoteMove)), "ход дошёл");
        True(Rules.TryMakeMove(gs, ge.First(e => e.Kind == Chess2.Core.Net.NetEventKind.RemoteMove).Move), "ход принят");
        Eq(Snapshot(hs), Snapshot(gs), "позиции совпадают");

        b.Close();
        True(Pump(host, he, guest, ge, () => he.Any(e => e.Kind == Chess2.Core.Net.NetEventKind.Disconnected)), "закрытие замечено");
    }

    private static void NetConnectFails()
    {
        // Берём свободный порт и сразу закрываем — подключаться некуда.
        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        int port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        var guest = Chess2.Core.Net.NetMatch.Join("127.0.0.1", port);
        var events = new List<Chess2.Core.Net.NetEvent>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 5000 && events.Count == 0)
        {
            events.AddRange(guest.Poll());
            System.Threading.Thread.Sleep(10);
        }
        True(events.Any(e => e.Kind == Chess2.Core.Net.NetEventKind.Disconnected), "событие отключения");
        True(!string.IsNullOrEmpty(guest.Transport.Error), "есть текст ошибки: " + guest.Transport.Error);
        guest.Close();
    }

    private static string Snapshot(GameState s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var p in s.Cells)
            sb.Append($"{(int)p.Type}{(int)p.Side}{(int)p.OriginalType}{(p.HasMoved ? 1 : 0)}{(p.PierceUsed ? 1 : 0)}{(p.DoubleUsed ? 1 : 0)}#{p.Id};");
        foreach (var p in s.Cargo)
            sb.Append($"{(int)p.Type}{(int)p.Side}#{p.Id};");
        sb.Append($"{s.SideToMove}|{s.Ply}|{s.NoProgressPlies}|{s.LastMove}|{s.Result}|{s.EndReason}");
        return sb.ToString();
    }

    private static void BotMateInOne()
    {
        foreach (var level in new[] { BotDifficulty.Medium, BotDifficulty.Hard, BotDifficulty.Expert })
        {
            var s = Empty(RuleSet.ClassicMate);
            s.Put("h1", Side.White, PieceType.King);
            s.Put("g2", Side.White, PieceType.Pawn);
            s.Put("h2", Side.White, PieceType.Pawn);
            s.Put("a8", Side.Black, PieceType.Rook);
            s.Put("e8", Side.Black, PieceType.King);
            s.Put("e7", Side.Black, PieceType.Pawn);
            s.SideToMove = Side.Black;
            var m = new Bot(level, seed: 1).FindBestMove(s);
            Eq("a8-a1", m?.ToString(), "мат ладьёй, " + level);
        }
    }

    private static void BotFinishes()
    {
        var s = Empty();
        s.Put("a1", Side.White, PieceType.Rook);
        s.Put("c3", Side.White, PieceType.King);
        s.Put("a8", Side.Black, PieceType.Ghost);
        var m = new Bot(BotDifficulty.Hard, seed: 1).FindBestMove(s);
        Eq("a1xa8", m?.ToString(), "добить призрака");
    }

    private static void BotHitsQueen()
    {
        var s = Empty();
        s.Put("b1", Side.White, PieceType.King);
        s.Put("h1", Side.White, PieceType.Rook);
        s.Put("h7", Side.Black, PieceType.Queen);
        s.Put("a8", Side.Black, PieceType.King);
        s.Put("a7", Side.Black, PieceType.Pawn);
        s.Put("b7", Side.Black, PieceType.Pawn);
        var m = new Bot(BotDifficulty.Hard, seed: 1).FindBestMove(s);
        Eq("h1xh7", m?.ToString(), "ослабить ферзя");
    }

    private static void BotLegalInMateMode()
    {
        var rnd = new Random(3);
        var bot = new Bot(new BotSettings { MaxDepth = 2, TimeLimitMs = 200, QuiescenceDepth = 2 }, seed: 5);
        for (int game = 0; game < 10; game++)
        {
            var s = GameState.CreateStandard(RuleSet.ClassicMate);
            while (!s.IsOver && s.Ply < 120)
            {
                Move? m = s.SideToMove == Side.White ? bot.FindBestMove(s) : RandomMove(s, rnd);
                if (m == null) break;
                if (!Rules.TryMakeMove(s, m.Value)) { Fail($"бот выбрал запрещённый ход {m}\n{s.ToAscii()}"); return; }
            }
        }
    }

    private static Move? RandomMove(GameState s, Random rnd)
    {
        var legal = Rules.GetLegalMoves(s);
        return legal.Count == 0 ? (Move?)null : legal[rnd.Next(legal.Count)];
    }

    private static void BotSpeed()
    {
        foreach (var level in new[] { BotDifficulty.Easy, BotDifficulty.Medium, BotDifficulty.Hard, BotDifficulty.Expert })
        {
            var s = GameState.CreateStandard();
            Rules.TryMakeMove(s, "e2", "e4");
            Rules.TryMakeMove(s, "e7", "e5");
            var bot = new Bot(level, seed: 2);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var m = bot.FindBestMove(s);
            sw.Stop();
            Console.WriteLine($"      {level,-7} ход {m}  глубина {bot.LastDepth}  узлов {bot.LastNodes,9:N0}  {sw.ElapsedMilliseconds} мс  оценка {bot.LastScore}");
            True(sw.ElapsedMilliseconds < bot.Settings.TimeLimitMs + 300, "уложился во время");
        }
    }

    /// <summary>Турнир уровней: dotnet run -- arena [партий на пару]</summary>
    private static int Arena(int gamesPerPair)
    {
        var levels = new[] { BotDifficulty.Easy, BotDifficulty.Medium, BotDifficulty.Hard };
        foreach (var rules in new[] { RuleSet.Default, RuleSet.Chaos, RuleSet.ClassicMate })
        {
            Console.WriteLine($"=== Режим {rules.WinCondition}");
            for (int a = 0; a < levels.Length; a++)
            for (int b = a + 1; b < levels.Length; b++)
            {
                int winA = 0, winB = 0, draw = 0;
                for (int g = 0; g < gamesPerPair; g++)
                {
                    // Чтобы не ждать вечность, режем время, но глубину оставляем как у уровня.
                    var sa = BotSettings.For(levels[a]); sa.TimeLimitMs = Math.Min(sa.TimeLimitMs, 150);
                    var sb = BotSettings.For(levels[b]); sb.TimeLimitMs = Math.Min(sb.TimeLimitMs, 150);
                    var botA = new Bot(sa, seed: 100 + g);
                    var botB = new Bot(sb, seed: 200 + g);
                    bool aWhite = g % 2 == 0;
                    var s = GameState.CreateStandard(rules);
                    while (!s.IsOver && s.Ply < 300)
                    {
                        bool aTurn = (s.SideToMove == Side.White) == aWhite;
                        var m = (aTurn ? botA : botB).FindBestMove(s);
                        if (m == null) break;
                        Rules.MakeMove(s, m.Value);
                    }
                    if (s.Result == GameResult.Draw || s.Result == GameResult.Ongoing) draw++;
                    else if ((s.Result == GameResult.WhiteWins) == aWhite) winA++;
                    else winB++;
                }
                Console.WriteLine($"{levels[a]} vs {levels[b]}: {winA} : {winB}, ничьих {draw}");
            }
        }
        return 0;
    }

    // ---------- помощники ----------

    private static GameState Empty(RuleSet rules = null) => new GameState(rules ?? RuleSet.Chaos);

    private static void Kings(GameState s)
    {
        s.Put("h1", Side.White, PieceType.King);
        s.Put("h8", Side.Black, PieceType.King);
    }

    private static bool MoveGhostH(GameState s)
    {
        string from = s["h8"].IsEmpty ? "g8" : "h8";
        string to = from == "h8" ? "g8" : "h8";
        return Rules.TryMakeMove(s, from, to);
    }

    /// <summary>Каждый шаг урока выполним: найдётся цепочка правильных ходов (до 5), ведущая к цели.</summary>
    private static void LessonSolvable(Lesson lesson)
    {
        GameState s = lesson.Setup();
        True(!s.IsOver, "позиция не закончена");
        True(s.SideToMove == Side.White, "ходят белые");
        True(Rules.GetLegalMoves(s).Count > 0, "у белых есть ходы");
        foreach (LessonStep step in lesson.Steps)
        {
            GameState next = SolveStep(s, step, 5);
            if (next == null)
            {
                Fail("не решается шаг: " + step.Task + Environment.NewLine + s.ToAscii());
                return;
            }
            s = next;
        }
    }

    private static GameState SolveStep(GameState s, LessonStep step, int depth)
    {
        if (depth == 0)
            return null;
        foreach (Move m in Rules.GetLegalMoves(s))
        {
            GameState c = s.Clone();
            var effects = new List<MoveEffect>();
            if (!Rules.TryMakeMove(c, m, effects))
                continue;
            LessonVerdict v = step.Check(c, c.LastMove ?? m, effects);
            if (v == LessonVerdict.Wrong)
                continue;
            if (v == LessonVerdict.StepDone)
            {
                // Последний шаг может закончить партию, остальные — нет.
                Tutorial.PassTurn(c);
                return c;
            }
            if (c.IsOver)
                continue;
            Tutorial.PassTurn(c);
            GameState r = SolveStep(c, step, depth - 1);
            if (r != null)
                return r;
        }
        return null;
    }

    private static void Run(string name, Action test)
    {
        int before = _failed;
        try { test(); }
        catch (Exception e) { Fail("исключение: " + e); }
        if (_failed == before) { _passed++; Console.WriteLine("  OK   " + name); }
        else Console.WriteLine("  FAIL " + name);
    }

    private static void True(bool cond, string what)
    {
        if (!cond) Fail(what);
    }

    private static void Eq<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            Fail($"{what}: ждали {expected}, получили {actual}");
    }

    private static void Fail(string msg)
    {
        _failed++;
        Console.WriteLine("       ✗ " + msg);
    }
}
