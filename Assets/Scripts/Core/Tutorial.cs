using System;
using System.Collections.Generic;

namespace Chess2.Core
{
    /// <summary>Что произошло после хода игрока в уроке.</summary>
    public enum LessonVerdict
    {
        /// <summary>Шаг выполнен.</summary>
        StepDone,

        /// <summary>Ход правильный, но до цели ещё не дошли (например, ударили ферзя, а он ещё не пешка).</summary>
        Progress,

        /// <summary>Не то — позиция вернётся к началу шага.</summary>
        Wrong
    }

    /// <summary>Один шаг урока: задание и проверка хода.</summary>
    public sealed class LessonStep
    {
        /// <summary>Задание (показывается над доской и в панели урока).</summary>
        public string Task;

        /// <summary>Пояснение после выполнения шага.</summary>
        public string Done;

        /// <summary>Подсказка, если ход не тот.</summary>
        public string Hint;

        /// <summary>Шаг выполнен: позиция после хода, сам ход и что произошло.</summary>
        public Func<GameState, Move, List<MoveEffect>, bool> IsDone;

        /// <summary>Ход по делу, но шаг ещё не закончен. По умолчанию — ничего не подходит.</summary>
        public Func<GameState, Move, List<MoveEffect>, bool> IsProgress;

        public LessonVerdict Check(GameState s, Move m, List<MoveEffect> effects)
        {
            if (IsDone(s, m, effects))
                return LessonVerdict.StepDone;
            if (IsProgress != null && IsProgress(s, m, effects))
                return LessonVerdict.Progress;
            return LessonVerdict.Wrong;
        }
    }

    /// <summary>Урок: расстановка, вступление и несколько шагов. Чёрные в уроках не ходят.</summary>
    public sealed class Lesson
    {
        public string Id;
        public string Title;
        public string Intro;
        public string Outro;
        public Func<GameState> Setup;
        public LessonStep[] Steps;

        /// <summary>За кого играет игрок (в задачах дня бывает и за чёрных).</summary>
        public Side PlayerSide = Side.White;

        /// <summary>Это задача дня, а не урок обучения.</summary>
        public bool IsDaily;
    }

    /// <summary>
    /// Обучение правилам «Пепла и короны» на маленьких позициях.
    /// Игрок всегда за белых, чёрные стоят на месте (ход после белых сразу возвращается белым).
    /// </summary>
    public static class Tutorial
    {
        public static readonly IReadOnlyList<Lesson> Lessons = Build();

        /// <summary>
        /// После хода игрока: чёрные пропускают ход, снова ходят белые.
        /// Вызывать, только если партия не закончилась.
        /// </summary>
        public static void PassTurn(GameState s, Side player = Side.White)
        {
            if (s.IsOver || s.SideToMove == player)
                return;
            s.SideToMove = player;
            s.Ply++;
        }

        private static GameState Board(Action<GameState> setup)
        {
            var s = new GameState(RuleSet.Default) { Seed = 2024 };
            setup(s);
            return s;
        }

        private static void Ignite(GameState s, string square)
        {
            int sq = Sq.Parse(square);
            Piece p = s.Cells[sq];
            p.Empowered = true;
            s.Cells[sq] = p;
        }

        private static bool Is(GameState s, string square, Side side, PieceType type)
        {
            Piece p = s[square];
            return p.Side == side && p.Type == type;
        }

        private static bool From(Move m, string square) => m.From == Sq.Parse(square);
        private static bool To(Move m, string square) => m.To == Sq.Parse(square);

        private static bool Has(List<MoveEffect> effects, EffectKind kind)
        {
            foreach (MoveEffect e in effects)
                if (e.Kind == kind)
                    return true;
            return false;
        }

        private static IReadOnlyList<Lesson> Build() => new[]
        {
            new Lesson
            {
                Id = "degrade",
                Title = "Удар ослабляет",
                Intro = "В «Пепле и короне» фигуры не умирают. Ударенная фигура становится на ступень слабее:\n"
                        + "король → ферзь → ладья → слон → конь → пешка → призрак.\n"
                        + "Атакующая фигура при этом остаётся на месте.",
                Setup = () => Board(s =>
                {
                    s.Put("h1", Side.White, PieceType.King);
                    s.Put("a1", Side.White, PieceType.Rook);
                    s.Put("h8", Side.Black, PieceType.King);
                    s.Put("a5", Side.Black, PieceType.Queen);
                }),
                Steps = new[]
                {
                    new LessonStep
                    {
                        Task = "Бей чёрного ферзя ладьёй, пока он не станет пешкой",
                        Hint = "Ладья на a1 бьёт по вертикали — кликни её, потом фигуру на a5",
                        Done = "Ферзь прошёл весь путь до пешки, а ладья так и стоит на a1.",
                        IsDone = (s, m, e) => Is(s, "a5", Side.Black, PieceType.Pawn),
                        IsProgress = (s, m, e) => From(m, "a1") && To(m, "a5")
                    }
                },
                Outro = "Чтобы убрать сильную фигуру, по ней придётся ударить несколько раз."
            },
            new Lesson
            {
                Id = "ghost",
                Title = "Призрак",
                Intro = "Ударенная пешка становится призраком. Призрак ходит на одну клетку в любую сторону, "
                        + "но никого не бьёт.\nЕсли ударить призрака, он уходит с доски, а атакующая фигура встаёт на его место.",
                Setup = () => Board(s =>
                {
                    s.Put("h1", Side.White, PieceType.King);
                    s.Put("d1", Side.White, PieceType.Rook);
                    s.Put("h8", Side.Black, PieceType.King);
                    s.Put("d5", Side.Black, PieceType.Pawn);
                }),
                Steps = new[]
                {
                    new LessonStep
                    {
                        Task = "Убери чёрную пешку с доски",
                        Hint = "Бей ладьёй по d5 — два раза",
                        Done = "Пешка стала призраком, а призрак ушёл с доски.",
                        IsDone = (s, m, e) => Is(s, "d5", Side.White, PieceType.Rook),
                        IsProgress = (s, m, e) => From(m, "d1") && To(m, "d5")
                    }
                },
                Outro = "Призраки безопасны, но мешают проходу — их тоже иногда приходится убирать."
            },
            new Lesson
            {
                Id = "revive",
                Title = "Возрождение",
                Intro = "Ослабленная до пешки фигура помнит, кем была. Дойдя до последней горизонтали, "
                        + "она возрождается в свою исходную фигуру.\nПешка на e7 — это бывший ферзь.",
                Setup = () => Board(s =>
                {
                    s.Put("h1", Side.White, PieceType.King);
                    s.Put("e7", Side.White, PieceType.Pawn, original: PieceType.Queen);
                    s.Put("a1", Side.Black, PieceType.King);
                    s.Put("c5", Side.Black, PieceType.Knight);
                }),
                Steps = new[]
                {
                    new LessonStep
                    {
                        Task = "Проведи пешку на e8",
                        Hint = "Пешка ходит вперёд — на e8",
                        Done = "Ферзь вернулся! Обычная пешка с начала партии стала бы ферзём.",
                        IsDone = (s, m, e) => Is(s, "e8", Side.White, PieceType.Queen)
                    }
                },
                Outro = "Ослабленную фигуру можно вернуть в строй, если довести её до конца доски."
            },
            new Lesson
            {
                Id = "kingfall",
                Title = "Мат и огонь",
                Intro = "Королей не бьют — им ставят шах и мат. Но мат не заканчивает партию:\n"
                        + "король уходит с доски, а 1–3 фигуры его армии загораются и получают способности.",
                Setup = () => Board(s =>
                {
                    s.Put("g1", Side.White, PieceType.King);
                    s.Put("f2", Side.White, PieceType.Pawn);
                    s.Put("g2", Side.White, PieceType.Pawn);
                    s.Put("h2", Side.White, PieceType.Pawn);
                    s.Put("a1", Side.White, PieceType.Rook);
                    s.Put("h8", Side.Black, PieceType.King);
                    s.Put("g7", Side.Black, PieceType.Pawn);
                    s.Put("h7", Side.Black, PieceType.Pawn);
                    s.Put("b3", Side.Black, PieceType.Pawn);
                    s.Put("b2", Side.Black, PieceType.Rook);
                    s.Put("d4", Side.Black, PieceType.Knight);
                    s.Put("e6", Side.Black, PieceType.Pawn);
                    s.Put("d5", Side.Black, PieceType.Pawn);
                    s.Put("f6", Side.Black, PieceType.Pawn);
                }),
                Steps = new[]
                {
                    new LessonStep
                    {
                        Task = "Поставь мат чёрному королю в один ход",
                        Hint = "Король заперт своими пешками. Подумай про последнюю горизонталь",
                        Done = "Чёрный король пал, и его фигуры загорелись!",
                        IsDone = (s, m, e) => Has(e, EffectKind.KingFell)
                    }
                },
                Outro = "Теперь у чёрных нет короля, и шах им не страшен. Они побеждают, если поставят мат "
                        + "твоему королю, и проигрывают, когда у них не останется фигур."
            },
            new Lesson
            {
                Id = "queen",
                Title = "Горящий ферзь",
                Intro = "Твой король пал, зато фигуры горят. Горящий ферзь ходит ещё и как конь — буквой «Г».",
                Setup = () => Board(s =>
                {
                    s.Put("d4", Side.White, PieceType.Queen);
                    Ignite(s, "d4");
                    s.Put("a2", Side.White, PieceType.Pawn);
                    s.Put("h7", Side.Black, PieceType.King);
                    s.Put("f5", Side.Black, PieceType.Knight);
                    s.Put("b5", Side.Black, PieceType.Pawn);
                }),
                Steps = new[]
                {
                    new LessonStep
                    {
                        Task = "Ударь коня на f5 ходом коня",
                        Hint = "Выбери ферзя на d4 — среди его ходов есть f5",
                        Done = "Обычный ферзь так не умеет.",
                        IsDone = (s, m, e) => From(m, "d4") && To(m, "f5")
                    }
                },
                Outro = "Горящий ферзь — самая опасная фигура на доске."
            },
            new Lesson
            {
                Id = "pierce",
                Title = "Пробивание",
                Intro = "Горящие ладья и слон один раз за игру могут ударить сквозь одну фигуру — "
                        + "чужую или свою.\nТакие ходы подсвечены фиолетовым.",
                Setup = () => Board(s =>
                {
                    s.Put("a1", Side.White, PieceType.Rook);
                    Ignite(s, "a1");
                    s.Put("c1", Side.White, PieceType.Bishop);
                    Ignite(s, "c1");
                    s.Put("e3", Side.White, PieceType.Pawn);
                    s.Put("h8", Side.Black, PieceType.King);
                    s.Put("a4", Side.Black, PieceType.Pawn);
                    s.Put("a7", Side.Black, PieceType.Queen);
                    s.Put("g5", Side.Black, PieceType.Rook);
                }),
                Steps = new[]
                {
                    new LessonStep
                    {
                        Task = "Ударь ладьёй ферзя на a7 сквозь пешку",
                        Hint = "Выбери ладью на a1 — фиолетовая метка на a7",
                        Done = "Пешка осталась цела, а ферзь ослаб.",
                        IsDone = (s, m, e) => m.Kind == MoveKind.Pierce && From(m, "a1")
                    },
                    new LessonStep
                    {
                        Task = "Ударь слоном ладью на g5 сквозь свою пешку",
                        Hint = "Выбери слона на c1 — фиолетовая метка на g5",
                        Done = "Сквозь свои фигуры тоже можно.",
                        IsDone = (s, m, e) => m.Kind == MoveKind.Pierce && From(m, "c1")
                    }
                },
                Outro = "У каждой горящей ладьи и слона пробивание одно на всю игру — береги его."
            },
            new Lesson
            {
                Id = "trojan",
                Title = "Троянский конь",
                Intro = "Горящий конь — троянский. Он возит одну свою фигуру, раз за игру прыгает дважды "
                        + "и может взорваться.",
                Setup = () => Board(s =>
                {
                    s.Put("d4", Side.White, PieceType.Knight);
                    Ignite(s, "d4");
                    s.Put("e3", Side.White, PieceType.Bishop);
                    s.Put("a2", Side.White, PieceType.Pawn);
                    s.Put("h8", Side.Black, PieceType.King);
                    s.Put("c7", Side.Black, PieceType.Rook);
                    s.Put("e7", Side.Black, PieceType.Queen);
                    s.Put("d8", Side.Black, PieceType.Bishop);
                    s.Put("f6", Side.Black, PieceType.Knight);
                    s.Put("e6", Side.Black, PieceType.Pawn);
                }),
                Steps = new[]
                {
                    new LessonStep
                    {
                        Task = "Посади слона в коня: выбери слона и кликни коня",
                        Hint = "Слон на e3 стоит рядом с конём — у коня зелёное кольцо",
                        Done = "Слон внутри. Если коня ослабят, пассажир выпадет рядом.",
                        IsDone = (s, m, e) => m.Kind == MoveKind.Embark
                    },
                    new LessonStep
                    {
                        Task = "Сделай двойной прыжок конём",
                        Hint = "Бирюзовые метки — двойной прыжок. Если в конце удар, конь остаётся на средней клетке",
                        Done = "Два прыжка за один ход. Дальше двойной прыжок этому коню недоступен.",
                        IsDone = (s, m, e) => m.IsDouble
                    },
                    new LessonStep
                    {
                        Task = "Взорви коня: выбери его и нажми кнопку справа от доски",
                        Hint = "Выбери коня — появится кнопка «Взорвать коня»",
                        Done = "Бум! Все фигуры вокруг ослабли — и чужие, и свои. Конь и пассажир исчезли.",
                        IsDone = (s, m, e) => m.Kind == MoveKind.Explode
                    }
                },
                Outro = "Взрыв не трогает королей. Взрывай там, где рядом много чужих фигур."
            }
        };
    }
}
